//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using Gsplat;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Interdigital.Arf
{
    [RequireComponent(typeof(BakedMesh))]
    public sealed class GaussianStitchingUpdater : MonoBehaviour
    {
        private BakedMesh bakedMesh;
        private GsplatRenderer splats;
        private GsplatAssetUncompressed asset;
        private Vector3Int[] triangleVertices;
        private Vector3[] weights;
        private Vector3[] displacements;
        private float[] radii;
        private Vector4[] gpuPositions;
        private Texture2D stagingTexture;
        private Vector4[] stagingPixels;
        private bool warnedTextureUpload;

        public void Initialize(GaussianModel model, BakedMesh bakedMesh, GsplatRenderer splats, uint[] sourceIndices)
        {
            if (triangleVertices != null)
                throw new InvalidOperationException("The stitching updater is already initialized.");
            if (model == null || !model.isStitched)
                throw new ArgumentException("A stitched Gaussian model is required.", nameof(model));
            if (bakedMesh == null || bakedMesh.gameObject != gameObject)
                throw new ArgumentException("The baked mesh must be on this GameObject.", nameof(bakedMesh));
            if (splats == null || splats.gameObject != gameObject)
                throw new ArgumentException("The splat renderer must be on this GameObject.", nameof(splats));
            if (sourceIndices == null || sourceIndices.Length % 3 != 0)
                throw new ArgumentException("Triangle indices are required.", nameof(sourceIndices));

            var uncompressed = splats.GsplatAsset as GsplatAssetUncompressed;
            if (uncompressed == null)
                throw new ArgumentException("An uncompressed splat asset is required.", nameof(splats));

            var sourceMesh = GetComponent<SkinnedMeshRenderer>().sharedMesh;
            if (sourceMesh == null)
                throw new InvalidOperationException("The skinned mesh has not been assigned.");

            uint[] faceIds = model.faces.GetValues<uint>();
            float[] bary = model.baryCenters.GetValues<float>();
            float[] offsets = model.displacements.GetValues<float>();
            int count = faceIds.Length;
            if (uncompressed.Positions.Length != count || bary.Length != 3 * count ||
                offsets.Length != 3 * count)
                throw new ArgumentException("Stitching data does not match the splat count.", nameof(model));

            var indices = new Vector3Int[count];
            var normalizedWeights = new Vector3[count];
            var unityOffsets = new Vector3[count];
            var splatRadii = new float[count];
            uint vertexCount = checked((uint)sourceMesh.vertexCount);
            uint faceCount = checked((uint)(sourceIndices.Length / 3));
            for (int g = 0; g < count; g++)
            {
                uint face = faceIds[g];
                if (face >= faceCount)
                    throw new ArgumentException($"Gaussian {g} references an invalid face.", nameof(model));
                int triangle = checked((int)face * 3);
                uint i0 = sourceIndices[triangle];
                uint i1 = sourceIndices[triangle + 1];
                uint i2 = sourceIndices[triangle + 2];
                if (i0 >= vertexCount || i1 >= vertexCount || i2 >= vertexCount)
                    throw new ArgumentException($"Gaussian {g} references an invalid vertex.", nameof(sourceIndices));
                indices[g] = new Vector3Int((int)i0, (int)i1, (int)i2);

                int k = 3 * g;
                float w0 = Mathf.Abs(bary[k]);
                float w1 = Mathf.Abs(bary[k + 1]);
                float w2 = Mathf.Abs(bary[k + 2]);
                float sum = w0 + w1 + w2;
                if (!IsFinite(sum) || sum <= 1e-12f)
                    throw new ArgumentException($"Gaussian {g} has invalid barycentric weights.", nameof(model));
                normalizedWeights[g] = new Vector3(w0 / sum, w1 / sum, w2 / sum);

                if (!IsFinite(offsets[k]) || !IsFinite(offsets[k + 1]) || !IsFinite(offsets[k + 2]))
                    throw new ArgumentException($"Gaussian {g} has an invalid displacement.", nameof(model));
                // glTF positions and displacements are converted from LUF to Unity RUF.
                unityOffsets[g] = new Vector3(-offsets[k], offsets[k + 1], offsets[k + 2]);

                Vector3 scale = uncompressed.Scales[g];
                splatRadii[g] = 3f * Mathf.Max(scale.x, Mathf.Max(scale.y, scale.z));
            }

            this.bakedMesh = bakedMesh;
            this.splats = splats;
            asset = uncompressed;
            triangleVertices = indices;
            weights = normalizedWeights;
            displacements = unityOffsets;
            radii = splatRadii;
            gpuPositions = new Vector4[count];
            if (isActiveAndEnabled)
                bakedMesh.Baked += OnBaked;
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private void OnEnable()
        {
            if (bakedMesh != null)
                bakedMesh.Baked += OnBaked;
        }

        private void OnDisable()
        {
            if (bakedMesh != null)
                bakedMesh.Baked -= OnBaked;
        }

        private void OnBaked(BakedMesh baked)
        {
            var vertices = baked.Vertices;
            Vector3 minimum = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Vector3 maximum = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
            for (int g = 0; g < triangleVertices.Length; g++)
            {
                Vector3Int triangle = triangleVertices[g];
                Vector3 w = weights[g];
                Vector3 position = w.x * vertices[triangle.x] +
                                   w.y * vertices[triangle.y] +
                                   w.z * vertices[triangle.z] + displacements[g];
                asset.Positions[g] = position;
                gpuPositions[g] = new Vector4(position.x, position.y, position.z, 0f);

                Vector3 extent = Vector3.one * radii[g];
                minimum = Vector3.Min(minimum, position - extent);
                maximum = Vector3.Max(maximum, position + extent);
            }

            Bounds bounds = new Bounds((minimum + maximum) * 0.5f, maximum - minimum);
            asset.Bounds = bounds;

            var resource = splats.GsplatResource as GsplatResourceUncompressed;
            if (resource == null || resource.Disposed || resource.UploadedCount < asset.SplatCount)
                return;

            splats.Bounds = bounds;
            if (resource.PositionBuffer != null)
                resource.PositionBuffer.SetData(gpuPositions);
            else if (resource.PositionTexture != null)
            {
                if (!UploadPositionTexture(resource.PositionTexture))
                    return;
            }
            else
                return;

            splats.ForceRefresh();
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
            if (bakedMesh != null)
                bakedMesh.Baked -= OnBaked;
            ReleaseStagingTexture();
        }
    }
}
