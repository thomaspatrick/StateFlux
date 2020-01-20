using UnityEngine;
using StateFlux.Client;
using StateFlux.Model;
using System.Collections;
using System.Collections.Generic;
using System;

public class StateFluxClient : MonoBehaviour
{
    private Client client;
    private string myClientId;
    private Color myColor;

    public GameObject prefab;
    public string endpoint;

    void Start()
    {
        myClientId = Guid.NewGuid().ToString();
        myColor = UnityEngine.Random.ColorHSV();

        client = new Client();
        client.Endpoint = endpoint; // "ws://localhost:8888/Service";
        client.RequestedUsername = "bichon";
        client.SessionSaveFilename = "currentplayer.json";
        client.Start();
        StartCoroutine("SendState");
        StartCoroutine("ReceiveMessages");
    }

    private void OnApplicationQuit()
    {
        if (client != null) client.Stop();
    }

    IEnumerator SayHellos()
    {
        while(!client.SocketOpenWithIdentity)
        {
            Debug.Log("Waiting for a connection");
            yield return new WaitForSeconds(1);
        }

        for(int i = 0; i<10000; i++)
        {
            ChatSayMessage msg = new ChatSayMessage();
            msg.say = "Mouse is at position: " + Input.mousePosition.ToString();
            client.SendRequest(msg);
            yield return new WaitForSeconds(2);
        }
    }

    IEnumerator SendState()
    {
        while (!client.SocketOpenWithIdentity)
        {
            Debug.Log("Waiting for a connection");
            yield return new WaitForSeconds(1);
        }

        int cnt = 0;
        float tick = Time.fixedTime;
        while (client.SocketOpenWithIdentity)
        {
            var msg = new StateChangeMessage();
            var stateChange = msg.Payload = new StateChange();
            stateChange.changes = new List<Change2d>();

            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            var change = new Change2d
            {
                ObjectID = myClientId + ".mouse",
                Event = ChangeEvent.Updated,
                Transform = new Transform2d
                {
                    Pos = new Vector2d
                    {
                        X = mousePos.x,
                        Y = mousePos.y
                    }
                },
                Measures = new Measures
                {
                    Ammo = myColor.r,
                    Fuel= myColor.g,
                    Health= myColor.b
                }
            };

            stateChange.changes.Add(change);
            client.SendRequest(msg);

            if (cnt > 100)
            {
                float tock = Time.fixedTime;
                float rate = cnt / (tock - tick);
                tick = tock;
                cnt = 0;
                Debug.Log("Sending " + rate + " state messages per second to server.");
            }
            cnt++;

            yield return new WaitForSeconds(0.1f);
        }
    }


    IEnumerator ReceiveMessages()
    {
        while (!client.SocketOpenWithIdentity)
        {
            Debug.Log("Waiting for a connection");
            yield return new WaitForSeconds(1);
        }

        int cnt = 0;
        float tick = Time.fixedTime;
        while(true)
        {
            Message message = client.ReceiveResponse();
            if(message != null)
            {
                if (message.MessageType == MessageTypeNames.StateChanged)
                {
                    StateChangedMessage msg = (StateChangedMessage)message;
                    foreach(var change in msg.Payload.changes)
                    {
                        GameObject hand = GameObject.Find(change.ObjectID);
                        if(hand == null)
                        {
                            hand = Instantiate(prefab, new Vector3(0, 0, 0), Quaternion.identity);
                            SpriteRenderer sr = hand.GetComponent<SpriteRenderer>();
                            sr.color = UnityEngine.Random.ColorHSV();
                            hand.name = change.ObjectID;
                        }
                        hand.GetComponent<SpriteRenderer>().color = new Color((float)change.Measures.Ammo, (float)change.Measures.Fuel, (float)change.Measures.Health);
                        hand.transform.position = new Vector3((float)change.Transform.Pos.X, (float)change.Transform.Pos.Y, 5);
                    }
                    if (cnt > 100)
                    {
                        float tock = Time.fixedTime;
                        float rate = cnt / (tock - tick);
                        tick = tock;
                        cnt = 0;
                        Debug.Log("Receiving " + rate + " state messages per second from server.");
                    }
                    cnt++;
                }
            }
            yield return null;
        }
    }
}
