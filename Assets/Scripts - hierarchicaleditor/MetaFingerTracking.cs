using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;

public class MetaFingerTracking : MonoBehaviour
{
    public Motion motionTracker;

    public Transform leftOpenXRHand;

    public Transform rightOpenXRHand;
    
    string ParticipantID;

    public TMP_Dropdown PID, Cohort, Build;
  

    private StreamWriter writer;
    private StringBuilder stringBuilder;
    private string log_path;
    public bool startTracking;
    private float writingTime = 60.0f;
    private float timeStamp = 0;

    public List<Transform> _leftJointTransforms = new List<Transform>();
    public List<Transform> _rightJointTransforms = new List<Transform>();
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
       
        stringBuilder = new StringBuilder();
    }

    public void FingerMotionLogLeft()
    {
        startTracking = true; 
        

        if (startTracking)
        {

            /// <summary>
            /// Gathering participant data from the Participant Log gameobject
            /// </summary>
            ParticipantID = PID.options[PID.value].text;
            var Cohorts = Cohort.options[Cohort.value].text;
            var builds = Build.options[Build.value].text;

            string directory = $"MetaHandsFingerMotion/Cohort{Cohorts}";

            /// <summary>
            /// Check if the directory exists
            /// </summary>
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string fileName = $"FAST_P{ParticipantID}_build{builds}_{DateTime.Now.ToString("MMddyy-HHmm")}.csv";
            string fullPath = Path.Combine(directory, fileName);

            string headers = "Timetsamp,";

            for (int i = 0; i < _leftJointTransforms.Count; i++)
            {
                string fingers = _leftJointTransforms[i].name;
                headers += $"{fingers}_Pos(x),{fingers}_Pos(y), {fingers}_Pos(z),{fingers}_Quat_(x),{fingers}_Quat_(y),{fingers}_Quat_(z),{fingers}_Quat_(w),{fingers}_Six_(x),{fingers}_Six_(y),{fingers}_Six_(z),{fingers}_Six_(a),{fingers}_Six_(b),{fingers}_Six_(c),{fingers}_Euler_x, {fingers}_Euler_y, {fingers}_Euler_z,";
            }

            /// <summary>
            /// this will add the headers to the .csv file
            /// </summary>
            /// 
            writer = new StreamWriter(fullPath);
            writer.WriteLine(headers);

        }
        Debug.Log("Finger tracking started");
    }

    // Update is called once per frame
    void Update()
    {
        if (startTracking)
        {
            timeStamp += Time.deltaTime;
            stringBuilder.AppendLine($"\n{timeStamp:0.0000},");

            for (int i = 0; i < _leftJointTransforms.Count; i++)
            {
                Quaternion quaternion = _leftJointTransforms[i].transform.rotation;
                Vector3 euler = _leftJointTransforms[i].transform.localRotation.eulerAngles;
                float[] sixDegrees = SixDConversions.To6D(quaternion); //convert the Quaternion values to 6DoF
                stringBuilder.Append($"{_leftJointTransforms[i].transform.position.x:0.0000},{_leftJointTransforms[i].transform.position.y:0.0000},{_leftJointTransforms[i].transform.position.z:0.0000},{_leftJointTransforms[i].transform.rotation.x:0.0000}, {_leftJointTransforms[i].transform.rotation.y:0.0000}, {_leftJointTransforms[i].transform.rotation.z:0.0000},{_leftJointTransforms[i].transform.rotation.w:0.0000},");
                stringBuilder.Append($"{sixDegrees[0]:0.0000}, {sixDegrees[1]:0.0000},{sixDegrees[2]:0.0000},{sixDegrees[3]:0.0000}, {sixDegrees[4]:0.0000}, {sixDegrees[5]:0.0000},");
                stringBuilder.Append($"{euler.x:0.0000}, {euler.y:0.0000}, {euler.z:0.0000},");
            }
        }

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
        /// <summary>
        /// Turn the logging off
        /// </summary>
        startTracking = false;
        Debug.Log("tracking stopped");
        writer.Close();
    }

}
