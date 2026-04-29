//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Interdigital {
namespace Arf {

[CustomEditor(typeof(ArfAvatar))]
public class ArfAvatarEditor : UnityEditor.Editor
{
    public int selectedFaceAnimation = 0;
    public Dictionary<string, ArfControllers> faceControllers = new Dictionary<string, ArfControllers>();

    public override void OnInspectorGUI()
    {
        if (!this) return;

		var avatar = target as ArfAvatar;
		if (!avatar) return;

        EditorGUI.BeginDisabledGroup(true);
        EditorGUILayout.LabelField("Name", avatar.name);
        EditorGUILayout.LabelField("Id", avatar.id);
        EditorGUILayout.DoubleField("Age", avatar.age);
        EditorGUILayout.LabelField("Gender", avatar.gender);

        EditorGUILayout.LabelField("Face URNs:");
		EditorGUI.indentLevel++;
		EditorGUILayout.HelpBox(string.Join("\n", avatar.faceURNs), MessageType.None);
		EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();
		
        EditorGUILayout.LabelField("Face mapper URNs:");
		EditorGUI.indentLevel++;
		EditorGUILayout.HelpBox(string.Join("\n", avatar.faceMapperURNs), MessageType.None);
		EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();
		
        EditorGUILayout.LabelField("Body URNs:");
		EditorGUI.indentLevel++;
		EditorGUILayout.HelpBox(string.Join("\n", avatar.bodyURNs), MessageType.None);
		EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();		

        EditorGUILayout.LabelField("Body mapper URNs:");
		EditorGUI.indentLevel++;
		EditorGUILayout.HelpBox(string.Join("\n", avatar.bodyMapperURNs), MessageType.None);
		EditorGUI.indentLevel--;
        EditorGUI.EndDisabledGroup();		

        if (avatar.faceAnimations.Count > 0)
        {
            EditorGUILayout.Space();
            string[] urns = avatar.faceAnimations.Keys.ToArray();
            selectedFaceAnimation = EditorGUILayout.Popup("Face animation", selectedFaceAnimation, urns);
            string urn = urns[selectedFaceAnimation];
            AnimationFramework framework = avatar.faceAnimations[urn];
            ArfControllers controllers;
            if (!faceControllers.ContainsKey(urn)) {
                controllers = new ArfControllers(framework, avatar.faceMappers[urn]);
                faceControllers[urn] = controllers;
            }
            else {
                controllers = faceControllers[urn];
            }
            EditorGUI.BeginChangeCheck();
            for(int controllerIndex = 0; controllerIndex < controllers.count; controllerIndex++)
            {
                EditorGUILayout.LabelField(controllers.names[controllerIndex]);
                controllers.weights[controllerIndex] = EditorGUILayout.Slider(
                    controllers.weights[controllerIndex], 
                    controllers.mins[controllerIndex], 
                    controllers.maxs[controllerIndex]
                );
            }
            if (EditorGUI.EndChangeCheck())
            {
                try {
                    controllers.UpdateComponents(avatar.animationComponents);
                    avatar.UpdateGameObjects();
                }
                catch(Exception e) {
                    Debug.LogError($"Error during components update: {e}");
                }
            }
        }
    }

}

}
}
