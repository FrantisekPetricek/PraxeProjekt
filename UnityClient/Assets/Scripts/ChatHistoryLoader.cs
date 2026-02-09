using NUnit.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class ChatHistoryLoader : MonoBehaviour
{
    private string chatHistoryEndpoint;

    [System.Serializable]
    public class ChatMessage
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    public class HistoryResponce
    {
        public List<ChatMessage> messages;
    }

    public void Start()
    {
        chatHistoryEndpoint = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryEndpoint);
    }

    public void LoadHistory(System.Action<List<ChatMessage>> callback)
    {
        StartCoroutine(GetHistoryRoutine(callback));
    }

    public void DeleteHistory(System.Action onSuccess = null)
    {
        StartCoroutine(DeleteHistoryRoutine(onSuccess));
    }

    private IEnumerator DeleteHistoryRoutine(System.Action onSuccess)
    {
        string url = "";

        if (ConfigLoader.config != null)
            url = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryDelete);

        using (UnityWebRequest req = UnityWebRequest.Delete(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"Chyba pøi mazání historie: {req.error}");
            }
            else
            {
                Debug.Log("<color=red> Historie na serveru smazána. </color>");
                onSuccess?.Invoke();
            }
        }
    }

    private IEnumerator GetHistoryRoutine(System.Action<List<ChatMessage>> callback = null)
    {
        string url = chatHistoryEndpoint;

        if (ConfigLoader.config != null)
        {
            url = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryEndpoint);
        }

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Chyba pøi stahování historie: {req.error}");
            }
            else
            {
                string json = req.downloadHandler.text;
                //Debug.Log($"Stažená historie (RAW): {json}");

                try
                {
                    // Deserializace JSONu do objektu
                    HistoryResponce response = JsonUtility.FromJson<HistoryResponce>(json);

                    if (response != null && response.messages != null)
                    {
                        Debug.Log($"<color=green>Historie naètena! Poèet zpráv: {response.messages.Count}</color>");

                        // Výpis do konzole pro kontrolu
                        foreach (var msg in response.messages)
                        {
                            //Debug.Log($"[{msg.role}]: {msg.content}");
                        }

                        // Zavolání callbacku (pokud chceme data pøedat dál, napø. do UI)
                        callback?.Invoke(response.messages);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Chyba parsování JSONu: {e.Message}");
                }
            }
        }
    }
}
