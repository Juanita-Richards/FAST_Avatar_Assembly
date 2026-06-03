using UnityEngine;

public class ChangePhysicalObjects : MonoBehaviour
{
    public GameObject physObjC, physObjB;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (PlayInstructions.instance == null)
        {
            Debug.LogError("Play instructions instance not found");
            return;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (PlayInstructions.instance._instructions.name == "Int-B")
        {
            physObjB.gameObject.SetActive(true);
            Debug.Log($"Found it");
            physObjC.gameObject.SetActive(false);
        }
        else
        {
            physObjC.gameObject.SetActive(true);
            physObjB.gameObject.SetActive(false);
        }
    }


    public void ChangeObjects()
    {
       
    }
}
