using System;
using System.Threading.Tasks;

public class NpcDialogueService
{
    readonly ILlmProvider _llm;
    readonly IPromptBuilder _promptBuilder;

    public NpcDialogueService(ILlmProvider llm, IPromptBuilder promptBuilder)
    {
        _llm = llm;
        _promptBuilder = promptBuilder;
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
}
