using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshAnalyzer : MonoBehaviour
{
    private static readonly Vector3 DOWN = Vector3.down;
    private static readonly Vector3 UP = Vector3.up;
    private const float UPPER_LIMIT = 5f;

    public HashSet<Vector3> downNormals = new HashSet<Vector3>();
    public HashSet<Vector3> upNormals = new HashSet<Vector3>();
    public HashSet<Vector3> horizontalNormals = new HashSet<Vector3>();

    public float scanTime = 30.0f;

    private bool meshProcessed = false;
    private bool analyze = false;

    // Use this for initialization
    void Start()
    {

    }

    // Update is called once per frame
    private void Update()
    {

        if (meshProcessed || ((Time.time - SpatialMappingManager.Instance.StartTime) < scanTime))
        {
            // Wait until enough time has passed before processing the mesh.
        }
        else
        {
            if (SpatialMappingManager.Instance.IsObserverRunning())
            {
                SpatialMappingManager.Instance.StopObserver();
            }

            StartCoroutine(AnalyzeRoutine());
            meshProcessed = true;
        }
    }

    private IEnumerator AnalyzeRoutine()
    {
        Debug.Log("Start mesh analysis");
        List<MeshFilter> filters = SpatialMappingManager.Instance.GetMeshFilters();
        Dictionary<Vector3, int> histogram = new Dictionary<Vector3, int>(new Vector3CoordComparer());

        for (int index = 0; index < filters.Count; index++)
        {
            MeshFilter filter = filters[index];
            if (filter != null && filter.sharedMesh != null)
            {
                filter.mesh.RecalculateNormals();
                Vector3[] normals = filter.mesh.normals;

                foreach (Vector3 normal in normals)
                {
                    categorizeNormal(Vector3.Normalize(normal));
                }
            }
        }

        Debug.Log("Normals categorized! Up's: " + upNormals.Count + " Down's: " + downNormals.Count + " Horizontal's: " + horizontalNormals.Count);

        foreach (Vector3 n in upNormals)
        {
            Debug.DrawLine(new Vector3(0, 0, 0), n, Color.red, 300f, false);
        }

        foreach (Vector3 n in downNormals)
        {
            Debug.DrawLine(new Vector3(0, 0, 0), n, Color.blue, 300f, false);
        }

        foreach (Vector3 n in horizontalNormals)
        {
            Debug.DrawLine(new Vector3(0, 0, 0), n, Color.green, 300f, false);
        }

        yield return null;
    }

    private void categorizeNormal(Vector3 normal)
    {
        if (Vector3.Angle(UP, normal) <= UPPER_LIMIT)
        {
            upNormals.Add(normal);
        }
        else if (Vector3.Angle(DOWN, normal) <= UPPER_LIMIT)
        {
            downNormals.Add(normal);
        }
        else if (Vector3.Angle(new Vector3(normal.x, 0, normal.z), normal) <= UPPER_LIMIT)
        {
            horizontalNormals.Add(normal);
        }
    }

    class Vector3CoordComparer : IEqualityComparer<Vector3>
    {
        public bool Equals(Vector3 a, Vector3 b)
        {
            if (Mathf.Abs(a.x - b.x) > 0.1) return false;
            if (Mathf.Abs(a.y - b.y) > 0.1) return false;
            if (Mathf.Abs(a.z - b.z) > 0.1) return false;

            return true; //indeed, very close
        }

        public int GetHashCode(Vector3 obj)
        {
            //a cruder than default comparison, allows to compare very close-vector3's into same hash-code.
            return Math.Round(obj.x, 1).GetHashCode()
                 ^ Math.Round(obj.y, 1).GetHashCode() << 2
                 ^ Math.Round(obj.z, 1).GetHashCode() >> 2;
        }
    }
}
