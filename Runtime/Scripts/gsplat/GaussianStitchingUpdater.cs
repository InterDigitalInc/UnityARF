//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;

namespace Interdigital.Arf
{
    // Keeps the face mapping on the source mesh. The avatar-level renderer owns GPU uploads.
    [RequireComponent(typeof(BakedMesh))]
    public sealed class GaussianStitchingUpdater : MonoBehaviour
    {
        private Vector3Int[] triangleVertices;
        private Vector3[] weights;
        private Vector3[] displacements;

        public int Count => triangleVertices?.Length ?? 0;

        public void Initialize(GaussianModel model, BakedMesh bakedMesh, uint[] sourceIndices)
        {
            if (triangleVertices != null)
                throw new InvalidOperationException("The stitching updater is already initialized.");
            if (model == null || !model.isStitched)
                throw new ArgumentException("A stitched Gaussian model is required.", nameof(model));
            if (bakedMesh == null || bakedMesh.gameObject != gameObject)
                throw new ArgumentException("The baked mesh must be on this GameObject.", nameof(bakedMesh));
            if (sourceIndices == null || sourceIndices.Length % 3 != 0)
                throw new ArgumentException("Triangle indices are required.", nameof(sourceIndices));

            var source = GetComponent<SkinnedMeshRenderer>();
            if (source == null || source.sharedMesh == null)
                throw new InvalidOperationException("The source skinned mesh has not been assigned.");

            uint[] faceIds = model.faces.GetValues<uint>();
            float[] bary = model.baryCenters.GetValues<float>();
            float[] offsets = model.displacements.GetValues<float>();
            int count = checked((int)model.count);
            if (faceIds.Length != count || bary.Length != 3 * count || offsets.Length != 3 * count)
                throw new ArgumentException("Stitching data does not match the splat count.", nameof(model));

            var indices = new Vector3Int[count];
            var normalizedWeights = new Vector3[count];
            var unityOffsets = new Vector3[count];
            uint vertexCount = checked((uint)source.sharedMesh.vertexCount);
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
            }

            triangleVertices = indices;
            weights = normalizedWeights;
            displacements = unityOffsets;
        }

        public void WritePositions(BakedMesh baked, Vector3[] destination, int offset, Matrix4x4 toRoot)
        {
            if (triangleVertices == null)
                throw new InvalidOperationException("The stitching updater has not been initialized.");
            if (baked == null || !baked.HasSnapshot)
                throw new InvalidOperationException("The source mesh has not been baked.");
            if (destination == null || offset < 0 || destination.Length - offset < Count)
                throw new ArgumentException("The destination has insufficient space.", nameof(destination));

            var vertices = baked.Vertices;
            for (int g = 0; g < Count; g++)
            {
                Vector3Int triangle = triangleVertices[g];
                Vector3 w = weights[g];
                Vector3 local = w.x * vertices[triangle.x] +
                                w.y * vertices[triangle.y] +
                                w.z * vertices[triangle.z] + displacements[g];
                destination[offset + g] = toRoot.MultiplyPoint3x4(local);
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
