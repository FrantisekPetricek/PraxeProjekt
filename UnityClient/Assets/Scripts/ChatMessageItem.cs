using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatMessageItem : MonoBehaviour
{
    public Color playerChatColor;
    public Color npcChatColor;

    public TextMeshProUGUI messageText;
    public Image background;

    public void Setup(string role, string text)
    {
        // Porovnáváme bez ohledu na velká/malá písmena
        bool isPlayer = role.Equals("Player", System.StringComparison.OrdinalIgnoreCase) ||
                        role.Equals("user", System.StringComparison.OrdinalIgnoreCase) ||
                        role.Equals("USER", System.StringComparison.OrdinalIgnoreCase);

        string speakerName = isPlayer ? "Hráè" : "Eliška (AI)";

        messageText.text = $"<b>{speakerName}</b> {text}";

        if (isPlayer)
        {
            background.color = playerChatColor;
            messageText.alignment = TextAlignmentOptions.Right; 
        }
        else
        {
            background.color = npcChatColor;
            messageText.alignment = TextAlignmentOptions.Left;
        }
    }

    public void SetNoHistoryText()
    {
        messageText.text = $"<b>System</b> No history found";
    }
}