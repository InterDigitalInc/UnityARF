using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Playables;
using Interdigital.Arf;

public class AnimationSampleProducer : IDisposable
{
    private readonly string path;
    private readonly ConcurrentQueue<UnitAnimationSample> queue;
    private readonly int maxQueueSize;
    private readonly CancellationTokenSource cts = new();
    private Task worker;

    public AnimationSampleProducer(string path, ConcurrentQueue<UnitAnimationSample> queue, int maxQueueSize = 300)
    {
        this.path = path;
        this.queue = queue;
        this.maxQueueSize = maxQueueSize;
    }

    public void Start()
    {
        worker = Task.Run(async () =>
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 64 * 1024, useAsync: true);
                Debug.Log(path);
                AnimationSampleStream stream = new AnimationSampleStream();
                while (!cts.IsCancellationRequested && fs.Position < fs.Length)
                {
                    Debug.Log(queue.Count);
                    while (queue.Count >= maxQueueSize && !cts.IsCancellationRequested)
                        await Task.Delay(5, cts.Token);

                    UnitAnimationSample sample;
                    try
                    {
                        int count = 1024;
                        if (count > (fs.Length - fs.Position)) {
                            count = (int)(fs.Length - fs.Position);
                        }
                        byte[] bytes = new byte[count];
                        fs.Read(bytes, 0, count);
                        //Debug.Log(string.Join(", ", bytes));
                        stream.Feed(bytes);
                        sample = stream.NextUnitSample();
                        if (!sample.IsValid()) {
                            continue;
                        }
                        Debug.Log(sample.GetTimestamp());
                        Debug.Log(sample.GetUnitType());
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"Failed to parse animation sample: {ex.Message}");
                        break;
                    }
                    queue.Enqueue(sample);
                }
            }
            catch (OperationCanceledException) { /* normal shutdown */ }
            catch (Exception ex)
            {
                Debug.LogError($"AnimationSampleProducer error: {ex}");
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
