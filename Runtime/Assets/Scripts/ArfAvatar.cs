//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;
using System.Collections.Generic;

namespace Interdigital {
namespace Arf {

public class ArfAvatar : MonoBehaviour
{
    public string id;
    public double age;
    public string gender;

    public Dictionary<string, ArfControllers> faceAnimations = new Dictionary<string, ArfControllers>();

    public AnimationComponents animationComponents;

    public void Init(Arf arf)
    {
        Metadata metadata = arf.metadata;        
        name = metadata.name;
        id = metadata.id;
        age = metadata.age;
        gender = metadata.gender;

        Preamble preamble = arf.preamble;
        SupportedAnimations supportedAnimations = preamble.supportedAnimations;
        if (supportedAnimations.HasFaceAnimations()) 
        {
            foreach (string urn in supportedAnimations.faceAnimations)
            { 
                try 
                {
                    AnimationFramework framework = new AnimationFramework(urn);
                    framework.AutoFill();
                    faceAnimations[urn] = new ArfControllers(arf, framework);
                }
                catch {
                    Debug.Log($"Animation framework {urn} is ignored because it is not supported or its inputs are not weights");
                }
            }
        }

        animationComponents = new AnimationComponents(arf);
    }

    public void Update()
    {
        
    }
};

}
}
