using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Oculus.Interaction;

public class Timer : MonoBehaviour
{
    public TextMeshProUGUI timer, timerVisualtext;
    public float timerCountDown;
    public bool timeStarted;
    public float reset;
    public GameObject structureImage, timerVisual;
    //public EventSystem eventSystem;
    //public GameObject pcm;
    public EventSystem pcm_event;

    public event Action OnTimerExpired;

    private bool timerExpired;
   
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

        if (!timeStarted) return;

        timerCountDown -= Time.deltaTime;


        if (timerCountDown <= 0)
        {
            timerCountDown = 0f;

            structureImage.SetActive(false);
            timerVisual.SetActive(false);
            //pcm.gameObject.GetComponent<PointableCanvasModule>().enabled = false;
            pcm_event.GetComponent<PointableCanvasModule>().enabled = true;

            TurnTimerOff();

            //Fire only once
            if (!timerExpired)
            {
                timerExpired = true;
                OnTimerExpired?.Invoke();
            }

        }
        DisplayTime(timerCountDown);

        //if (timeStarted) StartCoroutine(TimeCount());

    }

    public void TurnTimerOn()
    {
        timeStarted = true;
        timerExpired = false;
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
            structureImage.SetActive(false);
            timerVisual.SetActive(false);
            TurnTimerOff();
           
        }

        //check if !HandleBuildCompletion.DoComplete() && timer == 0
        //Activate the spreadsheet

        DisplayTime(timerCountDown);
        yield return null;
    }
}
