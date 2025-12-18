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
}