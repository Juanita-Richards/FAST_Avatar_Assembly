using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using XRTLogging;
public class HandleBuildCompletion : MonoBehaviour
{

    public List<GameObject> gameObjectsToDisableAfterTime;
    public List<GameObject> gameObjectsToHideImmediately;
    public ExperimentManager experimentManager;
    public float secondsUntilDisableGameObjects = 20f; //was 10 seconds

    public void DoComplete()
    {
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
        Motion.Instance.StopTrackingOff(); //may need to remove
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
