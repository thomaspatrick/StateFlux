using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ModalBackdrop : MonoBehaviour
{
    void OnMouseOver()
    {
        if(Input.GetMouseButtonDown(0))
        {
            Debug.Log("Clicked modal backdrop");
            GameObject lobbyManager = GameObject.Find("LobbyManager");
            lobbyManager.SendMessage("OnClickedModalBackdrop");
        }
    }
}
