using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class ErrorPanelBehavior : MonoBehaviour
{
    public float showErrorDuration; //sec
    private float timeLeft;

    void Start()
    {
        StartCoroutine(AutomaticDismiss());    
    }
    
    public void OnStateFluxError(string error)
    {
        var text = GetComponentInChildren<Text>();
        text.text = error;
        timeLeft += showErrorDuration;
        var canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup.alpha == 0)
        {
            canvasGroup.alpha = 1;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
    }

    public void OnClickDismiss()
    {
        timeLeft = 0;
    }

    public IEnumerator AutomaticDismiss()
    {
        while(true)
        {
            yield return new WaitForSeconds(1);
            timeLeft -= 1f;
            if(timeLeft<=0)
            {
                var canvasGroup = GetComponent<CanvasGroup>();
                canvasGroup.alpha = 0;
                canvasGroup.blocksRaycasts = false;
                canvasGroup.interactable = false;
            }
        }
    }
}
