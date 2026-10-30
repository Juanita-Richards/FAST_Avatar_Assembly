using TMPro;
using UnityEngine;
using System.Collections;

public class TimerCountUp : MonoBehaviour
{

    public TextMeshProUGUI timer;
    public float timerCountDown;
    public bool timeStarted;
    
 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        timer.text = timerCountDown.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (timeStarted)
        {
            StartCoroutine(TimeCount());
        }

    }
    public void TurnTimerOn()
    {
        timeStarted = true;

    }

    public void TurnTImerOff()
    {
        timeStarted = false;
    }
    void DisplayTime(float time)
    {
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time % 60);

        timer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
       
    }
    IEnumerator TimeCount()
    {
        timerCountDown += Time.deltaTime;


        DisplayTime(timerCountDown);
        yield return null;
    }
}
