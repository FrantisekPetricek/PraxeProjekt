using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;
using System; 

public class ChatUIWindow : MonoBehaviour
{
    public ChatHistoryLoader historyLoader;
    public GameObject messagePrefab;
    public Transform contentContainer;
    public ScrollRect scrollRect;

    public bool loadOnStart = true;

    [HideInInspector]
    public int currentMessageCount = 0;

    void Start()
    {
        if (loadOnStart && historyLoader != null)
        {
            RefreshHistory();
        }
    }

    public void RefreshHistory()
    {
        historyLoader.LoadHistory(OnHistoryRecieved);
    }

    private void OnHistoryRecieved(List<ChatHistoryLoader.ChatMessage> message)
    {
        currentMessageCount = message.Count;
        
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        if (message.Count == 0)
        {
            Debug.Log("No chat history found.");
            GameObject item =  Instantiate(messagePrefab, contentContainer);
            ChatMessageItem itemScript = item.GetComponent<ChatMessageItem>();

            if (itemScript != null)
            {
                itemScript.SetNoHistoryText();
            }
        }
        else
        {
            foreach (var msg in message)
            {
                GameObject newItem = Instantiate(messagePrefab, contentContainer);
                ChatMessageItem itemScript = newItem.GetComponent<ChatMessageItem>();

                if (itemScript != null)
                {
                    itemScript.Setup(msg.role, msg.content);
                }
            }
        }


        StartCoroutine(ScrollToBottom());
    }

    public void ClearAllHistory(Action onSuccess = null)
    {
        if (historyLoader != null)
        {
            historyLoader.DeleteHistory(() =>
            {
                RefreshHistory();
                onSuccess?.Invoke(); 
            });
        }
    }

    IEnumerator ScrollToBottom()
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 0f;
        yield return new WaitForEndOfFrame();
        scrollRect.verticalNormalizedPosition = 0f;
    }
}