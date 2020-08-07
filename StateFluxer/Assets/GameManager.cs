using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateFlux.Model;
using TMPro;
using StateFlux.Client;
using System.Linq;
using Newtonsoft.Json;

public class GameManager : MonoBehaviour, IStateFluxListener
{
    private Vector3 lastPos;
    private GameObject mousePointer;
    private StateFluxClient stateFluxClient;
    public GameObject mousePointerPrefab;
    public StateFlux.Model.Color myColor;

private string id;

    private Dictionary<string, GameObject> playersMap;

    // Start is called before the first frame update
    void Start()
    {
        playersMap = new Dictionary<string, GameObject>();

        stateFluxClient = GameObject.Find("StateFlux").GetComponent<StateFluxClient>();
        if (stateFluxClient == null)
        {
            Debug.LogError("Failed to connect with StateFluxClient");
            return;
        }
        stateFluxClient.AddListener(this);

        id = stateFluxClient.userName;

        myColor = new StateFlux.Model.Color { Red = Random.value, Green = Random.value, Blue = Random.Range(0, 1), Alpha = 1f };

        mousePointer = GameObject.Instantiate(mousePointerPrefab, transform);
        mousePointer.transform.position = new Vector3(0, 0, 0);

        StartCoroutine("SendState");
    }

    private IEnumerator SendState()
    {
        while(true)
        {
            Vector3 mousePoint = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            mousePoint.z = 0;

            if (lastPos != mousePoint)
            {
                mousePointer.transform.position = mousePoint;

                if (stateFluxClient.isHosting)
                {
                    var stateChange = BuildStateChange(id, mousePoint);
                    var stateChangeMessage = new HostStateChangeMessage() { Payload = stateChange };
                    stateFluxClient.SendRequest(stateChangeMessage);
                }
                else
                {
                    var stateChange = BuildStateChange(id, mousePoint);
                    var stateChangeMessage = new GuestStateChangeMessage() { Payload = stateChange };
                    stateFluxClient.SendRequest(stateChangeMessage);
                }
                lastPos = mousePoint;
            }
            yield return new WaitForSeconds(.05f);
        }
    }

    private StateChange BuildStateChange(string id, Vector3 mousePos)
    {
        var stateChange = new StateChange()
        {
            Changes = new List<Change2d>()
        };
        stateChange.Changes.Add(new Change2d()
        {
            ObjectID = id,
            Transform = new Transform2d()
            {
                Pos = new StateFlux.Model.Vec2d() { X = mousePos.x, Y = mousePos.y }
            },
            Attributes = new Attributes
            {
                Color = myColor
            }
        });
        return stateChange;
    }

    public void OnStateFluxHostStateChanged(HostStateChangedMessage message)
    {
        foreach(var change in message.Payload.Changes)
        {
            if (!playersMap.TryGetValue(change.ObjectID, out GameObject gameObject))
            {
                // we haven't created a gameobject for this ObjectID - so make a new one and add it
                gameObject = Instantiate(mousePointerPrefab);
                if(change.Attributes.Color != null)
                {
                    var spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        var color = change.Attributes.Color;
                        spriteRenderer.color = new UnityEngine.Color(color.Red, color.Green, color.Green, color.Alpha);
                    }
                }
                playersMap.Add(change.ObjectID, gameObject);
            }

            gameObject.transform.position = new Vector3(change.Transform.Pos.X, change.Transform.Pos.Y, 0);
        }
    }

    // we get this message only when hosting a game
    public void OnStateFluxGuestStateChanged(GuestStateChangedMessage message)
    {
        foreach (var change in message.Payload.Changes)
        {
            if (!playersMap.TryGetValue(change.ObjectID, out GameObject gameObject))
            {
                // we haven't created a gameobject for this ObjectID - so make a new one and add it
                gameObject = Instantiate(mousePointerPrefab);
                if (change.Attributes.Color != null)
                {
                    var spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
                    if (spriteRenderer != null)
                    {
                        spriteRenderer.color = new UnityEngine.Color(change.Attributes.Color.Red, change.Attributes.Color.Green, change.Attributes.Color.Green, change.Attributes.Color.Alpha);
                    }
                }
                playersMap.Add(change.ObjectID, gameObject);
            }

            gameObject.transform.position = new Vector3(change.Transform.Pos.X, change.Transform.Pos.Y, 0);
        }
    }

    public void OnStateFluxInitialize()
    {
    }

    public void OnStateFluxWaitingToConnect()
    {
    }

    public void OnStateFluxConnect()
    {
    }

    public void OnStateFluxDisconnect()
    {
    }

    public void OnStateFluxServerError(ServerErrorMessage message)
    {
    }

    public void OnStateFluxPlayerListing(PlayerListingMessage message)
    {
    }

    public void OnStateFluxGameInstanceCreated(GameInstanceCreatedMessage message)
    {
    }

    public void OnStateFluxGameInstanceJoined(GameInstanceJoinedMessage message)
    {
    }
    public void OnStateFluxGameInstanceLeft(GameInstanceLeftMessage message)
    {
        throw new System.NotImplementedException();
    }

    public void OnStateFluxGameInstanceListing(GameInstanceListingMessage message)
    {
    }

    public void OnStateFluxGameInstanceStart(GameInstanceStartMessage message)
    {
    }

    public void OnStateFluxChatSaid(ChatSaidMessage message)
    {
    }

    public void OnStateFluxOtherMessage(Message message)
    {
    }

}
