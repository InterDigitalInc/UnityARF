//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;

namespace Interdigital {
namespace Arf {

[CustomEditor(typeof(ArfImporter))]
public class ArfImporterEditor : AssetImporterEditor
{
    public override void OnInspectorGUI()
    {
        if (!this) return;

		var t = target as ArfImporter;
		if (!t) return;

        base.OnInspectorGUI(); 
    }

}

}
}
