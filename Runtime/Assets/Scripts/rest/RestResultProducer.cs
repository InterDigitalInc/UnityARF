using Interdigital.Arf;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Playables;

public class RestResultProducer : IDisposable
{
    private readonly string uri;
    private readonly ConcurrentQueue<FacesResult> queue;
    private readonly int maxQueueSize;
    private readonly CancellationTokenSource cts = new();
    private Task worker;

    public RestResultProducer(string uri, ConcurrentQueue<FacesResult> queue, int maxQueueSize = 10)
    {
        this.uri = uri;
        this.queue = queue;
        this.maxQueueSize = maxQueueSize;
    }

    public void Start()
    {
        worker = Task.Run(async () =>
        {
            try
            {
                while(!cts.IsCancellationRequested)
                {
                    using (UnityWebRequest request = UnityWebRequest.Get(uri))
                    {
                        request.SetRequestHeader("Accept", "application/json");

                        await request.SendWebRequest();
                        if (request.result == UnityWebRequest.Result.Success)
                        {
                            Debug.Log("Response: " + request.downloadHandler.text);
                        }
                        else
                        {
                            Debug.LogError($"GET failed: {request.responseCode} - {request.error}\n{request.downloadHandler.text}");
                        }
                    }
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                Debug.LogError($"RestResultProducer error: {ex}");
            }
        }, cts.Token);
    }

    public void Stop()
    {
        cts.Cancel();
        try { worker?.Wait(500); } catch { }
    }

    public void Dispose() => Stop();

}
