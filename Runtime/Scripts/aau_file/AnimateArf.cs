//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
using Interdigital.Arf;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
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
    public bool convertAxis = true;
    public string animationFilePath;
    public string animationFrameworkURN = "urn:blender:avatar:animation:2024";

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
        mapper = avatar.bodyMappers[animationFrameworkURN];
        arf.Close();

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
            if (timestamp > timeAccumulator) {
                break; // change to continue when problems are solved
            }
            Debug.Log($"timeAccumulator: {timeAccumulator}, timestamp: {timestamp}, type: {sample.GetUnitType()}");
            queue.TryDequeue(out _);
            if (sample.GetUnitType() == AnimationUnitType.AAU_BLENDSHAPE)
            { 
                var blendshape = sample.ToBlendshapeSample(); 
                //Debug.Log($"Blendshape w9: {blendshape.GetBlendshapeWeight(9)}");
                mapper.UpdateComponents(components, blendshape);
                avatar.UpdateGameObjects(convertAxis);
            }
            else if (sample.GetUnitType() == AnimationUnitType.AAU_JOINT)
            { 
                var joint = sample.ToJointSample(); 
                mapper.UpdateComponents(components, joint);
                avatar.UpdateGameObjects(convertAxis);
            }
            break;
        }
    }


    List<GameObject> orderedJoints = new List<GameObject>();
    int currentId = 0;
    void BuildNodes(Transform root)
    {
        if (root == null)
            return;

        orderedJoints.Clear();
        currentId = 0;

        void Traverse(Transform t)
        {
            bool isOriginalRoot = (t == root);

            if (!isOriginalRoot) // todo see if we keep that
            {
                orderedJoints.Add(t.gameObject);

                int id = currentId;
                currentId++;

                GameObject meshGO = t.gameObject;

                ArfComponent component = meshGO.GetComponent<ArfComponent>();
                if (component == null)
                {
                    component = meshGO.AddComponent<ArfComponent>();
                }

                component.skeleton = 0;

                SkinnedMeshRenderer renderer = meshGO.GetComponent<SkinnedMeshRenderer>();
                if (renderer == null)
                    renderer = meshGO.AddComponent<SkinnedMeshRenderer>();
            }

            for (int i = 0; i < t.childCount; i++)
            {
                Traverse(t.GetChild(i));
            }
        }

        Traverse(root);
    }
}

}
}
