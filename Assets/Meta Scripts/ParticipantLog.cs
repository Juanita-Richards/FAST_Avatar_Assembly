using System;
using System.Collections;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using TMPro.Examples;



public class ParticipantLog : MonoBehaviour
{
    
    public TMP_Dropdown cohort_dropdown, gender_dropdown, PID_dropdown;
    public TextMeshProUGUI time;
    
    //don't use readonly key word
    string log_path = "Participant_Logs.csv";


    //this is to log in the spreadsheet what each person did
    public void ParticipantData()
    {
        string dateTime = DateTime.Now.ToString("MM-dd-yy-HH-mm");
        string ParticipantID = PID_dropdown.options[PID_dropdown.value].text;
        string gender = gender_dropdown.options[gender_dropdown.value].text;
        string cohort = cohort_dropdown.options[cohort_dropdown.value].text;

        if (!File.Exists(log_path))
        {
            using StreamWriter writer = File.CreateText(log_path);
            writer.WriteLine("ParticipantID, Gender, Cohort, date-time, Time");

            writer.WriteLine($"{ParticipantID}, {gender}, {cohort}, {dateTime}, {time.text}");
            writer.Close();
        }

        else 
        { 
            using StreamWriter writer = File.AppendText(log_path);
            writer.WriteLine($"{ParticipantID}, {gender}, {cohort}, {dateTime}, {time.text}");
            writer.Close();
        }

    }
}
