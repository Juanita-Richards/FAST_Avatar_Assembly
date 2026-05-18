using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SkinToneMapping : MonoBehaviour
{
    //public ConditionFlow conditionAvatar;

    ////List of buttons and  //List of buttons and skin colors
    //public List<Button> buttons;
    //public List<Material> F_materials, M_materials;

    //public GameObject Human, leftHand, rightHand;

    //[SerializeField]
    ////private SkinnedMeshRenderer[] playerRenderer;
    //public List<SkinnedMeshRenderer> playerRenderers = new List<SkinnedMeshRenderer>();
    //public List<Material> femaleMaterials = new List<Material>();
    //public List<Material> maleMaterials = new List<Material>();
    

    //public string buttonName = "";


    //public void LoadSkinnedMesh()
    //{
    //    Human = conditionAvatar.selectedAvatar; //set the avatar from the condition flow
    //    Debug.Log($"Human name: {Human.name}");
    //    //playerRenderer = Human.GetComponent<SkinnedMeshRenderer>();

    //    //leftHand = GameObject.Find(Human.name + "/Hands");
    //    //rightHand = GameObject.Find(Human.name + "/Hands");

    //    //playerRenderers.Add(leftHand.GetComponent<SkinnedMeshRenderer>());
    //    //playerRenderers.Add(rightHand.GetComponent<SkinnedMeshRenderer>());

    //    //Fastest way I fixed it
    //    if (Human.name == "Female")
    //    {
    //        leftHand = GameObject.Find(Human.name + "/Hands");
    //        rightHand = GameObject.Find(Human.name + "/Hands");

    //        playerRenderers.Add(leftHand.GetComponent<SkinnedMeshRenderer>());
    //        playerRenderers.Add(rightHand.GetComponent<SkinnedMeshRenderer>());
    //    }


    //    else if (Human.name == "Male")
    //    {
    //        leftHand = GameObject.Find(Human.name + "/Hands" + "/Left Hand");
    //        rightHand = GameObject.Find(Human.name + "/Hands" + "/Right Hand");

    //        playerRenderers.Add(leftHand.GetComponent<SkinnedMeshRenderer>());
    //        playerRenderers.Add(rightHand.GetComponent<SkinnedMeshRenderer>());
    //    }


    //    foreach (var item in buttons)
    //    {
    //        var btn = item;
    //        btn.onClick.AddListener(() => ChangePlayerSkin(btn));
    //    }
    //}

    //public void ChangePlayerSkin(Button button)
    //{
    //    buttonName = button.name;
    //    //Grab buttons color
    //    var img = button.GetComponent<Image>();
    //    if (img == null)
    //    {
    //        Debug.LogWarning("Button has no Image component");
    //        return;
    //    }

    //    //TODO: No longer using color, change to material
    //    var color = img.color;
    //    foreach (var renderer in playerRenderers)
    //    {
    //        renderer.material.color = color;
    //    }
    //    //playerRenderer.material.color = color;

    //}

    //public void ChangeMaterialTexture(Button button)
    //{
    //    buttonName = button.name;

         
    //}
}
