//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Interdigital.Arf;
using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Playables;

namespace Interdigital {
namespace Arf {

public class AnimateArf : MonoBehaviour
{
    public string filePath;
    public int lod = 0;
    public bool loadBlendshapes = true;
    public bool loadSkeletons = true;
    public bool loadSkins = true;
    public string animationFilePath;
    public string animationFrameworkURN;


    public int maxQueueSize = 10;
    private ConcurrentQueue<UnitAnimationSample> queue;
    private AnimationSampleProducer producer;

    void OnEnable()
    {
        queue = new ConcurrentQueue<UnitAnimationSample>();
        producer = new AnimationSampleProducer(animationFilePath, queue, maxQueueSize);
        producer.Start();
    }

    void OnDisable()
    {
        producer?.Stop();
        producer = null;
    }

    void Start()
    {       
        ArfParser arf = ArfParser.Load(filePath); 
        
        GameObject avatar = arf.createAvatar(
            transform, lod,
            loadBlendshapes: loadBlendshapes,
            loadSkeletons: loadSkeletons,
            loadSkins: loadSkins
        );


    }

    void Update()
    {
        // _timeAccumulator += Time.deltaTime;
        /*while (queue.TryPeek(out var next)) // && next.GetTimestamp() <= _timeAccumulator)
        {
            queue.TryDequeue(out _);
        }*/
    }



}

}
}
