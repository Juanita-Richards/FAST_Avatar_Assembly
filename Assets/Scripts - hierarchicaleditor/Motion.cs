using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Motion : MonoBehaviour
{
    public static Motion Instance;

    public List<GameObject> TrackedBodies;
    string ParticipantID;

    public TMP_Dropdown PID, Cohort, Build;

    private StreamWriter writer;
    private StringBuilder stringBuilder;
    private string log_path;
    public bool startTracking;
    private float writingTime = 60.0f;
    private float timeStamp = 0;


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
            var builds = Build.options[Build.value].text;
           
            string directory = $"Tracking/{"Cohort "}{Cohorts}"; //P{ParticipantID}_{DateTime.Now.ToString("MMddyy-HHmm")}.csv
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string fileName = $"P{ParticipantID}_build{builds}_{DateTime.Now.ToString("MMddyy-HHmm")}.csv";
            string fullPath = Path.Combine(directory, fileName);

            //string headers = "Timestamp, TimeElapsed,"; //placeholder for now
            string headers = "TimeStamp,";
            for (int i = 0; i < TrackedBodies.Count; i++)
            {
                string bodyparts = TrackedBodies[i].name;
                headers += $"{bodyparts}_Pos(x),{bodyparts}_Pos(y), {bodyparts}_Pos(z),{bodyparts}_Quat_(x),{bodyparts}_Quat_(y),{bodyparts}_Quat_(z),{bodyparts}_Quat_(w),{bodyparts}_Six_(x),{bodyparts}_Six_(y),{bodyparts}_Six_(z),{bodyparts}_Six_(a),{bodyparts}_Six_(b),{bodyparts}_Six_(c),{bodyparts}_Euler_x, {bodyparts}_Euler_y, {bodyparts}_Euler_z,";
                //headers += $"{objName}_position_x,{objName}_position_y,{objName}_position_z, {objName}_quat_x,{objName}_quat_y,{objName}_quat_z,{objName}_quat_w,{objName}_six_a,{objName}_six_b,{objName}_six_c,{objName}_six_d, {objName}_Six_e, {objName}_Six_f,";

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

            timeStamp += Time.deltaTime;
            stringBuilder.Append($"{timeStamp:0.0000},"); //removed "\n" 7.28.26

            
            for (int i = 0; i < TrackedBodies.Count; i++)
            {
                Quaternion quaternion = TrackedBodies[i].transform.rotation;
                Vector3 euler = TrackedBodies[i].transform.localRotation.eulerAngles;
                float[] sixDegrees = SixDConversions.To6D(quaternion); //convert the Quaternion values to 6DoF
                stringBuilder.Append($"{TrackedBodies[i].transform.position.x:0.0000},{TrackedBodies[i].transform.position.y:0.0000},{TrackedBodies[i].transform.position.z:0.0000},{TrackedBodies[i].transform.rotation.x:0.0000}, {TrackedBodies[i].transform.rotation.y:0.0000}, {TrackedBodies[i].transform.rotation.z:0.0000},{TrackedBodies[i].transform.rotation.w:0.0000},");
                stringBuilder.Append($"{sixDegrees[0]:0.0000}, {sixDegrees[1]:0.0000},{sixDegrees[2]:0.0000},{sixDegrees[3]:0.0000}, {sixDegrees[4]:0.0000}, {sixDegrees[5]:0.0000},");
                stringBuilder.Append($"{euler.x:0.0000}, {euler.y:0.0000}, {euler.z:0.0000},");
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
