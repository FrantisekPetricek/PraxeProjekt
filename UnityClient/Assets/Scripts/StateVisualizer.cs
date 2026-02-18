using UnityEngine;
using UnityEngine.UI;

public class SimpleStateVisualizer : MonoBehaviour
{
    [Header("References")]
    public Image targetImage;       
    public RuntimeTTS runtimeTTS;   

    [Header("State Colors")]
    public Color idleColor = Color.green;      
    public Color listeningColor = Color.red;   
    public Color thinkingColor = Color.yellow; 
    public Color speakingColor = Color.blue;   

    [Header("Settings")]
    public float colorChangeSpeed = 10f; 

    void Update()
    {
        if (targetImage == null || runtimeTTS == null) return;

        Color targetColor = idleColor; 

        // --- Rozhodovací logika (Priorita stavù) ---

        // 1. Nejvyšší priorita: Uživatel mluví (drží klávesu)
        if (Input.GetKey(ConfigLoader.talkKey))
        {
            targetColor = listeningColor;
        }
        // 2. AI mluví (pøehrává audio)
        // Dáváme pøednost mluvení pøed "thinking", protože isProcessing je true i bìhem mluvení.
        else if (runtimeTTS.IsSpeaking)
        {
            targetColor = speakingColor;
        }
        // 3. AI pøemýšlí (je processing, ale ještì nemluví)
        else if (runtimeTTS.isProcessing)
        {
            targetColor = thinkingColor;
        }
        // 4. Jinak zùstává idleColor (zelená)

        // --- Aplikace barvy ---
        // Použijeme Lerp pro plynulý pøechod mezi barvami, vypadá to lépe než ostré bliknutí.
        targetImage.color = Color.Lerp(targetImage.color, targetColor, Time.deltaTime * colorChangeSpeed);
    }
}