using Interdigital.Arf;
using System;
using System.Collections.Concurrent;
using UnityEngine;

public class RestClient : MonoBehaviour
{
    public String uri = "http://localhost:6001/faces";
    public int maxQueueSize = 10;
    public float pollIntervalSeconds = 0.01f;
    private ConcurrentQueue<FacesResult> queue;
    private RestResultProducer producer;
    private FacesResult lastResult;

    public string filePath;
    public string lodName = "high_quality";
    public bool loadBlendshapes = true;
    public bool loadSkeletons = true;
    public bool loadSkins = true;
    public string animationFrameworkURN = "urn:mpeg:morgan:blendshapes";

    private ArfAvatar avatar;
    private AnimationMapper mapper;
    private AnimationComponents components;
    private AnimationData animationData;

    private void Start()
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
        animationData = avatar.faceAnimations[animationFrameworkURN].CreateData();
    }

    void OnEnable()
    {
        queue = new ConcurrentQueue<FacesResult>();

        producer = gameObject.GetComponent<RestResultProducer>();
        if (producer == null) producer = gameObject.AddComponent<RestResultProducer>();

        producer.Init(uri, queue, maxQueueSize, pollIntervalSeconds);
        producer.StartProducing();
    }

    void OnDisable()
    {
        producer?.StopProducing();
        producer = null;
    }

    void Update()
    {
        if (mapper is null) {
            return;
        }
        while (queue.TryDequeue(out var r))
        {
            lastResult = r;
        }
        if (lastResult != null)
        {
            if (!string.IsNullOrEmpty(lastResult.error))
            {
                Debug.LogWarning(lastResult.error);
            }
            else if (lastResult.content != null)
            {
                if (lastResult.content.faces?.Count > 0) {
                    UpdateAvatarBlendshapes(lastResult.content.faces[0]);
                }
            }
        }
    }

    void UpdateAvatarBlendshapes(Face face)
    {
        if (face.blendshapes == null) return;
        var weights = face.blendshapes.weights;
        if (weights == null) return;
        var totalBlendshapeCount = animationData.weights.Length;
        if (weights.Count > totalBlendshapeCount) {
            Debug.LogWarning($"Invalid blendshape count {weights.Count} > {animationData.weights.Length}");
        }
        while(weights.Count < totalBlendshapeCount) {
            weights.Add(0f);
        }
        animationData.weights = weights.ToArray();
        mapper.UpdateComponents(components, animationData);
        avatar.UpdateGameObjects();
    }
}
