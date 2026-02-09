using UnityEngine;
using System;

public static class SimpleWav
{
    public static AudioClip ToAudioClip(byte[] wavFile)
    {
        try
        {
            int channels = wavFile[22];
            int frequency = BitConverter.ToInt32(wavFile, 24);
            int pos = 12;

            // Hledání 'fmt ' a 'data' chunku (zjednodušené)
            while (!(wavFile[pos] == 100 && wavFile[pos + 1] == 97 && wavFile[pos + 2] == 116 && wavFile[pos + 3] == 97))
            {
                pos += 4;
                int chunkSize = wavFile[pos] + wavFile[pos + 1] * 256 + wavFile[pos + 2] * 65536 + wavFile[pos + 3] * 16777216;
                pos += 4 + chunkSize;
                if (pos >= wavFile.Length - 8) return null; // Nenalezeno data
            }
            pos += 8;

            int sampleCount = (wavFile.Length - pos) / 2; // Pøedpoklad 16-bit PCM
            if (channels == 2) sampleCount /= 2;

            float[] data = new float[sampleCount];
            int i = 0;
            while (pos < wavFile.Length - 1 && i < sampleCount)
            {
                short val = (short)(wavFile[pos] | (wavFile[pos + 1] << 8));
                data[i] = val / 32768f;
                pos += 2;
                if (channels == 2) pos += 2; // Skip right channel for mono conversion (hack)
                i++;
            }

            AudioClip clip = AudioClip.Create("TTS_Clip", sampleCount, 1, frequency, false);
            clip.SetData(data, 0);
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError("Wav Parse Error: " + e.Message);
            return null;
        }
    }
}