using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Bridge to the Coqui TTS CLI. Generates a WAV file and plays it through a provided AudioSource.
/// </summary>
public static class CoquiTts
{
    const string TtsExecutable = "/Users/jeromebyrne/coqui-tts-env/bin/tts"; // adjust to your env path if different
    const string ModelName = "tts_models/en/vctk/vits";
    const string SpeakerIdx = "p364";

    public static async Task PlayAsync(string text, AudioSource output)
    {
        if (string.IsNullOrWhiteSpace(text) || output == null)
        {
            return;
        }

        string tempWavPath = Path.Combine(Application.temporaryCachePath, "npc_coqui.wav");
        string escapedText = text.Replace("\"", "\\\"");

        var startInfo = new ProcessStartInfo
        {
            FileName = TtsExecutable,
            Arguments = $"--model_name \"{ModelName}\" --speaker_idx {SpeakerIdx} --text \"{escapedText}\" --out_path \"{tempWavPath}\"",
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

        using (var process = Process.Start(startInfo))
        {
            stdout = process.StandardOutput.ReadToEnd();
            stderr = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                UnityEngine.Debug.LogError($"Coqui TTS exited with code {process.ExitCode}. Stdout: {stdout} Stderr: {stderr}");
                return;
            }
        }

        if (!File.Exists(tempWavPath))
        {
            UnityEngine.Debug.LogError("Coqui TTS did not produce an audio file.");
            return;
        }

        using (var request = UnityWebRequestMultimedia.GetAudioClip("file://" + tempWavPath, AudioType.WAV))
        {
            await request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError("Failed to load Coqui audio: " + request.error);
                return;
            }

            var clip = DownloadHandlerAudioClip.GetContent(request);
            output.clip = clip;
            output.Play();
        }
    }
}
