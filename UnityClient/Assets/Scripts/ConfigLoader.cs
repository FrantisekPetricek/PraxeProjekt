using System;
using System.IO;
using UnityEngine;

[System.Serializable]
public class AppConfig
{
    public string apiBaseUrl;
    public string ttsEndpoint;
    public string chatRealTime;
    public string sttEndpoint;
    public string chatHistoryEndpoint;
    public string chatHistoryDelete;
    public string inputKey;
}

public class ConfigLoader : MonoBehaviour
{
    public static AppConfig config;
    public static KeyCode talkKey;

    private void Awake()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "config.json");

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                config = JsonUtility.FromJson<AppConfig>(json);

                talkKey = (KeyCode)Enum.Parse(typeof(KeyCode), config.inputKey);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error reading config file: {e.Message}");
                SetDefaults();
            }
        }
        else
        {
            Debug.LogWarning("Config file not found, using default settings.");
            SetDefaults();
        }
    }

    void SetDefaults()
    {
        config = new AppConfig
        {
            apiBaseUrl = "http://localhost:8000",

            ttsEndpoint = "/tts",
            chatRealTime = "/chat_realtime",
            sttEndpoint = "/stt_file",
            chatHistoryEndpoint = "/get_history",
            chatHistoryDelete = "/delete_history",

            inputKey = "LeftControl"
        };
        talkKey = KeyCode.LeftControl;
    }

    public static string GetUrl(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint)) return "";

        string baseClean = config.apiBaseUrl.TrimEnd('/');

        if (!endpoint.StartsWith("/")) endpoint = "/" + endpoint;

        return baseClean + endpoint;
    }
}