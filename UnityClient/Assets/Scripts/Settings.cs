using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class Settings : MonoBehaviour
{
    [Header("References")]
    public ConfigLoader configLoader;
    public STTManager sttManager;
    public GameObject settingsPanel;

    [Header("Keybinding Buttons")]
    public Button talkKeyButton;
    public TextMeshProUGUI talkKeyText;

    public Button stopKeyButton;
    public TextMeshProUGUI stopKeyText;

    [Header("Endpoint Inputs")]
    public TMP_InputField apiBaseInput;
    public TMP_InputField ttsEndpointInput;
    public TMP_InputField chatStreamInput;
    public TMP_InputField sttEndpointInput;
    public TMP_InputField historyGetInput;
    public TMP_InputField historyDelInput;
    public TMP_InputField stopEndpointInput;

    [Header("Other Inputs")]
    public TMP_Dropdown micDropdown;

    // Doèasné promìnné
    private KeyCode tempTalkKey;
    private KeyCode tempStopKey;
    private bool waitingForTalkKey = false;
    private bool waitingForStopKey = false;

    void Start()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
    }

    public void ToggleSettings()
    {
        if (settingsPanel)
        {
            bool isActive = !settingsPanel.activeSelf;
            settingsPanel.SetActive(isActive);

            if (isActive)
            {
                // Naèteme aktuální klávesy do temp promìnných
                tempTalkKey = ConfigLoader.talkKey;
                tempStopKey = ConfigLoader.stopKey;

                RefreshMicrophones();
                LoadValuesToUI();

                waitingForTalkKey = false;
                waitingForStopKey = false;
            }
        }
    }

    // --- ZMÌNA ZDE ---
    private void LoadValuesToUI()
    {
        if (ConfigLoader.config == null) return;

        // 1. Base URL necháme tak, jak je (to je základ)
        apiBaseInput.text = ConfigLoader.config.apiBaseUrl;

        // 2. Ostatní endpointy naèteme jako KOMPLETNÍ URL pomocí GetUrl()
        // Tím se spojí BaseUrl + /tts -> http://localhost:8000/tts
        ttsEndpointInput.text = ConfigLoader.GetUrl(ConfigLoader.config.ttsEndpoint);
        chatStreamInput.text = ConfigLoader.GetUrl(ConfigLoader.config.chatRealTime);
        sttEndpointInput.text = ConfigLoader.GetUrl(ConfigLoader.config.sttEndpoint);
        historyGetInput.text = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryEndpoint);
        historyDelInput.text = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryDelete);
        stopEndpointInput.text = ConfigLoader.GetUrl(ConfigLoader.config.stopEndpoint);

        // Klávesy
        if (talkKeyText) talkKeyText.text = tempTalkKey.ToString();
        if (stopKeyText) stopKeyText.text = tempStopKey.ToString();

        RefreshMicrophones();
    }
    // -----------------

    // --- LOGIKA TLAÈÍTEK ---
    public void OnClick_ChangeTalkKey()
    {
        waitingForTalkKey = true;
        waitingForStopKey = false;
        if (talkKeyText) talkKeyText.text = "Stiskni klávesu...";
    }

    public void OnClick_ChangeStopKey()
    {
        waitingForStopKey = true;
        waitingForTalkKey = false;
        if (stopKeyText) stopKeyText.text = "Stiskni klávesu...";
    }

    void OnGUI()
    {
        if (!waitingForTalkKey && !waitingForStopKey) return;
        Event e = Event.current;
        if (e.isKey && e.keyCode != KeyCode.None)
        {
            if (waitingForTalkKey)
            {
                tempTalkKey = e.keyCode;
                talkKeyText.text = e.keyCode.ToString();
                waitingForTalkKey = false;
            }
            else if (waitingForStopKey)
            {
                tempStopKey = e.keyCode;
                stopKeyText.text = e.keyCode.ToString();
                waitingForStopKey = false;
            }
        }
    }

    private void RefreshMicrophones()
    {
        if (micDropdown == null) return;
        micDropdown.ClearOptions();
        string[] devices = Microphone.devices;
        List<string> options = new List<string>();
        int currentMicIndex = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            options.Add(devices[i]);
            if (sttManager != null && devices[i] == sttManager.GetCurrentMicrophone())
            {
                currentMicIndex = i;
            }
        }
        micDropdown.AddOptions(options);
        micDropdown.value = currentMicIndex;
        micDropdown.RefreshShownValue();
    }

    // --- ULOŽENÍ ---
    public void SaveAndClose()
    {
        if (ConfigLoader.config != null)
        {
            // Tady ukládáme to, co je v InputFieldu (což je teï celá adresa)
            // ConfigLoader si s tím poradí, protože pozná, že to zaèíná na "http"
            ConfigLoader.config.apiBaseUrl = apiBaseInput.text;

            ConfigLoader.config.ttsEndpoint = ttsEndpointInput.text;
            ConfigLoader.config.chatRealTime = chatStreamInput.text;
            ConfigLoader.config.sttEndpoint = sttEndpointInput.text;
            ConfigLoader.config.chatHistoryEndpoint = historyGetInput.text;
            ConfigLoader.config.chatHistoryDelete = historyDelInput.text;
            ConfigLoader.config.stopEndpoint = stopEndpointInput.text;
        }

        ConfigLoader.talkKey = tempTalkKey;
        ConfigLoader.stopKey = tempStopKey;

        if (micDropdown != null && micDropdown.options.Count > 0)
        {
            string selectedMic = micDropdown.options[micDropdown.value].text;
            if (sttManager != null) sttManager.SetMicrophoneDevice(selectedMic);
        }

        if (configLoader != null) configLoader.SaveConfig();

        if (settingsPanel) settingsPanel.SetActive(false);
        Debug.Log("Nastavení uloženo.");
    }

    public void Cancel()
    {
        waitingForStopKey = false;
        waitingForTalkKey = false;
        if (settingsPanel) settingsPanel.SetActive(false);
    }
}