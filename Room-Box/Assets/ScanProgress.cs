using HoloToolkit.Unity;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScanProgress: Singleton<ScanProgress> {

    protected ScanProgress() { }

    protected class Sensor {
        public Vector3 direction;
        public float lng, lat;
        public bool detected = false;

        public Sensor(Vector3 direction, float lng, float lat) {
            this.direction = direction;
            this.lng = lng;
            this.lat = lat;
        }
    }

    // sensors in each direction
    // first order is longitude, second latitude
    Sensor[,] sensors;

    // degrees between each sensor
    private int frequency = 20;

    void Start() {
        sensors = new Sensor[360 / frequency, 180 / frequency];
        
        for (int ilng = 0; ilng < sensors.GetLength(0); ilng++) {
            for (int ilat = 0; ilat < sensors.GetLength(1); ilat++) {
                float lng = getLongitude(ilng);
                float lat = getLatitude(ilat);
                Vector3 dir = createDirectionVector(lat, lng);
                sensors[ilng, ilat] = new Sensor(dir, lng, lat);
            }
        }        
    }

    private float getLongitude(int sensor_index) {
        return frequency * sensor_index - 180 + frequency/2;
    }
    private float getLatitude(int sensor_index) {
        return frequency * sensor_index - 90 + frequency/2;
    }

    Vector3 createDirectionVector(float lat, float lng) {
        return Quaternion.AngleAxis(lat, Vector3.right) * Quaternion.AngleAxis(lng, Vector3.up) * Vector3.forward;
    }

    void Update() {
        // check each sensor
        foreach (Sensor sensor in sensors) {
            if (Physics.Raycast(transform.position, sensor.direction))
                sensor.detected = true;
        }

        foreach (Sensor sensor in sensors) {
            Color sensorColor = (sensor.detected) ? Color.green : Color.red;
            Debug.DrawRay(transform.position, sensor.direction, sensorColor);
        }
    }
}