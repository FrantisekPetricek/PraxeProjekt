using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ErrorManager : MonoBehaviour
{
    // Tohle je ta magie. Díky tomuto øádku bude skript dostupný odkudkoliv.
    public static ErrorManager Instance;

    [Header("UI References")]
    public GameObject errorPanel;
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI messageText;
    public Button closeButton;

    void Awake()
    {
        // Nastavení Singletonu (zajistí, že existuje vždy jen jeden ErrorManager)
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && errorPanel.activeSelf)
        {
            HideError();
        }
    }

    void Start()
    {
        // Ujistíme se, že je panel na startu skrytý
        if (errorPanel != null) errorPanel.SetActive(false);

        // Pøiøadíme tlaèítku funkci pro zavøení
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(HideError);
        }
    }

    // Tuto funkci budeš volat, když se nìco pokazí
    public void ShowError(string message, string title = "Chyba pøipojení!")
    {
        if (errorPanel == null) return;

        titleText.text = title;
        messageText.text = message;
        errorPanel.SetActive(true);
    }

    // Funkce pro schování panelu
    public void HideError()
    {
        if (errorPanel != null) 
            errorPanel.SetActive(false);
    }
}