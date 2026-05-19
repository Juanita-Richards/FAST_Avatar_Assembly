using Meta.XR.ImmersiveDebugger.UserInterface;
using Oculus.Platform.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class FPSQuestionnaire : MonoBehaviour
{
    public static FPSQuestionnaire Instance;

    //pids and trial

    public ParticipantLog participantLog;


    //for writing to csv
    private string logPath;
    private StringBuilder sb;

    [SerializeField] string PIDS = "";

   
    //question advancer
    [SerializeField]
    int counter = 0;

    private int[] answers = new int[10]; //10 questions, each stores an answer

    [SerializeField]
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

    // Update is called once per frame
    void Update()
    {

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

    public void LogAnswer(int questionIndex, int value)
    {
        answers[questionIndex] = value;
        Debug.Log($"Recorded Q{questionIndex + 1} = {value}");
    }

    public void FPSToCSV()
    {
        var cohort = participantLog.cohort_dropdown.options[participantLog.cohort_dropdown.value].text;
        PIDS = participantLog.cohort_dropdown.options[participantLog.cohort_dropdown.value].text;
        logPath = $"FASTA_{PIDS}_{cohort}_FPS.csv";
        

        //Create directory FIRST
        string directoryPath = Path.Combine(
            "Questionnaire",
            "FPS"
        );

        Directory.CreateDirectory(directoryPath);

        string fullpath = Path.Combine(directoryPath, logPath);


        bool fileExists = File.Exists(fullpath);

        using (var writer = new StreamWriter(fullpath, append: true))
        {
            // Write header only once
            if (!fileExists)
            {
                writer.WriteLine("Trial#,Q1,Q2,Q3,Q4,Q5,Q6,Q7,Q8,Q9,Q10");
            }
            //trial
            string row = $"{PIDS}_{cohort}," + string.Join(",", answers);
            writer.WriteLine(row);
        }
        //trial
        Debug.Log($"Saved trial {PIDS}_{cohort} to CSV.");
    }

    public void ResetAnswers()
    {
        for (int i = 0; i < answers.Length; i++)
            answers[i] = 0;

        Debug.Log("Answers reset for new round.");
    }

    public void FinishQuestionnaire()
    {
        FPSQuestionnaire.Instance.FPSToCSV();
        FPSQuestionnaire.Instance.ResetAnswers();
    }

    public void ResetQuestions()
    {
        counter = 0;
        foreach (var item in questions)
        {
            item.SetActive(false);

        }
    }
    public void RestartQuestions()
    {
        questions[0].SetActive(true);
    }
}
