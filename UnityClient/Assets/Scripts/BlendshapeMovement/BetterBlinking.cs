using UnityEngine;

public class BetterBlinking : MonoBehaviour
{
    public SkinnedMeshRenderer characterMesh;

    [Header("Blendshapes pro Víèka")]
    public string blinkLeft = "EyeBlink_L";
    public string blinkRight = "EyeBlink_R";
    public string openLeft = "EyeOpen_L";  
    public string openRight = "EyeOpen_R";

    [Header("Blendshapes pro Oèi ")]
    public string eyesY = "EyesUp"; 
    public string eyesDown = "EyesDown"; 

    [Header("Nastavení Mrkání")]
    public float minBlinkInterval = 2.0f;
    public float maxBlinkInterval = 6.0f;
    public float blinkSpeed = 15.0f;

    [Header("Nastavení Sledování Oèí")]
    [Tooltip("Jak moc víèka reagují na pohyb oèí nahoru/dolù")]
    public float sensitivity = 1.5f;

    // Vnitøní stav
    private float blinkTimer;
    private float currentBlinkValue; // 0 = otevøeno, 100 = zavøeno
    private bool isBlinking;
    private int blinkStage; 
    // 0=nic, 1=zavírání, 2=otevírání

    void Start()
    {
        if (!characterMesh) characterMesh = GetComponent<SkinnedMeshRenderer>();
        blinkTimer = Random.Range(minBlinkInterval, maxBlinkInterval);
    }

    void LateUpdate()
    {
        // LateUpdate je lepší pro reakci na animace, které probìhly v Update
        HandleBlinking();
        ApplyEyelidLogic();
    }

    void HandleBlinking()
    {
        blinkTimer -= Time.deltaTime;

        // Spuštìní mrknutí
        if (blinkTimer <= 0 && !isBlinking)
        {
            isBlinking = true;
            blinkStage = 1; // Zaèít zavírat
        }

        if (isBlinking)
        {
            if (blinkStage == 1) // Zavírání
            {
                currentBlinkValue += Time.deltaTime * blinkSpeed * 100f;
                if (currentBlinkValue >= 100f)
                {
                    currentBlinkValue = 100f;
                    blinkStage = 2; // Zaèít otevírat
                }
            }
            else if (blinkStage == 2) // Otevírání
            {
                currentBlinkValue -= Time.deltaTime * blinkSpeed * 80f; // Otevírání je pomalejší
                if (currentBlinkValue <= 0f)
                {
                    currentBlinkValue = 0f;
                    isBlinking = false;
                    // Reset èasovaèe s náhodou
                    blinkTimer = Random.Range(minBlinkInterval, maxBlinkInterval);

                    // 10% šance na "dvojité mrknutí" (krátký interval)
                    if (Random.value > 0.9f) blinkTimer = 0.15f;
                }
            }
        }
    }

    void ApplyEyelidLogic()
    {
        // Zjistíme, kam se oèi dívají 
        float eyeVerticalLook = GetShapeWeight(eyesY); // Vrací 0-100

        // Pokud model používá EyesDown pro pohled dolù, musíme to zohlednit
        
        float upperLidOffset = 0f;
        float blinkAdd = 0f;

        // Simulace: Pokud se oèi dívají nahoru (hodnota > 0)
        if (eyeVerticalLook > 1f)
        {
            // Oèi jdou nahoru -> otevøeme víèka víc (EyeOpen)
            upperLidOffset = eyeVerticalLook * sensitivity;
        }

        // Pokud EyesDown > 0 -> oèi jdou dolù -> pøivøeme víèka (pøidáme k mrkání)
        float eyeDownLook = GetShapeWeight(eyesDown );
        if (eyeDownLook > 1f)
        {
            blinkAdd = eyeDownLook * 0.5f * sensitivity; // Pøivøeme víèka na 50% pohybu oka
        }

        // Hodnota pro EyeOpen (Wide eyes) - jen když nemrkáme
        float finalOpen = Mathf.Clamp(upperLidOffset - currentBlinkValue, 0, 100);

        // Hodnota pro EyeBlink (Zavøené oèi) - Mrkání + Pohled dolù
        float finalBlink = Mathf.Clamp(currentBlinkValue + blinkAdd, 0, 100);

        SetShape(blinkLeft, finalBlink);
        SetShape(blinkRight, finalBlink);
        SetShape(openLeft, finalOpen);
        SetShape(openRight, finalOpen);
    }

    float GetShapeWeight(string name)
    {
        int index = characterMesh.sharedMesh.GetBlendShapeIndex(name);
        if (index != -1) return characterMesh.GetBlendShapeWeight(index);
        return 0f;
    }

    void SetShape(string name, float value)
    {
        int index = characterMesh.sharedMesh.GetBlendShapeIndex(name);
        if (index != -1) characterMesh.SetBlendShapeWeight(index, value);
    }
}