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
        string speakerName = (role == "USER") ? "Hr·Ë" : "Eliöka (AI)";

        messageText.text = $"<b>{speakerName}</b> {text}";

        if (role == "USER")
        {
            background.color = playerChatColor;
            messageText.alignment = TextAlignmentOptions.Left;
        }
        else
        {
            background.color = npcChatColor;
            messageText.alignment = TextAlignmentOptions.Left;
        }

    }
}
