using System.Threading.Tasks;

/// <summary>
/// Ollama-backed LLM provider.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    readonly OllamaClient _client;
    readonly string _model;

    public OllamaLlmProvider(string model = "llama3:8b", string baseUrl = "http://localhost:11434")
    {
        _model = model;
        _client = new OllamaClient(baseUrl);
    }

    public Task<string> GetReplyAsync(string npcName, string npcPersona, string history, string playerLine)
    {
        string prompt = NpcDialogueService.BuildPrompt(npcName, npcPersona, history, playerLine);
        return _client.GenerateAsync(_model, prompt);
    }
}
