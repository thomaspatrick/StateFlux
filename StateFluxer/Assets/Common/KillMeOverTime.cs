using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KillMeOverTime : MonoBehaviour
{
    public float lifeTime;
    private float startTime;

    private void Start()
    {
        startTime = Time.time;
    }
    void Update()
    {
        if(Time.time > (startTime + lifeTime))
            GameObject.Destroy(gameObject);
    }
}
