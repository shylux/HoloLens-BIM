using HoloToolkit.Unity.SpatialMapping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class RoomBox : MonoBehaviour {

    public GameObject scanManagerObject;
    private ScanManager scanManager;

    // TODO: Replace with a more sensible requirement. Like shooting rays in every direction.
    public int minSurfacesRequired = 20;
    public int minPlanesRequired = 50;

    private List<BoundedPlane> planes = new List<BoundedPlane>(); // stores all found planes
    private BoundedPlane[] newPlanes; // stores new planes found by the worker

    private List<PlaneFinding.MeshData> meshData = new List<PlaneFinding.MeshData>();
    private bool planeFindingInProgress = false;
    private float lastPlaneFindingFinishTime;
    private bool previousPlaneFindInProgress = false;

    void Start () {
        lastPlaneFindingFinishTime = Time.realtimeSinceStartup;
        scanManager = scanManagerObject.GetComponent<ScanManager>() as ScanManager;
        scanManager.OnMeshUpdate += OnMeshUpdate;
	}
	
	void Update () {
        // triggers when the worker finished
        if (previousPlaneFindInProgress && !planeFindingInProgress) {
            lastPlaneFindingFinishTime = Time.realtimeSinceStartup;
            OnPlanesFound();
        }
        previousPlaneFindInProgress = planeFindingInProgress;

        // start plane finding worker
		if (scanManager.numberOfBakedSurfaces >= minSurfacesRequired &&
            !planeFindingInProgress &&
            Time.realtimeSinceStartup >= lastPlaneFindingFinishTime + 1.0f) { // wait 1 sec to not clog up
                                                                              // Copy / Convert mesh data

            Debug.Log("Copy surfaces.");
            meshData.Clear();
            foreach (SurfaceEntry surface in scanManager.surfaces.Values) {
                MeshFilter mf = surface.gameObject.GetComponent<MeshFilter>();
                if (!mf || !mf.sharedMesh || mf.sharedMesh.triangles.Length == 0)
                    continue; // not sure why those value are null
                meshData.Add(new PlaneFinding.MeshData(mf));
            }

            Debug.Log("Done copy surfaces.");

            planeFindingInProgress = true;
            ThreadPool.QueueUserWorkItem(FindPlanes);

        }
	}

    private void FindPlanes(object state) {
        newPlanes = PlaneFinding.FindSubPlanes(meshData, 0.0f);
        Debug.Log("Added " + newPlanes.Length + " planes.");
        planeFindingInProgress = false;
    }

    void OnMeshUpdate(object sender, EventArgs args) {
        Debug.Log("Got an update");
    }

    void OnPlanesFound() {
        planes.AddRange(newPlanes);
        Debug.Log("Added "+newPlanes.Length+" planes.");

        if (planes.Count >= minPlanesRequired) {
            Debug.Log("Yay");
        }
    }
}
