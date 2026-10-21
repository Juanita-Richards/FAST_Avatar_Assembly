using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using UnityEngine;
using UnityEngine.VFX;
using XRTLogging;
public class HandleBuildCompletion : MonoBehaviour
{

    public List<GameObject> gameObjectsToDisableAfterTime;
    public List<GameObject> gameObjectsToHideImmediately;

    public ExperimentManager experimentManager;
    public float secondsUntilDisableGameObjects = 60f; //was 10 seconds

    public GameObject Questionnaire;
    public Timer timerLog;
    public GameObject timerVisual;
    public Motion motionLogger;
    
    private bool taskCompleted = false;

    public GameObject interactionCue;

    private void OnEnable()
    {
        if (timerLog != null)
            timerLog.OnTimerExpired += HandleTimerExpired;
    }


    private void OnDisable()
    {
        if (timerLog != null)
            timerLog.OnTimerExpired -= HandleTimerExpired;
    }

    public void DoComplete()
    {

        if (taskCompleted) return;

        taskCompleted = true;

        //participant finishes normally

        timerLog.TurnTimerOff();

        motionLogger.StopTrackingOff();

        foreach (var g in gameObjectsToHideImmediately)
        {
            HideMesh(g);
        }

        StartCoroutine(WaitThenHideSubstructure());

        /// <Old>
        /// timerLog.TurnTimerOn();
        /// timerVisual.gameObject.SetActive(true);
        /// </Old>
    }


    private void HandleTimerExpired()
    {
        //They finish before 3 minutes
        if (taskCompleted) return;


        Debug.Log("TIME LIMIT REACHED - Participant did not complete structure");

        taskCompleted = true;

        //Stop interaction cue
        if (interactionCue != null) interactionCue.SetActive(false);

        //Stop tracking 
        motionLogger.StopTrackingOff();
        experimentManager.StopLogging();

        //Hide stuff
        foreach (var g in gameObjectsToHideImmediately)
        {
            HideMesh(g);
        }

        foreach (var g in gameObjectsToDisableAfterTime)
        {
            g.SetActive(false);
        }

        Questionnaire.SetActive(true);
    }

    private IEnumerator WaitThenHideSubstructure()
    {
        yield return new WaitForSeconds(secondsUntilDisableGameObjects);
        experimentManager.StopLogging();

        //timerLog.TurnTimerOff();
        //timerVisual.gameObject.SetActive(false);

        Questionnaire.gameObject.SetActive(true);
       
        foreach (var g in gameObjectsToDisableAfterTime)
        {
            g.SetActive(false);

        }


    }
    private void HideMesh(GameObject g)
    {
        var meshRenderers =  g.GetComponentsInChildren<MeshRenderer>();
        foreach (var m in meshRenderers)
        {
            m.enabled = false;
        }
    }

}
