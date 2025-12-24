using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Bridge to the Coqui TTS CLI. Generates a WAV file and plays it through a provided AudioSource.
/// </summary>
public class CoquiTts : ITtsProvider
{
    readonly string _ttsExecutable;
    readonly string _modelName;
    readonly string _speakerIdx;

    public float LengthScale => 1f;

    public CoquiTts(
        string ttsExecutable = null,
        string modelName = "tts_models/en/vctk/vits",
        string speakerIdx = "p364")
    {
        string projectRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, ".."));
        _ttsExecutable = ttsExecutable ?? System.IO.Path.Combine(projectRoot, "coqui-tts-env/bin/tts");
        _modelName = modelName;
        _speakerIdx = speakerIdx;
    }

    public async Task<AudioClip> GenerateClipAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        string tempWavPath = Path.Combine(Application.temporaryCachePath, "npc_coqui.wav");
        string escapedText = text.Replace("\"", "\\\"");

        // Run the heavy CLI work off the Unity thread.
        bool ok = await Task.Run(() =>
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _ttsExecutable,
                Arguments = $"--model_name \"{_modelName}\" --speaker_idx {_speakerIdx} --text \"{escapedText}\" --out_path \"{tempWavPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            // Ensure Coqui finds espeak-ng when Unity doesn't inherit shell env.
            startInfo.EnvironmentVariables["TTS_ESPEAK_LIBRARY"] = "/opt/homebrew/opt/espeak-ng/lib/libespeak-ng.dylib";
            startInfo.EnvironmentVariables["PATH"] = "/opt/homebrew/bin:" + startInfo.EnvironmentVariables["PATH"];

            string stdout = string.Empty;
            string stderr = string.Empty;

            try
            {
                using (var process = Process.Start(startInfo))
                {
                    stdout = process.StandardOutput.ReadToEnd();
                    stderr = process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    if (process.ExitCode != 0)
                    {
                        UnityEngine.Debug.LogError($"Coqui TTS exited with code {process.ExitCode}. Stdout: {stdout} Stderr: {stderr}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"Coqui TTS failed to start: {ex.Message}");
                return false;
            }

            if (!File.Exists(tempWavPath))
            {
                UnityEngine.Debug.LogError("Coqui TTS did not produce an audio file.");
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
                    UnityEngine.Debug.LogError("Failed to load Coqui audio: " + request.error);
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
