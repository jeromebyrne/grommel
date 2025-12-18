using System.Text;
using System.Threading.Tasks;

public static class NpcDialogueService
{
    public static async Task<string> GetNpcReply(string npcName, string persona, string history, string playerLine)
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

        string prompt = sb.ToString();
        string reply = await OllamaClient.GenerateAsync("llama3:8b", prompt);
        return reply;
    }
}