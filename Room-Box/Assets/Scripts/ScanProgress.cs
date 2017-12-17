using HoloToolkit.Unity;
using UnityEngine;
using System.Linq;

[RequireComponent(typeof(Camera))]
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

    public enum State {
        InProgress,
        Finished
    }

    /* There are two ways to determine the scan progress.
     * SingleFrame requires a sensor to detect a wall on this frame to be counted towards the total.
     * On continuous strategy a sensor is counted as long as he detected a wall at any time since the start of the app.
     * */
    public enum Strategy {
        SingleFrame,
        Continuous
    }

    private State state = State.InProgress;

    [Tooltip("SingleFrame: sensor has to detect on the same frame.\n" +
             "Continuous: sensor has to detect only once.")]
    public Strategy progressStrategy = Strategy.Continuous;

    [Range(0.0f, 1.0f)]
    public float percentConsideredFinished = 0.5f;

    public float timeBetweenHints = 5f;

    // sensors in each direction
    // first order is longitude, second latitude
    Sensor[,] sensors;

    // max degrees between each sensor
    private int frequency = 10;

    // Speech output
    TextToSpeech tts;

    void Start() {
        tts = GetComponentInChildren<TextToSpeech>();
        tts.StartSpeaking("Starting scanning process.");

        lastHintTime = Time.realtimeSinceStartup;

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
        // disable when finished
        if (state == State.Finished) return;

        // check each sensor
        foreach (Sensor sensor in sensors) {
            if (progressStrategy == Strategy.SingleFrame) // reset the detection state on singleframe strategy
                sensor.detected = false;

            // do raycast, but only if the sensor didn't already detect
            if (!sensor.detected && Physics.Raycast(transform.position, sensor.direction, LayerMask.GetMask("SpatialMesh")))
                sensor.detected = true;

            // draw debug rays
            Color sensorColor = (sensor.detected) ? Color.green : Color.red;
            Debug.DrawRay(transform.position, sensor.direction, sensorColor);
        }

        // check if progress is considered finished
        bool[] progress = CheckProgress();

        if (progress.All(b => b)) {
            state = State.Finished;
            tts.StartSpeaking("Well done. Scann process complete.");
            return;
        }

        GiveDirectionHints(progress);
    }

    /**
     * Checks if progress in every direction exceeds a predefined threshhold.
     * */
    private bool[] CheckProgress() {
        // first 8 for walls then floor & ceiling
        bool[] progressByDirection = new bool[10];

        // check walls
        // walls sensors are ±30 degrees latidute
        // the checks are done in 45 deg longitude intervalls
        for (int i = 0; i < 8; i++) {
            // the % operation isn't actually modulo. it will leave negative numbers in place
            var wallSens = from Sensor s in sensors
                       where s.lat > -30 && s.lat < 30 && mod(s.lng, 360) >= i*45 && mod(s.lng, 360) < (i+1)*45
                       select s;

            progressByDirection[i] = (wallSens.Count(s => s.detected) / (float) wallSens.Count() > percentConsideredFinished);
        }

        // check floor
        var floorSens = from Sensor s in sensors
                        where s.lat >= 30
                        select s;
        progressByDirection[8] = (floorSens.Count(s => s.detected) / (float) floorSens.Count() > percentConsideredFinished);

        // check ceiling
        var ceilingSens = from Sensor s in sensors
                        where s.lat <= -30
                        select s;
        progressByDirection[9] = (ceilingSens.Count(s => s.detected) / (float) ceilingSens.Count() > percentConsideredFinished);

        return progressByDirection;
    }

    private float lastHintTime;
    private int lastSector;
    private void GiveDirectionHints(bool[] progress) {
        // stop the constant nagging
        if (Time.realtimeSinceStartup - lastHintTime < timeBetweenHints) return;
        lastHintTime = Time.realtimeSinceStartup;

        Camera cam = GetComponent<Camera>();
        float lookDirection = cam.transform.eulerAngles.y;

        int sector = Mathf.FloorToInt(lookDirection / 45);
        bool sectorDidntChange = (sector == lastSector);
        lastSector = sector;

        // check front
        if (!progress[sector] ||
            (!sectorDidntChange && !progress[mod(sector + 1, 8)]) ||
            (!sectorDidntChange && !progress[mod(sector - 1, 8)])) {
            tts.StartSpeaking("Keep going.");
            return;
        }

        // check a bit right
        if (!progress[mod(sector + 1, 8)]) {
            tts.StartSpeaking("A bit to your right.");
            return;
        }

        // check a bit left
        if (!progress[mod(sector + 1, 8)]) {
            tts.StartSpeaking("A bit to your left.");
            return;
        }

        // check right
        if (!progress[mod(sector + 2, 8)]) {
            tts.StartSpeaking("Scan the area to your right.");
            return;
        }

        // check left
        if (!progress[mod(sector - 2, 8)]) {
            tts.StartSpeaking("Scan the area to your left.");
            return;
        }

        // check behind
        if (!progress[mod(sector + 3, 8)] ||
            !progress[mod(sector + 4, 8)] ||
            !progress[mod(sector + 5, 8)]) {
            tts.StartSpeaking("Scan the area behind you.");
            return;
        }

        // check floor
        if (!progress[8]) {
            tts.StartSpeaking("Scan the floor.");
            return;
        }

        // check ceiling
        if (!progress[9]) {
            tts.StartSpeaking("Scan the ceiling.");
            return;
        }
    }

    public bool isFinished() {
        return (state == State.Finished);
    }

    public int mod(float val, int basis) {
        return Mathf.FloorToInt((val + basis) % basis);
    }
}