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
    public bool useAAU = true;
    public long blendshapeSetId = 1;

    private ArfAvatar avatar;
    private AnimationMapper mapper;
    private AnimationComponents components;
    private AnimationFramework animationFramework;
    private AnimationData animationData;
    private string[] blendshapesName;

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
        animationFramework = avatar.faceAnimations[animationFrameworkURN];
        blendshapesName = animationFramework.GetInputNames();
        animationData = animationFramework.CreateData();
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
                //Debug.LogWarning(lastResult.error);
            }
            else if (lastResult.content != null)
            {
                if (lastResult.content.faces?.Count > 0) {
                    UpdateAvatarBlendshapes(lastResult.content, 0);
                }
            }
        }
    }

    void UpdateAvatarBlendshapes(Faces content, int index)
    {
        if (content == null) return;
        var faces = content.faces;
        var contentBlendshapesName = content.blendshapesName;
        if (index < 0 || index >= faces.Count) return;
        var face = faces[index];
        if (face.blendshapes == null) return;
        var contentWeights = face.blendshapes.weights;
        if (contentWeights == null) return;
        if (contentWeights.Count == 0) return;
        if (contentBlendshapesName.Count != contentWeights.Count) {
            Debug.LogWarning($"Invalid weight count {contentBlendshapesName.Count} != {contentWeights.Count}");
            return;
        }
        var totalBlendshapeCount = animationData.weights.Length;
        if (blendshapesName.Length != totalBlendshapeCount) {
            Debug.LogWarning($"Invalid blendshape count {blendshapesName.Length} != {totalBlendshapeCount}");
            return;
        }
        float[] weights = new float[totalBlendshapeCount];
        for (int i = 0; i < totalBlendshapeCount; i++) 
        {
            var j = contentBlendshapesName.IndexOf(blendshapesName[i]);
            if (j >= 0) {
                weights[i] = contentWeights[j];
            }
            else {
                weights[i] = 0;
            }
        }
        if (useAAU) {
            BlendshapeAnimationSample blendshapeAnimationSample = BlendshapeAnimationSample.Create();
            blendshapeAnimationSample.setId = blendshapeSetId;
            blendshapeAnimationSample.confidencePresent = false;
            blendshapeAnimationSample.SetBlendshapeCount(weights.Length);
            for (long i = 0; i < weights.Length; i++) { 
                blendshapeAnimationSample.SetBlendshapeId(i, i);
                blendshapeAnimationSample.SetBlendshapeWeight(i, weights[i]);
            }
            mapper.UpdateComponents(components, blendshapeAnimationSample);
        }
        else { 
            animationData.weights = weights;
            mapper.UpdateComponents(components, animationData);
        }
        avatar.UpdateGameObjects();
    }
}
