using UnityEngine;

public class UI : MonoBehaviour
{
    public GameObject objectToHide;
    public ChatHistoryLoader chatHistoryLoader;

    // Update is called once per frame

    public void ChangeVisibility()
    {
        if (objectToHide != null)
        {

            bool isActive = objectToHide.activeSelf;
            objectToHide.SetActive(!isActive);
        }
    }
}
