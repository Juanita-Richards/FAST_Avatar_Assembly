using Oculus.Interaction.Input;
using UnityEngine;
using UnityEngine.UI;

public class Highlight : MonoBehaviour
{
    public GameObject highlighter;
    public Toggle HandsToggle;
    public GameObject highlighted;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       
    }

    public void HighLight(GameObject thingToHightlight)
    {
        highlighter.SetActive(true);
        highlighter.GetComponent<MeshFilter>().mesh = thingToHightlight.GetComponent<MeshFilter>().mesh;
        highlighter.transform.parent = thingToHightlight.transform;
        highlighter.transform.localPosition = Vector3.zero;
        highlighter.transform.localRotation = Quaternion.identity;
    }

    public void OnHighLight()
    {
        if (!HandsToggle.isOn)
        {
            HighLight(highlighted);
        }
    }
}
