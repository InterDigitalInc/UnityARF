//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Interdigital {
namespace Arf {

public class ArfAvatar : MonoBehaviour
{
    public string id;
    public double age;
    public string gender;
    private bool transposeTransforms = true;

    public Dictionary<string, AnimationFramework> faceAnimations = new Dictionary<string, AnimationFramework>();
    public Dictionary<string, AnimationFramework> bodyAnimations = new Dictionary<string, AnimationFramework>();
    public Dictionary<string, AnimationMapper> animationMappers = new Dictionary<string, AnimationMapper>();

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
                    AnimationFramework framework = AnimationFramework.Create(urn);    
                    faceAnimations[urn] = framework;
                    animationMappers[urn] = new AnimationMapper(arf, framework);
                }
                catch(Exception ex) {
                    Debug.LogWarning($"Animation framework {urn} is ignored because it is not supported (error: {ex})");
                }
            }
        }
        if (supportedAnimations.HasBodyAnimations()) 
        {
            foreach (string urn in supportedAnimations.bodyAnimations)
            { 
                try 
                {
                    AnimationFramework framework = AnimationFramework.Create(urn);    
                    bodyAnimations[urn] = framework;
                    animationMappers[urn] = new AnimationMapper(arf, framework);
                }
                catch(Exception ex) {
                    Debug.LogWarning($"Animation framework {urn} is ignored because it is not supported (error: {ex})");
                }
            }
        }

        animationComponents = new AnimationComponents(arf);
    }

    public void UpdateGameObjects()
    {
        UpdateGameObjects(gameObject);
    }

    public void UpdateGameObjects(GameObject gameObject) 
    {
        if (gameObject.TryGetComponent(out ArfComponent component))
        {
            if (gameObject.TryGetComponent(out SkinnedMeshRenderer renderer))
            {
                if (component.blendshapeSet.HasValue) 
                { 
                    long blendshapeSet = component.blendshapeSet.Value;                
                    float[] weights = animationComponents.GetBlendshapeWeights(blendshapeSet);
                    for (int i = 0; i < weights.Length; i++) {
                        renderer.SetBlendShapeWeight(i, weights[i]);
                    }
                }
                if (component.skeleton.HasValue) 
                { 
                    long skeleton = component.skeleton.Value;        
                    long jointCount = animationComponents.GetJointCount(skeleton);
                    float[] transforms = animationComponents.GetJointTransforms(skeleton);

                    //Debug.Log($"skeleton = {skeleton}, {jointCount}");
                    //Debug.Log(string.Join(", ", transforms));
                    //Debug.Log(string.Join(", ", transforms.Skip(16).Take(16)));
                    //Debug.Log(string.Join(", ", transforms.Skip(16*39).Take(16)));

                    for (int i = 0; i < jointCount; i++) {
                        Matrix4x4 mat = UnityConvert.ToMatrix4x4(transforms, i * 16);
                        if (transposeTransforms) {
                            mat = mat.transpose;
                        }
                        UnityConvert.SetLocalTransform(renderer.bones[i], mat);
                    }
                }
            }
        }
        
        foreach (Transform child in gameObject.transform)
        {
            UpdateGameObjects(child.gameObject);
        }
    }
};

}
}
