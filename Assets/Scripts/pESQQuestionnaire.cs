using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.VisualScripting;
using UnityEditor.Analytics;
using UnityEngine;

public class pESQQuestionnaire : MonoBehaviour
{
    public static pESQQuestionnaire Instance;
    private string logPath;
    private StringBuilder sb;

    public ParticipantLog participantLog;

    [SerializeField] string PIDS = "";

    private int[] answers = new int[16];
    [SerializeField] int counter = 0;

    public List<GameObject> questions = new List<GameObject>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        sb = new StringBuilder();
    }

    public void LogAnswer(int questionIndex, int value)
    {
        answers[questionIndex] = value;
        Debug.Log($"Recorded Q{questionIndex} = {value}");
    }

    public void Next()
    {
        if (counter != questions.Count - 1)
        {
            questions[counter].SetActive(false);
            questions[counter + 1].SetActive(true);
        }

        counter++;
    }

    public void pESQtoCSV()
    {
        var cohort = participantLog.cohort_dropdown.options[participantLog.cohort_dropdown.value].text;
        PIDS = participantLog.cohort_dropdown.options[participantLog.PID_dropdown.value].text;

        logPath = $"FASTA_pESQ.csv";

        //Create directory FIRST
        string directoryPath = Path.Combine(
            "Questionnaire",
            "pESQ"
        );

        Directory.CreateDirectory(directoryPath);

        string fullpath = Path.Combine(directoryPath, logPath);

        bool fileExists = File.Exists(fullpath);

        using (var writer = new StreamWriter(fullpath, append: true)) 
        {
            //write header only once
            if (!fileExists)
            {
                writer.WriteLine("PID #, Cohort, Q1, Q2, Q3, Q4, Q5, Q6, Q7, Q8," +
                    "Q9, Q10, Q11, Q12, Q13, Q14, Q15, Q16");
            }

            string row = $"{PIDS}, {cohort}" +
            $"{answers[0]}, {answers[1]}, {answers[2]}, {answers[3]}, {answers[4]}, {answers[5]}, {answers[6]}, {answers[7]}"+
            $"{answers[8]}, {answers[9]}, {answers[10]}, {answers[11]}, {answers[12]}, {answers[13]}"+
            $"{answers[14]}, {answers[15]}";

            writer.WriteLine(row);
            
        }

        Debug.Log($"Saved pESQ CSV to: {fullpath}");
    }

    public void FinishQuestionnaire()
    {

        counter = 0;
        foreach (var item in questions)
        {
            item.SetActive(false);
        }
    }
    public void Quit()
    {
        Application.Quit();
    }
}
