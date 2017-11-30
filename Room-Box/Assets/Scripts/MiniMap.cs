using HoloToolkit.Unity;
using HoloToolkit.Unity.SpatialMapping;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MiniMap : Singleton<MiniMap> {

    public enum State { MAXI_MAP, MINI_MAP }

    public State state = State.MAXI_MAP;
    

    public Material lineMaterial;
    public float tableScaleFactor = 15;
    public float transitionTime = 1f;

    bool placedDown = false;
    private Vector3 placedDownPosition;

    private GameObject player;
    
	void Start () {
        player = transform.Find("Player").gameObject;
        //addCube(new Vector3(-0.5f, 1, -0.8f), new Vector3(7, 3, 10));
	}

    void Update() {
        float transitionProgress = (Time.realtimeSinceStartup - transitionStartTime) / transitionTime;

        // player position
        player.SetActive(transitionProgress < 1 || state != State.MAXI_MAP);
        player.transform.localPosition = Camera.main.transform.position;

        
        switch (state) {
            case State.MAXI_MAP:
                transform.localScale = Vector3.Lerp(transitionStartScale, Vector3.one, transitionProgress);
                transform.position = Vector3.Lerp(transitionStartPosition, Vector3.zero, transitionProgress);
                foreach (LineRenderer rend in GetComponentsInChildren<LineRenderer>())
                    rend.widthMultiplier = Mathf.Lerp(transitionStartLineWidth, 0.05f, transitionProgress);
                break;
            case State.MINI_MAP:
                transform.localScale = Vector3.Lerp(transitionStartScale, Vector3.one / tableScaleFactor, transitionProgress);
                Vector3 targetPosition = (placedDown) ? placedDownPosition : getGazePlacement();
                transform.position = Vector3.Lerp(transitionStartPosition, targetPosition, transitionProgress);
                foreach (LineRenderer rend in GetComponentsInChildren<LineRenderer>())
                    rend.widthMultiplier = Mathf.Lerp(transitionStartLineWidth, 0.01f, transitionProgress);
                break;
            default:
                Debug.Log("How do you even...");
                break;
        }
    }

    private Vector3 getGazePlacement() {
        RaycastHit hitInfo;
        if (Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out hitInfo, 1.5f, SpatialMappingManager.Instance.LayerMask)) {
            return hitInfo.point;
        }
        return Camera.main.transform.position + Camera.main.transform.forward * 1.5f;
    }

    private float transitionStartTime = float.MinValue;
    private Vector3 transitionStartPosition = Vector3.zero;
    private Vector3 transitionStartScale = Vector3.one;
    private float transitionStartLineWidth;
    public void setState(string stateName) {
        if (stateName == "MAXI_MAP" && state != State.MAXI_MAP) {
            // transition to maxi map
            state = State.MAXI_MAP;
            startTransition();

        } else if (stateName == "MINI_MAP" && state != State.MINI_MAP) {
            // transition to mini map
            state = State.MINI_MAP;
            startTransition();
        }
    }

    private void startTransition() {
        transitionStartTime = Time.realtimeSinceStartup;
        transitionStartPosition = transform.position;
        transitionStartScale = transform.localScale;
        transitionStartLineWidth = GetComponentInChildren<LineRenderer>().widthMultiplier;
    }

    public void placeDown() {
        if (state != State.MINI_MAP) return;
        placedDown = true;
        placedDownPosition = transform.position;
    }

    public void pickUp() {
        if (state != State.MINI_MAP) return;
        placedDown = false;
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
