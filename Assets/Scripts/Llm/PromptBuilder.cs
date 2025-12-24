using System.Text;

public interface IPromptBuilder
{
    string BuildPrompt(string npcName, string persona, string history, string playerLine);
}

public class DefaultPromptBuilder : IPromptBuilder
{
    public string BuildPrompt(string npcName, string persona, string history, string playerLine)
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
