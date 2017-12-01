using Assets.Scripts.Graph;
using Assets.Scripts.Utils;
using HoloToolkit.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshAnalyzer : Singleton<MeshAnalyzer> {
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

    public GameObject miniMap;
    public int numberOfPlanesToDisplay = 4;

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
    public bool displayFoundWalls = false;

    private float drawDuration = 300f;

    private bool isMappingDone = false;
    private bool isAnalysisDone = false;

    private List<Plane> foundWalls = new List<Plane>();
    private float maxCeiling, minFloor;

    private Vector3 roomDimensions = Vector3.zero;
    private Vector3[] roomCorners;

    private void Update()
    {
        if (state == State.Inactive && ScanProgress.Instance.isFinished()) {
            state = State.InProgress;
            StartCoroutine(AnalyzeRoutine());
        }

        if (displayFoundWalls && state == State.Finished)
        {
            DisplayFoundWalls();
            displayFoundWalls = false;
        }
    }

    private IEnumerator AnalyzeRoutine() {
        Debug.Log("Start mesh analysis");
        List<Graph<Vector3, Line>> graphs = new List<Graph<Vector3, Line>>();
        List<MeshFilter> filters = ScanManager.Instance.GetMeshFilters();
        for (int index = 0; index < filters.Count; index++) {
            MeshFilter filter = filters[index];
            if (filter != null && filter.sharedMesh != null) {
                Graph<Vector3, Line> graph = new Graph<Vector3, Line>(v => v.Origin, VectorUtils.V3_COMPARER);
                Mesh mesh = filter.sharedMesh;
                Debug.Log("New Graph created (" + mesh.vertexCount + ")");
                mesh.RecalculateBounds();
                mesh.RecalculateNormals();
                mesh.RecalculateTangents();

                int[] triangles = mesh.triangles;
                Vector3[] vertices = mesh.vertices;
                Vector3[] normals = mesh.normals;

                for (int i = 0; i < triangles.Length; i += 3) {
                    Vector3 v0 = filter.transform.TransformPoint(vertices[triangles[i]]);
                    Vector3 n0 = filter.transform.TransformDirection(normals[triangles[i]].normalized);
                    Line e0 = new Line(v0, n0);

                    Vector3 v1 = filter.transform.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 n1 = filter.transform.TransformDirection(normals[triangles[i + 1]].normalized);
                    Line e1 = new Line(v1, n1);

                    Vector3 v2 = filter.transform.TransformPoint(vertices[triangles[i + 2]]);
                    Vector3 n2 = filter.transform.TransformDirection(normals[triangles[i + 2]].normalized);
                    Line e2 = new Line(v2, n2);

                    Node<Line> node0 = graph.AddNode(e0);
                    Node<Line> node1 = graph.AddNode(e1);
                    Node<Line> node2 = graph.AddNode(e2);

                    graph.AddUndirectedEdge(node0, node1);
                    graph.AddUndirectedEdge(node1, node2);
                    graph.AddUndirectedEdge(node2, node0);

                    Vector3 center = (v0 + v1 + v2) / 3;
                    Vector3 dir = Vector3.Normalize(Vector3.Cross(v1 - v0, v2 - v0));

                    CategorizeNormal(dir, center);
                }

                graphs.Add(graph);
                Debug.Log("Graph added (" + graph.Count + ")");

                yield return null;

                //for (int i = 0; i < vertices.Length; i++) {
                //    Vector3 vertice = filter.transform.TransformPoint(vertices[i]);
                //    Vector3 normal = filter.transform.TransformDirection(normals[i].normalized);

                //    CategorizeNormal(normal, vertice);
                //}
            }
            yield return null;
        }

        Debug.Log("Normals categorized! Up's: " + upNormals.Count + " Down's: " + downNormals.Count + " Horizontal's: " + horizontalNormals.Count);
        yield return null;

        foundWalls = FindPlanesFromGraphs(graphs, numberOfPlanesToDisplay);
        //foundWalls = FindWallsFromNormals(horizontalNormals);
        yield return null;

        FindFloorAndCeiling(upNormals, downNormals);
        CalculateRoomDimensions();
        yield return null;

        state = State.Finished;
    }

    private void CategorizeNormal(Vector3 normal, Vector3 origin) {
        if (IsOrientationEqual(UP, normal)) {
            upNormals.Add(new Line(origin, normal));
            if (drawUpNormals)
                Debug.DrawRay(origin, normal.normalized * normalsScale, colorUpNormals, drawDuration);
        }
        else if (IsOrientationEqual(DOWN, normal)) {
            downNormals.Add(new Line(origin, normal));
            if (drawDownNormals)
                Debug.DrawRay(origin, normal.normalized * normalsScale, colorDownNormals, drawDuration);
        }
        else if (IsOrientationEqual(new Vector3(normal.x, 0, normal.z), normal)) {
            horizontalNormals.Add(new Line(origin, normal));
            if (drawHorizontalNormals)
                Debug.DrawRay(origin, normal.normalized * normalsScale, colorHorizontalNormals, drawDuration);
        }
    }

    public void DisplayFoundWalls()
    {
        GameObject container = new GameObject("FoundWalls");
        if (miniMap != null)
            container.transform.parent = miniMap.transform;

        MiniMap.Instance.addCube(roomCorners);

        int displayNumber = Math.Min(foundWalls.Count, numberOfPlanesToDisplay);
        for (int i = 0; i < displayNumber; i++)
        {

            Plane plane = foundWalls[i];
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

    private List<Plane> FindPlanesFromGraphs(IEnumerable<Graph<Vector3, Line>> graphs, int numberOfPlanes) {
        List<Plane> planes = new List<Plane>();

        foreach (Graph<Vector3, Line> graph in graphs) {
            for (int i = 0; i < graph.Nodes.Count; i++) {
                Node<Line> node = graph.Nodes[i];
                if (!node.Visited) {
                    Plane plane = null;
                    node.Visited = true;
                    foreach (Plane p in planes) {
                        if (p.IsLineOnPlane(node.Value)) {
                            plane = p;
                            plane.AddLine(node.Value);
                        }
                    }
                    if (plane == null) {
                        plane = new Plane(node.Value.Origin, node.Value.Direction);
                        planes.Add(plane);
                    }
                    AddNeighbours(node, plane);
                }
            }
        }

        // sorts by line count
        planes.Sort();


        // merge similar planes
        List<Plane> mergedPlanes = new List<Plane>();
        foreach (Plane checkPlane in planes) {
            bool matchingPlaneFound = false;
            foreach (Plane mergePlane in mergedPlanes) {
                if (mergePlane.IsLineOnPlane(checkPlane.Line)) {
                    mergePlane.MergePlane(checkPlane);
                    matchingPlaneFound = true;
                    break;
                }
            }

            if (!matchingPlaneFound) {
                mergedPlanes.Add(checkPlane);
            }
        }

        return mergedPlanes;
    }

    private void AddNeighbours(Node<Line> node, Plane p) {
        for (int i = 0; i < node.Neighbours.Count; i++) {
            Node<Line> neighbour = node.Neighbours[i];
            if (!neighbour.Visited && p.IsLineOnPlane(neighbour.Value)) {
                neighbour.Visited = true;
                p.AddLine(neighbour.Value);
                AddNeighbours(node, p);
            }
        }
    }

    private List<Plane> FindWallsFromNormals(IEnumerable<Line> availableLines) {
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
        foreach (Plane checkPlane in planes) {
            bool matchingPlaneFound = false;
            foreach (Plane mergePlane in mergedPlanes) {
                if (mergePlane.IsLineOnPlane(checkPlane.Line)) {
                    mergePlane.MergePlane(checkPlane);
                    matchingPlaneFound = true;
                    break;
                }
            }

            if (!matchingPlaneFound) {
                mergedPlanes.Add(checkPlane);
            }
        }

        return mergedPlanes;
    }

    private void FindFloorAndCeiling(IEnumerable<Line> floorLines, IEnumerable<Line> ceilingLines) {
        minFloor = float.MaxValue;
        foreach (Line l in floorLines) {
            if (l.Origin.y < minFloor) minFloor = l.Origin.y;
        }

        maxCeiling = float.MinValue;
        foreach (Line l in ceilingLines) {
            if (l.Origin.y > maxCeiling) maxCeiling = l.Origin.y;
        }
    }

    private void CalculateRoomDimensions() {
        roomDimensions.y = maxCeiling - minFloor;

        // take the biggest wall and search for the one opposite to it
        Plane mainWall = foundWalls[0];
        Plane mainOppositeWall = FindWallWithNormal(-mainWall.Normal);
        // calculate the distance between the two walls
        float mainDist = Vector3.Distance(mainWall.UnityPlane.ClosestPointOnPlane(mainOppositeWall.Origin), mainOppositeWall.Origin);

        // search the two secondary walls perpendicular to the main wall
        Vector3 secWallDirection = Quaternion.AngleAxis(90, Vector3.up) * mainWall.Normal;
        Vector3 secOppositeWallDirection = Quaternion.AngleAxis(-90, Vector3.up) * mainWall.Normal;
        Plane secWall = FindWallWithNormal(secWallDirection);
        Plane secOppositeWall = FindWallWithNormal(secOppositeWallDirection);

        // calculate the distance between the two secundary walls
        float secDist = Vector3.Distance(secWall.UnityPlane.ClosestPointOnPlane(secOppositeWall.Origin), secOppositeWall.Origin);

        // Put the bigger width/length of the room on the x coordinate
        roomDimensions.x = Math.Max(mainDist, secDist);
        roomDimensions.z = Math.Min(mainDist, secDist);

        // Construct planes for floor and ceiling to do the intersection checks
        Plane floorPlane = new Plane(new Vector3(0, minFloor, 0), Vector3.up);
        Plane ceilingPlane = new Plane(new Vector3(0, maxCeiling, 0), Vector3.down);

        // calculate all corner points of the room
        roomCorners = new Vector3[8];
        
        roomCorners[0] = Intersection(mainWall, secWall, floorPlane);
        roomCorners[1] = Intersection(mainWall, secOppositeWall, floorPlane);
        roomCorners[2] = Intersection(mainOppositeWall, secOppositeWall, floorPlane);
        roomCorners[3] = Intersection(mainOppositeWall, secWall, floorPlane);
        roomCorners[4] = Intersection(mainWall, secWall, ceilingPlane);
        roomCorners[5] = Intersection(mainWall, secOppositeWall, ceilingPlane);
        roomCorners[6] = Intersection(mainOppositeWall, secOppositeWall, ceilingPlane);
        roomCorners[7] = Intersection(mainOppositeWall, secWall, ceilingPlane);
    }

    private static Vector3 Intersection(Plane a, Plane b, Plane c) {
        return Intersection(a, Intersection(b, c));
    }

    private static Vector3 Intersection(Plane p, Line l) {
        float rayDistance;
        Ray ray = new Ray(l.Origin, l.Direction);
        bool result = p.UnityPlane.Raycast(ray, out rayDistance);

        if (!result && rayDistance == 0) throw new Exception("Plane and line are parallel");

        return ray.GetPoint(rayDistance);
    }

    /**
     * Source: https://forum.unity.com/threads/how-to-find-line-of-intersecting-planes.109458/
     * */
    private static Line Intersection(Plane a, Plane b) {
        Vector3 direction = Vector3.Cross(a.Normal, b.Normal);

        Vector3 ldir = Vector3.Cross(b.Normal, direction);
        float numerator = Vector3.Dot(a.Normal, ldir);
        Vector3 aTob = a.Origin - b.Origin;
        float t = Vector3.Dot(a.Normal, aTob) / numerator;
        Vector3 origin = b.Origin + t * ldir;

        return new Line(origin, direction);
    }


    /** HELPER FUNCTIONS **/

    private Plane FindWallWithNormal(Vector3 normal) {
        foreach (Plane w in foundWalls) {
            if (IsOrientationEqual(normal, w.Normal))
                return w;
        }

        throw new Exception("No matching wall found.");
    }

    private bool IsOrientationEqual(Vector3 a, Vector3 b)
    {
        float angle = Vector3.Angle(a, b);
        return angle <= maxOrientationDifference;
    }

    public List<Plane> FoundWalls
    {
        get
        {
            return foundWalls;
        }
    }

    public struct Line {

        public static readonly Line ZERO = new Line(Vector3.zero, Vector3.zero);

        public Vector3 Origin { get; private set; }
        public Vector3 Direction { get; private set; }

        public Line(Vector3 origin, Vector3 direction) {
            Origin = origin;
            Direction = direction;
        }

        public override bool Equals(object obj) {
            if (obj == null || GetType() != obj.GetType()) {
                return false;
            }

            Line that = (Line)obj;

            return VectorUtils.V3_COMPARER.Equals(this.Origin, that.Origin) && VectorUtils.V3_COMPARER.Equals(this.Direction, that.Direction);
        }

        public override int GetHashCode() {
            unchecked {
                int hash = 17;
                hash = hash * 31 + VectorUtils.V3_COMPARER.GetHashCode(Origin);
                hash = hash * 31 + VectorUtils.V3_COMPARER.GetHashCode(Direction);
                return hash;
            }
        }
        
    }

    public class Plane : IComparable<Plane> {
        // The lines that define the plane
        private List<Line> lines = new List<Line>();

        UnityEngine.Plane plane = new UnityEngine.Plane(Vector3.zero, Vector3.zero);

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

            this.plane.SetNormalAndPosition(Normal, Origin);
        }

        public void MergePlane(Plane p) {
            foreach (Line l in p.Lines) {
                AddLine(l);
            }
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
                Line maxHLinePositive = Line.ZERO, maxHLineNegative = Line.ZERO;
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
        public Line Line {
            get { return new Line(Origin, Normal); }
        }
        public UnityEngine.Plane UnityPlane {
            get { return this.plane; }
        }
    }
}
