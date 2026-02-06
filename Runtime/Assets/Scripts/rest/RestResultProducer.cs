using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.Networking;
using Debug = UnityEngine.Debug;

public class RestResultProducer : MonoBehaviour
{
    private float pollIntervalSeconds = 0.01f;
    private string uri;
    private ConcurrentQueue<FacesResult> queue;
    private int maxQueueSize;

    private Coroutine routine;
    private bool running;

    public void Init(string uri, ConcurrentQueue<FacesResult> queue, int maxQueueSize = 10, float pollIntervalSeconds = 0.1f)
    {
        this.uri = uri;
        this.queue = queue;
        this.maxQueueSize = maxQueueSize;
        this.pollIntervalSeconds = pollIntervalSeconds;
    }

    public void StartProducing()
    {
        if (running) return;
        running = true;
        routine = StartCoroutine(PollLoop());
    }

    public void StopProducing()
    {
        running = false;
        if (routine != null) StopCoroutine(routine);
        routine = null;
    }

    private IEnumerator PollLoop()
    {
        var wait = new WaitForSeconds(pollIntervalSeconds);

        while (running)
        {
            yield return GetOnce();
            yield return wait;
        }
    }

    private IEnumerator GetOnce()
    {
        using var request = UnityWebRequest.Get(uri);
        request.SetRequestHeader("Accept", "application/json");
        request.timeout = 10;

        yield return request.SendWebRequest();

        var result = new FacesResult();
        if (request.result == UnityWebRequest.Result.Success)
        {
            try
            {
                result = JsonUtility.FromJson<FacesResult>(request.downloadHandler.text);
                if (result.error == "ok") { 
                    result.error = null;
                }
            }
            catch (Exception ex)
            {
                result.content = null;
                result.error = "JSON parse error: " + ex.Message;
            }
        }
        else
        {
            result.content = null;
            result.error = $"HTTP {request.responseCode}: {request.error} | body={request.downloadHandler?.text}";
        }

        while (queue.Count >= maxQueueSize && queue.TryDequeue(out _)) { }
        queue.Enqueue(result);
    }

    private void OnDisable()
    {
        StopProducing();
    }
}