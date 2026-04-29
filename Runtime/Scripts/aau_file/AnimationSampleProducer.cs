//
// Copyright (c) 2010-2025, InterDigital
// All rights reserved.
// See LICENSE under the root folder.
//
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
    private Stream fs;

    public AnimationSampleProducer(string path, ConcurrentQueue<UnitAnimationSample> queue, int maxQueueSize = 300)
    {
        this.path = path;
        this.queue = queue;
        this.maxQueueSize = maxQueueSize;
    }

    public static UnitAnimationSample GetNextSample(Interdigital.Arf.AnimationSampleStream stream, Stream fs)
	{
		while(true)
		{
			UnitAnimationSample sample = stream.NextUnitSample();
			if (sample.IsValid()) {
				return sample;
			}
			if (stream.GetState() == Interdigital.Arf.AnimationSampleStreamState.NOT_ENOUGH_DATA) 
			{
			    if (fs.Position >= fs.Length) {
					throw new EndOfStreamException();
				}
				int count = 100024;
				if (count > (fs.Length - fs.Position)) {
					count = (int)(fs.Length - fs.Position);
				}
				byte[] bytes = new byte[count];
				int n = fs.Read(bytes, 0, count);
				if (n != count) {
					throw new SystemException("I/O error");
				}
				stream.Feed(bytes); 
				continue;
			}
			throw new SystemException($"Failed getting next sample: {stream.GetErrorMsg()}");
		}
	}

    public void Start()
    {
        fs = StreamLoader.OpenRead(path);
        worker = Task.Run(() => WorkerLoop(cts.Token), cts.Token);
    }

    public async Task WorkerLoop(CancellationToken token)
    {
        try
        {                
            AnimationSampleStream stream = new AnimationSampleStream();
            while (!cts.IsCancellationRequested && fs.Position < fs.Length)
            {
                while (queue.Count >= maxQueueSize && !cts.IsCancellationRequested)
                    await Task.Delay(5, token);

                UnitAnimationSample sample;
                try
                {
			        sample = GetNextSample(stream, fs);
                    if (fs.Position >= fs.Length) {
                        fs.Seek(0, SeekOrigin.Begin);
                    }
                }
                catch (EndOfStreamException)
                {
                    fs.Seek(0, SeekOrigin.Begin);
                    continue;
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
    }

    public void Stop()
    {
        cts.Cancel();
        try { worker?.Wait(500); } catch { }
        fs?.Dispose();
        fs = null;

    }

    public void Dispose() => Stop();

}
