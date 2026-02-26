using UnityEngine;

public class QuitManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject quitPopupPanel;

    void Start()
    {
        if (quitPopupPanel != null)
        {
            quitPopupPanel.SetActive(false);
        }
    }

    void Update()
    {
        // Hlídá, jestli hráè zmáèkl klávesu Escape
        if (Input.GetKeyDown(ConfigLoader.stopMenuKey))
        {
            ToggleQuitPopup();
        }
    }

    public void ToggleQuitPopup()
    {
        if (quitPopupPanel != null)
        {
            // Zjistíme, jestli se okno právì zapíná nebo vypíná
            bool willBeActive = !quitPopupPanel.activeSelf;
            quitPopupPanel.SetActive(willBeActive);

            if (willBeActive)
            {
                Time.timeScale = 0f; // Zamrazí hru
            }
            else
            {
                Time.timeScale = 1f; // Rozbìhne hru
            }
        }
    }

    public void CancelQuit()
    {
        if (quitPopupPanel != null)
        {
            quitPopupPanel.SetActive(false);

            Time.timeScale = 1f;
        }
    }

    public void ConfirmQuit()
    {
        Debug.Log("Vypínám hru...");

        // Pro jistotu vrátíme èas do normálu, kdyby se scéna nìkdy znovu naèítala
        Time.timeScale = 1f;

        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}