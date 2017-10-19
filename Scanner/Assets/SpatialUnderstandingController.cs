using HoloToolkit.Unity;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpatialUnderstandingController : MonoBehaviour {

	// Use this for initialization
	void Start () {
        SpatialUnderstanding.Instance.OnScanDone += OnScanDone;

        SpatialUnderstanding.Instance.RequestBeginScanning();
    }
	
	// Update is called once per frame
	void Update () {
		if (Time.realtimeSinceStartup > 30f) {
            SpatialUnderstanding.Instance.RequestFinishScan();
        }
	}

    void OnScanDone() {
        Debug.Log("Yay");
        SpatialUnderstandingDll.Imports.PlayspaceStats stats = SpatialUnderstanding.Instance.UnderstandingDLL.GetStaticPlayspaceStats();
        Debug.Log("Yay");
    }
}
