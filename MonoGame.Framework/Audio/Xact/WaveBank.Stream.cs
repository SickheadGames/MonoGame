// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Xna.Framework.Audio
{
    partial class WaveBank
    {
        private SoundEffectInstance PlatformCreateStream(StreamInfo info)
        {
            MiniFormatTag codec;
            int channels, rate, alignment;
            DecodeFormat(info.Format, out codec, out channels, out rate, out alignment);

            var sound = new DynamicSoundEffectInstance(rate, channels == 2 ? AudioChannels.Stereo : AudioChannels.Mono);
            sound._isXAct = true;

            // This is 1 second of audio per buffer.
            var bufferSize = rate * alignment;

            var queue = new ConcurrentQueue<byte[]>();
            var signal = new AutoResetEvent(false);
            var stop = new AutoResetEvent(false);

            sound.BufferNeeded += (o, e) =>
            {
                byte[] buff = null;

                // We need to retry here until we submit a 
                // buffer or the stream is finished.
                while (true)
                {
                    // Submit all the buffers we got to keep the sound fed.         
                    int submitted = 0;
                    while (queue.Count > 0)
                    {
                        if (queue.TryDequeue(out buff))
                        {
                            sound.SubmitBuffer(buff);
                            submitted++;
                        }
                    }

                    // Tell the task to go read more buffers while
                    // the buffers we just submitted are played.
                    signal.Set();

                    // If we submitted buffers then we're done.
                    if (submitted > 0)
                        return;

                    // If there were no buffers then look and see if we've 
                    // reached the end of the stream and should stop.
                    if (stop.WaitOne(0))
                    {
                        sound.Stop();
                        return;
                    }
                }
            };
                                                                                                
            var task = Task.Factory.StartNew(() =>
            {
                var stream = AudioEngine.OpenStream(_waveBankFileName);
                var start = _playRegionOffset + info.FileOffset;
                var length = info.FileLength;
                stream.Seek(start, SeekOrigin.Begin);

                var bindex = 0;
                var buffers = new byte[][]
                {
                    new byte[bufferSize],
                    new byte[bufferSize],
                    new byte[bufferSize],
                };

                while (!sound.IsDisposed)
                {
                    while (queue.Count < 3 && length > 0)
                    {
                        var buffer = buffers[bindex % 3];
                        ++bindex;

                        var read = Math.Min(bufferSize, length);
                        read = stream.Read(buffer, 0, read);
                        length -= read;
                        queue.Enqueue(buffer);

                        // If we've run out of file then the sound should 
                        // stop and this task can complete.
                        if (length <= 0)
                        {
                            stop.Set();
                            stream.Close();
                            return;
                        }
                    }

                    // Wait for a signal that we need more buffers.
                    signal.WaitOne(1000);
                }

                stream.Close();
            });


            return sound;
        }
    }
}

