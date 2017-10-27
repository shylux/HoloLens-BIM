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
    float maxFloor = 0, maxCeiling = 0;
    BoundedPlane maxFloorPlane, maxCeilingPlane;

    void Start() {
        lastPlaneFindingFinishTime = Time.realtimeSinceStartup;
        scanManager = scanManagerObject.GetComponent<ScanManager>() as ScanManager;
        scanManager.OnMeshUpdate += OnMeshUpdate;
    }

    void Update() {
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

            if (maxFloor != 0)
                return;

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
#if !UNITY_EDITOR && UNITY_WSA
            await System.Threading.Tasks.Task.Run(() => HoloFindPlanes());
#else
            ThreadPool.QueueUserWorkItem(FindPlanes);
#endif

        }
    }

#if !UNITY_EDITOR && UNITY_WSA
    private async System.Threading.Tasks.Task HoloFindPlanes() {
#else
    private void FindPlanes(object state) {
#endif
        newPlanes = PlaneFinding.FindSubPlanes(meshData, 0.0f);
        Debug.Log("Added " + newPlanes.Length + " planes.");
        planeFindingInProgress = false;
    }


    void OnMeshUpdate(object sender, EventArgs args) {
        Debug.Log("Got an update");
    }

    void OnPlanesFound() {
        if (maxFloor != 0)
            return; // rewrite to state
        planes.AddRange(newPlanes);
        Debug.Log("Added " + newPlanes.Length + " planes.");

        if (planes.Count >= minPlanesRequired) {
            Debug.Log("Yay");
            CalculateRoomBox();
        }
    }

    void CalculateRoomBox() {
        if (maxFloor != 0)
            return; // only calculate once

        List<BoundedPlane> floors = new List<BoundedPlane>();
        List<BoundedPlane> ceilings = new List<BoundedPlane>();
        List<BoundedPlane> walllike = new List<BoundedPlane>();

        foreach (BoundedPlane plane in planes) {
            if (Vector3.Angle(Vector3.up, plane.Plane.normal) <= 5) {
                floors.Add(plane);
                if (plane.Bounds.Center.y < maxFloor) {
                    maxFloor = plane.Bounds.Center.y;
                    maxFloorPlane = plane;
                }
            }
            else if (Vector3.Angle(Vector3.down, plane.Plane.normal) <= 5) {
                ceilings.Add(plane);
                if (plane.Bounds.Center.y > maxCeiling) {
                    maxCeiling = plane.Bounds.Center.y;
                    maxCeilingPlane = plane;
                }
            }
            else
                walllike.Add(plane);
        }

        Transform floor = transform.Find("Floor");
        floor.gameObject.SetActive(true);
        floor.position = new Vector3(floor.position.x, maxFloorPlane.Bounds.Center.y, floor.position.z);

        Transform ceiling = transform.Find("Ceiling");
        ceiling.gameObject.SetActive(true);
        ceiling.position = new Vector3(ceiling.position.x, maxCeilingPlane.Bounds.Center.y, ceiling.position.z);

        Debug.Log("Calculated!");
    }

    void OnDrawGizmos() {
        if (maxFloor == 0) return;

        // floor
        Gizmos.color = Color.red;
        //Gizmos.DrawCube(maxFloorPlane.Bounds.Center, new Vector3(5, 0.01f, 5));
        // ceiling
        Gizmos.color = Color.yellow;
        //Gizmos.DrawCube(maxCeilingPlane.Bounds.Center, new Vector3(5, 0.01f, 5));
    }
}
