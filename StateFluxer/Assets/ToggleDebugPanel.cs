using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ToggleDebugPanel : MonoBehaviour
{
    public void OnDebugButtonClick()
    {
        GameObject debugPanel = GameObject.Find("DebugPanel");
        var canvasGroup = debugPanel.GetComponent<CanvasGroup>();
        if(canvasGroup.alpha == 1)
            canvasGroup.alpha = 0;
        else
        {
            canvasGroup.alpha = 1;
        }

    }
}
