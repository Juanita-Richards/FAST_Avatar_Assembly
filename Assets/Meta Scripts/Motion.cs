using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using XRTLogging;

public class Motion : MonoBehaviour
{
    public List<GameObject> MotionObjects;
    string ParticipantID;

    public TMP_Dropdown PID, Cohort;

    private StreamWriter writer;
    private StringBuilder stringBuilder;
    private string log_path;
    public bool startTracking;
    private float writingTime = 60.0f;
    private float timeElapsed = 0;


    // Start is called before the first frame update
    void Start()
    {
        stringBuilder = new StringBuilder();
    }

    public void StartTrackingOn()
    {
        startTracking = true;


        if (startTracking)
        {
           
            ParticipantID = PID.options[PID.value].text;
            var Cohorts = Cohort.options[Cohort.value].text;
           
            string directory = $"Tracking/{"Cohort"} {Cohorts}";
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string fileName = $"P_{ParticipantID+1}_{DateTime.Now.ToString("MMddyy-HHmm")}.csv";
            string fullPath = Path.Combine(directory, fileName);

            //string headers = "Timestamp, TimeElapsed,"; //placeholder for now
            string headers = "TimeStamp,";
            for (int i = 0; i < MotionObjects.Count; i++)
            {
                string objName = MotionObjects[i].name;
                headers += $"{objName}_position_x,{objName}_position_y,{objName}_position_z, {objName}_quat_x,{objName}_quat_y,{objName}_quat_z,{objName}_quat_w,{objName}_six_a,{objName}_six_b,{objName}_six_c,{objName}_six_d, {objName}_Six_e, {objName}_Six_f,";

            }

            //this will add the headers to the .csv file
            writer = new StreamWriter(fullPath);
            writer.WriteLine(headers);

        }

        Debug.Log("tracking started");
    }

    // Update is called once per frame
    void Update()
    {
        if (startTracking)
        {
            //Add Label definition here
    
            timeElapsed += Time.deltaTime;
            stringBuilder.Append($"\n{timeElapsed:0.0000},"); 
            
            for (int i = 0; i < MotionObjects.Count; i++)
            {
                Quaternion quaternion = MotionObjects[i].transform.rotation;
                float[] sixDegrees = SixDConversions.To6D(quaternion); //convert the Quaternion values to 6DoF
                stringBuilder.Append($"{MotionObjects[i].transform.position.x:0.0000},{MotionObjects[i].transform.position.y:0.0000},{MotionObjects[i].transform.position.z:0.0000},{MotionObjects[i].transform.rotation.x:0.0000}, {MotionObjects[i].transform.rotation.y:0.0000}, {MotionObjects[i].transform.rotation.z:0.0000},{MotionObjects[i].transform.rotation.w:0.0000},");
                stringBuilder.Append($"{sixDegrees[0]:0.0000}, {sixDegrees[1]:0.0000},{sixDegrees[2]:0.0000},{sixDegrees[3]:0.0000}, {sixDegrees[4]:0.0000}, {sixDegrees[5]:0.0000},");
                
            }

        }
        //writing to the disk 
        writingTime -= Time.deltaTime;
        if (writingTime <= 0.0f)
        {
            writer.Write(stringBuilder.ToString());
            stringBuilder.Clear();
            writingTime = 60.0f;
        }
    }

    public void StopTrackingOff()
    {
        //turn the logging off
        startTracking = false;
        Debug.Log("tracking stopped");
        writer.Close();
    }
}
