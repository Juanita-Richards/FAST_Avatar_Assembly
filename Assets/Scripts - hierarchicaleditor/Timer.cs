using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
public class Timer : MonoBehaviour
{
    public TextMeshProUGUI timer, timerVisualtext;
    public float timerCountDown;
    public bool timeStarted;
    public float reset;
    //public float minimumTime = 2f;
    //public GameObject timerVisual;

    // Start is called before the first frame update
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

            /// <Here>
            /// The below could be used as well
            /// </Here>
            //timerCountDown -= Time.deltaTime;
            //DisplayTime(timerCountDown);

        }
    }

    public void TurnTimerOn()
    {
        timeStarted = true;
        
    }
    
    public void TurnTimerOff()
    {
        timeStarted = false;
    }

    void DisplayTime(float time)
    {
        float minutes = Mathf.FloorToInt(time / 60);
        float seconds = Mathf.FloorToInt(time  % 60);

        timer.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        timerVisualtext.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
    IEnumerator TimeCount()
    {
        timerCountDown -= Time.deltaTime;

        ////This is disabled for now
        //if (timerCountDown <= 120)
        //    timerVisual.SetActive(true);

        if (timerCountDown <= 0)
        {
            timerCountDown = 0f;
        }

        DisplayTime(timerCountDown);
        yield return null;
    }
}
