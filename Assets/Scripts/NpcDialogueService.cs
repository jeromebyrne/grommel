using System;
using System.Text;
using System.Threading.Tasks;

public class NpcDialogueService
{
    readonly ILlmProvider _llm;

    public NpcDialogueService(ILlmProvider llm)
    {
        _llm = llm;
    }

    public Task<string> GetNpcReplyStreamed(string npcName, string persona, string history, string playerLine, Action<string> onDelta)
    {
        // Streaming not yet implemented on ILlmProvider; fallback to single reply and emit once.
        return GetNpcReply(npcName, persona, history, playerLine);
    }

    public Task<string> GetNpcReply(string npcName, string persona, string history, string playerLine)
    {
        return _llm.GetReplyAsync(npcName, persona, history, playerLine);
    }

    public static string BuildPrompt(string npcName, string persona, string history, string playerLine)
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
