using System.Text;
using System.Threading.Tasks;

public static class NpcDialogueService
{
    public static async Task<string> GetNpcReplyStreamed(string npcName, string persona, string history, string playerLine, System.Action<string> onDelta)
    {
        string prompt = BuildPrompt(npcName, persona, history, playerLine);
        string reply = await OllamaClient.GenerateStreamAsync("llama3:8b", prompt, onDelta);
        return reply;
    }

    public static async Task<string> GetNpcReply(string npcName, string persona, string history, string playerLine)
    {
        string prompt = BuildPrompt(npcName, persona, history, playerLine);
        string reply = await OllamaClient.GenerateAsync("llama3:8b", prompt);
        return reply;
    }

    static string BuildPrompt(string npcName, string persona, string history, string playerLine)
    {
        var sb = new StringBuilder();
        sb.Append("You are ");
        sb.Append(npcName);
        sb.Append(". ");
        sb.Append(persona);
        sb.Append("\n\nConversation so far:\n");
        sb.Append(history);
        sb.Append("\nPlayer: ");
        sb.Append(playerLine);
        sb.Append("\n");
        sb.Append(npcName);
        sb.Append(":");
        return sb.ToString();
    }
}
