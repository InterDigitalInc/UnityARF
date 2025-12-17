//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.XR;

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
    public bool transposeTransforms = true;

    public ArfControllers(AnimationFramework framework, AnimationMapper mapper)
    {
        this.mapper = mapper;
        names = framework.GetInputNames();
        count = names.Length;
        weights = new float[count];
        (mins, maxs) = framework.GetInputRanges();
        animationData = framework.CreateData();
    }

    public void UpdateComponents(AnimationComponents components) 
    {
        animationData.SetWeights(weights);
        mapper.UpdateComponents(components, animationData);
    }

};

}
}
