using Oculus.Platform.Models;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using static UnityEngine.ParticleSystem;


public class VEQuestionnaire : MonoBehaviour
{
    public static VEQuestionnaire Instance;
    private string logPath;
    private StringBuilder sb;

    
    public ParticipantLog participantLog;
    

    [SerializeField] string PIDS = ""; 

    private int[] answers = new int[12];
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

    private float Average(int start, int count)
    {

        float sum = 0;
        int answered = 0;

        for (int i = start; i < start + count; i++)
        {
            if (answers[i] != 0)
            {
                sum += answers[i];
                answered++;
            }
        }

        if (answered == 0) return 0f;
        return sum / answered; //return stops the execution of a function and returns a value
    }


    public void VEQQuestionnaireToCSV()
    {
        float average_one = Average(0, 4);
        float average_two = Average(4, 4);
        float average_three = Average(8, 4);

        
        var cohort = participantLog.cohort_dropdown.options[participantLog.cohort_dropdown.value].text;

        PIDS = participantLog.PID_dropdown.options[participantLog.PID_dropdown.value].text;
        logPath = $"FASTA_VEQ.csv";

        //Create directory FIRST
        string directoryPath = Path.Combine(
            "Questionnaire",
            "VEQ"
        );

        Directory.CreateDirectory(directoryPath);

        // ✅ Full absolute file path
        string fullpath = Path.Combine(directoryPath, logPath);

        bool fileExists = File.Exists(fullpath);

        using (var writer = new StreamWriter(fullpath, append: true))
        {
            if (!fileExists)
            {
                writer.WriteLine(
                    "PID #,Cohort ,AC1,AC2,AC3,AC4,AC_AVERAGE," +
                    "CO1,CO2,CO3,CO4,CO_AVERAGE," +
                    "CH1,CH2,CH3,CH4,CH_AVERAGE"
                );
            }

            string row =
                //$"{trial + 1}," +
                $"{PIDS}, {cohort}," +
                $"{answers[0]},{answers[1]},{answers[2]},{answers[3]},{average_one}," +
                $"{answers[4]},{answers[5]},{answers[6]},{answers[7]},{average_two}," +
                $"{answers[8]},{answers[9]},{answers[10]},{answers[11]},{average_three}";

            writer.WriteLine(row);
        }

        Debug.Log($"Saved VEQ CSV to: {fullpath}");
    }

    public void ResetAnswers()
    {
        for (int i = 0; i < answers.Length; i++)
            answers[i] = 0;

        Debug.Log("Answers reset for new round.");
    }

    public void FinishQuestionnaire()
    {
        VEQuestionnaire.Instance.VEQQuestionnaireToCSV();
        //VEQuestionnaire.Instance.ResetAnswers();
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
