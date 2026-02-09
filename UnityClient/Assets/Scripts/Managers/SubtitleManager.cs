using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class SubtitleManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI subtitleText;
    public GameObject subtitlePanel; 

    [Header("Settings")]
    public int maxCharSize = 250;
    public float defaultDuration = 3.0f; 

    private void Awake()
    {
        
        HideSubtitles();
    }

    public void ShowSubtitleSynced(string fullText, float audioDuration)
    {
        if (subtitlePanel) subtitlePanel.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(ProcessSubtitlesSynced(fullText, audioDuration));
    }

    public void ShowSubtitleStatic(string text)
    {
        if (subtitlePanel) subtitlePanel.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(ShowStaticRoutine(text));
    }

    // Okamžité skrytí
    public void HideSubtitles()
    {
        StopAllCoroutines();
        subtitleText.text = "";
        if (subtitlePanel) subtitlePanel.SetActive(false);
    }
    public void SetSubtitleText(string text)
    {
        StopAllCoroutines(); // Zastavíme pøípadné èasovaèe z minula
        if (subtitlePanel) subtitlePanel.SetActive(true);
        subtitleText.text = text;
    }

    // --- COROUTINES ---

    IEnumerator ProcessSubtitlesSynced(string fullText, float totalAudioDuration)
    {
        int totalTextLength = fullText.Length;
        List<string> pages = SplitTextToPages(fullText, maxCharSize);

        foreach (string page in pages)
        {
            subtitleText.text = page;

            // Výpoèet èasu pro tento titulek
            float pageDuration = totalAudioDuration * ((float)page.Length / totalTextLength);

            // Pojistka, aby text neblikl moc rychle, pokud je audio dlouhé
            if (pageDuration < 1.0f && totalAudioDuration > 2.0f) pageDuration = 1.0f;

            yield return new WaitForSeconds(pageDuration);
        }

        HideSubtitles();
    }
    IEnumerator ShowStaticRoutine(string text)
    {
        subtitleText.text = text;
        // Èekáme fixní dobu (defaultDuration)
        yield return new WaitForSeconds(defaultDuration);
        // Konec -> Skrýt
        HideSubtitles();
    }

    // --- HELPER ---
    List<string> SplitTextToPages(string text, int maxChars)
    {
        List<string> result = new List<string>();
        string[] words = text.Split(' ');
        string currentLine = "";

        foreach (string word in words)
        {
            if ((currentLine + word).Length > maxChars)
            {
                result.Add(currentLine.Trim());
                currentLine = "";
            }
            currentLine += word + " ";
        }
        if (!string.IsNullOrWhiteSpace(currentLine)) result.Add(currentLine.Trim());
        return result;
    }
}