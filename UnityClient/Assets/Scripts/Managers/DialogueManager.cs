using UnityEngine;
using System.Collections;
using System.Collections.Generic; // Potøeba pro List
using System.Linq;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance;

    // --- ZMÌNA 1: Definice struktury pro pøiøazení jména k objektu ---
    [System.Serializable]
    public struct DialogueActor
    {
        [Tooltip("Text, který se musí nacházet v názvu souboru (napø. 'Expert', 'Noob', 'Adam').")]
        public string characterID;

        [Tooltip("Controller, který bude mluvit.")]
        public SpeakerController controller;
    }

    [Header("Odkazy na Scénu")]
    // Zmìnil jsem typ na MonoBehaviour, protože standardní 'Camera' nemá metodu SetTarget.
    // Sem pøetáhnìte váš skript na kameøe.
    public MonoBehaviour cameraTracker;

    [Tooltip("Seznam mluvèích a jejich ID (jmen v souborech).")]
    public List<DialogueActor> actors; // Místo prostého pole 'speakers'

    [Header("Nastavení Dialogu")]
    public string resourcePath = "AudioDialogs";
    public KeyCode startKey = KeyCode.Space;

    // Privátní stavy
    private AudioClip[] dialogueClips;
    private int currentClipIndex = 0;
    private SpeakerController activeSpeaker;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        LoadDialogueClips();

        if (dialogueClips == null || dialogueClips.Length == 0)
        {
            Debug.LogError($"Nenalezeny žádné AudioClipy ve složce Resources/{resourcePath}.");
            return;
        }

        if (actors == null || actors.Count == 0)
        {
            Debug.LogError("Chybí nastavení Actors! Musíte pøiøadit ID a Controllery v Inspectoru.");
            return;
        }

        Debug.Log($"Naèteno {dialogueClips.Length} klipù. Stisknìte {startKey} pro start.");
    }

    void Update()
    {
        if (Input.GetKeyDown(startKey) && currentClipIndex == 0)
        {
            PlayNextLine();
        }
    }

    private void LoadDialogueClips()
    {
        dialogueClips = Resources.LoadAll<AudioClip>(resourcePath)
            .OrderBy(clip => clip.name)
            .ToArray();
    }

    private void PlayNextLine()
    {
        if (currentClipIndex >= dialogueClips.Length)
        {
            Debug.Log("Dialog dokonèen.");
            currentClipIndex = 0;
            return;
        }

        AudioClip clipToPlay = dialogueClips[currentClipIndex];

        // --- ZMÌNA 2: Inteligentní výbìr mluvèího podle názvu souboru ---
        activeSpeaker = FindSpeakerByFilename(clipToPlay.name);

        if (activeSpeaker == null)
        {
            Debug.LogError($"Nepodaøilo se najít mluvèího pro soubor: {clipToPlay.name}. Zkontrolujte Character ID v Inspectoru.");
            // Nouzový posun na další, aby se hra nezasekla
            currentClipIndex++;
            PlayNextLine();
            return;
        }

        // 1. Nastavení cíle kamery (pomocí SendMessage nebo GetComponent, pokud neznáme pøesný typ scriptu)
        if (cameraTracker != null && activeSpeaker.cameraTarget != null)
        {
            // Pøedpokládám, že na kameøe máte metodu "SetTarget"
            cameraTracker.SendMessage("SetTarget", activeSpeaker.cameraTarget, SendMessageOptions.DontRequireReceiver);
        }

        // 2. Spuštìní mluvení
        activeSpeaker.StartSpeaking(clipToPlay);
    }

    // --- ZMÌNA 3: Logika hledání ---
    private SpeakerController FindSpeakerByFilename(string filename)
    {
        // Projdeme všechny nadefinované herce
        foreach (var actor in actors)
        {
            // Pokud název souboru obsahuje ID herce (napø. "PC_01_Expert_Intro" obsahuje "Expert")
            if (filename.Contains(actor.characterID))
            {
                return actor.controller;
            }
        }
        return null; // Nikdo nenalezen
    }

    public void NotifySpeakerStarted(SpeakerController speaker)
    {
        StartCoroutine(WaitForLineEnd(speaker));
    }

    IEnumerator WaitForLineEnd(SpeakerController speaker)
    {
        while (speaker.IsSpeaking)
        {
            yield return null;
        }

        currentClipIndex++;
        PlayNextLine();
    }
}