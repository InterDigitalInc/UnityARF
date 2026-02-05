using Interdigital.Arf;
using System;
using System.Collections.Concurrent;
using UnityEngine;

public class RestClient : MonoBehaviour
{
    public String uri = "https://localhost:6001/faces";
    public int maxQueueSize = 10;
    private ConcurrentQueue<FacesResult> queue;
    private RestResultProducer producer;

    void OnEnable()
    {
        queue = new ConcurrentQueue<FacesResult>();
        producer = new RestResultProducer(uri, queue, maxQueueSize);
        producer.Start();
    }

    void OnDisable()
    {
        producer?.Stop();
        producer = null;
    }

    void Start()
    {       
    }

    void Update()
    {    
    
    }
}
