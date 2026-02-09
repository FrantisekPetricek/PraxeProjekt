using UnityEngine;
using System.IO;
using System.Text;
using System; // Dùležité pro BitConverter

public static class WavUtility
{
    // --- VAŠE PÙVODNÍ FUNKCE (PRO ODESÍLÁNÍ NA SERVER) ---
    public static byte[] FromAudioClip(AudioClip clip)
    {
        using (var stream = new MemoryStream())
        {
            var writer = new BinaryWriter(stream);
            var samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + samples.Length * 2);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((ushort)(clip.channels * 2));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(samples.Length * 2);

            foreach (var sample in samples)
            {
                writer.Write((short)(sample * 32767f));
            }
            return stream.ToArray();
        }
    }

    // --- NOVÉ FUNKCE (PRO PØEHRÁVÁNÍ STREAMU ZE SERVERU) ---

    /// <summary>
    /// Pøevede RAW WAV byty (hlavièka + data) na Unity AudioClip.
    /// Používá se pro zpracování streamu z XTTS.
    /// </summary>
    public static AudioClip ToAudioClip(byte[] wavFile)
    {
        try
        {
            // XTTS a vìtšina generátorù vrací standardní WAV s 44-byte hlavièkou
            int headerOffset = 44;

            // Pokud je soubor pøíliš malý na to, aby mìl hlavièku, vrátíme null
            if (wavFile.Length < headerOffset) return null;

            // Ètení metadat z hlavièky (dle specifikace WAV)
            // Offset 22: Poèet kanálù (2 byty)
            int channels = wavFile[22];

            // Offset 24: Sample rate (4 byty)
            int frequency = BitConverter.ToInt32(wavFile, 24);

            // Konverze dat (pøedpokládáme 16-bit PCM, což XTTS používá)
            float[] data = Convert16BitByteArrayToAudioClipData(wavFile, headerOffset, wavFile.Length - headerOffset);

            if (data.Length == 0) return null;

            // Vytvoøení clipu v Unity
            AudioClip clip = AudioClip.Create("StreamedVoice", data.Length, channels, frequency, false);
            clip.SetData(data, 0);
            return clip;
        }
        catch (Exception e)
        {
            Debug.LogError("Chyba pøi parsování WAV streamu: " + e.Message);
            return null;
        }
    }

    /// <summary>
    /// Pomocná funkce pro pøevod pole bytù (16-bit integer) na pole floatù (-1.0 až 1.0)
    /// </summary>
    private static float[] Convert16BitByteArrayToAudioClipData(byte[] source, int offset, int length)
    {
        // Každý vzorek má 2 byty (16 bitù)
        int sampleCount = length / 2;
        float[] data = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            // Vezmeme dva byty z pole
            int sourceIndex = offset + i * 2;

            // Bezpeènostní kontrola, abychom neèetli mimo pole
            if (sourceIndex + 1 >= source.Length) break;

            // Pøevedeme 2 byty na èíslo (short / Int16)
            short value = BitConverter.ToInt16(source, sourceIndex);

            // Normalizace: short má rozsah -32768 až 32767. 
            // Unity chce float v rozsahu -1.0 až 1.0.
            data[i] = value / 32768.0f;
        }

        return data;
    }
}