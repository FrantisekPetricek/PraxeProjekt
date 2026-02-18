using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MicVisualizer : MonoBehaviour
{
    [Header("References")]
    public STTManager sttManager;
    public Transform barsContainer;
    public GameObject barPrefab;
    public CanvasGroup canvasGroup; 

    [Header("Settings")]
    public int numberOfBars = 11;
    public float sensitivity = 8.0f;
    public float minHeight = 10f;
    public float maxHeight = 120f;
    public float smoothSpeed = 15f;
    public float fadeSpeed = 10f;   

    [Header("Shape Settings")]
    [Range(0f, 1f)]
    public float edgeMultiplier = 0.3f;

    private List<RectTransform> bars = new List<RectTransform>();
    private bool isActive = false;

    void Start()
    {
        // Pokud jsi zapomnìl pøiøadit CanvasGroup v inspectoru, zkusíme ji najít
        if (canvasGroup == null && barsContainer != null)
            canvasGroup = barsContainer.GetComponent<CanvasGroup>();

        // Okamžitì skryjeme na zaèátku
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        // Vyèištìní
        foreach (Transform child in barsContainer) { Destroy(child.gameObject); }
        bars.Clear();

        // Generování barù
        for (int i = 0; i < numberOfBars; i++)
        {
            GameObject obj = Instantiate(barPrefab, barsContainer);
            bars.Add(obj.GetComponent<RectTransform>());
            obj.GetComponent<RectTransform>().sizeDelta = new Vector2(obj.GetComponent<RectTransform>().sizeDelta.x, minHeight);
        }

        // Napojení na události
        if (sttManager != null)
        {
            sttManager.OnRecordingStart += () => SetActive(true);
            sttManager.OnRecordingStop += () => SetActive(false);
        }

        SetActive(false);
    }

    void Update()
    {
        // --- 1. ØÍZENÍ VIDITELNOSTI (FADE IN / FADE OUT) ---
        if (canvasGroup != null)
        {
            float targetAlpha = isActive ? 1f : 0f;
            // Plynulá zmìna prùhlednosti
            canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, targetAlpha, Time.deltaTime * fadeSpeed);

            // Pokud je to témìø neviditelné, ukonèíme Update, abychom šetøili výkon
            if (canvasGroup.alpha < 0.01f && !isActive) return;
        }

        // --- 2. ANIMACE BARÙ ---

        // Pokud nenahráváme (a ještì jsme nezmizeli úplnì), zmenšujeme bary na minimum
        if (!isActive)
        {
            foreach (var bar in bars)
            {
                bar.sizeDelta = Vector2.Lerp(bar.sizeDelta, new Vector2(bar.sizeDelta.x, minHeight), Time.deltaTime * smoothSpeed);
            }
            return;
        }

        // Získáme hlasitost
        float volume = sttManager.GetCurrentVolume() * sensitivity;
        int count = bars.Count;

        for (int i = 0; i < count; i++)
        {
            float normalizedPos = (count > 1) ? (i / (float)(count - 1)) * 2.0f - 1.0f : 0f;
            float parabolaShape = 1.0f - (normalizedPos * normalizedPos);
            float finalShapeMultiplier = Mathf.Lerp(edgeMultiplier, 1.0f, parabolaShape);

            float targetHeight = Mathf.Clamp(volume * maxHeight * finalShapeMultiplier, minHeight, maxHeight);

            bars[i].sizeDelta = Vector2.Lerp(bars[i].sizeDelta, new Vector2(bars[i].sizeDelta.x, targetHeight), Time.deltaTime * smoothSpeed);
        }
    }

    private void SetActive(bool state)
    {
        isActive = state;
    }
}