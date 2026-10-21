using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
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

    public void LogAnswer(int questionIndex, int value)
    {
        answers[questionIndex] = value;
        Debug.Log($"Recorded Q{questionIndex + 1} = {value}");
    }


    public void Next()
    {
        if (counter != questions.Count - 1)
        {
            questions[counter].SetActive(false);
            questions[counter + 1].SetActive(true);

            StartCoroutine(LockQuestionButtons(questions[counter + 1]));
        }
        counter++;
    }
    private IEnumerator LockQuestionButtons(GameObject question)
    {
        //Find all buttons on the newly displayed question
        Button[] buttons = question.GetComponentsInChildren<Button>();

        // Disable them
        foreach (Button button in buttons)
        {
            button.interactable = false;
        }

        yield return new WaitForSeconds(3f);

        //enable them again
        foreach (Button button in buttons)
        {
            button.interactable = true;
        }
    }


    public void FPSToCSV()
    {
        var cohort = participantLog.cohort_dropdown.options[participantLog.cohort_dropdown.value].text;
        PIDS = participantLog.PID_dropdown.options[participantLog.PID_dropdown.value].text;

        logPath = $"FASTA_FPS.csv";

        //Create directory FIRST
        string directoryPath = Path.Combine(
            "Questionnaire"
            //"FPS" - this makes another folder
        );

        Directory.CreateDirectory(directoryPath);

        string fullpath = Path.Combine(directoryPath, logPath);


        bool fileExists = File.Exists(fullpath);

        var IPS = (answers[0] + answers[1] + answers[3] + answers[4] + answers[5] + answers[6] + answers[7] + answers[8]) / 8;
        var SPS = (answers[2] + answers[3] + answers[7]) / 3;
        var DPS = (answers[0] + answers[1] + answers[2] + answers[3] + answers[6] + answers[7] + answers[9]) / 7;
        var TPS = 0;
       

        using (var writer = new StreamWriter(fullpath, append: true))
        {
            // Write header only once
            if (!fileExists)
            {
                writer.WriteLine("PID #, Cohort,Q1,Q2,Q3,Q4,Q5,Q6,Q7,Q8,Q9,Q10,Interaction Presence, Scenario Presence, Display Presence, Total Presence,");
            }
            //trial
            foreach (var answer in answers)
            {
                TPS = (TPS + answer) / 10;
            }
            string row = $"{PIDS}, {cohort}," + string.Join(",", answers) + $",{IPS}, {SPS}, {DPS}, {TPS}";
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
        Application.Quit();
        //FPSQuestionnaire.Instance.ResetAnswers();
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
