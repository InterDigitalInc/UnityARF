//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Interdigital.Arf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Interdigital {
namespace Arf {

public class AnimateArf : MonoBehaviour
{
    public string filePath;
    public ArfParserOptions options = new ArfParserOptions();
    public bool convertAxis = true;
    public string animationFilePath;
    public string animationFrameworkURN = "urn:blender:avatar:animation:2024";

    public int maxQueueSize = 10;
    public int maxSamplesPerFrame = 10;
    private ConcurrentQueue<(long LoopIndex, HeaderAnimationSample Header, UnitAnimationSample Sample)> queue;
    private AnimationSampleProducer producer;
    private float timeAccumulator = 0;
    private long currentLoopIndex = -1;

    public class Config
    {
        public string profile;
        public float timeScale = 0;
        public Config(string profile, float timeScale) {
            this.profile = profile;
            this.timeScale = timeScale;
        }
    }
    private Dictionary<(AnimationUnitType, long), Config> configs;

    private ArfAvatar avatar;
    private AnimationMapper mapper;
    private AnimationComponents components;

    void OnEnable()
    {
        queue = new ConcurrentQueue<(long LoopIndex, HeaderAnimationSample Header, UnitAnimationSample Sample)>();
        producer = new AnimationSampleProducer(animationFilePath, queue, maxQueueSize);
        producer.Start();
        timeAccumulator = 0;
        currentLoopIndex = -1;
        configs = new Dictionary<(AnimationUnitType, long), Config>();
    }

    void OnDisable()
    {
        producer?.Stop();
        producer = null;
        while (queue != null && queue.TryDequeue(out var item)) {
            DisposeSample(item.Header, item.Sample);
        }
    }

    void Start()
    {       
        GameObject avatarObject = ArfParser.LoadAvatar(filePath, options);  
        avatar = avatarObject.GetComponent<ArfAvatar>();
        components = avatar.animationComponents;
        mapper = avatar.bodyMappers[animationFrameworkURN];

        timeAccumulator = 0;
    }

    void Update()
    {        
        if (mapper is null) {
            return;
        }
        timeAccumulator += Time.deltaTime;
        int processedSamples = 0;
        while (processedSamples < Math.Max(1, maxSamplesPerFrame) && queue.TryPeek(out var item))
        {
            var (loopIndex, header, sample) = item;
            if (!header.IsValid() || !sample.IsValid()) {
                if (queue.TryDequeue(out var invalidItem)) {
                    DisposeSample(invalidItem.Header, invalidItem.Sample);
                    processedSamples++;
                }
                continue;
            }
            if (loopIndex != currentLoopIndex) {
                currentLoopIndex = loopIndex;
                timeAccumulator = 0;
                configs.Clear();
            }
            if (Keyboard.current != null &&
                Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Application.Quit();
            }
            if (sample.GetUnitType() == AnimationUnitType.AAU_CONFIG) 
            {
                if (!queue.TryDequeue(out var configItem)) {
                    continue;
                }
                processedSamples++;
                try
                {
                    using (var config = configItem.Sample.ToConfigAnimationSample())
                    {
                        for (int i = 0; i < config.GetProfileCount(); i++)
                        {
                            var profile = config.GetProfile(i);
                            for (int j = 0; j < config.GetAssociationCount(i); j++)
                            {
                                var unitType = config.GetAssociationUnitType(i, j);
                                var setId = config.GetAssociationSetId(i, j);
                                var timescale = config.GetAssociationTimescale(i, j);
                                if (timescale <= 0) {
                                    Debug.LogError("Invalid time scale");
                                    continue;
                                }
                                configs[(unitType, setId)] = new Config(profile, timescale);
                            }
                        }
                    }
                }
                finally
                {
                    DisposeSample(configItem.Header, configItem.Sample);
                }
            }
            else
            { 
                var unitType = sample.GetUnitType();
                float timestamp = header.GetTimestamp();
                long setId = -1;
                if (unitType == AnimationUnitType.AAU_BLENDSHAPE)
                {
                    using (var blendshape = sample.ToBlendshapeAnimationSample()) {
                        setId = blendshape.GetSetId();
                    }
                }
                else if (unitType == AnimationUnitType.AAU_JOINT)
                {
                    using (var joint = sample.ToJointAnimationSample()) {
                        setId = joint.GetSetId();
                    }
                }
                else
                {
                    if (queue.TryDequeue(out var unsupportedItem)) {
                        DisposeSample(unsupportedItem.Header, unsupportedItem.Sample);
                        processedSamples++;
                    }
                    continue;
                }

                if (configs.TryGetValue((unitType, setId), out var config))
                {
                    timestamp /= config.timeScale;
                    if (timestamp > timeAccumulator) {
                        break; // Too early, keep it for next
                    }
                    if (queue.TryDequeue(out var dueItem)) {
                        processedSamples++;
                        try {
                            mapper.UpdateComponents(components, dueItem.Sample);
                            avatar.UpdateGameObjects(convertAxis);
                        }
                        finally {
                            DisposeSample(dueItem.Header, dueItem.Sample);
                        }
                    }
                }
                else
                {
                    if (queue.TryDequeue(out var unmappedItem)) {
                        DisposeSample(unmappedItem.Header, unmappedItem.Sample);
                        processedSamples++;
                    }
                }
            }
        }
    }

    private static void DisposeSample(HeaderAnimationSample header, UnitAnimationSample sample)
    {
        sample?.Dispose();
        header?.Dispose();
    }
}

}
}
