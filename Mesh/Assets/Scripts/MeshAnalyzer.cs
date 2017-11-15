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

    private HashSet<Line> downNormals = new HashSet<Line>();
    private HashSet<Line> upNormals = new HashSet<Line>();
    private HashSet<Line> horizontalNormals = new HashSet<Line>();

    public float scanTime = 30.0f;
    public int numberOfPlanesToFind = 4;

    [Range(0.0f, 180.0f)]
    public float maxOrientationDifference = 2f;

    public float maxDistanceToPlane = .05f;

    [Range(0.0f, 1.0f)]
    public float normalsScale = 1.0f;

    public bool drawUpNormals = false;
    public Color colorUpNormals = Color.red;
    public bool drawDownNormals = false;
    public Color colorDownNormals = Color.blue;
    public bool drawHorizontalNormals = false;
    public Color colorHorizontalNormals = Color.green;
    public bool drawWallNormals = false;
    public Color colorWallNormals = Color.yellow;

    private float drawDuration = 300f;

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
                Mesh mesh = filter.sharedMesh;
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

                    //categorizeNormal(dir, center);
                }

                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 vertice = filter.transform.TransformPoint(vertices[i]);
                    Vector3 normal = filter.transform.TransformDirection(normals[i].normalized);

                    CategorizeNormal(normal, vertice);
                }
            }
            yield return null;
        }

        Debug.Log("Normals categorized! Up's: " + upNormals.Count + " Down's: " + downNormals.Count + " Horizontal's: " + horizontalNormals.Count);

        yield return null;

        FindPlanesFromNormals(horizontalNormals, numberOfPlanesToFind);

        yield return null;
    }

    private void CategorizeNormal(Vector3 normal, Vector3 origin)
    {
        if (IsOrientationEqual(UP, normal))
        {
            upNormals.Add(new Line(origin, normal));
            if (drawUpNormals)
                Debug.DrawRay(origin, normal.normalized * normalsScale, colorUpNormals, drawDuration);
        }
        else if (IsOrientationEqual(DOWN, normal))
        {
            downNormals.Add(new Line(origin, normal));
            if (drawDownNormals)
                Debug.DrawRay(origin, normal.normalized * normalsScale, colorDownNormals, drawDuration);
        }
        else if (IsOrientationEqual(new Vector3(normal.x, 0, normal.z), normal))
        {
            horizontalNormals.Add(new Line(origin, normal));
            if (drawHorizontalNormals)
                Debug.DrawRay(origin, normal.normalized * normalsScale, colorHorizontalNormals, drawDuration);
        }
    }

    private void FindPlanesFromNormals(IEnumerable<Line> normals, int numberOfPlanes)
    {
        List<Line> availableLines = new List<Line>(normals);
        GameObject container = new GameObject("FoundPlanes");

        for (int i = 0; i < numberOfPlanes; i++)
        {
            List<Line> lines = FindMostPopulatedPlane(availableLines);
            Line planeLine = lines[0];

            // Create a cube as representation of the found plane.
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Plane #" + i + " (" + lines.Count.ToString() + " normals)";
            cube.transform.up = planeLine.Direction;
            cube.transform.position = planeLine.Origin;
            cube.transform.localScale = new Vector3(10, maxDistanceToPlane, 10);
            cube.transform.parent = container.transform;

            // Remove used lines
            availableLines.RemoveAll(l => lines.Contains(l));

            // Debug
            if (drawWallNormals)
            {
                foreach (Line n in lines)
                {
                    Debug.DrawRay(n.Origin, n.Direction.normalized * normalsScale, colorWallNormals, drawDuration, true);
                }
            }
        }
    }

    private List<Line> FindMostPopulatedPlane(IEnumerable<Line> availableLines)
    {
        List<Line> linesInPlane = new List<Line>();

        foreach (Line line in availableLines)
        {
            Plane plane = new Plane(line.Direction, line.Origin);
            List<Line> containedLines = new List<Line>
            {
                line
            };

            foreach (Line l in availableLines)
            {
                if (!containedLines.Contains(l) && IsPointInPlane(plane, l.Origin) && IsOrientationEqual(line.Direction, l.Direction))
                {
                    containedLines.Add(l);
                }
            }

            if (containedLines.Count > linesInPlane.Count)
            {
                linesInPlane = containedLines;
            }
        }


        return linesInPlane;
    }

    private bool IsOrientationEqual(Vector3 a, Vector3 b)
    {
        float angle = Vector3.Angle(a, b);
        return angle <= maxOrientationDifference || angle >= 180 - maxOrientationDifference;
    }

    private bool IsPointInPlane(Plane plane, Vector3 point)
    {
        float distance = Math.Abs(plane.GetDistanceToPoint(point));
        return distance <= (maxDistanceToPlane / 2);
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

    class Line
    {
        private Vector3 origin;
        private Vector3 direction;

        public Line(Vector3 origin, Vector3 direction)
        {
            this.origin = origin;
            this.direction = direction;
        }

        public Vector3 Origin
        {
            get
            {
                return origin;
            }
        }

        public Vector3 Direction
        {
            get
            {
                return direction;
            }
        }
    }
}
