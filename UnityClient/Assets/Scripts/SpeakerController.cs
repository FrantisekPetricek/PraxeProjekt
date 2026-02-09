using UnityEngine;

public class SpeakerController : MonoBehaviour
{
    [Tooltip("Audio Source pro pøehrávání dialogù.")]
    public AudioSource audioSource;

    [Tooltip("Transform, na který se má kamera dívat (obvykle prázdný objekt u oblièeje).")]
    public Transform cameraTarget;

    // Veøejná vlastnost pro kontrolu mluvení
    public bool IsSpeaking => audioSource.isPlaying && audioSource != null;

    public void StartSpeaking(AudioClip clip)
    {
        if(audioSource == null)

        // Kontrola, zda již nemluví (pro jistotu)
        if (audioSource.isPlaying) return;

        audioSource.clip = clip;
        audioSource.Play();

        // Informuje manažera, že se zaèalo mluvit
        DialogueManager.Instance.NotifySpeakerStarted(this);
    }
}