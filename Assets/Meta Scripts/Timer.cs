using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class Timer : MonoBehaviour
{
    public TextMeshProUGUI timer;
    public float timerCountUp;
    public bool timeStarted;
    public float reset;


    // Start is called before the first frame update
    void Start()
    {
        timer.text = timerCountUp.ToString();
    }

    // Update is called once per frame
    void Update()
    {
        if (timeStarted)
        {
            StartCoroutine(TimeCount());
        }
    }

    public void TurnBoolOn()
    {
        timeStarted = true;
    }
    
    public void TurnBoolOff()
    {
        timeStarted = false;
    }

    void DisplayTime(float time)
    {
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time  % 60);

        timer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    IEnumerator TimeCount()
    {
        timerCountUp += Time.deltaTime;

        DisplayTime(timerCountUp);
        yield return null;
    }
}
