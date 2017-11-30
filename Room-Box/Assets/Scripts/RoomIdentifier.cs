using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class RoomIdentifier : Singleton<RoomIdentifier> {

    [Tooltip("Amount of tests performed for each wall.")]
    public int sensitivity = 200;

    [Tooltip("Length of the scan probe. 0.2f means the probe start 10cm before the wall and ends 10cm after the wall.")]
    public float probeDepth = 0.4f;

    [Tooltip("The probes used on the blender model.")]
    public bool showVirtualProbes = false;
    [Tooltip("The probes used on the hololens provided mesh.")]
    public bool showPhysicalProbes = false;

    public enum State {
        Inactive,
        InProgress,
        Finished
    }
    public State state = State.Inactive;

    private MeshAnalyzer analyzer;

    private PhysicalRoom physicalRoom;
    private List<VirtualRoom> virtualRooms = new List<VirtualRoom>();

    void Start () {
        analyzer = MeshAnalyzer.Instance;
        VirtualRoomBehavior[] vRoomBhvrs = FindObjectsOfType<VirtualRoomBehavior>();
        foreach (VirtualRoomBehavior v in vRoomBhvrs) {
            virtualRooms.Add(v.room);
        }

        GenerateFootprints();
	}
	
	void Update () {
        if (this.state == State.Inactive && analyzer.state == MeshAnalyzer.State.Finished) {
            this.state = State.InProgress;
            IdentifyRoom();
        }

        if (showVirtualProbes) {
            foreach (VirtualRoom vr in virtualRooms) {
                vr.DrawProbes();
            }
        }
        if (showPhysicalProbes && physicalRoom != null) {
            physicalRoom.DrawProbes();
        }
	}

    // does the tests on the virtual rooms
    void GenerateFootprints() {
        foreach (Room r in virtualRooms) {
            r.GenerateFootprint();
        }
    }

    void IdentifyRoom() {
        physicalRoom = new PhysicalRoom(MeshAnalyzer.Instance.roomDimensions, MeshAnalyzer.Instance.roomCorners);
        physicalRoom.GenerateFootprint();

        //MiniMap.Instance.Activate();
    }
}

public abstract class Room {
    public Vector3 dimensions;
    public bool[] footprint;

    private Dictionary<Ray, bool> probes = new Dictionary<Ray, bool>();

    // does a raycast on the relevant colliders
    protected abstract bool RayCast(Ray r);

    // Starting at a bottom corner where, looking from the inside, the bigger wall is on the right and the smaller on the left.
    // Second corner is the one along the bigger wall. Rotate clockwise, then to the same with the top corners.
    protected abstract Vector3[] RoomCorners();

    public void GenerateFootprint() {
        footprint = new bool[4 * RoomIdentifier.Instance.sensitivity];

        Vector3[] rc = RoomCorners();
        
        // start in the direction of the big wall
        bool[] first = ScanWall((rc[0] + rc[4]) / 2, (rc[1] + rc[5]) / 2);
        System.Array.Copy(first, 0, footprint, 0, RoomIdentifier.Instance.sensitivity);

        // follow the wall
        bool[] second = ScanWall((rc[1] + rc[5]) / 2, (rc[2] + rc[6]) / 2);
        System.Array.Copy(second, 0, footprint, RoomIdentifier.Instance.sensitivity, RoomIdentifier.Instance.sensitivity);

        bool[] third = ScanWall((rc[2] + rc[6]) / 2, (rc[3] + rc[7]) / 2);
        System.Array.Copy(third, 0, footprint, 2 * RoomIdentifier.Instance.sensitivity, RoomIdentifier.Instance.sensitivity);

        bool[] fourth = ScanWall((rc[3] + rc[7]) / 2, (rc[0] + rc[4]) / 2);
        System.Array.Copy(fourth, 0, footprint, 3 * RoomIdentifier.Instance.sensitivity, RoomIdentifier.Instance.sensitivity);
    }

    public bool[] ScanWall(Vector3 start, Vector3 end) {
        bool[] scan = new bool[RoomIdentifier.Instance.sensitivity];

        // do not start exactly at the corner, because the other walls will interfere with the results
        start = start + (end - start).normalized * 0.1f;
        end = end + (start - end).normalized * 0.1f;

        Vector3 wallNormal = Vector3.Cross(end - start, Vector3.up).normalized;

        // move the start/end away from the wall to the ray start positions
        start = start + wallNormal * RoomIdentifier.Instance.probeDepth / 2;
        end = end + wallNormal * RoomIdentifier.Instance.probeDepth / 2;

        for (int i = 0; i < RoomIdentifier.Instance.sensitivity; i++) {
            Vector3 rayStart = Vector3.Lerp(start, end, (float)i / RoomIdentifier.Instance.sensitivity);

            scan[i] = RayCast(new Ray(rayStart, -wallNormal));

            probes.Add(new Ray(rayStart, -wallNormal), scan[i]);
        }

        return scan;
    }

    public void DrawProbes() {
        foreach (Ray ray in probes.Keys) {
            Debug.DrawLine(ray.origin, ray.origin + ray.direction * RoomIdentifier.Instance.probeDepth, (probes[ray]) ? Color.green : Color.red, 0.1f, true);
        }
    }
}


public class PhysicalRoom : Room {

    private Vector3[] corners;

    public PhysicalRoom(Vector3 _dimensions, Vector3[] _corners) {
        this.dimensions = _dimensions;
        this.corners = _corners;
    }

    protected override bool RayCast(Ray r) {
        RaycastHit hit = new RaycastHit();
        return Physics.Raycast(r, out hit, RoomIdentifier.Instance.probeDepth, LayerMask.GetMask("SpatialMesh"));
    }

    protected override Vector3[] RoomCorners() {
        // the corners from the analyzer script follow the big wall from the first to the second corner.
        // but we don't know if it is clock or counter-clock wise.

        Vector3 normal = Vector3.Cross(corners[1] - corners[0], corners[3] - corners[0]);

        if (normal.y < 0) {
            // the corners are counter-clock wise. switch them around.
            Vector3 tmp;

            // bottom
            /*
            tmp = corners[1];
            corners[1] = corners[3];
            corners[3] = tmp;

            // top
            tmp = corners[5];
            corners[5] = corners[7];
            corners[7] = tmp;
            /**/

            // since we changed the order the first step is now along the smaller wall.
            // rotate by 1 to start with a big wall.

            // bottom
            /*
            tmp = corners[3];
            System.Array.Copy(corners, 0, corners, 1, 3);
            corners[0] = tmp;

            // top
            tmp = corners[7];
            System.Array.Copy(corners, 4, corners, 5, 3);
            corners[4] = tmp;
            /**/
        }
        
        return corners;
    }
}