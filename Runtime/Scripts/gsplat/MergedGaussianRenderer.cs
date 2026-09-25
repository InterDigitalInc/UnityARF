//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections.Generic;
using Gsplat;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.Rendering;

namespace Interdigital.Arf
{
    /// <summary>
    /// Combines the avatar's Gaussian models into one splat asset so all splats share one sort.
    /// Source models and their mesh stitching remain on their individual mesh GameObjects.
    /// </summary>
    [DefaultExecutionOrder(1000)]
    public sealed class MergedGaussianRenderer : MonoBehaviour
    {
        private sealed class Part
        {
            public GaussianModel Model;
            public BakedMesh Baked;
            public GaussianStitchingUpdater Stitching;
            public Vector3[] SourcePositions;
            public int Offset;
            public int Count;
        }

        private Part[] parts;
        private GsplatAssetUncompressed asset;
        private GsplatRenderer splats;
        private Vector4[] gpuPositions;
        private Texture2D stagingTexture;
        private Vector4[] stagingPixels;
        private bool warnedTextureUpload;
        private bool warnedCpuSortUnavailable;
        private uint[] depthKeys;
        private uint[] sortOrder;
        private uint[] sortScratch;
        private readonly int[] histogram = new int[256];
        private readonly int[] offsets = new int[256];

        // The CPU fallback sorts for one camera, as UnitySplats does. Override this for
        // an untagged or secondary Game camera.
        public Camera CpuSortCamera;

        public GsplatRenderer Renderer => splats;

        public void Initialize()
        {
            if (parts != null)
                throw new InvalidOperationException("The merged Gaussian renderer is already initialized.");

            GaussianModel[] models = GetComponentsInChildren<GaussianModel>(true);
            if (models.Length == 0)
                throw new InvalidOperationException("There are no Gaussian models under this object.");

            int total = 0;
            byte maxBands = 0;
            string colorSpace = models[0].colorSpace;
            parts = new Part[models.Length];
            for (int i = 0; i < models.Length; i++)
            {
                GaussianModel model = models[i];
                if (model.colorSpace != colorSpace)
                    throw new InvalidOperationException("All Gaussian models in one renderer must use the same color space.");
                ValidateTranslationOnly(model.transform);
                int count = checked((int)model.count);
                var part = new Part
                {
                    Model = model,
                    Baked = model.GetComponent<BakedMesh>(),
                    Stitching = model.GetComponent<GaussianStitchingUpdater>(),
                    Offset = total,
                    Count = count
                };
                if (model.isStitched && (part.Baked == null || part.Stitching == null ||
                                         part.Stitching.Count != count))
                    throw new InvalidOperationException($"Missing stitching components on {model.name}.");
                parts[i] = part;
                total = checked(total + count);
                maxBands = Math.Max(maxBands, checked((byte)(model.shs.Length - 1)));
            }

            var combined = new GsplatDecodedData(total, maxBands);
            int targetStride = GsplatUtils.SHBandsToCoefficientCount(maxBands);
            for (int i = 0; i < parts.Length; i++)
            {
                Part part = parts[i];
                GsplatDecodedData decoded = part.Model.ToGsplatDecodedData();
                part.SourcePositions = decoded.Positions;
                Array.Copy(decoded.Scales, 0, combined.Scales, part.Offset, part.Count);
                Array.Copy(decoded.Rotations, 0, combined.Rotations, part.Offset, part.Count);
                Array.Copy(decoded.Colors, 0, combined.Colors, part.Offset, part.Count);
                int sourceStride = GsplatUtils.SHBandsToCoefficientCount(decoded.SHBands);
                for (int g = 0; g < part.Count; g++)
                    Array.Copy(decoded.SHs, g * sourceStride,
                        combined.SHs, (part.Offset + g) * targetStride, sourceStride);

                Matrix4x4 toRoot = transform.worldToLocalMatrix * part.Model.transform.localToWorldMatrix;
                for (int g = 0; g < part.Count; g++)
                    combined.Positions[part.Offset + g] = toRoot.MultiplyPoint3x4(decoded.Positions[g]);
            }

            combined.Bounds = CalculateBounds(combined.Positions, combined.Scales);
            combined.Validate();
            asset = ScriptableObject.CreateInstance<GsplatAssetUncompressed>();
            asset.name = $"{name} (merged Gaussians)";
            asset.LoadFromDecoded(combined);
            gpuPositions = new Vector4[total];

            splats = gameObject.AddComponent<GsplatRenderer>();
            splats.GsplatAsset = asset;
            splats.SHDegree = maxBands;
            splats.GammaToLinear = colorSpace != "lin_rec709_display";
            splats.SortMode = GsplatRenderer.GsplatSortMode.Always;

            foreach (Part part in parts)
                if (part.Baked != null)
                    part.Baked.AutoBake = false;

            MergedGaussianSortHook.Register(this);
        }

        private void LateUpdate()
        {
            if (asset == null || splats == null || parts == null)
                return;
            MergedGaussianSortHook.EnsureInstalled();

            foreach (Part part in parts)
            {
                if (part.Model == null)
                    continue;
                if (!HasTranslationOnly(part.Model.transform))
                {
                    Debug.LogError($"Gaussian model {part.Model.name} acquired a rotation or scale relative to its merged renderer.", this);
                    enabled = false;
                    return;
                }
                Matrix4x4 toRoot = transform.worldToLocalMatrix * part.Model.transform.localToWorldMatrix;
                if (part.Stitching != null)
                {
                    part.Baked.Bake();
                    if (!part.Baked.HasSnapshot)
                        continue;
                    part.Stitching.WritePositions(part.Baked, asset.Positions, part.Offset, toRoot);
                }
                else
                {
                    for (int g = 0; g < part.Count; g++)
                        asset.Positions[part.Offset + g] = toRoot.MultiplyPoint3x4(part.SourcePositions[g]);
                }
            }

            Bounds bounds = CalculateBounds(asset.Positions, asset.Scales);
            asset.Bounds = bounds;
            var resource = splats.GsplatResource as GsplatResourceUncompressed;
            if (resource == null || resource.Disposed || resource.UploadedCount < asset.SplatCount)
                return;

            for (int g = 0; g < gpuPositions.Length; g++)
            {
                Vector3 position = asset.Positions[g];
                gpuPositions[g] = new Vector4(position.x, position.y, position.z, 0f);
            }
            if (resource.PositionBuffer != null)
                resource.PositionBuffer.SetData(gpuPositions);
            else if (resource.PositionTexture != null)
            {
                if (!UploadPositionTexture(resource.PositionTexture))
                    return;
            }
            else
                return;

            splats.Bounds = bounds;
            if (GsplatSorter.Instance.CpuFallbackEnabled && !splats.HasActiveRanges &&
                splats.SorterResource?.OrderBuffer != null)
            {
                // Our synchronous order replaces the fallback's background sort.
                // Avoid launching another full CPU sort for every deformation.
                splats.SortMode = GsplatRenderer.GsplatSortMode.SortEveryNFrames;
                splats.SortRefreshRate = uint.MaxValue;
            }
            else
            {
                splats.SortMode = GsplatRenderer.GsplatSortMode.Always;
                splats.ForceRefresh();
            }
        }

        // Direct3D 11 and other CPU fallback backends sort on a background task. For a
        // deforming asset that result can lag behind the positions drawn this frame.
        // Called after UnitySplats' player-loop hook, so its pending result cannot replace
        // this frame's order before rendering.
        internal void SortCpuNow()
        {
            if (!isActiveAndEnabled || asset == null || splats == null ||
                !GsplatSorter.Instance.CpuFallbackEnabled || !splats.Valid ||
                splats.GsplatResource is not GsplatResourceUncompressed resource ||
                resource.Disposed || resource.UploadedCount < asset.SplatCount)
                return;

            if (splats.HasActiveRanges || splats.SorterResource?.OrderBuffer == null)
            {
                if (!warnedCpuSortUnavailable)
                {
                    Debug.LogWarning("Synchronous sorting of animated Gaussians requires a CPU-fallback order buffer without active ranges.", this);
                    warnedCpuSortUnavailable = true;
                }
                return;
            }

            Camera camera = CpuSortCamera != null ? CpuSortCamera : Camera.main;
            if (camera == null && Camera.allCamerasCount > 0)
                camera = Camera.allCameras[0];
            if (camera == null)
                return;

            int count = asset.Positions.Length;
            if (sortOrder == null || sortOrder.Length != count)
            {
                depthKeys = new uint[count];
                sortOrder = new uint[count];
                sortScratch = new uint[count];
            }

            Matrix4x4 view = camera.worldToCameraMatrix * transform.localToWorldMatrix;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = asset.Positions[i];
                float depth = view.m20 * p.x + view.m21 * p.y + view.m22 * p.z + view.m23;
                if (float.IsNaN(depth))
                    depth = float.PositiveInfinity;
                uint bits = unchecked((uint)BitConverter.SingleToInt32Bits(depth));
                uint mask = unchecked((uint)-(int)(bits >> 31)) | 0x80000000u;
                depthKeys[i] = bits ^ mask;
                sortOrder[i] = (uint)i;
            }

            uint[] source = sortOrder;
            uint[] destination = sortScratch;
            for (int pass = 0; pass < 4; pass++)
            {
                Array.Clear(histogram, 0, histogram.Length);
                int shift = pass * 8;
                for (int i = 0; i < count; i++)
                    histogram[(depthKeys[source[i]] >> shift) & 0xffu]++;

                int offset = 0;
                for (int digit = 0; digit < 256; digit++)
                {
                    offsets[digit] = offset;
                    offset += histogram[digit];
                }
                for (int i = 0; i < count; i++)
                {
                    uint id = source[i];
                    int digit = (int)((depthKeys[id] >> shift) & 0xffu);
                    destination[offsets[digit]++] = id;
                }
                (source, destination) = (destination, source);
            }
            splats.SorterResource.OrderBuffer.SetData(source, 0, 0, count);
        }

        private void ValidateTranslationOnly(Transform source)
        {
            if (!HasTranslationOnly(source))
                throw new NotSupportedException($"Gaussian model {source.name} must have no rotation or scale relative to its merged renderer.");
        }

        private bool HasTranslationOnly(Transform source)
        {
            Matrix4x4 matrix = transform.worldToLocalMatrix * source.localToWorldMatrix;
            const float tolerance = 1e-4f;
            return Mathf.Abs(matrix.m00 - 1f) < tolerance && Mathf.Abs(matrix.m11 - 1f) < tolerance &&
                   Mathf.Abs(matrix.m22 - 1f) < tolerance &&
                   Mathf.Abs(matrix.m01) < tolerance && Mathf.Abs(matrix.m02) < tolerance &&
                   Mathf.Abs(matrix.m10) < tolerance && Mathf.Abs(matrix.m12) < tolerance &&
                   Mathf.Abs(matrix.m20) < tolerance && Mathf.Abs(matrix.m21) < tolerance;
        }

        private static Bounds CalculateBounds(Vector3[] positions, Vector3[] scales)
        {
            Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int g = 0; g < positions.Length; g++)
            {
                Vector3 scale = scales[g];
                Vector3 extent = Vector3.one * (3f * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z)));
                minimum = Vector3.Min(minimum, positions[g] - extent);
                maximum = Vector3.Max(maximum, positions[g] + extent);
            }
            return new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
        }

        private bool UploadPositionTexture(Texture2D destination)
        {
            if ((SystemInfo.copyTextureSupport & CopyTextureSupport.Basic) == 0)
            {
                if (!warnedTextureUpload)
                {
                    Debug.LogWarning("This graphics backend cannot copy updated splat positions into a texture.", this);
                    warnedTextureUpload = true;
                }
                return false;
            }

            if (stagingTexture == null || stagingTexture.width != destination.width ||
                stagingTexture.height != destination.height)
            {
                ReleaseStagingTexture();
                stagingTexture = new Texture2D(destination.width, destination.height,
                    destination.graphicsFormat, TextureCreationFlags.None);
                stagingPixels = new Vector4[checked(destination.width * destination.height)];
            }
            Array.Copy(gpuPositions, stagingPixels, gpuPositions.Length);
            stagingTexture.SetPixelData(stagingPixels, 0);
            stagingTexture.Apply(false, false);
            Graphics.CopyTexture(stagingTexture, destination);
            return true;
        }

        private void ReleaseStagingTexture()
        {
            if (stagingTexture == null)
                return;
            if (Application.isPlaying)
                Destroy(stagingTexture);
            else
                DestroyImmediate(stagingTexture);
            stagingTexture = null;
            stagingPixels = null;
        }

        private void OnDestroy()
        {
            MergedGaussianSortHook.Unregister(this);
            ReleaseStagingTexture();
            if (asset == null)
                return;
            if (splats != null)
                splats.GsplatAsset = null;
            if (Application.isPlaying)
                Destroy(asset);
            else
                DestroyImmediate(asset);
        }
    }

    // Runs after UnitySplats' own CPU sort/upload and before PostLateUpdate rendering.
    internal static class MergedGaussianSortHook
    {
        private static readonly List<MergedGaussianRenderer> renderers = new List<MergedGaussianRenderer>();
        private static bool installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            installed = false;
            renderers.Clear();
        }

        internal static void Register(MergedGaussianRenderer renderer)
        {
            if (!renderers.Contains(renderer))
                renderers.Add(renderer);
            EnsureInstalled();
        }

        internal static void Unregister(MergedGaussianRenderer renderer) => renderers.Remove(renderer);

        internal static void EnsureInstalled()
        {
            if (installed)
                return;
            PlayerLoopSystem loop = PlayerLoop.GetCurrentPlayerLoop();
            if (Contains(ref loop, typeof(MergedGaussianSortHook)))
            {
                installed = true;
                return;
            }
            if (InsertAfterGsplatHook(ref loop))
            {
                PlayerLoop.SetPlayerLoop(loop);
                installed = true;
            }
        }

        private static bool Contains(ref PlayerLoopSystem system, Type type)
        {
            if (system.type == type)
                return true;
            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null)
                return false;
            for (int i = 0; i < children.Length; i++)
                if (Contains(ref children[i], type))
                    return true;
            return false;
        }

        private static bool InsertAfterGsplatHook(ref PlayerLoopSystem system)
        {
            PlayerLoopSystem[] children = system.subSystemList;
            if (children == null)
                return false;
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].type == typeof(GsplatPlayerLoopHook))
                {
                    // Reinstallation can happen when entering Play mode without a domain reload.
                    if (i + 1 < children.Length && children[i + 1].type == typeof(MergedGaussianSortHook))
                        return true;
                    var updated = new PlayerLoopSystem[children.Length + 1];
                    Array.Copy(children, 0, updated, 0, i + 1);
                    updated[i + 1] = new PlayerLoopSystem
                    {
                        type = typeof(MergedGaussianSortHook),
                        updateDelegate = Update
                    };
                    Array.Copy(children, i + 1, updated, i + 2, children.Length - i - 1);
                    system.subSystemList = updated;
                    return true;
                }
                PlayerLoopSystem child = children[i];
                if (InsertAfterGsplatHook(ref child))
                {
                    children[i] = child;
                    return true;
                }
            }
            return false;
        }

        private static void Update()
        {
            for (int i = renderers.Count - 1; i >= 0; i--)
            {
                if (renderers[i] == null)
                    renderers.RemoveAt(i);
                else
                    renderers[i].SortCpuNow();
            }
        }
    }
}
