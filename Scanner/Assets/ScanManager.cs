using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;

public class ScanManager : MonoBehaviour
{

    private SurfaceObserver observer;

    // Update frequency
    private float lastUpdateTime;
    public float updateFrequencyInSeconds = 2f;

    // Use this for initialization
    void Start()
    {
        observer = new SurfaceObserver();
        // define a huge scan area to get every update
        observer.SetVolumeAsAxisAlignedBox(Vector3.zero, new Vector3(10000, 10000, 10000));

        lastUpdateTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update()
    {
        if (lastUpdateTime + updateFrequencyInSeconds < Time.realtimeSinceStartup)
        {
            lastUpdateTime = Time.realtimeSinceStartup;

            observer.Update(onSurfaceChanged);
        }
    }

    void onSurfaceChanged(SurfaceId id, SurfaceChange changeType, Bounds bounds, DateTime updateTime) {
        Debug.Log("Got an update!");
    }
}
