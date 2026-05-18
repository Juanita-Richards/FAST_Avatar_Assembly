using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class VEQButton : MonoBehaviour
{
    public int questionIndex;
    public int answerValue;

    public void OnEnable()
    {
        Button btn = GetComponent<Button>();
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnButtonClick);
    }

    public void OnButtonClick()
    {
        //VEQuestionnaire.Instance.LogAnswer(questionIndex, answerValue);
    }
}
