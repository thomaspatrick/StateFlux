using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using StateFlux;

public class ChangeColorOnOver : MonoBehaviour
{
    public Color Norm;
    public Color OverC;
    public static ChangeColorOnOver RealCol;
    public void Start()
    {
        RealCol = this;
        MyColor = Random.ColorHSV();
        OverC = MyColor;
    }
    public Color MyColor;
    void Update()
    {
        SpriteRenderer me = this.GetComponent<SpriteRenderer>();


        if(StateFluxClient.Instance.connected)
        me.color = Color.Lerp(me.color, OverC, 1f);

    }
    public bool Over;
    public bool Over2;
    private void OnMouseEnter()
    {

        OverC = MyColor;
        Over = true;
    }
    public void OnMouseExit()
    {
        Over = false; 
        OverC = Norm;
    }

    public void RemotePlayerIsOver(Color otherColor)
    {
        OverC = otherColor;
    }
}
