using CrazyMinnow.SALSA;
using UnityEngine;

public class EmotionController : MonoBehaviour
{
    [Header("Settings")]
    public Emoter emoter; // Opraveno na "Emoter" dle tvé verze

    [Tooltip("Doba pøechodu mezi emocemi (v sekundách)")]
    public float fadeDuration = 0.3f;

    private string currentEmotion = "";

    void Start()
    {
        // Automatické nalezení komponenty
        if (emoter == null)
        {
            emoter = GetComponent<Emoter>();
        }

        if (emoter == null)
        {
            Debug.LogError("[EmotionController] CHYBA: Na objektu chybí komponenta 'Emoter'!");
        }
    }

    public void SetEmotion(string emotionInput)
    {
        if (emoter == null) return;

        string cleanInput = emotionInput.Trim().ToLower();

        // 1. Vypnutí pøedchozí emoce (pokud nìjaká byla)
        if (!string.IsNullOrEmpty(currentEmotion))
        {
            // Kontrola, zda pøedchozí emoce stále existuje
            if (emoter.FindEmote(currentEmotion) != null)
            {
                try
                {
                    // Pokusíme se vypnout starou emoci
                    emoter.ManualEmote(currentEmotion, ExpressionComponent.ExpressionHandler.OneWay, fadeDuration, false, 0f);
                }
                catch
                {
                    // Pokud to spadne (tøeba chybí mesh), nic se nedìje, jen to ignorujeme
                }
            }
            currentEmotion = "";
        }

        if (cleanInput == "neutral" || cleanInput == "none") return;

        // 2. Mapování textu
        string targetEmote = "";

        switch (cleanInput)
        {
            case "happy":
            case "radost":
            case "smile":
                targetEmote = "Smile";
                break;
            case "sad":
            case "smutek":
            case "frown":
                targetEmote = "Frown";
                break;
            case "angry":
            case "zlost":
                targetEmote = "Anger";
                break;
            case "surprise":
            case "prekvapeni":
                targetEmote = "Surprise";
                break;
            default:
                Debug.LogWarning($"[EmotionController] Neznámý pøíkaz: '{cleanInput}'");
                return;
        }

        // 3. POKUS O SPUŠTÌNÍ NOVÉ EMOCE (Bezpeènì)
        var emoteDef = emoter.FindEmote(targetEmote);

        if (emoteDef != null)
        {
            try
            {
                // Zde se pokusíme emoci spustit.
                // Pokud v Inspectoru chybí Mesh, SALSA zde vyhodí chybu.
                // My ji ale chytíme v bloku 'catch', takže hra NESPADNE.
                emoter.ManualEmote(targetEmote, ExpressionComponent.ExpressionHandler.OneWay, fadeDuration, true, 1f);

                // Pokud jsme došli sem, vše probìhlo OK
                currentEmotion = targetEmote;
            }
            catch (System.Exception ex)
            {
                // Tady skonèíme, pokud máš v Inspectoru chybu (chybìjící Mesh)
                Debug.LogError($"[EmotionController] CHYBA NASTAVENÍ: Emoce '{targetEmote}' existuje, ale pøi pokusu o spuštìní selhala. Pravdìpodobnì chybí 'SkinnedMesh' v Emoter komponentì! Detaily: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[EmotionController] Chyba: Emoce '{targetEmote}' nebyla v seznamu Emoter nalezena!");
        }
    }
}