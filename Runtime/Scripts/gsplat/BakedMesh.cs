//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Interdigital.Arf
{
    [RequireComponent(typeof(SkinnedMeshRenderer))]
    public sealed class BakedMesh : MonoBehaviour
    {
        private SkinnedMeshRenderer source;
        private UnityEngine.Mesh snapshot;
        private readonly List<Vector3> vertices = new List<Vector3>();

        public UnityEngine.Mesh Snapshot => snapshot;
        public IReadOnlyList<Vector3> Vertices => vertices;
        public bool HasSnapshot { get; private set; }
        public bool AutoBake { get; set; } = true;

        public event Action<BakedMesh> Baked;

        private void Awake()
        {
            source = GetComponent<SkinnedMeshRenderer>();
            snapshot = new UnityEngine.Mesh { name = $"{name} (baked)" };
            if (source.sharedMesh != null)
                vertices.Capacity = source.sharedMesh.vertexCount;
        }

        private void LateUpdate()
        {
            if (AutoBake)
                Bake();
        }

        /// <param name="readVertices">False if the caller uses <see cref="CopyVertices"/> instead of
        /// <see cref="Vertices"/>, which avoids a managed copy of the vertices.</param>
        public void Bake(bool readVertices = true)
        {
            if (source == null || source.sharedMesh == null)
            {
                HasSnapshot = false;
                return;
            }

            // Positions stay in mesh-local space; the merged renderer applies the avatar transform.
            source.BakeMesh(snapshot, false);
            if (readVertices || Baked != null)
                snapshot.GetVertices(vertices);
            HasSnapshot = true;
            Baked?.Invoke(this);
        }

        public void CopyVertices(NativeArray<Vector3> destination)
        {
            using UnityEngine.Mesh.MeshDataArray data = UnityEngine.Mesh.AcquireReadOnlyMeshData(snapshot);
            data[0].GetVertices(destination);
        }

        private void OnDestroy()
        {
            if (snapshot == null)
                return;

            if (Application.isPlaying)
                Destroy(snapshot);
            else
                DestroyImmediate(snapshot);
        }
    }
}
