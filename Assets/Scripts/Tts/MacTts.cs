using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using UnityEngine;

public class MacTts : ITtsProvider
{
    readonly string _voice;
    readonly int _rate;

    public float LengthScale => 1f; // macOS "say" rate is explicit; we keep text speed unchanged.

    public MacTts(string voice = "Whisper", int rate = 200)
    {
        _voice = voice;
        _rate = rate;
    }

    public Task<AudioClip> GenerateClipAsync(string text)
    {
        Speak(text);
        return Task.FromResult<AudioClip>(null);
    }

    void Speak(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string ttsText = PrepareForTts(text);
        string escaped = "\"" + ttsText.Replace("\"", "\\\"") + "\"";

        var p = new Process();
        p.StartInfo.FileName = "/usr/bin/say";
        p.StartInfo.Arguments = $"-v {_voice} -r {_rate} " + escaped;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();
    }

    static string PrepareForTts(string text)
    {
        return Regex.Replace(text, "(?i)\\bkranust\\b", "Krahnust");
    }
}
