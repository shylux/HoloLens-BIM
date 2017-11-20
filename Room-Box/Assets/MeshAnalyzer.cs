using HoloToolkit.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MeshAnalyzer : Singleton<MeshAnalyzer>
{
    // MeshAnalyzer states
    public enum State {
        Inactive,
        InProgress,
        Finished
    }
    private State _state = State.Inactive;
    public State state {
        get { return _state; }
        set {
            _state = value;
            Debug.Log("MeshAnalyzer switches state to: " + value.ToString());
        }
    }

    private static readonly Vector3 DOWN = Vector3.down;
    private static readonly Vector3 UP = Vector3.up;

    private HashSet<Line> downNormals = new HashSet<Line>();
    private HashSet<Line> upNormals = new HashSet<Line>();
    private HashSet<Line> horizontalNormals = new HashSet<Line>();

    public int numberOfPlanesToFind = 4;

    [Range(0.0f, 180.0f)]
    public static float maxOrientationDifference = 2f;

    public static float maxDistanceToPlane = .05f;

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
    public bool displayFoundPlanes = false;

    private float drawDuration = 300f;

    private bool isMappingDone = false;
    private bool isAnalysisDone = false;

    private List<Plane> foundPlanes = new List<Plane>();

    private void Update()
    {
        if (state == State.Inactive && ScanProgress.Instance.isFinished()) {
            state = State.InProgress;
            StartCoroutine(AnalyzeRoutine());
        }

        if (displayFoundPlanes && state == State.Finished)
        {
            DisplayFoundPlanes();
            displayFoundPlanes = false;
        }
    }

    private IEnumerator AnalyzeRoutine()
    {
        Debug.Log("Start mesh analysis");

        List<MeshFilter> filters = ScanManager.Instance.GetMeshFilters();
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
                }

                yield return null;

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

        state = State.Finished;
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
        List<Plane> planes = FindMostPopulatedPlanes(normals);
        for (int i = 0; i < numberOfPlanes; i++) {
            foundPlanes.Add(planes[i]);
        }
    }

    public void DisplayFoundPlanes()
    {
        GameObject container = new GameObject("FoundPlanes");
        for (int i = 0; i < foundPlanes.Count; i++)
        {
            Plane plane = foundPlanes[i];
            plane.Normalize();

            GameObject cube = plane.Cube;
            cube.transform.parent = container.transform;

            // Debug
            if (drawWallNormals)
            {
                foreach (Line n in plane.Lines)
                {
                    Debug.DrawRay(n.Origin, n.Direction.normalized * normalsScale, colorWallNormals, drawDuration, true);
                }
            }
        }
    }

    private List<Plane> FindMostPopulatedPlanes(IEnumerable<Line> availableLines) {
        List<Plane> planes = new List<Plane>();
        foreach (Line line in availableLines) {
            bool matchingPlaneFound = false;

            // check planes if there is one that fits the line
            foreach (Plane plane in planes) {
                if (plane.IsLineOnPlane(line)) {
                    plane.AddLine(line);
                    matchingPlaneFound = true;
                    break;
                }
            }

            // add a new plane if no matching plane was found
            if (!matchingPlaneFound) {
                planes.Add(new Plane(line.Origin, line.Direction));
            }
        }

        // sorts by line count
        planes.Sort();

        // merge similar planes
        List<Plane> mergedPlanes = new List<Plane>();
        foreach (Plane plane in planes) {
        }

        return planes;
    }

    private bool IsOrientationEqual(Vector3 a, Vector3 b)
    {
        float angle = Vector3.Angle(a, b);
        return angle <= maxOrientationDifference;
    }

    public List<Plane> FoundPlanes
    {
        get
        {
            return foundPlanes;
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

    public class Line
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

    public class Plane: IComparable<Plane> {
        // The lines that define the plane
        private List<Line> lines = new List<Line>();

        UnityEngine.Plane plane;

        // origin and normal are continously updated to avoid
        // going through all lines to recalculate them
        private Vector3 origin;
        private Vector3 normal;

        public Plane(Vector3 origin, Vector3 normal) {
            this.origin = origin;
            this.normal = normal;
            lines.Add(new Line(origin, normal));
        }

        public bool IsLineOnPlane(Line l) {
            // check the angle between plane normal and line direction
            if (Vector3.Angle(l.Direction, normal) > maxOrientationDifference) return false;
            // check distance between plane and line origin
            if (Vector3.Distance(plane.ClosestPointOnPlane(l.Origin), l.Origin) > maxDistanceToPlane) return false;
            return true;
        }

        public void AddLine(Line l) {
            lines.Add(l);
            this.origin += (l.Origin - this.origin) / lines.Count;
            this.normal += (l.Direction - this.normal) / lines.Count;

            this.plane = new UnityEngine.Plane(this.normal.normalized, this.origin);
        }

        public int CompareTo(Plane otherPlane) {
            return -this.lines.Count.CompareTo(otherPlane.Lines.Count);
        }

        public void Normalize() {
            // make wall perpendicular to the floor/ceiling & normalize
            this.normal = Vector3.ProjectOnPlane(this.normal, Vector3.up).normalized;
        }

        public GameObject Cube {
            get {
                Normalize();

                // Create a cube as representation of the found plane.
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.name = "Plane (" + this.Lines.Count.ToString() + " normals)";
                cube.transform.up = this.Normal;
                // reset x and z rotation
                Vector3 rot = cube.transform.rotation.eulerAngles;
                rot.x = 0;
                rot.z = 90;
                cube.transform.rotation = Quaternion.Euler(rot);

                // find max distances to get an idea of the dimensions of the wall
                float maxV = float.MinValue, minV = float.MaxValue, maxHPositive = float.MinValue, maxHNegative = float.MinValue;
                Line maxHLinePositive = null, maxHLineNegative = null;
                foreach (Line l in Lines) {
                    float h = Vector2.Distance(new Vector2(Origin.x, Origin.z), new Vector2(l.Origin.x, l.Origin.z));
                    if (h > maxHPositive && Vector3.SignedAngle(Vector3.up, l.Origin - Origin, Normal) > 0) {
                        maxHPositive = h;
                        maxHLinePositive = l;
                    }
                    if (h > maxHNegative && Vector3.SignedAngle(Vector3.up, l.Origin - Origin, Normal) < 0) {
                        maxHNegative = h;
                        maxHLineNegative = l;
                    }

                    maxV = Math.Max(maxV, l.Origin.y);
                    minV = Math.Min(minV, l.Origin.y);
                }

                // set origin between max / min values
                Vector3 xzMiddle = (maxHLineNegative.Origin + maxHLinePositive.Origin) / 2;
                cube.transform.position = new Vector3(xzMiddle.x, minV + (maxV - minV) / 2, xzMiddle.z);

                cube.transform.localScale = new Vector3(maxV - minV, maxDistanceToPlane, maxHPositive + maxHNegative);
                return cube;
            }
        }

        public Vector3 Origin {
            get { return this.origin; }
        }
        public Vector3 Normal {
            get { return this.normal; }
        }
        public List<Line> Lines {
            get { return this.lines; }
        }
        public UnityEngine.Plane UnityPlane {
            get { return this.plane; }
        }
    }
}
