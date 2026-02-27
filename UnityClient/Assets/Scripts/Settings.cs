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

    // Odkaz na tvùj existující chatovací skript
    public ChatUIWindow chatUIWindow;

    [Header("History UI")]
    public TextMeshProUGUI historyCountText;
    public TextMeshProUGUI historyStatusText;
    public Button deleteHistoryButton;

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

    // --- NOVÉ: Prvky pro nastavení zobrazení ---
    [Header("Display Settings")]
    public TMP_Dropdown resolutionDropdown;
    public Toggle fullscreenToggle;
    private Resolution[] resolutions;
    // ------------------------------------------

    private KeyCode tempTalkKey;
    private KeyCode tempStopKey;
    private bool waitingForTalkKey = false;
    private bool waitingForStopKey = false;

    void Start()
    {
        if (settingsPanel) settingsPanel.SetActive(false);
        if (historyStatusText) historyStatusText.text = "";

        // Inicializujeme a naèteme rozlišení hned po startu
        InitializeDisplaySettings();
    }

    public void ToggleSettings()
    {
        if (settingsPanel)
        {
            bool isActive = !settingsPanel.activeSelf;
            settingsPanel.SetActive(isActive);

            if (isActive)
            {
                tempTalkKey = ConfigLoader.talkKey;
                tempStopKey = ConfigLoader.stopKey;

                RefreshMicrophones();
                LoadValuesToUI();

                waitingForTalkKey = false;
                waitingForStopKey = false;

                // Aktualizujeme UI pro historii hned pøi otevøení
                UpdateHistoryUI();
            }
        }
    }

    private void LoadValuesToUI()
    {
        if (ConfigLoader.config == null) return;

        apiBaseInput.text = ConfigLoader.config.apiBaseUrl;
        ttsEndpointInput.text = ConfigLoader.GetUrl(ConfigLoader.config.ttsEndpoint);
        chatStreamInput.text = ConfigLoader.GetUrl(ConfigLoader.config.chatRealTime);
        sttEndpointInput.text = ConfigLoader.GetUrl(ConfigLoader.config.sttEndpoint);
        historyGetInput.text = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryEndpoint);
        historyDelInput.text = ConfigLoader.GetUrl(ConfigLoader.config.chatHistoryDelete);
        stopEndpointInput.text = ConfigLoader.GetUrl(ConfigLoader.config.stopEndpoint);

        if (talkKeyText) talkKeyText.text = tempTalkKey.ToString();
        if (stopKeyText) stopKeyText.text = tempStopKey.ToString();
    }

    // --- LOGIKA HISTORIE PØES TVÙJ SKRIPT ---
    private void UpdateHistoryUI()
    {
        if (historyStatusText) historyStatusText.text = "";

        if (chatUIWindow != null)
        {
            // Vezmeme poèet zpráv pøímo z tvého ChatUIWindow
            int count = chatUIWindow.currentMessageCount;

            if (historyCountText) historyCountText.text = $"{count}";
            if (deleteHistoryButton) deleteHistoryButton.interactable = (count > 0);
        }
        else
        {
            if (historyCountText) historyCountText.text = "err";
            if (deleteHistoryButton) deleteHistoryButton.interactable = false;
        }
    }

    public void OnClick_DeleteHistory()
    {
        if (chatUIWindow != null)
        {
            if (historyStatusText)
            {
                historyStatusText.color = Color.yellow;
                historyStatusText.text = "Mažu...";
            }
            if (deleteHistoryButton) deleteHistoryButton.interactable = false;

            // Zavoláme tvou funkci a øekneme jí, co má udìlat, až skonèí
            chatUIWindow.ClearAllHistory(() =>
            {
                if (historyStatusText)
                {
                    historyStatusText.color = Color.green;
                    historyStatusText.text = "Historie byla úspìšnì vymazána!";
                }
                if (historyCountText) historyCountText.text = "0";
            });
        }
    }
    // ----------------------------------------------

    // --- NOVÉ: Inicializace a logika pro rozlišení / fullscreen ---
    private void InitializeDisplaySettings()
    {
        if (resolutionDropdown == null || fullscreenToggle == null) return;

        resolutions = Screen.resolutions;
        resolutionDropdown.ClearOptions();

        List<string> options = new List<string>();
        int currentResolutionIndex = 0;

        // Naèteme uložená data (pokud nejsou, použijeme aktuální nastavení monitoru)
        bool savedFullscreen = PlayerPrefs.GetInt("fullscreen", Screen.fullScreen ? 1 : 0) == 1;
        int savedWidth = PlayerPrefs.GetInt("resWidth", Screen.currentResolution.width);
        int savedHeight = PlayerPrefs.GetInt("resHeight", Screen.currentResolution.height);

        for (int i = 0; i < resolutions.Length; i++)
        {
            string option = resolutions[i].width + " x " + resolutions[i].height;
            options.Add(option);

            // Najdeme index uloženého/aktuálního rozlišení v seznamu
            if (resolutions[i].width == savedWidth && resolutions[i].height == savedHeight)
            {
                currentResolutionIndex = i;
            }
        }

        resolutionDropdown.AddOptions(options);
        resolutionDropdown.value = currentResolutionIndex;
        resolutionDropdown.RefreshShownValue();
        fullscreenToggle.isOn = savedFullscreen;

        #if UNITY_STANDALONE_LINUX
                Debug.Log("Linux detected: Forcing windowed mode to prevent Docker crash.");
                Screen.SetResolution(savedWidth, savedHeight, false); // false = vždy v oknì
                fullscreenToggle.isOn = false;
        #else
                    Screen.SetResolution(savedWidth, savedHeight, savedFullscreen);
        #endif


        // Aplikujeme rozlišení hned po startu
        Screen.SetResolution(savedWidth, savedHeight, savedFullscreen);
    }

    public void SetResolution(int resolutionIndex)
    {
        if (resolutions == null || resolutions.Length == 0) return;

        Resolution resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.width, resolution.height, Screen.fullScreen);

        // Uložení do PlayerPrefs
        PlayerPrefs.SetInt("resWidth", resolution.width);
        PlayerPrefs.SetInt("resHeight", resolution.height);
        PlayerPrefs.Save();
    }

    public void SetFullscreen(bool isFullscreen)
    {
        #if UNITY_STANDALONE_LINUX
            Screen.fullScreen = false; // Na linuxu prostì nepovolíme fullscreen
        #else
            Screen.fullScreen = isFullscreen;
        #endif

        Screen.fullScreen = isFullscreen;

        // Uložení do PlayerPrefs
        PlayerPrefs.SetInt("fullscreen", isFullscreen ? 1 : 0);
        PlayerPrefs.Save();
    }
    // --------------------------------------------------------------

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
            if (waitingForTalkKey) { tempTalkKey = e.keyCode; talkKeyText.text = e.keyCode.ToString(); waitingForTalkKey = false; }
            else if (waitingForStopKey) { tempStopKey = e.keyCode; stopKeyText.text = e.keyCode.ToString(); waitingForStopKey = false; }
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
            if (sttManager != null && devices[i] == sttManager.GetCurrentMicrophone()) currentMicIndex = i;
        }
        micDropdown.AddOptions(options);
        micDropdown.value = currentMicIndex;
        micDropdown.RefreshShownValue();
    }

    public void SaveAndClose()
    {
        if (ConfigLoader.config != null)
        {
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
    }

    public void Cancel()
    {
        waitingForStopKey = false;
        waitingForTalkKey = false;
        if (settingsPanel) settingsPanel.SetActive(false);
    }
}