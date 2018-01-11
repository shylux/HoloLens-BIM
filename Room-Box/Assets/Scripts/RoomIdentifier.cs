using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;
using System.Linq;
using System;

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
	}
	
	void Update () {
        if (this.state == State.Inactive && analyzer.state == MeshAnalyzer.State.Finished) {
            this.state = State.InProgress;
            VirtualRoom vr = IdentifyRoom();
            AlignVirtualAndPhysicalSpace(vr);
            ScanManager.Instance.hideSurfaceMesh();
            ScanProgress.Instance.tts.StartSpeaking(vr.IdentifyMessage);
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

    VirtualRoom IdentifyRoom() {
        VirtualRoomBehavior[] vRoomBhvrs = FindObjectsOfType<VirtualRoomBehavior>();
        foreach (VirtualRoomBehavior v in vRoomBhvrs) {
            virtualRooms.Add(v.room);
        }

        GenerateFootprints();

        physicalRoom = new PhysicalRoom(MeshAnalyzer.Instance.roomDimensions, MeshAnalyzer.Instance.roomCorners);
        physicalRoom.GenerateFootprint();

        // compare footprints
        List<KeyValuePair<VirtualRoom, float>> roomDiffList = new List<KeyValuePair<VirtualRoom, float>>();
        foreach (VirtualRoom vr in virtualRooms) {
            roomDiffList.Add(new KeyValuePair<VirtualRoom, float>(
                vr,
                physicalRoom.CalculateDifference(vr)));
        }
        roomDiffList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

        if (roomDiffList[0].Value == float.MaxValue) {
            ScanProgress.Instance.tts.StartSpeaking("Error. Room not identified.");
            throw new Exception("Could not identify room.");
        }

        return roomDiffList[0].Key;
    }

    void AlignVirtualAndPhysicalSpace(VirtualRoom vr) {
        Transform floorPlan = MiniMap.Instance.transform.Find("FloorPlan");

        // rotate
        Vector3[] corners = physicalRoom.RoomCorners();
        Vector3 ph_rotation = ((corners[1] - corners[0]) + (corners[2] - corners[3]));
        Vector3[] vcorners = vr.RoomCorners();
        Vector3 vr_rotation = ((vcorners[1] - vcorners[0]) + (vcorners[2] - vcorners[3]));
        floorPlan.Rotate(Quaternion.FromToRotation(vr_rotation, ph_rotation).eulerAngles);
        if (vr.betterRotated) {
            floorPlan.Rotate(Quaternion.AngleAxis(180, Vector3.up).eulerAngles);
        }

        // translate
        floorPlan.position -= vr.Transform.position;
        floorPlan.position += physicalRoom.RoomCorners()[ (!vr.betterRotated) ? 0 : 2 ];

        MiniMap.Instance.Activate();
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
    public abstract Vector3[] RoomCorners();

    protected Vector3 roomCenter = Vector3.zero;

    public void GenerateFootprint() {
        footprint = new bool[4 * RoomIdentifier.Instance.sensitivity];

        Vector3[] rc = RoomCorners();
        
        // calc room center
        foreach (Vector3 v in rc) {
            roomCenter += v;
        }
        roomCenter /= rc.Length;
        
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

        // make sure the raycast start from inside the room
        // because the spatial mesh is only one sided and so are the colliders
        if (Vector3.Angle(roomCenter - start, wallNormal) > 90f) {
            wallNormal = -wallNormal;
        }

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

    public bool[] Footprint {
        get { return this.footprint; }
    }
    public bool[] FootprintRotated {
        get {
            bool[] rot = new bool[4 * RoomIdentifier.Instance.sensitivity];
            // copy second half of footprint to first half of rot, etc.
            // this has the effect that the starting corner is now opposite of othe one in footprint
            System.Array.Copy(footprint, 2 * RoomIdentifier.Instance.sensitivity, rot, 0, 2 * RoomIdentifier.Instance.sensitivity);
            System.Array.Copy(footprint, 0, rot, 2 * RoomIdentifier.Instance.sensitivity, 2 * RoomIdentifier.Instance.sensitivity);
            return rot;
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

    public override Vector3[] RoomCorners() {
        return corners;
    }

    /* This method checks how good the virtual room matches the physical one.
     * It checks the room twice. The second time 180deg rotated to find out the orientation.  
     * */
    public float CalculateDifference(VirtualRoom virtualRoom) {
        // check room dimensions first

        float diff = this.dimensions.magnitude - virtualRoom.dimensions.magnitude;

        if (Mathf.Abs(diff) > 0.5f) return float.MaxValue;

        // check the footprint
        float diffNormal = WallDifference(this.Footprint, virtualRoom.Footprint);
        float diffRotated = WallDifference(this.Footprint, virtualRoom.FootprintRotated);
        if (diffRotated < diffNormal)
            virtualRoom.betterRotated = true;

        float bestFootprintDiff = Mathf.Min(diffNormal, diffRotated);

        return bestFootprintDiff;
    }

    /* This method calculates how probable it is that the two wall footprints represent the same wall.
     * */
    private float WallDifference(bool[] pRoom, bool[] vRoom) {
        float diffScore = 0;

        for (int i = 0; i < pRoom.Length; i++) {
            // a wall in the model was not captured in the physical room
            // this can happen easily because the scan is not perfect
            if (!pRoom[i] && vRoom[i])
                diffScore += 0.1f;

            // there is a wall in the physical room which is not in the model
            // this is a strong indication that the footprint does not match
            if (pRoom[i] && !vRoom[i])
                diffScore += 1;
        }

        // make invariant to sensitivity
        diffScore /= pRoom.Length;

        return diffScore;
    }

    public Vector3 Anchor {
        get { return corners[0]; }
    }
}