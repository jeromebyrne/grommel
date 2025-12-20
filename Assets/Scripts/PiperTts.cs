using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Bridge to the Piper TTS CLI. Feeds text via stdin and plays the generated WAV.
/// </summary>
public static class PiperTts
{
    // Adjust these to your install.
    const string PiperExecutable = "/Users/jeromebyrne/Documents/git/grommel/piper1-gpl-git/.venv/bin/piper";
    const string ModelPath = "/Users/jeromebyrne/models/piper/en_GB-vctk-medium.onnx";
    const string TempFileName = "npc_piper.wav";

    public static async Task<AudioClip> GenerateClipAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string tempWavPath = Path.Combine(Application.temporaryCachePath, TempFileName);

        bool ok = await Task.Run(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = PiperExecutable,
                Arguments = $"--model \"{ModelPath}\" --output_file \"{tempWavPath}\"",
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
