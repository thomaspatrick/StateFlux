using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConPanel : MonoBehaviour
{
    public void OnClickDismiss()
    {
        var canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
    }
}
