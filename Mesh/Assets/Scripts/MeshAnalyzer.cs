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

    public bool drawUpNormals = false;
    public Color colorUpNormals = Color.red;
    public bool drawDownNormals = false;
    public Color colorDownNormals = Color.blue;
    public bool drawHorizontalNormals = false;
    public Color colorHorizontalNormals = Color.green;

    private float drawDuration = 300f;

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

        for (int index = 0; index < filters.Count; index++)
        {
            MeshFilter filter = filters[index];
            if (filter != null && filter.sharedMesh != null)
            {
                Mesh mesh = filter.mesh;
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();

                int[] triangles = mesh.triangles;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;

                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 v0 = filter.transform.TransformPoint(vertices[triangles[i]]);
                    Vector3 v1 = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 v2 = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    Vector3 center = (v0 + v1 + v2) / 3;
                    Vector3 dir = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));
                    dir /= 10;

                }

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vertice = filter.transform.TransformPoint(vertices[i]);
                    Vector3 normal = filter.transform.TransformDirection(normals[i].normalized);
                    normal /= 10;

                    categorizeNormal(normal, vertice);
                    
                }
            }
            yield return null;
        }

        Debug.Log("Normals categorized! Up's: " + upNormals.Count + " Down's: " + downNormals.Count + " Horizontal's: " + horizontalNormals.Count);

        yield return null;

        categorizeHorizontals(horizontalNormals);
    }

    private void categorizeNormal(Vector3 normal, Vector3 origin)
    {
        if (Vector3.Angle(UP, normal) <= UPPER_LIMIT)
        {
            upNormals.Add(normal);
            if (drawUpNormals)
                Debug.DrawRay(origin, normal.normalized, colorUpNormals, drawDuration);
        }
        else if (Vector3.Angle(DOWN, normal) <= UPPER_LIMIT)
        {
            downNormals.Add(normal);
            if (drawDownNormals)
                Debug.DrawRay(origin, normal.normalized, colorDownNormals, drawDuration);
        }
        else if (Vector3.Angle(new Vector3(normal.x, 0, normal.z), normal) <= UPPER_LIMIT)
        {
            horizontalNormals.Add(normal);
            if (drawHorizontalNormals)
                Debug.DrawRay(origin, normal.normalized, colorHorizontalNormals, drawDuration);
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
                float index = ((i / UPPER_ANGLE) * 90) + (i % UPPER_ANGLE) + position;
                float upperBound = (index + 0.5f) % 360;
                float lowerBound = (index - 0.5f) % 360;

                IEnumerable<KeyValuePair<float, Vector3>> matches = orientationMap.Where(kvp => kvp.Key < upperBound && kvp.Key >= lowerBound);

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
                Debug.Log("New max Subset with " + subset.Count + " normals found.");
                maxSubset = subset;
            }

            position++;
        }

        foreach (Vector3 n in maxSubset)
        {
            Debug.DrawRay(transform.TransformPoint(new Vector3(0, 0, 0)), n, Color.yellow, 300f, false);
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
