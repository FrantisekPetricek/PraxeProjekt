using UnityEngine;
using System.Collections;
using System.Linq;

public class DialogScript : MonoBehaviour
{
    [Header("Odkazy na Komponenty")]
    public AudioSource audioSource;

    [Header("Nastavení Sekvence")]
    [Tooltip("Název složky v Assets/Resources, odkud se budou klipy naèítat (napø. 'AudioDialogs').")]
    public string resourcePath = "AudioDialogs";

    [Tooltip("Stisknìte pro spuštìní dalšího klipu v sekvenci.")]
    public KeyCode nextClipKey = KeyCode.Space;

    // Privátní promìnné
    private AudioClip[] dialogueClips;
    private int currentClipIndex = 0;

    void Start()
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource není pøiøazen! Pøerušuji pøehrávání.");
            return;
        }

        // Automatické naètení klipù
        LoadDialogueClips();

        if (dialogueClips == null || dialogueClips.Length == 0)
        {
            Debug.LogError($"Nenalezeny žádné AudioClipy ve složce Resources/{resourcePath}. Zkontrolujte cestu a zda klipy existují.");
            return;
        }

        Debug.Log($"Naèteno {dialogueClips.Length} dialogových klipù z Resources/{resourcePath}.");
    }

    void Update()
    {
        if (Input.GetKeyDown(nextClipKey) && !audioSource.isPlaying)
        {
            PlayNextClip();
        }
    }

    private void LoadDialogueClips()
    {
        // Naète VŠECHNY AudioClipy ze zadané cesty uvnitø libovolné složky Resources.
        // Seøadíme je podle názvu, aby se pøehrávaly ve správném poøadí (napø. 01_Intro, 02_Next).
        dialogueClips = Resources.LoadAll<AudioClip>(resourcePath)
            .OrderBy(clip => clip.name)
            .ToArray();
    }

    public void PlayNextClip()
    {
        // ... (Zbytek kódu pro pøehrávání zùstává stejný)
        // (Zkráceno pro pøehlednost)

        if (currentClipIndex >= dialogueClips.Length)
        {
            Debug.Log("Sekvence dialogù dokonèena.");
            currentClipIndex = 0;
            return;
        }

        AudioClip nextClip = dialogueClips[currentClipIndex];

        audioSource.clip = nextClip;
        audioSource.Play();

        Debug.Log($"Spouštím klip: {nextClip.name} (Klip {currentClipIndex + 1}/{dialogueClips.Length})");

        currentClipIndex++;
    }
}