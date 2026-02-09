using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System; // Potřeba pro Action

public class STTManager : MonoBehaviour
{
    [Header("Server API")]
    private string urlSTT ;

    [Header("Microphone Settings")]
    public int sampleRate = 16000;
    public int maxDuration = 60;

    public Action<string> OnTranscriptionComplete;
    public Action OnRecordingStart;
    public Action OnRecordingStop;
    public Action<string> OnError;

    private AudioClip recording;
    private string micName;
    private bool isRecording = false;

    // Třída pro JSON odpověď
    [Serializable]
    private class STTResponse 
    { 
        public string status;
        public string text;
    }

    void Start()
    {
        if(ConfigLoader.config != null)
            urlSTT = ConfigLoader.GetUrl(ConfigLoader.config.sttEndpoint);

        if (Microphone.devices.Length > 0)
            micName = Microphone.devices[0];
        else
            Debug.LogError("[STTManager] Žádný mikrofon nenalezen!");
    }

    // Voláno z RuntimeTTS
    public void StartRecording()
    {
        if (isRecording) return;

        recording = Microphone.Start(micName, false, maxDuration, sampleRate);
        isRecording = true;

        // Dáme vědět ostatním, že nahráváme (např. pro UI)
        OnRecordingStart?.Invoke();
    }

    // Voláno z RuntimeTTS
    public void StopAndUpload()
    {
        if (!isRecording) return;

        isRecording = false;

        // Zjistíme délku
        int lastPos = Microphone.GetPosition(micName);
        Microphone.End(micName);

        OnRecordingStop?.Invoke();

        if (lastPos == 0) return; // Nic se nenahrálo

        // Oříznutí a upload
        StartCoroutine(ProcessAudio(lastPos));
    }

    private IEnumerator ProcessAudio(int lengthSamples)
    {
        // 1. Oříznutí Clipu
        float[] samples = new float[lengthSamples];
        recording.GetData(samples, 0);
        AudioClip trimmedClip = AudioClip.Create("VoiceInput", lengthSamples, 1, sampleRate, false);
        trimmedClip.SetData(samples, 0);

        // 2. Převod na WAV (Vyžaduje WavUtility.cs!)
        byte[] wavData = WavUtility.FromAudioClip(trimmedClip);

        // 3. Odeslání
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "voice.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(urlSTT, form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"Chyba STT: {www.error}";
                Debug.LogError(errorMsg);
                OnError?.Invoke(errorMsg);
            }
            else
            {
                // 4. Parsování odpovědi
                var response = JsonUtility.FromJson<STTResponse>(www.downloadHandler.text);

                // 5. ODESLÁNÍ VÝSLEDKU ZPĚT DO RUNTIMETTS  
                if (!string.IsNullOrEmpty(response.text))
                {
                    Debug.Log($"[STTManager] Rozpoznáno: {response.text}");
                    OnTranscriptionComplete?.Invoke(response.text);
                }
            }
        }
    }
}