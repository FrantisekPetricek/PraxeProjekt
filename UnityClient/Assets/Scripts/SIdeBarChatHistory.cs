using JetBrains.Annotations;
using NUnit.Framework.Constraints;
using NUnit.Framework.Internal;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class SIdeBarChatHistory : MonoBehaviour
{

    public RectTransform panelRect;
    public float animationDuration = 0.5f;

    public Vector2 hiddenPosition;
    public Vector2 visibleposition;

    private bool isOpen = false;
    private Coroutine currentAnimation;

    void Start()
    {
        if(panelRect != null) 
            panelRect = GetComponent<RectTransform>();

        panelRect.anchoredPosition = hiddenPosition;
        isOpen = false;
    }

    public void TogglePanel()
    {
        if (isOpen)
        {
            ClosePanel();
        }
        else
        {
            OpenPanel();
        }
    }
    public void OpenPanel()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(AnimateTo(visibleposition));
        isOpen = true;
    }

    public void ClosePanel()
    {
        if (currentAnimation != null)
            StopCoroutine(currentAnimation);
        currentAnimation = StartCoroutine(AnimateTo(hiddenPosition));
        isOpen = false;
    }

    IEnumerator AnimateTo(Vector2 targetPosition)
    {
        Vector2 startPostion = panelRect.anchoredPosition;
        float timeElapse = 0;

        while (timeElapse < animationDuration)
        {
            panelRect.anchoredPosition = Vector2.Lerp(startPostion, targetPosition, timeElapse / animationDuration);
            timeElapse += Time.deltaTime;
            yield return null;
        }

        panelRect.anchoredPosition = targetPosition;
    }
}
