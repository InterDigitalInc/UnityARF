//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace Interdigital.Arf
{
    // Keeps the face mapping on the source mesh. The avatar-level renderer owns GPU uploads.
    [RequireComponent(typeof(BakedMesh))]
    public sealed class GaussianStitchingUpdater : MonoBehaviour
    {
        private const int JobBatchSize = 4096;

        private NativeArray<Vector3Int> triangleVertices;
        private NativeArray<Vector3> weights;
        private NativeArray<Vector3> displacements;

        public int Count => triangleVertices.IsCreated ? triangleVertices.Length : 0;

        [BurstCompile(CompileSynchronously = true)]
        private struct WritePositionsJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<Vector3Int> Triangles;
            [ReadOnly] public NativeArray<Vector3> Weights;
            [ReadOnly] public NativeArray<Vector3> Displacements;
            [ReadOnly] public NativeArray<Vector3> Vertices;
            [WriteOnly] public NativeArray<Vector3> Destination;
            public Matrix4x4 ToRoot;

            public void Execute(int g)
            {
                Vector3Int triangle = Triangles[g];
                Vector3 w = Weights[g];
                Vector3 a = Vertices[triangle.x], b = Vertices[triangle.y], c = Vertices[triangle.z], d = Displacements[g];
                float x = w.x * a.x + w.y * b.x + w.z * c.x + d.x;
                float y = w.x * a.y + w.y * b.y + w.z * c.y + d.y;
                float z = w.x * a.z + w.y * b.z + w.z * c.z + d.z;
                Destination[g] = new Vector3(
                    ToRoot.m00 * x + ToRoot.m01 * y + ToRoot.m02 * z + ToRoot.m03,
                    ToRoot.m10 * x + ToRoot.m11 * y + ToRoot.m12 * z + ToRoot.m13,
                    ToRoot.m20 * x + ToRoot.m21 * y + ToRoot.m22 * z + ToRoot.m23);
            }
        }

        public void Initialize(GaussianModel model, BakedMesh bakedMesh, uint[] sourceIndices)
        {
            if (triangleVertices.IsCreated)
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

            var indices = new NativeArray<Vector3Int>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var normalizedWeights = new NativeArray<Vector3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            var unityOffsets = new NativeArray<Vector3>(count, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            try
            {
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
            }
            catch
            {
                indices.Dispose();
                normalizedWeights.Dispose();
                unityOffsets.Dispose();
                throw;
            }

            triangleVertices = indices;
            weights = normalizedWeights;
            displacements = unityOffsets;
        }

        public void WritePositions(BakedMesh baked, Vector3[] destination, int offset, Matrix4x4 toRoot)
        {
            if (!triangleVertices.IsCreated)
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

        /// <summary>Same computation as <see cref="WritePositions"/>, as a parallel Burst job.</summary>
        public JobHandle ScheduleWritePositions(NativeArray<Vector3> vertices, NativeArray<Vector3> destination, int offset,
            Matrix4x4 toRoot, JobHandle dependsOn = default)
        {
            if (!triangleVertices.IsCreated)
                throw new InvalidOperationException("The stitching updater has not been initialized.");
            if (offset < 0 || destination.Length - offset < Count)
                throw new ArgumentException("The destination has insufficient space.", nameof(destination));
            return new WritePositionsJob
            {
                Triangles = triangleVertices,
                Weights = weights,
                Displacements = displacements,
                Vertices = vertices,
                Destination = destination.GetSubArray(offset, Count),
                ToRoot = toRoot
            }.Schedule(Count, JobBatchSize, dependsOn);
        }

        private void OnDestroy()
        {
            if (triangleVertices.IsCreated) triangleVertices.Dispose();
            if (weights.IsCreated) weights.Dispose();
            if (displacements.IsCreated) displacements.Dispose();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
