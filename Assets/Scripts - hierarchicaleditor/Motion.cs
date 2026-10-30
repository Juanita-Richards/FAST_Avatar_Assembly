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

    //This is to get all of the items I want to get motion data on
    public List<GameObject> TrackedBodies;

    public Transform leftOpenXRHand;

    public Transform rightOpenXRHand;

    //Below are for keeping track of each participant

    string ParticipantID;

    public TMP_Dropdown PID, Cohort, Build;

    //This is for logguing the data

    private StreamWriter writer;
    private StringBuilder stringBuilder;
    private string log_path;
    public bool startTracking;
    private float writingTime = 60.0f;
    private float timeStamp = 0;


    //This for finger tracking
    public List<Transform> _leftJointTransforms = new List<Transform>();
    public List<Transform> _rightJointTransforms = new List<Transform>();


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

            string fileName = $"FAST_P{ParticipantID}_build{builds}_{DateTime.Now.ToString("MMddyy-HHmm")}.csv";
            string fullPath = Path.Combine(directory, fileName);

            //string headers = "Timestamp, TimeElapsed,"; //placeholder for now
            string headers = "TimeStamp,";
            for (int i = 0; i < TrackedBodies.Count; i++)
            {
                string bodyparts = TrackedBodies[i].name;
                headers += $"{bodyparts}_Pos(x),{bodyparts}_Pos(y),{bodyparts}_Pos(z),{bodyparts}_Quat_(x),{bodyparts}_Quat_(y),{bodyparts}_Quat_(z),{bodyparts}_Quat_(w),{bodyparts}_Six_(x),{bodyparts}_Six_(y),{bodyparts}_Six_(z),{bodyparts}_Six_(a),{bodyparts}_Six_(b),{bodyparts}_Six_(c),{bodyparts}_Euler_x,{bodyparts}_Euler_y,{bodyparts}_Euler_z,";

            }

            for (int i = 0; i < _leftJointTransforms.Count; i++)
            {
                string leftfingers = $"Left_{_leftJointTransforms[i].name}";
                headers += $"{leftfingers}_Pos(x),{leftfingers}_Pos(y),{leftfingers}_Pos(z),";
                headers += $"{leftfingers}_Quat_(x),{leftfingers}_Quat_(y),{leftfingers}_Quat_(z),{leftfingers}_Quat_(w),";
                headers += $"{leftfingers}_Six_(x),{leftfingers}_Six_(y),{leftfingers}_Six_(z),{leftfingers}_Six_(a),{leftfingers}_Six_(b),{leftfingers}_Six_(c),";
                headers += $"{leftfingers}_Euler_(x),{leftfingers}_Euler_(y),{leftfingers}_Euler_(z),";
            }

            for (int i = 0; i < _rightJointTransforms.Count; i++)
            {
                string rightfingers = $"Right_{_rightJointTransforms[i].name}";
                headers += $"{rightfingers}_Pos(x),{rightfingers}_Pos(y),{rightfingers}_Pos(z),";
                headers += $"{rightfingers}_Quat_(x),{rightfingers}_Quat_(y),{rightfingers}_Quat_(z),{rightfingers}_Quat_(w),";
                headers += $"{rightfingers}_Six_(x),{rightfingers}_Six_(y),{rightfingers}_Six_(z),{rightfingers}_Six_(a),{rightfingers}_Six_(b),{rightfingers}_Six_(c),";
                headers += $"{rightfingers}_Euler_(x),{rightfingers}_Euler_(y),{rightfingers}_Euler_z,";
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
            stringBuilder.Append($"{timeStamp:0.0000},"); 

            
            for (int i = 0; i < TrackedBodies.Count; i++)
            {
                Quaternion quaternion = TrackedBodies[i].transform.rotation;
                Vector3 euler = TrackedBodies[i].transform.localRotation.eulerAngles;
                float[] sixDegrees = SixDConversions.To6D(quaternion); //convert the Quaternion values to 6DoF
                stringBuilder.Append($"{TrackedBodies[i].transform.position.x:0.0000},{TrackedBodies[i].transform.position.y:0.0000},{TrackedBodies[i].transform.position.z:0.0000},{TrackedBodies[i].transform.rotation.x:0.0000}, {TrackedBodies[i].transform.rotation.y:0.0000}, {TrackedBodies[i].transform.rotation.z:0.0000},{TrackedBodies[i].transform.rotation.w:0.0000},");
                stringBuilder.Append($"{sixDegrees[0]:0.0000}, {sixDegrees[1]:0.0000},{sixDegrees[2]:0.0000},{sixDegrees[3]:0.0000}, {sixDegrees[4]:0.0000}, {sixDegrees[5]:0.0000},");
                stringBuilder.Append($"{euler.x:0.0000}, {euler.y:0.0000}, {euler.z:0.0000},");

            }

            for (int i = 0; i < _rightJointTransforms.Count; i++)
            {
                Quaternion quaternion = _rightJointTransforms[i].transform.rotation;
                Vector3 euler = _rightJointTransforms[i].transform.localRotation.eulerAngles;
                float[] sixDegrees = SixDConversions.To6D(quaternion); //convert the Quaternion values to 6DoF
                stringBuilder.Append($"{_rightJointTransforms[i].transform.position.x:0.0000},{_rightJointTransforms[i].transform.position.y:0.0000},{_rightJointTransforms[i].transform.position.z:0.0000},{_rightJointTransforms[i].transform.rotation.x:0.0000}, {_rightJointTransforms[i].transform.rotation.y:0.0000}, {_rightJointTransforms[i].transform.rotation.z:0.0000},{_rightJointTransforms[i].transform.rotation.w:0.0000},");
                stringBuilder.Append($"{sixDegrees[0]:0.0000}, {sixDegrees[1]:0.0000},{sixDegrees[2]:0.0000},{sixDegrees[3]:0.0000}, {sixDegrees[4]:0.0000}, {sixDegrees[5]:0.0000},");
                stringBuilder.Append($"{euler.x:0.0000}, {euler.y:0.0000}, {euler.z:0.0000},");
            }

            for (int i = 0; i < _leftJointTransforms.Count; i++)
            {
                Quaternion quaternion = _leftJointTransforms[i].transform.rotation;
                Vector3 euler = _leftJointTransforms[i].transform.localRotation.eulerAngles;
                float[] sixDegrees = SixDConversions.To6D(quaternion); //convert the Quaternion values to 6DoF
                stringBuilder.Append($"{_leftJointTransforms[i].transform.position.x:0.0000},{_leftJointTransforms[i].transform.position.y:0.0000},{_leftJointTransforms[i].transform.position.z:0.0000},{_leftJointTransforms[i].transform.rotation.x:0.0000}, {_leftJointTransforms[i].transform.rotation.y:0.0000}, {_leftJointTransforms[i].transform.rotation.z:0.0000},{_leftJointTransforms[i].transform.rotation.w:0.0000},");
                stringBuilder.Append($"{sixDegrees[0]:0.0000}, {sixDegrees[1]:0.0000},{sixDegrees[2]:0.0000},{sixDegrees[3]:0.0000}, {sixDegrees[4]:0.0000}, {sixDegrees[5]:0.0000},");
                stringBuilder.Append($"{euler.x:0.0000}, {euler.y:0.0000}, {euler.z:0.0000},");
            }
            stringBuilder.AppendLine();
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

        if (writer != null)
        {
            writer.Write(stringBuilder.ToString());
            stringBuilder.Clear();

            writer.Flush();
            writer.Close();

            writer = null;
        }

        Debug.Log("Tracking stopped and CSV saved.");
    }

   
}
