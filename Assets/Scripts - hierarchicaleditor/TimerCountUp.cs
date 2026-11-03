using System;
using System.Collections;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEditor;
using PlayStructure;


public class TimerCountUp : MonoBehaviour
{

    public TextMeshProUGUI timer;
    public float timerCountUp;
    public bool timeStarted;

    public ParticipantLog pLog;

    string log_path = "BuildA_D_times.csv";
    private string buildATime = "";
    private string buildDTime = "";
    private float buildASeconds = 0f;
    private float buildDSeconds = 0f;

    public enum TimingStage
    {
        BuildA,
        Questionnaire,
        BuildD,
    }

    public event Action OnTimerStopped;

    private TimingStage stage = TimingStage.BuildA;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timerCountUp = 0f;
        timeStarted = false;
        timer.text = timerCountUp.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (!timeStarted) return;

        timerCountUp += Time.deltaTime;

        DisplayTime(timerCountUp);

    }
    public void TurnTimerOn()
    {
        if (stage != TimingStage.BuildA) return;

        timeStarted = true;
    }

    public void TurnTimerOff()
    {
        if (stage != TimingStage.BuildA || !timeStarted)
            return;

        timeStarted = false;

        DisplayTime(timerCountUp);
        buildATime = timer.text;
        buildASeconds = timerCountUp;

        stage = TimingStage.Questionnaire;
        
       

        Debug.Log($"Build A time: {buildATime}");

        //Notify Motion for Phase 2
        OnTimerStopped?.Invoke(); //fire once
    }

    public void StartBuildD()
    {
        if (stage == TimingStage.BuildD)
            return;

        stage = TimingStage.BuildD;

        // Reset timer to zero
        timerCountUp = 0f;
        DisplayTime(timerCountUp);

        timeStarted = true;

        Debug.Log("Build D timer started at 00:00");
    }

    // Stop timing Build D when physical assembly finishes
    public void FinishBuildD()
    {
        if (stage != TimingStage.BuildD || !timeStarted)
            return;

        timeStarted = false;

        DisplayTime(timerCountUp);
        buildDTime = timer.text;
        buildDSeconds = timerCountUp;

        Debug.Log($"Build D time: {buildDTime}");

        // Save both Build A and Build D times
        SaveTime();
    }
    void DisplayTime(float time)
    {
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);

        timer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
       
    }
    //IEnumerator TimeCount()
    //{
    //    timerCountDown += Time.deltaTime;


    //    DisplayTime(timerCountDown);
    //    yield return null;
    //}


    public void SaveTime()
    {
        string dateTime = DateTime.Now.ToString("MM-dd-yy-HH-mm");
        string PID = pLog.PID_dropdown.options[pLog.PID_dropdown.value].text;
        string gender = pLog.gender_dropdown.options[pLog.gender_dropdown.value].text;
        string cohort = pLog.cohort_dropdown.options[pLog.cohort_dropdown.value].text;

        if (!File.Exists(log_path))
        {
            using StreamWriter writer = File.CreateText(log_path);
            writer.WriteLine("ParticipantID, Gender, Cohort, BuildA,BuildASecs,BuildD,BuildDSecs, Date-Time,");

            writer.WriteLine($"{PID}, {gender}, {cohort},{buildATime},{buildASeconds},{buildDTime},{buildDSeconds},{dateTime},");

        }
        else
        {
            using StreamWriter writer = File.AppendText(log_path);
            writer.WriteLine($"{PID}, {gender}, {cohort},{buildATime},{buildASeconds},{buildDTime},{buildDSeconds},{dateTime},");
            writer.Close();
        }
        Debug.Log("Build Times are saved is saved");
    }


    public void OnApplicationQuit()
    {
        FinishBuildD();
        Motion.Instance.StopTrackingOff();
    }

}
