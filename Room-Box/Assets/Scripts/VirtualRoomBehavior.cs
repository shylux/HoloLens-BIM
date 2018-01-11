using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class VirtualRoomBehavior : MonoBehaviour {

    [Tooltip("Axis aligned dimensions of the room. The x dimension is the biggest horizontal dimension (bigger than z).")]
    public Vector3 dimensions;

    public VirtualRoom room;

    public void Start() {
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
        foreach (RaycastHit hit in Physics.RaycastAll(r, RoomIdentifier.Instance.probeDepth, LayerMask.GetMask("Wall"))) {
            if (hit.transform.IsChildOf(this.behavior.transform)) return true;
        }
        return false;
    }

    public override Vector3[] RoomCorners() {
        Vector3[] corners = new Vector3[8];

        Vector3 origin = behavior.transform.position;
        corners[0] = origin;
        corners[1] = origin + new Vector3(-dimensions.x, 0, 0);
        corners[2] = origin + new Vector3(-dimensions.x, 0, -dimensions.z);
        corners[3] = origin + new Vector3(0, 0, -dimensions.z);

        corners[4] = origin + new Vector3(0, dimensions.y, 0);
        corners[5] = origin + new Vector3(-dimensions.x, dimensions.y, 0);
        corners[6] = origin + new Vector3(-dimensions.x, dimensions.y, -dimensions.z);
        corners[7] = origin + new Vector3(0, dimensions.y, -dimensions.z);

        // make sure the first step is along the longer coordinate
        if (dimensions.x < dimensions.z) {
            // change order

            // bottom
            Vector3 tmp;
            tmp = corners[1];
            corners[1] = corners[3];
            corners[3] = tmp;

            // top
            tmp = corners[5];
            corners[5] = corners[7];
            corners[7] = tmp;
        }

        return corners;
    }

    public Transform Transform {
        get { return behavior.transform; }
    }
}
