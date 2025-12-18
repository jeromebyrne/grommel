using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public static class OllamaClient
{
    static readonly HttpClient _client = new HttpClient();

    [Serializable]
    class GenerateRequest
    {
        public string model;
        public string prompt;
        public bool stream;
    }

    [Serializable]
    class GenerateResponse
    {
        public string response;
        public bool done;
    }

    public static async Task<string> GenerateAsync(string model, string prompt)
    {
        var request = new GenerateRequest
        {
            model = model,
            prompt = prompt,
            stream = false
        };

        string json = JsonUtility.ToJson(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var httpResponse = await _client.PostAsync("http://localhost:11434/api/generate", content);
        string body = await httpResponse.Content.ReadAsStringAsync();

        var response = JsonUtility.FromJson<GenerateResponse>(body);
        return response != null ? response.response : string.Empty;
    }

    public static async Task<string> GenerateStreamAsync(string model, string prompt, System.Action<string> onDelta)
    {
        var request = new GenerateRequest
        {
            model = model,
            prompt = prompt,
            stream = true
        };

        string json = JsonUtility.ToJson(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "http://localhost:11434/api/generate")
        {
            Content = content
        };

        var httpResponse = await _client.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead);
        using var responseStream = await httpResponse.Content.ReadAsStreamAsync();
        using var reader = new System.IO.StreamReader(responseStream, Encoding.UTF8);

        var sb = new StringBuilder();
        var buffer = new char[2048];
        var leftover = new StringBuilder();

        bool done = false;

        while (!done)
        {
            int read = await reader.ReadAsync(buffer, 0, buffer.Length);
            if (read <= 0)
            {
                break;
            }

            leftover.Append(buffer, 0, read);
            string combined = leftover.ToString();
            int lastNewline = combined.LastIndexOf('\n');

            if (lastNewline >= 0)
            {
                string[] lines = combined.Split('\n');
                // Keep the last fragment (could be partial)
                leftover.Length = 0;
                leftover.Append(lines[lines.Length - 1]);

                for (int i = 0; i < lines.Length - 1; i++)
                {
                    done = ProcessLine(lines[i], sb, onDelta);
                    if (done)
                    {
                        break;
                    }
                }
            }
        }

        // Process any remaining fragment
        if (!done && leftover.Length > 0)
        {
            ProcessLine(leftover.ToString(), sb, onDelta);
        }

        return sb.ToString();
    }

    static bool ProcessLine(string line, StringBuilder aggregate, System.Action<string> onDelta)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        GenerateResponse delta;
        try
        {
            delta = JsonUtility.FromJson<GenerateResponse>(line);
        }
        catch (System.Exception)
        {
            return false; // skip malformed lines
        }

        if (delta != null && !string.IsNullOrEmpty(delta.response))
        {
            aggregate.Append(delta.response);
            onDelta?.Invoke(aggregate.ToString());
        }

        return delta != null && delta.done;
    }
}
