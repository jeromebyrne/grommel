using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Bridge to the Piper TTS CLI. Feeds text via stdin and plays the generated WAV.
/// </summary>
public class PiperTts : ITtsProvider
{
    readonly string _piperExecutable;
    readonly string _modelPath;
    readonly string _speaker;
    readonly float _lengthScale;

    public float LengthScale => _lengthScale;

    public PiperTts(
        string piperExecutable = null,
        string modelPath = null,
        string speaker = "0",
        float lengthScale = 1.75f)
    {
        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        _piperExecutable = piperExecutable ?? Path.Combine(projectRoot, "piper1-gpl-git/.venv/bin/piper");
        _modelPath = modelPath ?? Path.Combine(projectRoot, "Assets/Piper/Models/en_GB-vctk-medium.onnx");
        _speaker = speaker ?? string.Empty;
        _lengthScale = lengthScale;
    }

    public async Task<AudioClip> GenerateClipAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        bool hasSpeaker = !string.IsNullOrWhiteSpace(_speaker);
        byte[] wavBytes = null;

        bool ok = await Task.Run(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _piperExecutable,
                Arguments = hasSpeaker
                    ? $"--model \"{_modelPath}\" --output_file - --speaker {_speaker} --length-scale {_lengthScale.ToString(CultureInfo.InvariantCulture)}"
                    : $"--model \"{_modelPath}\" --output_file - --length-scale {_lengthScale.ToString(CultureInfo.InvariantCulture)}",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Make sure Homebrew binaries are visible when Unity doesn't inherit shell env.
            startInfo.EnvironmentVariables["PATH"] = "/opt/homebrew/bin:" + startInfo.EnvironmentVariables["PATH"];
            startInfo.EnvironmentVariables["DYLD_LIBRARY_PATH"] = "/opt/homebrew/opt/espeak-ng/lib";

            string stderr = string.Empty;

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    process.StandardInput.Write(text);
                    process.StandardInput.Close();

                    using (var ms = new MemoryStream())
                    {
                        process.StandardOutput.BaseStream.CopyTo(ms);
                        wavBytes = ms.ToArray();
                    }

                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"Piper exited with code {process.ExitCode}. Stderr: {stderr}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Piper failed to start: {ex.Message}");
                return false;
            }

            if (wavBytes == null || wavBytes.Length < 44)
            {
                UnityEngine.Debug.LogError("Piper did not produce audio bytes.");
                return false;
            }

            return true;
        });

        if (!ok)
        {
            return null;
        }

        var clipFromWav = TryCreateClipFromWav(wavBytes);
        if (clipFromWav == null)
        {
            UnityEngine.Debug.LogError("Failed to parse Piper audio stream.");
        }
        return clipFromWav;
    }

    static AudioClip TryCreateClipFromWav(byte[] wav)
    {
        try
        {
            using (var ms = new MemoryStream(wav))
            using (var br = new BinaryReader(ms))
            {
                var riff = br.ReadChars(4);
                if (new string(riff) != "RIFF")
                {
                    return null;
                }
                br.ReadInt32(); // file size
                var wave = br.ReadChars(4);
                if (new string(wave) != "WAVE")
                {
                    return null;
                }

                int channels = 1;
                int sampleRate = 24000;
                short bitsPerSample = 16;

                while (br.BaseStream.Position + 8 <= br.BaseStream.Length)
                {
                    var chunkId = new string(br.ReadChars(4));
                    int chunkSize = br.ReadInt32();
                    if (chunkId == "fmt ")
                    {
                        short audioFormat = br.ReadInt16();
                        channels = br.ReadInt16();
                        sampleRate = br.ReadInt32();
                        br.ReadInt32(); // byte rate
                        br.ReadInt16(); // block align
                        bitsPerSample = br.ReadInt16();
                        if (chunkSize > 16)
                        {
                            br.ReadBytes(chunkSize - 16);
                        }
                    }
                    else if (chunkId == "data")
                    {
                        var dataBytes = br.ReadBytes(chunkSize);
                        return BuildClip(dataBytes, channels, sampleRate, bitsPerSample);
                    }
                    else
                    {
                        br.BaseStream.Seek(chunkSize, SeekOrigin.Current);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError("Error parsing WAV: " + ex.Message);
        }

        return null;
    }

    static AudioClip BuildClip(byte[] data, int channels, int sampleRate, short bitsPerSample)
    {
        if (bitsPerSample != 16)
        {
            UnityEngine.Debug.LogError("Unsupported WAV bit depth: " + bitsPerSample);
            return null;
        }

        int sampleCount = data.Length / (bitsPerSample / 8);
        float[] samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            short sample = BitConverter.ToInt16(data, i * 2);
            samples[i] = sample / 32768f;
        }

        int lengthSamples = sampleCount / Math.Max(1, channels);
        var clip = AudioClip.Create("npc_piper", lengthSamples, Math.Max(1, channels), sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
