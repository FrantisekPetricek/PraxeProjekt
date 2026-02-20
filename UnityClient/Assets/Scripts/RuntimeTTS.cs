using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
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
    private string urlPlayerTTS;
    private string urlChatStream;

    // Spotlighty
    private Spotlight npcSpotlight;
    private Spotlight playerSpotlight;

    public bool isProcessing = false;

    public bool IsSpeaking => npcAudioSource != null && npcAudioSource.isPlaying;

    // --- ID Generace pro zastavení starých requestů ---
    private int currentGenerationId = 0;

    /// <summary>
    /// Zvýší ID generace a vrátí jeho novou hodnotu.
    /// Používej pro start nové konverzace nebo pro invalidaci staré.
    /// </summary>
    private int NextGenerationId()
    {
        return ++currentGenerationId;
    }

    // Ukládáme si reference na běžící coroutiny, abychom je mohli stopnout
    private Coroutine playbackCoroutine;
    private Coroutine streamCoroutine;

    // --- STREAMING & PARSING STRUKTURY ---
    private enum ParseState { ReadTextLen, ReadText, ReadAudioLen, ReadAudio }
    private ParseState currentState = ParseState.ReadTextLen;
    private int bytesToRead = 4;
    private string tempTextBuffer = "";
    private string tempEmotionBuffer = "";

    
    private class DialogueSegment
    {
        public string text;
        public string emotion;
        public AudioClip clip;
    }

    private Queue<DialogueSegment> dialogueQueue = new Queue<DialogueSegment>();
    private bool isPlayingQueue = false;

    // --- API DATOVÉ TYPY ---
    [System.Serializable]
    public class RequestData
    {
        public string text;
        public string emotion;
        public string user_question;
    }

    [System.Serializable]
    public class TTSResponse
    {
        public string status;
        public string audio_url;
    }

    private void Start()
    {
        // Managers
        var managers = GameObject.Find("Managers");
        if (managers)
        {
            sttManager = managers.GetComponent<STTManager>();
            emotionHandler = managers.GetComponent<EmotionController>();
            subtitleManager = managers.GetComponent<SubtitleManager>();
        }

        //Player
        if (player)
        {
            playerEyeController = player.GetComponentInChildren<EyeContact>();
            playerAudioSource = player.GetComponentInChildren<AudioSource>();
            playerSpotlight = player.GetComponent<Spotlight>();
        }

        //NPC
        if (npc)
        {
            npcEyeController = npc.GetComponentInChildren<EyeContact>();
            npcAudioSource = npc.GetComponentInChildren<AudioSource>();
            npcSpotlight = npc.GetComponent<Spotlight>();
        }

        // 1. Načtení konfigurace
        if (ConfigLoader.config != null)
        {
            urlPlayerTTS = ConfigLoader.GetUrl(ConfigLoader.config.ttsEndpoint);
            urlChatStream = ConfigLoader.GetUrl(ConfigLoader.config.chatRealTime);
        }

        // 2. Setup STT Eventů
        if (sttManager != null)
        {
            sttManager.OnRecordingStart += () => {
                // Když začne hráč mluvit, automaticky stopneme AI
                StopAI();
                if (subtitleManager) subtitleManager.ShowSubtitleStatic("<i>(Poslouchám...)</i>");
            };
            sttManager.OnError += (msg) => {
                if (subtitleManager) subtitleManager.ShowSubtitleStatic($"<color=red>Chyba: {msg}</color>");
            };
            sttManager.OnTranscriptionComplete += HandleVoiceInput;
        }
    }

    private void OnDestroy()
    {
        if (sttManager != null) sttManager.OnTranscriptionComplete -= HandleVoiceInput;
    }

    public void ShowSubtitle(string speaker, string message)
    {
        if (subtitleManager != null)
            subtitleManager.ShowSubtitleStatic($"<b>{speaker}:</b> {message}");
    }

    void Update()
    {
        // ZASTAVENÍ AI KLÁVESOU (ESCAPE)
        if (Input.GetKeyDown(ConfigLoader.stopKey))
        {
            StopAI();
        }

        if (isProcessing) return;

        // Textový vstup (Enter)
        if (Input.GetKeyDown(KeyCode.Return) && playerInput && !string.IsNullOrWhiteSpace(playerInput.text))
        {
            string text = playerInput.text;
            playerInput.text = "";

            // Zvedneme ID generace -> nová konverzace
            int generationId = NextGenerationId();
            StartCoroutine(ConversationSequence(text, generationId));
        }

        // Hlasový vstup (Klávesa)
        if (sttManager != null)
        {
            if (Input.GetKeyDown(ConfigLoader.talkKey)) sttManager.StartRecording();
            if (Input.GetKeyUp(ConfigLoader.talkKey)) sttManager.StopAndUpload();
        }
    }

    // --- NOVÁ FUNKCE PRO ZASTAVENÍ AI ---
    public void StopAI()
    {
        // 1. Zneplatníme staré requesty (zvýšíme ID)
        NextGenerationId();

        // 2. Zastavíme audio
        if (npcAudioSource && npcAudioSource.isPlaying)
        {
            npcAudioSource.Stop();
        }

        // 3. Vymažeme frontu vět
        dialogueQueue.Clear();
        isPlayingQueue = false;

        // 4. Zastavíme běžící coroutiny (stahování streamu i přehrávání)
        if (streamCoroutine != null) StopCoroutine(streamCoroutine);
        if (playbackCoroutine != null) StopCoroutine(playbackCoroutine);

        // 5. Resetujeme UI a Emoce
        if (subtitleManager) subtitleManager.HideSubtitles();
        if (emotionHandler) emotionHandler.SetEmotion("neutral");
        if (npcEyeController) npcEyeController.SetTalkingState(false);
        if (npcSpotlight) npcSpotlight.ResetColor();

        // 6. Volitelně: Pošleme signál backendu (viz předchozí diskuze o /stop endpointu)
        StartCoroutine(SendStopSignalToBackend());

        isProcessing = false;
        Debug.Log("AI generace zastavena uživatelem.");
    }

    private IEnumerator SendStopSignalToBackend()
    {
        // Získání URL (stejné jako předtím)
        string stopUrl = ConfigLoader.config != null
            ? ConfigLoader.GetUrl(ConfigLoader.config.stopEndpoint)
            : "http://localhost:8000/stop_chat";


        // --- OPRAVA ---
        // Místo UnityWebRequest.Post(url, "") použijeme tento zápis:
        using (UnityWebRequest www = new UnityWebRequest(stopUrl, "POST"))
        {
            // Protože neposíláme žádná data (body), nepotřebujeme UploadHandler.
            // Ale potřebujeme DownloadHandler, aby Unity zpracovalo odpověď (i když je prázdná).
            www.downloadHandler = new DownloadHandlerBuffer();

            yield return www.SendWebRequest();
        }
    }


    private void HandleVoiceInput(string recognizedText)
    {
        if (playerInput) playerInput.text = recognizedText;

        // Zvedneme ID generace -> nová konverzace
        int generationId = NextGenerationId();
        StartCoroutine(ConversationSequence(recognizedText, generationId));
    }

    // --- HLAVNÍ LOGIKA KONVERZACE ---
    IEnumerator ConversationSequence(string text, int generationId)
    {
        isProcessing = true;

        // --- 1. HRÁČ MLUVÍ ---
        if (subtitleManager)
            subtitleManager.SetSubtitleText($"<b>{playerName}:</b> {text}");

        if (playerEyeController) playerEyeController.SetTalkingState(true);

        // ... (Kód pro TTS hráče - zkráceno pro přehlednost, zde se nic nemění) ...
        // ... Pokud chceš, můžeš sem přidat taky check `if (generationId != currentGenerationId) yield break;`

        // Simulace hráče (pro kompletnost tvého kódu jsem to nechal jak bylo, jen přidal checky)
        string ttsJson = JsonUtility.ToJson(new RequestData { text = text });
        using (UnityWebRequest req = new UnityWebRequest(urlPlayerTTS, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(ttsJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerAudioClip(urlPlayerTTS, AudioType.WAV);
            req.SetRequestHeader("Content-Type", "application/json");

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                if (generationId != currentGenerationId) yield break; // Check
                yield return null;
            }

            if (req.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
                if (clip != null)
                {
                    if (playerSpotlight) playerSpotlight.ChangeColorToTarget();
                    playerAudioSource.clip = clip;
                    playerAudioSource.Play();

                    // Čekání na domluvení hráče s checkem přerušení
                    float timer = 0;
                    while (timer < clip.length)
                    {
                        if (generationId != currentGenerationId) { playerAudioSource.Stop(); yield break; }
                        timer += Time.deltaTime;
                        yield return null;
                    }
                    yield return new WaitForSeconds(0.2f);

                    if (playerSpotlight) playerSpotlight.ResetColor();
                }
            }
        }

        // Úklid po hráči
        if (subtitleManager) subtitleManager.HideSubtitles();
        if (playerEyeController) playerEyeController.SetTalkingState(false);

        // Check před startem NPC
        if (generationId != currentGenerationId) { isProcessing = false; yield break; }


        // --- 2. NPC ODPOVÍDÁ ---
        ShowSubtitle("System", "<i>(NPC přemýšlí...)</i>");

        if (npcSpotlight) npcSpotlight.ChangeColorToTarget();
        if (npcEyeController) npcEyeController.SetTalkingState(true);
        if (emotionHandler) emotionHandler.SetEmotion("neutral");

        // Spustíme streamování a uložíme referenci
        streamCoroutine = StartCoroutine(StreamChatAudio(text, generationId));
        yield return streamCoroutine;

        // Úklid po NPC
        if (npcEyeController) npcEyeController.SetTalkingState(false);
        if (npcSpotlight) npcSpotlight.ResetColor();
        if (subtitleManager) subtitleManager.HideSubtitles();
        if (emotionHandler) emotionHandler.SetEmotion("neutral");

        isProcessing = false;
        RefreshChatHistory();
    }

    IEnumerator StreamChatAudio(string question, int generationId)
    {
        // Reset stavu parseru
        currentState = ParseState.ReadTextLen;
        bytesToRead = 4;
        tempTextBuffer = "";
        tempEmotionBuffer = "";
        dialogueQueue.Clear();
        List<byte> streamBuffer = new List<byte>();

        string jsonBody = JsonUtility.ToJson(new RequestData { user_question = question });
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest req = new UnityWebRequest(urlChatStream, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            var operation = req.SendWebRequest();
            int processedBytes = 0;

            while (!operation.isDone)
            {
                // DŮLEŽITÉ: Kontrola přerušení
                if (generationId != currentGenerationId)
                {
                    req.Abort(); // Zrušíme stahování
                    yield break;
                }

                if (req.downloadHandler != null && req.downloadHandler.data != null)
                {
                    byte[] allData = req.downloadHandler.data;
                    if (allData.Length > processedBytes)
                    {
                        int newBytesCount = allData.Length - processedBytes;
                        byte[] newChunk = new byte[newBytesCount];
                        Array.Copy(allData, processedBytes, newChunk, 0, newBytesCount);
                        ProcessSmartStream(newChunk, ref streamBuffer);
                        processedBytes = allData.Length;
                    }
                }

                if (!isPlayingQueue && dialogueQueue.Count > 0)
                {
                    // Uložíme referenci na playback
                    playbackCoroutine = StartCoroutine(PlayDialogueQueue(generationId));
                }

                yield return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                // Pokud byla chyba způsobena naším Abortem (změna ID), nehlásíme error
                if (generationId == currentGenerationId)
                {
                    Debug.LogError($"Stream Error: {req.error}");
                    ShowSubtitle("System", "<color=red>Chyba spojení s AI.</color>");
                }
            }
            else
            {
                // Zbytek dat
                if (req.downloadHandler != null && req.downloadHandler.data != null)
                {
                    byte[] finalData = req.downloadHandler.data;
                    if (finalData.Length > processedBytes)
                    {
                        byte[] newChunk = new byte[finalData.Length - processedBytes];
                        Array.Copy(finalData, processedBytes, newChunk, 0, newChunk.Length);
                        ProcessSmartStream(newChunk, ref streamBuffer);
                    }
                }
            }
        }

        // Čekáme na dohrání fronty (s kontrolou ID)
        while (isPlayingQueue || dialogueQueue.Count > 0)
        {
            if (generationId != currentGenerationId) yield break;

            if (!isPlayingQueue && dialogueQueue.Count > 0)
                playbackCoroutine = StartCoroutine(PlayDialogueQueue(generationId));

            yield return null;
        }
    }

    // --- PARSER (Beze změny) ---
    private void ProcessSmartStream(byte[] newChunk, ref List<byte> buffer)
    {
        // ... (Zde se nic nemění, zkopíruj si svou původní metodu) ...
        // Pro úsporu místa ji sem celou nepíšu, je stejná jako v tvém kódu.
        buffer.AddRange(newChunk);
        bool dataProcessed = true;
        int safetyLoop = 0;

        while (dataProcessed && safetyLoop < 100)
        {
            safetyLoop++;
            dataProcessed = false;

            if (currentState == ParseState.ReadTextLen)
            {
                if (buffer.Count >= 4)
                {
                    byte[] lenBytes = buffer.GetRange(0, 4).ToArray();
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    bytesToRead = BitConverter.ToInt32(lenBytes, 0);
                    buffer.RemoveRange(0, 4);

                    if (bytesToRead == 0) { currentState = ParseState.ReadAudioLen; bytesToRead = 4; }
                    else { currentState = ParseState.ReadText; }
                    dataProcessed = true;
                }
            }
            else if (currentState == ParseState.ReadText)
            {
                if (buffer.Count >= bytesToRead)
                {
                    byte[] textBytes = buffer.GetRange(0, bytesToRead).ToArray();
                    string jsonString = Encoding.UTF8.GetString(textBytes);

                    try
                    {
                        var data = JsonUtility.FromJson<RequestData>(jsonString);
                        if (!string.IsNullOrEmpty(data.text)) tempTextBuffer = data.text;
                        if (!string.IsNullOrEmpty(data.emotion)) tempEmotionBuffer = data.emotion;
                    }
                    catch
                    {
                        ExtractEmotionAndText(jsonString, out tempTextBuffer, out tempEmotionBuffer);
                    }

                    buffer.RemoveRange(0, bytesToRead);
                    currentState = ParseState.ReadAudioLen;
                    bytesToRead = 4;
                    dataProcessed = true;
                }
            }
            else if (currentState == ParseState.ReadAudioLen)
            {
                if (buffer.Count >= 4)
                {
                    byte[] lenBytes = buffer.GetRange(0, 4).ToArray();
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    int audioLen = BitConverter.ToInt32(lenBytes, 0);
                    buffer.RemoveRange(0, 4);

                    if (audioLen == 0) { currentState = ParseState.ReadTextLen; bytesToRead = 4; }
                    else { bytesToRead = audioLen; currentState = ParseState.ReadAudio; }
                    dataProcessed = true;
                }
            }
            else if (currentState == ParseState.ReadAudio)
            {
                if (buffer.Count >= bytesToRead)
                {
                    byte[] audioData = buffer.GetRange(0, bytesToRead).ToArray();
                    // Zde potřebuješ svou metodu SimpleWav nebo WavUtility!
                    // Předpokládám, že ji máš v projektu.
                    AudioClip clip = WavUtility.ToAudioClip(audioData); // Uprav dle tvé utility

                    if (clip != null)
                    {
                        dialogueQueue.Enqueue(new DialogueSegment
                        {
                            text = tempTextBuffer,
                            emotion = tempEmotionBuffer,
                            clip = clip
                        });
                    }
                    buffer.RemoveRange(0, bytesToRead);
                    currentState = ParseState.ReadTextLen;
                    bytesToRead = 4;
                    tempTextBuffer = "";
                    tempEmotionBuffer = "";
                    dataProcessed = true;
                }
            }
        }
    }

    private void ExtractEmotionAndText(string rawInput, out string cleanText, out string foundEmotion)
    {
        var match = Regex.Match(rawInput, @"\[(.*?)\]");
        if (match.Success)
        {
            foundEmotion = match.Groups[1].Value;
            cleanText = rawInput.Replace(match.Value, "").Trim();
        }
        else
        {
            foundEmotion = "";
            cleanText = rawInput;
        }
    }


    // --- PŘEHRÁVAČ FRONTY (s kontrolou ID) ---
    IEnumerator PlayDialogueQueue(int generationId)
    {
        isPlayingQueue = true;

        while (dialogueQueue.Count > 0)
        {
            // Kontrola na začátku každé věty
            if (generationId != currentGenerationId)
            {
                isPlayingQueue = false;
                yield break;
            }

            DialogueSegment segment = dialogueQueue.Dequeue();

            if (subtitleManager != null && !string.IsNullOrEmpty(segment.text))
                subtitleManager.SetSubtitleText($"<b>{npcName}:</b> {segment.text}");

            if (emotionHandler != null && !string.IsNullOrEmpty(segment.emotion))
                emotionHandler.SetEmotion(segment.emotion);

            if (segment.clip != null)
            {
                npcAudioSource.clip = segment.clip;
                npcAudioSource.Play();

                // Čekáme na domluvení věty (s neustálou kontrolou přerušení)
                while (npcAudioSource.isPlaying)
                {
                    if (generationId != currentGenerationId)
                    {
                        npcAudioSource.Stop();
                        isPlayingQueue = false;
                        yield break;
                    }
                    yield return null;
                }

                yield return new WaitForSeconds(0.1f);
            }
        }

        isPlayingQueue = false;
    }

    private void RefreshChatHistory()
    {
        // Najde objekt typu ChatUIWindow (pokud existuje) a obnoví historii
        var chatWindow = FindFirstObjectByType<ChatUIWindow>(); // Unity 2023+ syntaxe
        if (chatWindow) chatWindow.RefreshHistory();
        // Pro starší Unity: FindObjectOfType<ChatUIWindow>()?.RefreshHistory();
    }
}