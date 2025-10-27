//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Interdigital.Gltf2;
using Interdigital.Arf;

public class TestGltf : MonoBehaviour
{
    public string filePath;

    void Start()
    {
        Gltf gltf = Gltf.Load(filePath);

        GltfParser gltfParser = new GltfParser(gltf);
        Scene scene = gltf.scene;
        gltfParser.SetSkinnedMeshRenderer(transform, scene.nodes[0].GetPropertyIndex());
    }

    void Update()
    {
        
    }
}

