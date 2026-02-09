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

    [Header("Input")]
    public KeyCode recordKey;

    // Spotlighty
    private Spotlight npcSpotlight;
    private Spotlight playerSpotlight;

    private bool isProcessing = false;

    // --- STREAMING & PARSING STRUKTURY ---

    // Stavy parseru pro čtení binárního streamu
    private enum ParseState { ReadTextLen, ReadText, ReadAudioLen, ReadAudio }
    private ParseState currentState = ParseState.ReadTextLen;
    private int bytesToRead = 4; // Na začátku vždy čekáme 4 byty (int)
    private string tempTextBuffer = ""; // Dočasné úložiště pro text věty
    private string tempEmotionBuffer = ""; // Dočasné úložiště pro emoci věty


    // Struktura jedné věty (text + audio)
    private class DialogueSegment
    {
        public string text;
        public string emotion;
        public AudioClip clip;
    }

    
    // Fronta vět k přehrání
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
        sttManager = GameObject.Find("Managers").GetComponent<STTManager>();
        emotionHandler = GameObject.Find("Managers").GetComponent<EmotionController>();
        subtitleManager = GameObject.Find("Managers").GetComponent<SubtitleManager>();

        //Player
        playerEyeController = player.GetComponentInChildren<EyeContact>();
        playerAudioSource = player.GetComponentInChildren<AudioSource>();

        //NPC
        npcEyeController = npc.GetComponentInChildren<EyeContact>();
        npcAudioSource = npc.GetComponentInChildren<AudioSource>();


        // 1. Načtení konfigurace
        if (ConfigLoader.config != null)
        {
            urlPlayerTTS = ConfigLoader.GetUrl(ConfigLoader.config.ttsEndpoint);
            urlChatStream = ConfigLoader.GetUrl(ConfigLoader.config.chatRealTime);      
            //urlChatStream = ConfigLoader.GetUrl(ConfigLoader.config.chatStreamEndpoint);
            recordKey = ConfigLoader.talkKey;
        }

        if (player) playerSpotlight = player.GetComponent<Spotlight>();
        if (npc) npcSpotlight = npc.GetComponent<Spotlight>();

        // 2. Setup STT Eventů
        if (sttManager != null)
        {
            sttManager.OnRecordingStart += () => {
                if (subtitleManager) subtitleManager.ShowSubtitleStatic("<i>(Nahrávám...)</i>");
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
        if (isProcessing) return;

        // Textový vstup (Enter)
        if (Input.GetKeyDown(KeyCode.Return) && playerInput && !string.IsNullOrWhiteSpace(playerInput.text))
        {
            string text = playerInput.text;
            playerInput.text = "";
            StartCoroutine(ConversationSequence(text));
        }

        // Hlasový vstup (Klávesa)
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

    // --- HLAVNÍ LOGIKA KONVERZACE ---
    IEnumerator ConversationSequence(string text)
    {
        isProcessing = true;

        // --- 1. HRÁČ MLUVÍ (Optimalizovaný TTS request - přímo do RAM) ---

        if (subtitleManager)
        {
            subtitleManager.SetSubtitleText($"<b>{playerName}:</b> {text}");
        }

        if (playerEyeController)
            playerEyeController.SetTalkingState(true);

        // Příprava JSON dat
        string ttsJson = JsonUtility.ToJson(new RequestData { text = text });

        using (UnityWebRequest req = new UnityWebRequest(urlPlayerTTS, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(ttsJson);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);

            // Očekávej Audio (WAV)
            req.downloadHandler = new DownloadHandlerAudioClip(urlPlayerTTS, AudioType.WAV);
            req.SetRequestHeader("Content-Type", "application/json");

            // Odešleme a čekáme
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                // Vytáhneme Audio Clip přímo z odpovědi (z paměti)
                AudioClip clip = DownloadHandlerAudioClip.GetContent(req);

                if (clip != null)
                {
                    if (playerSpotlight)
                        playerSpotlight.ChangeColorToTarget();

                    // Přiřadíme a přehrajeme
                    playerAudioSource.clip = clip;
                    playerAudioSource.Play();

                    // Čekáme na dokončení audia hráče
                    yield return new WaitForSeconds(clip.length);
                    yield return new WaitForSeconds(0.2f); // Malá pauza pro přirozenost

                    if (playerSpotlight)
                        playerSpotlight.ResetColor();
                }
            }
            else
            {
                Debug.LogError($"[TTS Player Error]: {req.error} - {req.downloadHandler.text}");
            }
        }

        // Úklid po hráči
        if (subtitleManager != null)
            subtitleManager.HideSubtitles();

        if (playerEyeController)
            playerEyeController.SetTalkingState(false);


        // --- 2. NPC ODPOVÍDÁ (Smart Streaming - beze změny) ---
        ShowSubtitle("System", "<i>(NPC přemýšlí...)</i>");

        if (npcSpotlight) npcSpotlight.ChangeColorToTarget();
        if (npcEyeController) npcEyeController.SetTalkingState(true);

        if (emotionHandler != null)
            emotionHandler.SetEmotion("neutral");

        // Spustíme streamování a čekáme na dokončení
        yield return StartCoroutine(StreamChatAudio(text));

        // Úklid po dokončení NPC
        if (npcEyeController)
            npcEyeController.SetTalkingState(false);

        if (npcSpotlight)
            npcSpotlight.ResetColor();

        if (subtitleManager != null)
            subtitleManager.HideSubtitles();

        if (emotionHandler != null)
            emotionHandler.SetEmotion("neutral");

        isProcessing = false;
        RefreshChatHistory();
    }

    IEnumerator StreamChatAudio(string question)
    {
        // Reset stavu parseru před novým streamem
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
                // Kontrola proti pádu (NullReferenceException)
                if (req.downloadHandler != null && req.downloadHandler.data != null)
                {
                    byte[] allData = req.downloadHandler.data;

                    if (allData.Length > processedBytes)
                    {
                        // Máme nová data
                        int newBytesCount = allData.Length - processedBytes;
                        byte[] newChunk = new byte[newBytesCount];
                        Array.Copy(allData, processedBytes, newChunk, 0, newBytesCount);

                        // Pošleme data do parseru
                        ProcessSmartStream(newChunk, ref streamBuffer);
                        processedBytes = allData.Length;
                    }
                }

                // Pokud máme připravenou větu ve frontě, začneme ji hrát
                if (!isPlayingQueue && dialogueQueue.Count > 0)
                {
                    StartCoroutine(PlayDialogueQueue());
                }

                yield return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Stream Error: {req.error}");
                ShowSubtitle("System", "<color=red>Chyba spojení s AI.</color>");
            }
            else
            {
                // Zpracování posledního kousku dat
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

        // Čekáme, dokud NPC nedomluví všechno z fronty
        while (isPlayingQueue || dialogueQueue.Count > 0)
        {
            if (!isPlayingQueue && dialogueQueue.Count > 0) StartCoroutine(PlayDialogueQueue());
            yield return null;
        }

        if (subtitleManager != null)
        {
            subtitleManager.HideSubtitles();
        }
    }

    // --- PARSER STAVOVÉHO AUTOMATU ---
    // Očekávaný formát: [4B Délka Textu] -> [JSON Text] -> [4B Délka Audia] -> [WAV Data]
    // Upravený parser pro flexibilní stream
    // DEBUG VERZE PARSERU
    private void ProcessSmartStream(byte[] newChunk, ref List<byte> buffer)
    {
        // Výpis, že dorazila data ze sítě
        
        buffer.AddRange(newChunk);
        bool dataProcessed = true;
        int safetyLoop = 0; // Pojistka proti nekonečné smyčce

        while (dataProcessed && safetyLoop < 100)
        {
            safetyLoop++;
            dataProcessed = false;

            // KROK 1: Čtení délky textu
            if (currentState == ParseState.ReadTextLen)
            {
                if (buffer.Count >= 4)
                {
                    byte[] lenBytes = buffer.GetRange(0, 4).ToArray();
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    bytesToRead = BitConverter.ToInt32(lenBytes, 0);

                    buffer.RemoveRange(0, 4);

                    
                    if (bytesToRead == 0)
                    {
                        currentState = ParseState.ReadAudioLen;
                        bytesToRead = 4;
                    }
                    else
                    {
                        currentState = ParseState.ReadText;
                    }
                    dataProcessed = true;
                }
            }
            // KROK 2: Čtení samotného textu
            else if (currentState == ParseState.ReadText)
            {
                if (buffer.Count >= bytesToRead)
                {
                    byte[] textBytes = buffer.GetRange(0, bytesToRead).ToArray();
                    string jsonString = Encoding.UTF8.GetString(textBytes);

                    string newText = "";
                    try
                    {
                        var data = JsonUtility.FromJson<RequestData>(jsonString);
                        if (!string.IsNullOrEmpty(data.text))
                        {
                            tempTextBuffer = data.text; // Čistý text pro titulky
                        }
                        
                        // Zde si uložíme emoci přímo z JSONu (už žádný regex v C#)
                        if (!string.IsNullOrEmpty(data.emotion))
                        {
                            tempEmotionBuffer = data.emotion;
                        }
                    }
                    catch 
                    { 
                        newText = jsonString; }

                    ExtractEmotionAndText(newText, out string cleanText, out string foundEmotion);

                    // Jen si text uložíme, aby se přidal k následujícímu audiu
                    if (!string.IsNullOrEmpty(newText))
                    {
                        tempTextBuffer = cleanText;
                        tempEmotionBuffer = foundEmotion;       
                    }

                    buffer.RemoveRange(0, bytesToRead);

                    currentState = ParseState.ReadAudioLen;
                    bytesToRead = 4;
                    dataProcessed = true;
                }
            }

            // ... (zbytek kódu dole) ...
            // KROK 3: Čtení délky audia
            else if (currentState == ParseState.ReadAudioLen)
            {
                if (buffer.Count >= 4)
                {
                    byte[] lenBytes = buffer.GetRange(0, 4).ToArray();
                    if (BitConverter.IsLittleEndian) Array.Reverse(lenBytes);
                    int audioLen = BitConverter.ToInt32(lenBytes, 0);

                    buffer.RemoveRange(0, 4);

                    if (audioLen == 0)
                    {
                        currentState = ParseState.ReadTextLen;
                        bytesToRead = 4;
                    }
                    else
                    {
                        bytesToRead = audioLen;
                        currentState = ParseState.ReadAudio;
                    }
                    dataProcessed = true;
                }
            }
            // KROK 4: Čtení audia
            else if (currentState == ParseState.ReadAudio)
            {
                if (buffer.Count >= bytesToRead)
                {
                    byte[] audioData = buffer.GetRange(0, bytesToRead).ToArray();
                    
                    // Tady použijeme SimpleWav nebo WavUtility
                    // Pokud nemáš SimpleWav, použij WavUtility, ale zkontroluj výsledek!
                    AudioClip clip = SimpleWav.ToAudioClip(audioData);

                    if (clip != null)
                    {
                        dialogueQueue.Enqueue(new DialogueSegment
                        {
                            text = tempTextBuffer,
                            emotion = tempEmotionBuffer,
                            clip = clip
                        });

                        // Nakopneme přehrávání
                        if (!isPlayingQueue)
                        {
                            StartCoroutine(PlayDialogueQueue());
                        }
                    }
                    else
                    {
                        Debug.LogError($"<color=red>[PARSER] CHYBA: AudioClip je NULL! Hlavička: {BitConverter.ToString(audioData, 0, min(10, audioData.Length))}</color>");
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
        // Regex hledá text v hranatých závorkách: [happy], [sad], [angry]
        var match = Regex.Match(rawInput, @"\[(.*?)\]");

        if (match.Success)
        {
            foundEmotion = match.Groups[1].Value; // Vytáhne text uvnitř, např. "happy"
            cleanText = rawInput.Replace(match.Value, "").Trim(); // Odstraní tag z věty
        }
        else
        {
            foundEmotion = ""; // Žádná emoce
            cleanText = rawInput;
        }
    }

    // Pomocná funkce pro bezpečný výpis
    private int min(int a, int b) => (a < b) ? a : b;

    // --- PŘEHRÁVAČ FRONTY ---
    IEnumerator PlayDialogueQueue()
    {
        isPlayingQueue = true;

        while (dialogueQueue.Count > 0)
        {
            DialogueSegment segment = dialogueQueue.Dequeue();

            // --- ZMĚNA: TITULKY ZOBRAZÍME AŽ TEĎ ---
            // Zobrazíme je synchronizovaně se startem audia
            if (subtitleManager != null && !string.IsNullOrEmpty(segment.text))
            {
                subtitleManager.SetSubtitleText($"<b>{npcName}:</b> {segment.text}");
            }

            if (emotionHandler != null && !string.IsNullOrEmpty(segment.emotion))
            {
                emotionHandler.SetEmotion(segment.emotion);
            }

            if (segment.clip != null)
            {
                npcAudioSource.clip = segment.clip;
                npcAudioSource.Play();

                // Čekáme přesně délku klipu (titulky po celou dobu svítí)
                yield return new WaitForSeconds(segment.clip.length);

                // Malá pauza mezi větami pro přirozenost
                yield return new WaitForSeconds(0.1f);
            }
        }

        yield return new WaitForSeconds(0.5f); // Půl vteřiny necháme výraz, aby to neutnul hned
        if (emotionHandler != null)
        {
            emotionHandler.SetEmotion("neutral");
        }

        // Když fronta dojede, skryjeme titulky (volitelné, nebo je tam nechat viset)
        /* if (subtitleManager != null) 
        {
             subtitleManager.HideSubtitles();
        }
        */

        isPlayingQueue = false;
    }

    // --- HELPERY PRO HTTP ---
    IEnumerator SendPostRequest(string url, string json, System.Action<string> onSuccess = null)
    {
        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(json);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success) onSuccess?.Invoke(req.downloadHandler.text);
            else Debug.LogError($"API Error: {req.error}");
        }
    }

    IEnumerator DownloadAndPlayUrl(string url, AudioSource source)
    {
        // Pozor: Zde používáme AudioType.WAV, protože XTTS vrací WAV
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.WAV))
        {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success)
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                source.clip = clip;

            }
            else
            {
                Debug.LogError($"Chyba stahování audia hráče: {www.error}");
            }
        }
    }
    private void RefreshChatHistory()
    {
        FindFirstObjectByType<ChatUIWindow>().RefreshHistory();
    }

}