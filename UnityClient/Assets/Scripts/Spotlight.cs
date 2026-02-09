using NUnit.Framework.Constraints;
using UnityEngine;

public class Spotlight : MonoBehaviour
{
    public Light spotlight;
    private Color defaultColor;
    public Color targetColor = Color.royalBlue;


    void Start()
    {
        //spotlight = GetComponentInChildren<Light>();
        defaultColor = spotlight.color;

    }
    public void ChangeColorToTarget()
    {
        spotlight.color = targetColor;
    }

    public void ResetColor()
    {
        spotlight.color = defaultColor;
    }

}
