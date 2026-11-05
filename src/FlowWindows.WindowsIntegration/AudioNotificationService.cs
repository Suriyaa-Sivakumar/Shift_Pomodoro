using System;
using System.IO;
using System.Media;
using System.Threading.Tasks;
using FlowWindows.Core.Enums;
using FlowWindows.Core.Interfaces;

namespace FlowWindows.WindowsIntegration;

public class AudioNotificationService : IAudioNotificationService
{
    public void PlaySessionCompleteSound(SoundEffect effect, int volumePercent = 80)
    {
        if (effect == SoundEffect.None) return;
        Task.Run(() => PlaySynthesizedSound(effect, volumePercent));
    }

    public void PlayBreakCompleteSound(SoundEffect effect, int volumePercent = 80)
    {
        if (effect == SoundEffect.None) return;
        Task.Run(() => PlaySynthesizedSound(effect, volumePercent));
    }

    public void PlayTickSound()
    {
        Task.Run(() => PlayClick(1200, 0.02, 0.2));
    }

    public void PlayMetronomeSound()
    {
        Task.Run(() => PlayClick(800, 0.03, 0.4));
    }

    private static void PlaySynthesizedSound(SoundEffect effect, int volumePercent)
    {
        try
        {
            var volume = Math.Clamp(volumePercent / 100.0, 0.0, 1.0);
            byte[] wavBytes = effect switch
            {
                SoundEffect.Bell => GenerateBellWav(volume),
                SoundEffect.Chime => GenerateChimeWav(volume),
                SoundEffect.GentleGong => GenerateGongWav(volume),
                SoundEffect.DigitalAlert => GenerateDigitalAlertWav(volume),
                SoundEffect.MinimalBeep => GenerateBeepWav(volume),
                _ => Array.Empty<byte>()
            };

            if (wavBytes.Length > 0)
            {
                using var ms = new MemoryStream(wavBytes);
                using var player = new SoundPlayer(ms);
                player.Play();
            }
        }
        catch
        {
            // Fallback to system asterisk
            SystemSounds.Asterisk.Play();
        }
    }

    private static void PlayClick(int freq, double durationSec, double volume)
    {
        try
        {
            var bytes = GenerateToneWav(new[] { freq }, durationSec, volume, decay: true);
            using var ms = new MemoryStream(bytes);
            using var player = new SoundPlayer(ms);
            player.Play();
        }
        catch { }
    }

    private static byte[] GenerateBellWav(double volume)
    {
        // 880 Hz with long gentle decay
        return GenerateToneWav(new[] { 880, 1760 }, 1.2, volume, decay: true);
    }

    private static byte[] GenerateChimeWav(double volume)
    {
        // 3-note chime: E5 (659Hz), G#5 (830Hz), B5 (987Hz)
        return GenerateMultiToneWav(new[] { 659, 830, 987 }, 0.2, 0.8, volume);
    }

    private static byte[] GenerateGongWav(double volume)
    {
        // 220 Hz low resonance with long slow decay
        return GenerateToneWav(new[] { 220, 440, 660 }, 1.8, volume, decay: true);
    }

    private static byte[] GenerateDigitalAlertWav(double volume)
    {
        // 1046 Hz then 1318 Hz
        return GenerateMultiToneWav(new[] { 1046, 1318 }, 0.12, 0.3, volume);
    }

    private static byte[] GenerateBeepWav(double volume)
    {
        return GenerateToneWav(new[] { 600 }, 0.2, volume, decay: false);
    }

    private static byte[] GenerateToneWav(int[] freqs, double durationSeconds, double volume, bool decay)
    {
        const int sampleRate = 44100;
        int totalSamples = (int)(sampleRate * durationSeconds);
        var samples = new short[totalSamples];

        for (int i = 0; i < totalSamples; i++)
        {
            double t = (double)i / sampleRate;
            double amp = volume;
            if (decay)
            {
                amp *= Math.Exp(-3.0 * t / durationSeconds);
            }

            double sampleSum = 0;
            foreach (var f in freqs)
            {
                sampleSum += Math.Sin(2.0 * Math.PI * f * t);
            }
            sampleSum /= freqs.Length;

            samples[i] = (short)(sampleSum * amp * short.MaxValue * 0.8);
        }

        return CreateWavContainer(samples, sampleRate);
    }

    private static byte[] GenerateMultiToneWav(int[] sequenceFreqs, double toneDuration, double ringDuration, double volume)
    {
        const int sampleRate = 44100;
        int toneSamples = (int)(sampleRate * toneDuration);
        int ringSamples = (int)(sampleRate * ringDuration);
        int totalSamples = (toneSamples * sequenceFreqs.Length) + ringSamples;
        var samples = new short[totalSamples];

        int currentOffset = 0;
        for (int note = 0; note < sequenceFreqs.Length; note++)
        {
            int f = sequenceFreqs[note];
            int noteLen = (note == sequenceFreqs.Length - 1) ? toneSamples + ringSamples : toneSamples;

            for (int i = 0; i < noteLen && (currentOffset + i) < totalSamples; i++)
            {
                double t = (double)i / sampleRate;
                double decay = (note == sequenceFreqs.Length - 1) ? Math.Exp(-2.5 * t / (toneDuration + ringDuration)) : 1.0;
                double sample = Math.Sin(2.0 * Math.PI * f * t) * volume * decay;
                samples[currentOffset + i] = (short)(sample * short.MaxValue * 0.8);
            }

            currentOffset += toneSamples;
        }

        return CreateWavContainer(samples, sampleRate);
    }

    private static byte[] CreateWavContainer(short[] samples, int sampleRate)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

        int byteRate = sampleRate * 2; // 16-bit mono
        int subChunk2Size = samples.Length * 2;
        int chunkSize = 36 + subChunk2Size;

        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(chunkSize);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // fmt chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16); // subchunk1 size
        bw.Write((short)1); // PCM format
        bw.Write((short)1); // Mono
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)2); // block align
        bw.Write((short)16); // bits per sample

        // data chunk
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(subChunk2Size);

        for (int i = 0; i < samples.Length; i++)
        {
            bw.Write(samples[i]);
        }

        bw.Flush();
        return ms.ToArray();
    }
}

