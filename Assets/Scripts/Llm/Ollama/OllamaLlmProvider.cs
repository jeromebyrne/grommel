using System.Threading.Tasks;

/// <summary>
/// Ollama-backed LLM provider.
/// </summary>
public class OllamaLlmProvider : ILlmProvider
{
    readonly OllamaClient _client;
    readonly string _model;
    readonly IPromptBuilder _promptBuilder;

    public OllamaLlmProvider(string model = "llama3:8b", string baseUrl = "http://localhost:11434", IPromptBuilder promptBuilder = null)
    {
        _model = model;
        _client = new OllamaClient(baseUrl);
        _promptBuilder = promptBuilder ?? new DefaultPromptBuilder();
    }

    public Task<string> GetReplyAsync(string npcName, string npcPersona, string history, string playerLine)
    {
        string prompt = _promptBuilder.BuildPrompt(npcName, npcPersona, history, playerLine);
        return _client.GenerateAsync(_model, prompt);
    }
}
