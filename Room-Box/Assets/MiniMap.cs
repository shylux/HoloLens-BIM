using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniMap : MonoBehaviour {

    public enum State { MAXI_MAP, MINI_MAP }

    public State state = State.MAXI_MAP;
    private float transitionStartTime;

    public Material lineMaterial;
    public float tableScaleFactor = 10;
    public float transitionTime = 0.5f;
    
	void Start () {

        addCube(new Vector3(-0.5f, 1, -0.8f), new Vector3(7, 3, 10));
	}

    void Update() {

        float transitionProgress = (Time.realtimeSinceStartup - transitionStartTime) / transitionTime;
        switch (state) {
            case State.MAXI_MAP:
                transform.localScale = Vector3.Lerp(Vector3.one / tableScaleFactor, Vector3.one, transitionProgress);
                break;
            case State.MINI_MAP:
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one / tableScaleFactor, transitionProgress);
                break;
            default:
                Debug.Log("Something went reeealy wrong.");
                break;
        }
    }

    public void setState(string stateName) {
        if (stateName == "MAXI_MAP" && state != State.MAXI_MAP) {
            // transition to maxi map
            state = State.MAXI_MAP;
            transitionStartTime = Time.realtimeSinceStartup;
        } else if (stateName == "MINI_MAP" && state != State.MINI_MAP) {
            // transition to mini map
            state = State.MINI_MAP;
            transitionStartTime = Time.realtimeSinceStartup;
        }
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
    public void addCube(Vector3 center, Vector3 dimensions) {
        Vector3 origin = new Vector3(center.x - dimensions.x / 2, center.y - dimensions.y / 2, center.z - dimensions.z / 2);
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
