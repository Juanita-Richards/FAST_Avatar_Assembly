using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Timers;
using UnityEngine;
using XRTLogging;
public class HandleBuildCompletion : MonoBehaviour
{

    public List<GameObject> gameObjectsToDisableAfterTime;
    public List<GameObject> gameObjectsToHideImmediately;
    public ExperimentManager experimentManager;
    public float secondsUntilDisableGameObjects = 20f; //was 10 seconds
    public GameObject VEQ, ISDK_Ray;
    //public Timer timerLog;
    //public TimerCountUp timerBC;
   

    public void DoComplete()
    {
        //motionLogger.StopTrackingOff();
        foreach (var g in gameObjectsToHideImmediately)
        {
            HideMesh(g);
        }
        StartCoroutine(WaitThenHideSubstructure());

        
    }

    private IEnumerator WaitThenHideSubstructure()
    {
        yield return new WaitForSeconds(secondsUntilDisableGameObjects);
        experimentManager.StopLogging(); 
        //timerLog.TurnTImerOff();
        //timerBC.TurnTimerOn();
        VEQ.gameObject.SetActive(true);
        ISDK_Ray.gameObject.SetActive(true);
        
        foreach (var g in gameObjectsToDisableAfterTime)
        {
            g.SetActive(false);

            //call the end of the tracking data
           

            
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
