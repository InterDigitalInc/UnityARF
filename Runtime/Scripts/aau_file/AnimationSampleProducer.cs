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
using Interdigital.Arf;

public class AnimationSampleProducer : IDisposable
{
    private readonly string path;
    private readonly ConcurrentQueue<(long LoopIndex, HeaderAnimationSample Header, UnitAnimationSample Sample)> queue;
    private readonly int maxQueueSize;
    private readonly CancellationTokenSource cts = new();
    private Task worker;
    private Stream fs;

    public AnimationSampleProducer(string path, ConcurrentQueue<(long LoopIndex, HeaderAnimationSample Header, UnitAnimationSample Sample)> queue, int maxQueueSize = 300)
    {
        this.path = path;
        this.queue = queue;
        this.maxQueueSize = Math.Max(1, maxQueueSize);
    }

    public static (HeaderAnimationSample, UnitAnimationSample) GetNextSample(Interdigital.Arf.AnimationSampleStream stream, Stream fs)
	{
		while(true)
		{
			var (header, sample) = stream.NextUnitSample();
			if (header.IsValid() && sample.IsValid()) {
				return (header, sample);
			}
			sample.Dispose();
			header.Dispose();
			if (stream.GetState() == Interdigital.Arf.AnimationSampleStreamState.NOT_ENOUGH_DATA) 
			{
			    if (fs.Position >= fs.Length) {
					throw new EndOfStreamException();
				}
				int count = 1024;
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
            AnimationSampleStream stream = new AnimationSampleStream(isLittleEndian:false);
            long loopIndex = 0;
            bool producedSampleInLoop = false;
            try
            {
                while (!cts.IsCancellationRequested)
                {
                    while (queue.Count >= maxQueueSize && !cts.IsCancellationRequested) {
                        await Task.Delay(5, token);
                    }
                    if (cts.IsCancellationRequested) {
                        break;
                    }

                    HeaderAnimationSample header;
                    UnitAnimationSample sample;
                    try
                    {
			            (header, sample) = GetNextSample(stream, fs);
                        if (cts.IsCancellationRequested) {
                            sample.Dispose();
                            header.Dispose();
                            break;
                        }
                        queue.Enqueue((loopIndex, header, sample));
                        producedSampleInLoop = true;
                    }
                    catch (EndOfStreamException)
                    {
                        if (!producedSampleInLoop) {
                            Console.Error.WriteLine("The animation stream contains no samples");
                            break;
                        }
                        stream.Dispose();
                        stream = new AnimationSampleStream(isLittleEndian:false);
                        fs.Seek(0, SeekOrigin.Begin);
                        loopIndex++;
                        producedSampleInLoop = false;
                        continue;
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Failed to parse animation sample: {ex.Message}");
                        break;
                    }
                }
            }
            finally
            {
                stream.Dispose();
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"AnimationSampleProducer error: {ex}");
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
