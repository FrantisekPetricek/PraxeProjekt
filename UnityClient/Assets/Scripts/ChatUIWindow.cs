using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class ChatUIWindow : MonoBehaviour
{

    public ChatHistoryLoader historyLoader;
    public GameObject messagePrefab;
    public Transform contentContainer;
    public ScrollRect scrollRect;

    public bool loadOnStart = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
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
        foreach (Transform child in contentContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (var msg in message)
        {
            GameObject newItem = Instantiate(messagePrefab, contentContainer);
            ChatMessageItem itemScript = newItem.GetComponent<ChatMessageItem>();

            if (itemScript != null)
            {
                itemScript.Setup(msg.role, msg.content);
            }
        }

        StartCoroutine(ScrollToBottom());

    }

    public void ClearAllHistory()
    {
        if(historyLoader != null)
        {
            historyLoader.DeleteHistory(() =>

            RefreshHistory()
            );
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
