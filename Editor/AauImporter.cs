using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEngine;
using System.IO;

[ScriptedImporter(1, "aau")]
public class AauImporter : ScriptedImporter
{
    public override void OnImportAsset(AssetImportContext ctx)
    {
        byte[] data = File.ReadAllBytes(ctx.assetPath);
        var asset = new TextAsset(data);
        ctx.AddObjectToAsset("main", asset);
        ctx.SetMainObject(asset);
    }
}
