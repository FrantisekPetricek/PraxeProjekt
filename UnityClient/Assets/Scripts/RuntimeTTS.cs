using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;

public class RuntimeTTS : MonoBehaviour
{
    [Header("Modules")]
    private STTManager sttManager;
    private EyeContact playerEyeController;
    private EyeContact npcEyeController;
    private EmotionController emotionHandler;

    [Header("UI")]
    private SubtitleManager subtitleManager;
    public TMP_InputField playerInput;

    [Header("Lip Syncs & Audio")]
    public GameObject player;
    public GameObject npc;
    private AudioSource playerAudioSource;
    private AudioSource npcAudioSource;

    [Header("Names")]
    public string playerName = "Hráč";
    public string npcName = "Eliška";

    [Header("Server API")]
    private string urlPlayerTTS;  // http://localhost:8000/tts
    private string urlChat;       // http://localhost:8000/chat

    [Header("Input")]
    public KeyCode recordKey;

    // Spotlighty (vizuální efekt kdo mluví)
    private Spotlight npcSpotlight;
    private Spotlight playerSpotlight;

    private bool isProcessing = false;

    // --- STRUKTURY PRO FRONTU ---
    private class DialogueSegment
    {
        public string text;
        public string emotion;
        public AudioClip clip;
    }

    private Queue<DialogueSegment> dialogueQueue = new Queue<DialogueSegment>();
    
    // --- DATOVÉ TYPY PRO API ---
    [System.Serializable]
    public class RequestData
    {
        public string text;          // Pro /tts
        public string user_question; // Pro /chat
    }

    [System.Serializable]
    public class CloudResponse
    {
        public string text;       // Text odpovědi AI
        public string audio_url;  // Cesta k MP3 (např. "temp/npc_123.mp3")
        public string emotion;    // Emoce (např. "happy")
    }

    private void Start()
    {
        // 1. Najdeme manažery
        var managers = GameObject.Find("Managers");
        if (managers)
        {
            sttManager = managers.GetComponent<STTManager>();
            emotionHandler = managers.GetComponent<EmotionController>();
            subtitleManager = managers.GetComponent<SubtitleManager>();
        }

        // 2. Najdeme komponenty na postavách
        if (player)
        {
            playerEyeController = player.GetComponentInChildren<EyeContact>();
            playerAudioSource = player.GetComponentInChildren<AudioSource>();
            playerSpotlight = player.GetComponent<Spotlight>();
        }

        if (npc)
        {
            npcEyeController = npc.GetComponentInChildren<EyeContact>();
            npcAudioSource = npc.GetComponentInChildren<AudioSource>();
            npcSpotlight = npc.GetComponent<Spotlight>();
        }

        // 3. Načtení URL z Configu
        if (ConfigLoader.config != null)
        {
            urlPlayerTTS = ConfigLoader.GetUrl(ConfigLoader.config.ttsEndpoint);
            urlChat = ConfigLoader.GetUrl(ConfigLoader.config.chatRealTime);
            recordKey = ConfigLoader.talkKey;
        }

        // 4. Navázání STT Eventů
        if (sttManager != null)
        {
            sttManager.OnRecordingStart += () => { if (subtitleManager) subtitleManager.ShowSubtitleStatic("<i>(Nahrávám...)</i>"); };
            sttManager.OnError += (msg) => { if (subtitleManager) subtitleManager.ShowSubtitleStatic($"<color=red>Chyba: {msg}</color>"); };
            sttManager.OnTranscriptionComplete += HandleVoiceInput;
        }
    }

    private void OnDestroy()
    {
        if (sttManager != null) sttManager.OnTranscriptionComplete -= HandleVoiceInput;
    }

    // --- UPDATE (VSTUPY) ---
    void Update()
    {
        if (isProcessing) return;

        // A) Textový vstup (Enter)
        if (Input.GetKeyDown(KeyCode.Return) && playerInput && !string.IsNullOrWhiteSpace(playerInput.text))
        {
            string text = playerInput.text;
            playerInput.text = "";
            StartCoroutine(ConversationSequence(text));
        }

        // B) Hlasový vstup (Držení klávesy)
        if (sttManager != null)
        {
            if (Input.GetKeyDown(recordKey)) sttManager.StartRecording();
            if (Input.GetKeyUp(recordKey)) sttManager.StopAndUpload();
        }
    }

    private void HandleVoiceInput(string recognizedText)
    {
        if (playerInput) playerInput.text = recognizedText;
        StartCoroutine(ConversationSequence(recognizedText));
    }

    // =================================================================================
    // HLAVNÍ LOGIKA KONVERZACE
    // =================================================================================
    IEnumerator ConversationSequence(string text)
    {
        isProcessing = true;

        // ---------------------------------------------------------
        // 1. HRÁČ MLUVÍ (TTS)
        // ---------------------------------------------------------
        if (subtitleManager) subtitleManager.SetSubtitleText($"<b>{playerName}:</b> {text}");
        if (playerEyeController) playerEyeController.SetTalkingState(true);

        // Pošleme text na /tts endpoint a čekáme na MP3
        string ttsJson = JsonUtility.ToJson(new RequestData { text = text });

        using (UnityWebRequest req = new UnityWebRequest(urlPlayerTTS, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(ttsJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // DŮLEŽITÉ: ElevenLabs vrací MP3 -> AudioType.MPEG
            req.downloadHandler = new DownloadHandlerAudioClip(urlPlayerTTS, AudioType.MPEG);
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null)
                {
                    if (playerSpotlight) playerSpotlight.ChangeColorToTarget();
                    playerAudioSource.clip = clip;
                    playerAudioSource.Play();

                    // Čekáme, dokud hráč nedomluví
                    yield return new WaitForSeconds(clip.length + 0.2f);

                    if (playerSpotlight) playerSpotlight.ResetColor();
                }
            }
            else
            {
                Debug.LogError($"[TTS Player Error]: {req.error}");
            }
        }

        // Úklid po hráči
        if (subtitleManager) subtitleManager.HideSubtitles();
        if (playerEyeController) playerEyeController.SetTalkingState(false);


        // ---------------------------------------------------------
        // 2. NPC PŘEMÝŠLÍ A ODPOVÍDÁ (CHAT)
        // ---------------------------------------------------------
        if (subtitleManager) subtitleManager.ShowSubtitleStatic("<i>(NPC přemýšlí...)</i>");

        if (npcSpotlight) npcSpotlight.ChangeColorToTarget();
        if (npcEyeController) npcEyeController.SetTalkingState(true);
        if (emotionHandler) emotionHandler.SetEmotion("neutral"); // Reset emocí

        // Voláme logiku pro získání odpovědi a audia
        yield return StartCoroutine(HandleNPCChat(text));

        // Úklid po NPC
        if (npcEyeController) npcEyeController.SetTalkingState(false);
        if (npcSpotlight) npcSpotlight.ResetColor();
        if (subtitleManager) subtitleManager.HideSubtitles();
        if (emotionHandler) emotionHandler.SetEmotion("neutral");

        isProcessing = false;
        RefreshChatHistory();
    }

    // =================================================================================
    // LOGIKA PRO NPC (JSON -> URL -> AUDIO)
    // =================================================================================
    IEnumerator HandleNPCChat(string question)
    {
        string jsonBody = JsonUtility.ToJson(new RequestData { user_question = question });

        // KROK A: Pošleme dotaz na /chat a získáme JSON s odkazem na audio
        using (UnityWebRequest req = new UnityWebRequest(urlChat, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer(); // Tady stahujeme text (JSON)
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Parsujeme odpověď: { "text": "...", "audio_url": "temp/...", "emotion": "..." }
                CloudResponse response = JsonUtility.FromJson<CloudResponse>(req.downloadHandler.text);

                if (response != null && !string.IsNullOrEmpty(response.audio_url))
                {
                    // KROK B: Sestavíme plnou URL pro stažení souboru
                    // Backend vrací relativní cestu "temp/neco.mp3", musíme přidat "http://localhost:8000/"
                    string fullAudioUrl = response.audio_url;

                    if (!fullAudioUrl.StartsWith("http"))
                    {
                        // Získáme base URL z urlChat (např. http://localhost:8000)
                        try
                        {
                            var uri = new Uri(urlChat);
                            string baseUrl = $"{uri.Scheme}://{uri.Authority}";
                            fullAudioUrl = $"{baseUrl}/{response.audio_url}";
                        }
                        catch
                        {
                            Debug.LogError("Chyba při parsování URL.");
                        }
                    }

                    // KROK C: Stáhneme samotný MP3 soubor
                    using (UnityWebRequest audioReq = UnityWebRequestMultimedia.GetAudioClip(fullAudioUrl, AudioType.MPEG))
                    {
                        yield return audioReq.SendWebRequest();

                        if (audioReq.result == UnityWebRequest.Result.Success)
                        {
                            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);

                            // Vložíme do fronty (aby se přehrálo)
                            dialogueQueue.Clear();
                            dialogueQueue.Enqueue(new DialogueSegment
                            {
                                text = response.text,
                                emotion = response.emotion,
                                clip = clip
                            });

                            // Spustíme přehrávání
                            yield return StartCoroutine(PlayDialogueQueue());
                        }
                        else
                        {
                            Debug.LogError($"Chyba stahování audia z cloudu: {audioReq.error} | URL: {fullAudioUrl}");
                            if (subtitleManager) subtitleManager.ShowSubtitleStatic("<color=red>Chyba stahování hlasu.</color>");
                        }
                    }
                }
            }
            else
            {
                Debug.LogError($"Chat API Error: {req.error} | {req.downloadHandler.text}");
                if (subtitleManager) subtitleManager.ShowSubtitleStatic("<color=red>Chyba komunikace s AI.</color>");
            }
        }
    }

    // =================================================================================
    // PŘEHRÁVAČ FRONTY (LIP SYNC + AUDIO + TITULKY)
    // =================================================================================
    IEnumerator PlayDialogueQueue()
    {
        
        while (dialogueQueue.Count > 0)
        {
            DialogueSegment segment = dialogueQueue.Dequeue();

            // Nastavíme emoci
            if (emotionHandler && !string.IsNullOrEmpty(segment.emotion))
                emotionHandler.SetEmotion(segment.emotion);

            // Spustíme audio
            if (segment.clip != null)
            {
                npcAudioSource.clip = segment.clip;
                npcAudioSource.Play();

                // --- ZMĚNA PRO TITULKY ---
                // Místo SetSubtitleText voláme ShowSubtitleSynced s délkou audia
                if (subtitleManager && !string.IsNullOrEmpty(segment.text))
                {
                    // Volitelně vyčistit od [tagů] pokud to nedělá už Python
                    // string clean = System.Text.RegularExpressions.Regex.Replace(segment.text, @"\[.*?\]", "").Trim();

                    // Předáme text A délku klipu, aby SubtitleManager mohl stránkovat
                    subtitleManager.ShowSubtitleSynced(segment.text, segment.clip.length);
                }
                // -------------------------

                // Čekáme, dokud audio nehraje
                yield return new WaitForSeconds(segment.clip.length);

                // Malá pauza
                yield return new WaitForSeconds(0.1f);
            }
        }

        yield return new WaitForSeconds(0.5f);
        if (emotionHandler) emotionHandler.SetEmotion("neutral");

        // Skryjeme titulky, až vše skončí
        if (subtitleManager) subtitleManager.HideSubtitles();

    }

    private void RefreshChatHistory()
    {
        var chatUI = FindFirstObjectByType<ChatUIWindow>();
        if (chatUI) chatUI.RefreshHistory();
    }
}