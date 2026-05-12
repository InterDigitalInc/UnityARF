//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Interdigital {
namespace Arf {

[ScriptedImporter(1, new[] { "zip", "arf", "arfz" })]
public class ArfImporter : ScriptedImporter
{
    [SerializeField] public ArfParserOptions options;

    public override void OnImportAsset(AssetImportContext ctx)
    {        
        AssetCache cache = new AssetCache();
        ArfParser parser = ArfParser.Load(ctx.assetPath, cache);

        GameObject asset = parser.createAvatar(options);
        ctx.AddObjectToAsset("main", asset);
	
		var jsonAsset = new TextAsset(parser.arf.ToJsonString());
		jsonAsset.name = "arf.json";
		var holder = asset.AddComponent<ArfDataHolder>();
		holder.json = jsonAsset;
		ctx.AddObjectToAsset("arf.json", jsonAsset);
		
        foreach(var mesh in cache.meshes) {
            //Debug.Log(mesh.Key);
			try {
				ctx.AddObjectToAsset(mesh.Key, mesh.Value);
			} catch(System.InvalidOperationException e) {
				Debug.LogWarning(e.ToString(), mesh.Value);
			}
        }
        foreach(var material in cache.materials) {
            //Debug.Log(material.Key);
			try {
				ctx.AddObjectToAsset(material.Key, material.Value);
			} catch(System.InvalidOperationException e) {
				Debug.LogWarning(e.ToString(), material.Value);
			}
        }
        foreach(var texture in cache.textures) {
            //Debug.Log(texture.Key);
			try {
				ctx.AddObjectToAsset(texture.Key, texture.Value);
			} catch(System.InvalidOperationException e) {
				Debug.LogWarning(e.ToString(), texture.Value);
			}
        }

        ctx.SetMainObject(asset);

        parser.Close();
		parser.Dispose();
    }
}


}
}
