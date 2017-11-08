using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniMap : MonoBehaviour {

    public Material lineMaterial;
    
	void Start () {

        addCube(new Vector3(0, 0, 25), new Vector3(2, 3, 2));
	}
	
	void Update () {
		
	}

    public void addCube(Vector3[] corners) {
        LineRenderer rend = createRenderer();
        rend.positionCount = 16; 
        rend.SetPositions(new Vector3[] {
            corners[0],
            corners[1],
            corners[2],
            corners[3],
            corners[0],
            corners[4],
            corners[5],
            corners[1],
            corners[5],
            corners[6],
            corners[2],
            corners[6],
            corners[7],
            corners[3],
            corners[7],
            corners[4]
        });
    }

    private LineRenderer createRenderer() {
        GameObject box = new GameObject();
        box.transform.parent = transform;
        LineRenderer rend = box.AddComponent<LineRenderer>();
        rend.material = lineMaterial;
        rend.widthMultiplier = 0.05f;
        rend.useWorldSpace = false;
        return rend;
    }

    // This is a helper intendet to simulate a room
    public void addCube(Vector3 origin, Vector3 dimensions) {
        Vector3[] corners = new Vector3[8];
        corners[0] = origin;
        corners[1] = new Vector3(origin.x + dimensions.x, origin.y, origin.z);
        corners[2] = new Vector3(origin.x + dimensions.x, origin.y, origin.z + dimensions.z);
        corners[3] = new Vector3(origin.x, origin.y, origin.z + dimensions.z);

        corners[4] = new Vector3(origin.x, origin.y + dimensions.y, origin.z);
        corners[5] = new Vector3(origin.x + dimensions.x, origin.y + dimensions.y, origin.z);
        corners[6] = new Vector3(origin.x + dimensions.x, origin.y + dimensions.y, origin.z + dimensions.z);
        corners[7] = new Vector3(origin.x, origin.y + dimensions.y, origin.z + dimensions.z);

        addCube(corners);
    }

}
