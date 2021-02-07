using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffsetParent : MonoBehaviour
{
    private float time;
    private void FixedUpdate()
    {
        time++;
        if (time > 360)
            time = 0;
        transform.position = new Vector3(2.0f*Mathf.Sin(time/100f),2.0f*Mathf.Cos(time/100f));
    }
}
