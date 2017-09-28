using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VR.WSA;

public enum BakedState {
    NeverBaked = 0,
    Baked = 1,
    PendingUpdatePostBake = 2
}

class SurfaceEntry {
    public int id; // ID used by the HoloLens
    public float lastUpdateTime;
    public BakedState bakedState;
}

public class ScanManager : MonoBehaviour {

    private SurfaceObserver observer;
    private Dictionary<int, SurfaceEntry> surfaces;

    // Update frequency
    private float lastUpdateTime;
    public float updateFrequencyInSeconds = 2f;

    // Baking queue
    //TODO: Maybe implement as priority queue. Depends on whether we have enough data to queue.
    Queue<SurfaceEntry> bakingQueue = new Queue<SurfaceEntry>();
    bool isBaking = false;

    // Use this for initialization
    void Start() {
        observer = new SurfaceObserver();
        // define a huge scan area to get every update
        observer.SetVolumeAsAxisAlignedBox(Vector3.zero, new Vector3(10000, 10000, 10000));

        lastUpdateTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update() {
        if (lastUpdateTime + updateFrequencyInSeconds < Time.realtimeSinceStartup) {
            lastUpdateTime = Time.realtimeSinceStartup;

            observer.Update(onSurfaceChanged);
        }

        if (!isBaking && bakingQueue.Count > 0) {
            observer.RequestMeshAsync() <-- WIP
        }
    }

    void onSurfaceChanged(SurfaceId id, SurfaceChange changeType, Bounds bounds, DateTime updateTime) {
        if (changeType == SurfaceChange.Added) {
            SurfaceEntry newEntry = new SurfaceEntry();
            newEntry.id = id.handle;
            newEntry.bakedState = BakedState.NeverBaked;
            newEntry.lastUpdateTime = Time.realtimeSinceStartup;

            surfaces.Add(id.handle, newEntry);
        }

        if (changeType == SurfaceChange.Added ||
            changeType == SurfaceChange.Updated) {
            // queue for baking
            SurfaceEntry surface;
            if (surfaces.TryGetValue(id.handle, out surface)) {
                bakingQueue.Enqueue(surface);
            }
        }

        if (changeType == SurfaceChange.Removed) {
            surfaces.Remove(id.handle);
        }
    }

    void onSurfaceDataReady(SurfaceData sd, bool outputWritten, float elapsedBakeTimeSeconds) {
        Debug.Log("Data are here!");
    }
}
