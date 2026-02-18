using System;
using System.IO;
using UnityEngine;

[System.Serializable]
public class AppConfig
{
    // Hlavní server
    public string apiBaseUrl;

    // Endpointy
    public string ttsEndpoint;
    public string chatRealTime;
    public string sttEndpoint;
    public string chatHistoryEndpoint;
    public string chatHistoryDelete;
    public string stopEndpoint;

    // Klávesy jako text (napø. "LeftControl", "Escape")
    public string inputKey;
    public string stopKey;
}

public class ConfigLoader : MonoBehaviour
{
    public static AppConfig config;

    // Tyto promìnné používáš ve høe
    public static KeyCode talkKey;
    public static KeyCode stopKey;

    private void Awake()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "config.json");

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                config = JsonUtility.FromJson<AppConfig>(json);

                // --- 1. PARSE TALK KEY ---
                if (!Enum.TryParse(config.inputKey, out talkKey))
                {
                    Debug.LogWarning($"Neznámá klávesa pro input: {config.inputKey}, nastavuji LeftControl");
                    talkKey = KeyCode.LeftControl;
                }

                // --- 2. PARSE STOP KEY (Tohle jsi potøeboval pøidat) ---
                // Zkusíme pøevést string z JSONu na KeyCode. Pokud to selže, dáme Escape.
                if (!Enum.TryParse(config.stopKey, out stopKey))
                {
                    Debug.LogWarning($"Neznámá klávesa pro stop: {config.stopKey}, nastavuji Escape");
                    stopKey = KeyCode.Escape;
                }

                Debug.Log($"Config naèten. Talk: {talkKey}, Stop: {stopKey}");
            }
            catch (Exception e)
            {
                Debug.LogError($"Chyba pøi ètení configu: {e.Message}");
                SetDefaults();
            }
        }
        else
        {
            Debug.LogWarning("Config file nenalezen, používám výchozí nastavení.");
            SetDefaults();
            // SaveDefaults(path); // Volitelnì uložit defaultní config
        }
    }

    void SetDefaults()
    {
        config = new AppConfig
        {
            apiBaseUrl = "http://localhost:8000",

            ttsEndpoint = "/tts",
            chatRealTime = "/chat_stream",
            sttEndpoint = "/transcribe",
            chatHistoryEndpoint = "/get_history",
            chatHistoryDelete = "/delete_history",
            stopEndpoint = "/stop_chat",

            inputKey = "LeftControl",
            stopKey = "Escape" // Defaultní string
        };

        // Nastavíme i statické promìnné
        talkKey = KeyCode.LeftControl;
        stopKey = KeyCode.Escape;
    }

    public static string GetUrl(string endpointConfigValue)
    {
        if (string.IsNullOrEmpty(endpointConfigValue)) return "";

        if (endpointConfigValue.Contains("://"))
        {
            return endpointConfigValue;
        }

        string baseClean = config.apiBaseUrl.TrimEnd('/');

        if (!endpointConfigValue.StartsWith("/"))
            endpointConfigValue = "/" + endpointConfigValue;

        return baseClean + endpointConfigValue;
    }
    public void SaveConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "config.json");
        try
        {
            config.inputKey = talkKey.ToString();
            config.stopKey = stopKey.ToString();

            string json = JsonUtility.ToJson(config, true);
            File.WriteAllText(path, json);
            Debug.Log("Nastavení (vèetnì kláves) uloženo do: " + path);
        }
        catch (Exception e)
        {
            Debug.LogError($"Chyba pøi ukládání configu: {e.Message}");
        }
    }
}