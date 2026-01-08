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
    public string lodName = "high_quality";
    public bool loadBlendshapes = true;
    public bool loadSkeletons = true;
    public bool loadSkins = true;
    public string animationFilePath;
    private string animationFrameworkURN = "urn:khronos:openxr:body-tracking:meta-full-body";
    private long[] animatedJointIds = new long[] {
      // Head
      7,  // HEAD

      // Left arm chain (from shoulder)
      10, 11, 12, 18, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 39, 40, 41, 42, 43, // LEFT_ARM_UPPER to LEFT_HAND_WRIST_TWIST

      // Right arm chain (from shoulder)
      15, 16, 17, 44, 45, 46, 47, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66, 67, 68, 69 // RIGHT_ARM_UPPER to RIGHT_HAND_WRIST_TWIST
    };


    public int maxQueueSize = 10;
    private ConcurrentQueue<UnitAnimationSample> queue;
    private AnimationSampleProducer producer;
    private float timeAccumulator = 0;
    private float timeScale = 0;

    private ArfAvatar avatar;
    private AnimationMapper mapper;
    private AnimationComponents components;

    void OnEnable()
    {
        queue = new ConcurrentQueue<UnitAnimationSample>();
        producer = new AnimationSampleProducer(animationFilePath, queue, maxQueueSize);
        producer.Start();
        timeAccumulator = 0;
    }

    void OnDisable()
    {
        producer?.Stop();
        producer = null;
    }

    void Start()
    {       
        ArfParser arf = ArfParser.Load(filePath); 
        
        GameObject avatarObject = arf.createAvatar(
            transform, lodName,
            loadBlendshapes: loadBlendshapes,
            loadSkeletons: loadSkeletons,
            loadSkins: loadSkins
        );
        avatar = avatarObject.GetComponent<ArfAvatar>();
        components = avatar.animationComponents;
        mapper = avatar.animationMappers[animationFrameworkURN];

        timeAccumulator = 0;
    }

    void Update()
    {        
        if (mapper is null) {
            return;
        }
        timeAccumulator += Time.deltaTime;
        //Debug.Log($"timeAccumulator = {timeAccumulator}");
        while (queue.TryPeek(out var sample))
        {           
            if (sample.GetUnitType() == AnimationUnitType.AAU_CONFIG) 
            {
                var config = sample.ToConfigSample();
                timeScale = config.GetTimescale();
                queue.TryDequeue(out _);
                continue;
            }
            if (timeScale == 0) {
                Debug.LogError("No config unit defined a time scale");
                queue.TryDequeue(out _);
                break;
            }
            float timestamp = sample.GetTimestamp() / timeScale;
            //Debug.Log($"timestamp = {timestamp}");
            if (timestamp > timeAccumulator) {
                break;
            }
            queue.TryDequeue(out _);
            if (sample.GetUnitType() == AnimationUnitType.AAU_JOINT)
            { 
                var joint = sample.ToJointSample(); 
                joint.SelectJoints(animatedJointIds);
                mapper.UpdateComponents(components, joint);
                avatar.UpdateGameObjects();
            }
            mapper = null;
            break;
        }
    }



}

}
}
