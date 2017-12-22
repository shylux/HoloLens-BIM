using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshCollider))]
public class VirtualRoomBehavior : MonoBehaviour {

    [Tooltip("Axis aligned dimensions of the room. The x dimension is the biggest horizontal dimension (bigger than z).")]
    public Vector3 dimensions;

    public VirtualRoom room;

    public MeshCollider meshCollider;

    public void Start() {
        meshCollider = GetComponent<MeshCollider>();
        room = new VirtualRoom(dimensions, this);
    }
}

public class VirtualRoom : Room {
    VirtualRoomBehavior behavior;
    public bool betterRotated = false;

    public VirtualRoom(Vector3 _dimensions, VirtualRoomBehavior _behavior) {
        this.dimensions = _dimensions;
        this.behavior = _behavior;
    }

    protected override bool RayCast(Ray r) {
        RaycastHit hit = new RaycastHit();
        return this.behavior.meshCollider.Raycast(r, out hit, RoomIdentifier.Instance.probeDepth);
    }

    public override Vector3[] RoomCorners() {
        Vector3[] corners = new Vector3[8];

        Vector3 origin = behavior.transform.position;
        corners[0] = origin;
        corners[1] = origin + new Vector3(-dimensions.x, 0, 0);
        corners[2] = origin + new Vector3(-dimensions.x, 0, dimensions.z);
        corners[3] = origin + new Vector3(0, 0, dimensions.z);
        corners[4] = origin + new Vector3(0, dimensions.y, 0);
        corners[5] = origin + new Vector3(-dimensions.x, dimensions.y, 0);
        corners[6] = origin + new Vector3(-dimensions.x, dimensions.y, dimensions.z);
        corners[7] = origin + new Vector3(0, dimensions.y, dimensions.z);

        return corners;
    }

    public Transform Tansform {
        get { return behavior.transform; }
    }
}
