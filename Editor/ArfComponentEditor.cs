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
        if (component.mesh != null) {
            EditorGUILayout.LongField("Mesh", component.mesh.Value);
        }
        if (component.blendshapeSet != null) {
            EditorGUILayout.LongField("Blendshape set", component.blendshapeSet.Value);
        }
        if (component.skeleton != null) {
            EditorGUILayout.LongField("Skeleton", component.skeleton.Value);
        }
        EditorGUI.EndDisabledGroup();
    }

}

}
}
