using System;
using System.IO;
using UnityEngine;

namespace ChatMod
{
    /// <summary>PCM 16-bit WAV → AudioClip (used when Unity's loader returns bad clips).</summary>
    public static class WavLoader
    {
        public static AudioClip? LoadFromFile(string wavPath)
        {
            if (!File.Exists(wavPath))
                return null;

            byte[] bytes;
            try
            {
                bytes = File.ReadAllBytes(wavPath);
            }
            catch
            {
                return null;
            }

            if (bytes.Length < 44)
                return null;
            if (bytes[0] != 'R' || bytes[1] != 'I' || bytes[2] != 'F' || bytes[3] != 'F')
                return null;
            if (bytes[8] != 'W' || bytes[9] != 'A' || bytes[10] != 'V' || bytes[11] != 'E')
                return null;

            int channels = 1;
            int sampleRate = 44100;
            byte[]? dataChunk = null;
            int offset = 12;

            while (offset + 8 <= bytes.Length)
            {
                string chunkId = System.Text.Encoding.ASCII.GetString(bytes, offset, 4);
                int chunkSize = bytes[offset + 4] | (bytes[offset + 5] << 8) | (bytes[offset + 6] << 16) | (bytes[offset + 7] << 24);
                offset += 8;

                if (offset + chunkSize > bytes.Length)
                    break;

                if (chunkId == "fmt ")
                {
                    if (chunkSize >= 16)
                    {
                        int format = bytes[offset] | (bytes[offset + 1] << 8);
                        if (format != 1)
                            return null;
                        channels = bytes[offset + 2] | (bytes[offset + 3] << 8);
                        sampleRate = bytes[offset + 4] | (bytes[offset + 5] << 8) | (bytes[offset + 6] << 16) | (bytes[offset + 7] << 24);
                        int bitsPerSample = bytes[offset + 14] | (bytes[offset + 15] << 8);
                        if (bitsPerSample != 16)
                            return null;
                    }
                }
                else if (chunkId == "data")
                {
                    dataChunk = new byte[chunkSize];
                    Buffer.BlockCopy(bytes, offset, dataChunk, 0, chunkSize);
                }

                offset += chunkSize;
            }

            if (dataChunk == null || dataChunk.Length == 0 || channels < 1 || channels > 2)
                return null;

            int sampleCount = dataChunk.Length / 2;
            int frameCount = sampleCount / channels;
            var floatSamples = new float[sampleCount];

            for (int i = 0; i < frameCount; i++)
            {
                for (int c = 0; c < channels; c++)
                {
                    int idx = (i * channels + c) * 2;
                    int s = dataChunk[idx] | (dataChunk[idx + 1] << 8);
                    if (s >= 32768) s -= 65536;
                    floatSamples[i * channels + c] = s / 32768f;
                }
            }

            var clip = AudioClip.Create("ChatNotification", frameCount, channels, sampleRate, false);
            if (!clip.SetData(floatSamples, 0))
                return null;

            return clip;
        }
    }
}
