using UnityEngine;

public class BeMoreAlive : MonoBehaviour
{
    public SkinnedMeshRenderer characterMesh;
    [Header("Èelist a Dech")]
    public string jawOpen = "JawOpen";
    public string jawForward = "JawFwd";

    [Header("Jemné pohyby úst (Random Ticks)")]
    // Zde vypište blendshapes, které chcete náhodnì "propisovat"
    public string[] mouthTicks = {
        "MouthDimple_L",
        "MouthDimple_R",
        "LipsPucker",
        "MouthLeft",
        "MouthRight",
        "ChinUpperRaise"
    };

    [Header("Nastavení")]
    public float breathingSpeed = 1.5f; // Rychlost "dıchání" èelisti
    public float jawMaxOpenAmount = 3.0f; // O kolik % se èelist pootevøe pøi vıdechu (max)

    public float tickStrength = 8.0f; // Síla náhodnıch pohybù (ne moc, a to neruší øeè)
    public Vector2 tickInterval = new Vector2(3.0f, 8.0f); // Jak èasto

    // Vnitøní promìnné
    private float breathTimer;
    private float tickTimer;
    private int currentTickIndex = -1;
    private bool isTicking = false;
    private float tickProgress = 0;

    [Header("Oèi - Názvy Blendshapes")]
    public string eyeLeft = "EyesLeft";
    public string eyeRight = "EyesRight";
    public string eyeUp = "EyesUp";
    public string eyeDown = "EyesDown";

    [Header("Mikro-vırazy (Idle Noise)")]
    public string[] microExpressions = {
        "BrowsU_C",
        "BrowsD_L",
        "Sneer",
        "MouthDimple_L"
    };

    [Header("Nastavení Oèí")]
    public float eyeMovementRange = 0.6f;

    [Header("Nastavení Vırazù")]
    [Tooltip("Maximální síla vırazu (0-100). Zkuste dát víc (napø. 40), abyste vidìli efekt.")]
    public float microExpStrength = 40f;

    [Tooltip("Rychlost animace vırazu (vyšší = rychlejší cuknutí)")]
    public float expressionSpeed = 2.0f;

    [Tooltip("Pauza mezi vırazy (minimum, maximum)")]
    public Vector2 delayInterval = new Vector2(1.0f, 4.0f);

    // Promìnné pro Oèi
    private float targetX, targetY;
    private float currentX, currentY;
    private float eyeTimer;

    // Promìnné pro Vırazy
    private float expTimer;
    private int activeExpIndex = -1;
    private bool isAnimatingExpression = false;
    private float animationProgress = 0f; // 0 a PI (3.14)

    void Start()
    {
        if (!characterMesh) 
            characterMesh = GetComponent<SkinnedMeshRenderer>();
        // Reset vırazù na startu
        expTimer = Random.Range(delayInterval.x, delayInterval.y);
        tickTimer = Random.Range(tickInterval.x, tickInterval.y);
    }

    void Update()
    {
        HandleEyes();
        HandleMicroExpressions();
        HandleBreathingJaw();
        HandleMouthTicks();
    }

    void HandleEyes()
    {
        eyeTimer -= Time.deltaTime;
        if (eyeTimer <= 0)
        {
            targetX = Random.Range(-1f, 1f) * eyeMovementRange;
            targetY = Random.Range(-0.5f, 0.5f) * eyeMovementRange;
            eyeTimer = Random.Range(0.5f, 2.0f);
        }

        currentX = Mathf.Lerp(currentX, targetX, Time.deltaTime * 5f);
        currentY = Mathf.Lerp(currentY, targetY, Time.deltaTime * 5f);

        SetShapeEyes(eyeRight, currentX > 0 ? currentX * 100 : 0);
        SetShapeEyes(eyeLeft, currentX < 0 ? -currentX * 100 : 0);
        SetShapeEyes(eyeUp, currentY > 0 ? currentY * 100 : 0);
        SetShapeEyes(eyeDown, currentY < 0 ? -currentY * 100 : 0);
    }

    void HandleMicroExpressions()
    {
        if (microExpressions.Length == 0) return;

        if (isAnimatingExpression)
        {
            // Animace (Sinusoida 0 -> 1 -> 0)
            animationProgress += Time.deltaTime * expressionSpeed;

            // Sinusoida v rozsahu 0 a PI udìlá krásnı kopeèek
            float curve = Mathf.Sin(animationProgress);

            if (animationProgress >= Mathf.PI) // Konec animace 
            {
                // Ujistíme se, e je to èistá 0 a ukonèíme
                SetShapeEyes(microExpressions[activeExpIndex], 0);
                isAnimatingExpression = false;
                expTimer = Random.Range(delayInterval.x, delayInterval.y); // Nastavíme pauzu
            }
            else
            {
                // Aplikujeme váhu
                SetShapeEyes(microExpressions[activeExpIndex], curve * microExpStrength);
            }
        }
        else
        {
            // Èekání
            expTimer -= Time.deltaTime;
            if (expTimer <= 0)
            {
                // Vybereme novı vıraz a spustíme animaci
                activeExpIndex = Random.Range(0, microExpressions.Length);
                animationProgress = 0f;
                isAnimatingExpression = true;
            }
        }
    }

    void SetShapeEyes(string name, float value)
    {
        int index = characterMesh.sharedMesh.GetBlendShapeIndex(name);
        if (index != -1) characterMesh.SetBlendShapeWeight(index, value);
    }

    // Simulace dıchání pøes uvolnìní èelisti
    void HandleBreathingJaw()
    {
        breathTimer += Time.deltaTime * breathingSpeed;

        // Sinusoida 0..1 (pomalá a plynulá)
        float breathVal = (Mathf.Sin(breathTimer) + 1f) * 0.5f;

        // Aplikujeme na JawOpen (jen velmi jemnì)
        // Kdy vydechneme, svaly povolí a pusa se malinko pootevøe
        SetShapeMouth(jawOpen, breathVal * jawMaxOpenAmount);

        // Obèas posuneme èelist dopøedu (pøi nádechu)
        SetShapeMouth(jawForward, (1f - breathVal) * (jawMaxOpenAmount * 0.5f));
    }

    // Náhodné pohyby rtù a brady
    void HandleMouthTicks()
    {
        if (mouthTicks.Length == 0) return;

        if (isTicking)
        {
            // Animace tiku (Rychlı nábìh a pomalı ústup)
            tickProgress += Time.deltaTime * 3.0f; // Rychlost tiku
            float curve = Mathf.Sin(tickProgress); // Jednoduchı kopeèek

            if (tickProgress >= Mathf.PI)
            {
                SetShapeMouth(mouthTicks[currentTickIndex], 0); // Uklidíme
                isTicking = false;
                tickTimer = Random.Range(tickInterval.x, tickInterval.y);
            }
            else
            {
                SetShapeMouth(mouthTicks[currentTickIndex], curve * tickStrength);
            }
        }
        else
        {
            tickTimer -= Time.deltaTime;
            if (tickTimer <= 0)
            {
                currentTickIndex = Random.Range(0, mouthTicks.Length);
                isTicking = true;
                tickProgress = 0f;
            }
        }
    }

    void SetShapeMouth(string name, float value)
    {
        int index = characterMesh.sharedMesh.GetBlendShapeIndex(name);
        // Ochrana: Pokud LipSync zrovna mluví (JawOpen je tøeba > 10), tak my do toho nezasahujeme
        // (Toto je volitelné, záleí jak funguje váš lip sync)
        if (index != -1)
        {
            // Získáme aktuální váhu, abychom se nepøebíjeli s Lip Syncem, pokud je hodnota vysoká
            float currentWeight = characterMesh.GetBlendShapeWeight(index);

            // Pokud je blendshape u hodnì aktivní (asi mluví), nepøièítáme naše drobnosti
            if (currentWeight < 20f)
            {
                // Nastavíme hodnotu (nebo pøièteme, pokud byste chtìli 'Add' logiku)
                // Zde pouívám Set, ale pro JawOpen by bylo lepší hlídat, zda nemluví.
                characterMesh.SetBlendShapeWeight(index, value);
            }
        }
    }


}