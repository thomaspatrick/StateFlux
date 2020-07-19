using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateFlux.Model;
using TMPro;

public class GreenCircle : MonoBehaviour
{
    public bool isHosting;
    private Vector3 lastPos;
    private StateFluxClient stateFluxClient;

    // Start is called before the first frame update
    void Start()
    {
        stateFluxClient = GameObject.Find("StateFlux").GetComponent<StateFluxClient>();
        if (stateFluxClient == null)
        {
            Debug.LogError("Failed to connect with StateFluxClient");
            return;
        }
        isHosting = stateFluxClient.isHosting;

        StartCoroutine("SendState");
    }

    // Update is called once per frame
    void Update()
    {
    }

    private IEnumerator SendState()
    {
        while(true)
        {
            if (isHosting)
            {
                Vector3 mousePoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
                mousePoint.z = 0;

                if (lastPos != mousePoint)
                {
                    gameObject.transform.position = mousePoint;

                    var stateChange = BuildStateChange(mousePoint);
                    var stateChangeMessage = new StateChangeMessage() { Payload = stateChange };
                    stateFluxClient.SendRequest(stateChangeMessage);
                    lastPos = mousePoint;
                }
            }
            yield return new WaitForSeconds(.1f);
        }
    }

    private StateChange BuildStateChange(Vector3 mousePos)
    {
        var stateChange = new StateChange()
        {
            changes = new List<Change2d>()
        };
        stateChange.changes.Add(new Change2d()
        {
            ObjectID = "GreenCircle",
            Transform = new Transform2d()
            {
                Pos = new StateFlux.Model.Vector2d() { X = mousePos.x, Y = mousePos.y }
            }
        });
        return stateChange;
    }

}
