using UnityEngine;
using TMPro;
using System;
using System.Collections.Generic;
using PlayStructure;
using Unity.VisualScripting;

public class BuildTracker : MonoBehaviour
{
    public TMP_Dropdown Builds;

    public static BuildTracker Instance; //not really needed

   

    [SerializeField] private List<ScriptableInstructions> scriptableBuilds = new List<ScriptableInstructions>();

    private void Awake()
    {
        Instance = this;
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (Builds != null)
        {
            Builds.onValueChanged.AddListener(ChangeScriptableInstructions);
        }
    }

   

    public void ChangeScriptableInstructions(int dropdownIndex)
    {
        if (PlayInstructions.instance == null)
        {
            Debug.LogError("Play instructions instance not found");
            return;
        }

        if (dropdownIndex < 0 || dropdownIndex >= scriptableBuilds.Count)
        {
            Debug.LogError($"DropIndex index {scriptableBuilds[dropdownIndex]} does not match the Scriptable list");
           
        }

        PlayInstructions.instance.SetInstructions(scriptableBuilds[dropdownIndex]);
        

        Debug.Log($"Changed instructions to: {scriptableBuilds[dropdownIndex].name}"); 
        Debug.Log($"Scriptable Count: {scriptableBuilds.Count}");
        Debug.Log($"DropIndex {dropdownIndex}");

    }

}
