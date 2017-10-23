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
    private bool previousPlaneFindInProgress = false;

    void Start () {
        scanManager = scanManagerObject.GetComponent<ScanManager>() as ScanManager;
        scanManager.OnMeshUpdate += OnMeshUpdate;
	}
	
	void Update () {
        // triggers when the worker finished
        if (previousPlaneFindInProgress && !planeFindingInProgress) {
            OnPlanesFound();
        }
        previousPlaneFindInProgress = planeFindingInProgress;

        // start plane finding worker
		if (!planeFindingInProgress && scanManager.numberOfBakedSurfaces >= minSurfacesRequired) {

            // Copy / Convert mesh data
            meshData.Clear();
            foreach (SurfaceEntry surface in scanManager.surfaces.Values) {
                meshData.Add(new PlaneFinding.MeshData(surface.gameObject.GetComponent<MeshFilter>()));
            }

            planeFindingInProgress = true;
            ThreadPool.QueueUserWorkItem(FindPlanes);

        }
	}

    private void FindPlanes(object state) {
        newPlanes = PlaneFinding.FindSubPlanes(meshData, 0.0f);
        planeFindingInProgress = false;
    }

    void OnMeshUpdate(object sender, EventArgs args) {
        Debug.Log("Got an update");
    }

    void OnPlanesFound() {
        planes.AddRange(newPlanes);

        if (planes.Count >= minPlanesRequired) {
            Debug.Log("Yay");
        }
    }
}
