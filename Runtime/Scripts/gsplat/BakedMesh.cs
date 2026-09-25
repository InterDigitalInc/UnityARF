//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections.Generic;
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
            Bake();
        }

        public void Bake()
        {
            if (source == null || source.sharedMesh == null)
            {
                HasSnapshot = false;
                return;
            }

            source.BakeMesh(snapshot, true);
            snapshot.GetVertices(vertices);
            HasSnapshot = true;
            Baked?.Invoke(this);
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
