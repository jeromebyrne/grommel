using System.Threading.Tasks;

/// <summary>
/// Abstraction for large language model providers.
/// </summary>
public interface ILlmProvider
{
    Task<string> GetReplyAsync(string npcName, string npcPersona, string history, string playerLine);
}

public enum LlmProviderKind
{
    Ollama
}
