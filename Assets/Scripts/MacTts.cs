using System.Diagnostics;
using System.Text.RegularExpressions;

public static class MacTts
{
    public static void SpeakWhisperingParasite(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string ttsText = PrepareForTts(text);
        string escaped = "\"" + ttsText.Replace("\"", "\\\"") + "\"";

        var p = new Process();
        p.StartInfo.FileName = "/usr/bin/say";
        p.StartInfo.Arguments = "-v Whisper -r 200 " + escaped;
        p.StartInfo.UseShellExecute = false;
        p.StartInfo.CreateNoWindow = true;
        p.Start();
    }

    static string PrepareForTts(string text)
    {
        return Regex.Replace(text, "(?i)\\bkranust\\b", "Krahnust");
    }
}