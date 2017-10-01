using System;
using System.Collections;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR.WSA;

public enum BakedState {
    NeverBaked = 0,
    Baked = 1,
    PendingUpdatePostBake = 2
}

class SurfaceEntry {
    public int id; // ID used by the HoloLens
    public GameObject gameObject; // holds mesh, anchor and renderer
    public float lastUpdateTime;
    public BakedState bakedState;

    public SurfaceEntry(int surfaceId, bool rendering = false, Material mat = null) {
        this.id = surfaceId;
        this.bakedState = BakedState.NeverBaked;
        this.lastUpdateTime = Time.realtimeSinceStartup;
        this.gameObject = new GameObject(String.Format("Surface-{0}", surfaceId));
        this.gameObject.AddComponent<MeshFilter>();
        this.gameObject.AddComponent<WorldAnchor>();

        if (rendering) {
            MeshRenderer meshRenderer = this.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = new Material(mat);
            meshRenderer.sharedMaterial.SetColor("_WireColor", Color.red);
        }
    }
}

public class ScanManager : MonoBehaviour {

    private SurfaceObserver observer;
    private Dictionary<int, SurfaceEntry> surfaces = new Dictionary<int, SurfaceEntry>();

    // Update frequency
    private float lastUpdateTime;
    public float updateFrequencyInSeconds = 2f;

    // Baking queue
    //TODO: Maybe implement as priority queue. Depends on whether we have enough data to queue.
    Queue<SurfaceEntry> bakingQueue = new Queue<SurfaceEntry>();
    bool isBaking = false;

    // Rendering
    [Header("Rendering")]
    public bool renderMeshes = false;
    private Material meshMaterial;
    public float cooldownPeriod = 1f;
    public Color startColor = Color.red;
    public Color endColor = Color.white;

    // Use this for initialization
    void Start() {
        meshMaterial = Resources.Load("Wireframe") as Material;

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
            SurfaceEntry surfaceEntry = bakingQueue.Dequeue();
            SurfaceData request = new SurfaceData();
            request.id.handle = surfaceEntry.id;
            request.outputMesh = surfaceEntry.gameObject.GetComponent<MeshFilter>();
            request.outputAnchor = surfaceEntry.gameObject.GetComponent<WorldAnchor>();
            request.trianglesPerCubicMeter = 300.0f;

            try {
                if (observer.RequestMeshAsync(request, onSurfaceDataReady)) {
                    isBaking = true;
                }
                else {
                    Debug.Log(System.String.Format("Bake request for {0} failed.  Is {0} a valid Surface ID?", surfaceEntry.id));
                }
            } catch {
                Debug.Log(System.String.Format("Bake for id {0} failed unexpectedly!", surfaceEntry.id));
            }

            
        }

        // Update mesh colors
        foreach (SurfaceEntry surfaceEntry in surfaces.Values) {
            MeshRenderer meshRenderer = surfaceEntry.gameObject.GetComponent<MeshRenderer>();
            Color currentColor = Color.Lerp(startColor, endColor, (Time.realtimeSinceStartup - surfaceEntry.lastUpdateTime) / cooldownPeriod);
            meshRenderer.sharedMaterial.SetColor("_WireColor", currentColor);
        }
    }

    void onSurfaceChanged(SurfaceId id, SurfaceChange changeType, Bounds bounds, DateTime updateTime) {
        if (changeType == SurfaceChange.Added) {
            SurfaceEntry newEntry = new SurfaceEntry(id.handle, renderMeshes, meshMaterial);
            newEntry.gameObject.transform.parent = gameObject.transform;

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

    void onSurfaceDataReady(SurfaceData surfaceData, bool outputWritten, float elapsedBakeTimeSeconds) {
        isBaking = false;
        SurfaceEntry surfaceEntry;
        if (surfaces.TryGetValue(surfaceData.id.handle, out surfaceEntry)) {
            surfaceEntry.bakedState = BakedState.Baked;
            surfaceEntry.lastUpdateTime = Time.realtimeSinceStartup;
        }
    }
}
