using HoloToolkit.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.VR.WSA;

public enum BakedState {
    NeverBaked = 0,
    Baked = 1,
    PendingUpdatePostBake = 2
}

public class SurfaceEntry {
    public int id; // ID used by the HoloLens
    public GameObject gameObject; // holds mesh, anchor and renderer
    public Bounds aabb; // bounding box
    public float lastUpdateTime;
    public float lastLookedAtTime;
    public BakedState bakedState;

    public SurfaceEntry(int surfaceId, Bounds bounds, bool rendering = false, Material mat = null) {
        this.id = surfaceId;
        this.aabb = bounds;
        this.bakedState = BakedState.NeverBaked;
        this.lastUpdateTime = Time.realtimeSinceStartup;
        this.lastLookedAtTime = Time.realtimeSinceStartup;
        this.gameObject = new GameObject(String.Format("Surface-{0}", surfaceId));
        this.gameObject.AddComponent<MeshFilter>();
        this.gameObject.AddComponent<WorldAnchor>();
        this.gameObject.AddComponent<MeshCollider>();
        this.gameObject.layer = 8;

        if (rendering) {
            MeshRenderer meshRenderer = this.gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.sharedMaterial = new Material(mat);
            meshRenderer.sharedMaterial.SetColor("_WireColor", Color.red);
        }
    }

    public class SurfaceEntryComparer : IComparer<SurfaceEntry> {
        public int Compare(SurfaceEntry x, SurfaceEntry y) {
            if (x.bakedState != y.bakedState) {
                return (x.bakedState < y.bakedState) ? -1 : 1;
            }
            return (x.lastLookedAtTime > y.lastLookedAtTime) ? -1 : 1;
        }
    }
}

public class ScanManager : Singleton<ScanManager> {

    private SurfaceObserver observer;
    public Dictionary<int, SurfaceEntry> surfaces = new Dictionary<int, SurfaceEntry>();

    // Update frequency
    private float lastUpdateTime;
    public float updateFrequencyInSeconds = 2f;

    // Baking queue
    //TODO: Maybe implement as priority queue. Depends on whether we have enough data to queue.
    LazyPriorityQueue<SurfaceEntry> bakingQueue = new LazyPriorityQueue<SurfaceEntry>(new SurfaceEntry.SurfaceEntryComparer());
    bool isBaking = false;
    public int numberOfBakedSurfaces = 0;
    public event EventHandler OnMeshUpdate;

    // Rendering
    [Header("Rendering")]
    public bool renderMeshes = false;
    public Material meshMaterial;
    public float cooldownPeriod = 1f;
    public Color startColor = Color.red;
    public Color endColor = Color.white;

    // Use this for initialization
    void Start() {
        observer = new SurfaceObserver();
        // define a huge scan area to get every update
        observer.SetVolumeAsAxisAlignedBox(Vector3.zero, new Vector3(10000, 10000, 10000));

        lastUpdateTime = Time.realtimeSinceStartup;
    }

    // Update is called once per frame
    void Update() {
        // request update from observer
        if (lastUpdateTime + updateFrequencyInSeconds < Time.realtimeSinceStartup) {
            lastUpdateTime = Time.realtimeSinceStartup;

            observer.Update(onSurfaceChanged);
        }

        // update priorities to bake the mesh the user is looking at
        Ray gazeRay = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        foreach (SurfaceEntry entry in bakingQueue) {
            if (entry.aabb.IntersectRay(gazeRay)) {
                entry.lastLookedAtTime = Time.realtimeSinceStartup;
            }
        }

        // bake the mesh
        if (!isBaking && bakingQueue.Count > 0) {
            SurfaceEntry surfaceEntry = bakingQueue.Pop();
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
            SurfaceEntry newEntry = new SurfaceEntry(id.handle, bounds, renderMeshes, meshMaterial);
            newEntry.gameObject.transform.parent = gameObject.transform;

            surfaces.Add(id.handle, newEntry);
        }

        if (changeType == SurfaceChange.Added ||
            changeType == SurfaceChange.Updated) {
            // queue for baking
            SurfaceEntry surface;
            if (surfaces.TryGetValue(id.handle, out surface)) {
                bakingQueue.Add(surface);
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
            if (surfaceEntry.bakedState == BakedState.NeverBaked)
                numberOfBakedSurfaces++;
            surfaceEntry.bakedState = BakedState.Baked;
            surfaceEntry.lastUpdateTime = Time.realtimeSinceStartup;

            if (renderMeshes) {
                MeshCollider mc = surfaceEntry.gameObject.GetComponent<MeshCollider>();
                mc.sharedMesh = null;
                mc.sharedMesh = surfaceEntry.gameObject.GetComponent<MeshFilter>().sharedMesh;
            }

            if (OnMeshUpdate != null)
                OnMeshUpdate(this, new EventArgs());
        }
    }

    public List<MeshFilter> GetMeshFilters() {
        List<MeshFilter> renderers = new List<MeshFilter>();

        foreach (SurfaceEntry surfaceEntry in surfaces.Values) {
            renderers.Add(surfaceEntry.gameObject.GetComponent<MeshFilter>());
        }

        return renderers;
    }

    public void hideSurfaceMesh() {
        foreach (SurfaceEntry surfaceEntry in surfaces.Values) {
            surfaceEntry.gameObject.GetComponent<MeshRenderer>().enabled = false;
        }
    }
}