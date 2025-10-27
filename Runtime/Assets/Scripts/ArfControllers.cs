//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using UnityEngine;

namespace Interdigital {
namespace Arf {

public class ArfControllers
{
    public long count;
    public string[] names;
    public float[] mins, maxs;
    public float[] weights;
    public AnimationMapper mapper;
    public AnimationData animationData;

    public ArfControllers(Arf arf, AnimationFramework framework)
    {
        names = framework.GetInputNames();
        count = names.Length;
        weights = new float[count];
        (mins, maxs) = framework.GetInputRanges();
        mapper = new AnimationMapper(arf, framework);
        animationData = framework.CreateData();
    }

    public void UpdateComponents(GameObject avatar, AnimationComponents components) 
    {
        animationData.SetWeights(weights);
        mapper.UpdateComponents(components, animationData);
        UpdateGameObjects(avatar, components);
    }

    public void UpdateGameObjects(GameObject gameObject, AnimationComponents components) 
    {
        if (gameObject.TryGetComponent(out ArfComponent component))
        {
            if (gameObject.TryGetComponent(out SkinnedMeshRenderer renderer))
            {
                if (component.blendshapeSet.HasValue) 
                { 
                    long blendshapeSet = component.blendshapeSet.Value;                
                    float[] weights = components.GetBlendshapeWeights(blendshapeSet);
                    for (int i = 0; i < weights.Length; i++) {
                        renderer.SetBlendShapeWeight(i, weights[i]);
                    }
                }
                if (component.skeleton.HasValue) 
                { 
                    long skeleton = component.skeleton.Value;        
                    long jointCount = components.GetJointCount(skeleton);
                    float[] transforms = components.GetJointTransforms(skeleton);
                    for (int i = 0; i < jointCount; i++) {
                        UnityConvert.SetTransform(renderer.bones[i], transforms, i * 16);
                    }
                }
            }
        }
        
        foreach (Transform child in gameObject.transform)
        {
            UpdateGameObjects(child.gameObject, components);
        }
    }
};

}
}
