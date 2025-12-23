using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Bridge to the Piper TTS CLI. Feeds text via stdin and plays the generated WAV.
/// </summary>
public static class PiperTts
{
    // Resolved relative to the Unity project root (parent of Assets).
    static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
    static readonly string PiperExecutable = Path.Combine(ProjectRoot, "piper1-gpl-git/.venv/bin/piper");
    static readonly string ModelPath = Path.Combine(ProjectRoot, "Assets/Piper/Models/en_GB-vctk-medium.onnx"); // single-speaker
    public const string Speaker = "0"; // leave empty for single-speaker models
    public const float LengthScale = 1.7f; // >1 slows speech, <1 speeds it up
    const string TempFileName = "npc_piper.wav";

    public static async Task<AudioClip> GenerateClipAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string tempWavPath = Path.Combine(Application.temporaryCachePath, TempFileName);
        bool hasSpeaker = !string.IsNullOrWhiteSpace(Speaker);

        bool ok = await Task.Run(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = PiperExecutable,
                Arguments = hasSpeaker
                    ? $"--model \"{ModelPath}\" --output_file \"{tempWavPath}\" --speaker {Speaker} --length-scale {LengthScale.ToString(CultureInfo.InvariantCulture)}"
                    : $"--model \"{ModelPath}\" --output_file \"{tempWavPath}\" --length-scale {LengthScale.ToString(CultureInfo.InvariantCulture)}",
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

            if (!File.Exists(tempWavPath))
            {
                UnityEngine.Debug.LogError("Piper did not produce an audio file.");
                return false;
            }

            return true;
        });

        if (!ok)
        {
            return null;
        }

        try
        {
            using (var request = UnityWebRequestMultimedia.GetAudioClip("file://" + tempWavPath, AudioType.WAV))
            {
                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                {
                    UnityEngine.Debug.LogError("Failed to load Piper audio: " + request.error);
                    return null;
                }

                var clip = DownloadHandlerAudioClip.GetContent(request);
                return clip;
            }
        }
        finally
        {
            try
            {
                if (File.Exists(tempWavPath))
                {
                    File.Delete(tempWavPath);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
