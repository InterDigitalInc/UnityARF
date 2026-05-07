//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Interdigital {

public class AssetCache
{
    public string baseName = "";
    public Dictionary<string, UnityEngine.Mesh> meshes = new Dictionary<string, UnityEngine.Mesh>();
    public Dictionary<string, UnityEngine.Material> materials = new Dictionary<string, UnityEngine.Material>();
    public Dictionary<string, UnityEngine.Texture2D> textures = new Dictionary<string, UnityEngine.Texture2D>();

    public AssetCache(string baseName = "") {
        this.baseName = baseName;
    }

    public void AddMesh(string name, UnityEngine.Mesh mesh) 
    {
        meshes.Add(baseName + "mesh" + name, mesh);
    }

    public void AddMaterial(string name, UnityEngine.Material material) 
    {
        materials.Add(baseName + "material" + name, material);
    }

    public void AddTexture(string name, UnityEngine.Texture2D texture) 
    {
        textures.Add(baseName + "texture" + name, texture);
    }

}


}
