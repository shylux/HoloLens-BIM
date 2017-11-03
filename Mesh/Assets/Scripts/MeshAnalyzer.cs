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
    private static readonly Vector3 FORWARD = Vector3.forward;
    private const float UPPER_LIMIT = 5f;
    private const int UPPER_ANGLE = 20;

    public HashSet<Vector3> downNormals = new HashSet<Vector3>();
    public HashSet<Vector3> upNormals = new HashSet<Vector3>();
    public HashSet<Vector3> horizontalNormals = new HashSet<Vector3>();

    public float scanTime = 30.0f;

    private bool analyze = false;

    // Update is called once per frame
    private void Update()
    {

        if (analyze || ((Time.time - SpatialMappingManager.Instance.StartTime) < scanTime))
        {
            // Wait until enough time has passed before processing the mesh.
            Debug.Log("No mesh analysis for now.");
        }
        else
        {
            if (SpatialMappingManager.Instance.IsObserverRunning())
            {
                SpatialMappingManager.Instance.StopObserver();
            }

            analyze = true;
            StartCoroutine(AnalyzeRoutine());
            categorizeHorizontals(horizontalNormals);
            
        }
    }

    private IEnumerator AnalyzeRoutine()
    {
        Debug.Log("Start mesh analysis");
        List<MeshFilter> filters = SpatialMappingManager.Instance.GetMeshFilters();
        Dictionary<Vector3, int> histogram = new Dictionary<Vector3, int>(new Vector3CoordComparer());
        int totalNormals = 0;

        for (int index = 0; index < filters.Count; index++)
        {
            MeshFilter filter = filters[index];
            if (filter != null && filter.sharedMesh != null)
            {
                filter.mesh.RecalculateNormals();
                Vector3[] normals = filter.mesh.normals;
                totalNormals += normals.Length;

                foreach (Vector3 normal in normals)
                {
                    categorizeNormal(Vector3.Normalize(normal));
                }
            }
        }

        Debug.Log("Total normals: " + totalNormals);
        Debug.Log("Normals categorized! Up's: " + upNormals.Count + " Down's: " + downNormals.Count + " Horizontal's: " + horizontalNormals.Count);

        yield return null;

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

        categorizeHorizontals(horizontalNormals);
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

    private void categorizeHorizontals(HashSet<Vector3> horizontalNormals)
    {
        Debug.Log("Start horizontals categorization");
        Dictionary<float, Vector3> orientationMap = new Dictionary<float, Vector3>();

        foreach (Vector3 n in horizontalNormals)
        {
            float angle = Vector3.Angle(new Vector3(n.x, 0, n.z), FORWARD);
            if (!orientationMap.ContainsKey(angle))
            {
                orientationMap.Add(angle, n);
            }
        }

        int position = 0;
        HashSet<Vector3> maxSubset = new HashSet<Vector3>();

        int thresholdSpace = 4 * UPPER_ANGLE;
        Debug.Log("thresholdSpace: " + thresholdSpace);

        while (position < 90)
        {
            HashSet<Vector3> subset = new HashSet<Vector3>();
            for (int i = 0; i < thresholdSpace; i++)
            {
                //Vector3 n;
                float index = ((i / UPPER_ANGLE) * 90) + (i % UPPER_ANGLE) + position;
                float upperBound = (index + 0.5f) % 360;
                float lowerBound = (index - 0.5f) % 360;

                Debug.Log("position: " + position + "i: " + i + "index: " + index + " upperBound: " + upperBound + " lowerBound: " + lowerBound);
                IEnumerable<KeyValuePair<float, Vector3>> matches =  orientationMap.Where(kvp => kvp.Key < upperBound && kvp.Key >= lowerBound);

                foreach (KeyValuePair<float, Vector3> m in matches)
                {
                    if (!subset.Contains(m.Value))
                    {
                        subset.Add(m.Value);
                    }
                }
            }

            Debug.Log("Subset with " + subset.Count + " normals found.");
            if (subset.Count > maxSubset.Count)
            {
                Debug.Log("new max");
                maxSubset = subset;
            }
            Debug.Log("position " + position);
            position = position + 1;
        }

        foreach (Vector3 n in maxSubset)
        {
            Debug.DrawLine(new Vector3(0, 0, 0), n, Color.yellow, 300f, false);
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
