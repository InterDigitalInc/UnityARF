//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using UnityEngine;
using UnityEditor;

namespace Interdigital {
namespace Arf {

[CustomEditor(typeof(ArfComponent))]
public class ArfComponentEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        if (!this) return;

		var component = target as ArfComponent;
		if (!component) return;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LongField("Asset", component.asset);
        if (component.mesh >= 0) {
            EditorGUILayout.LongField("Mesh", component.mesh);
        }
        if (component.blendshapeSet >= 0) {
            EditorGUILayout.LongField("Blendshape set", component.blendshapeSet);
        }
        if (component.skeleton >= 0) {
            EditorGUILayout.LongField("Skeleton", component.skeleton);
        }
        EditorGUI.EndDisabledGroup();
    }

}

}
}
