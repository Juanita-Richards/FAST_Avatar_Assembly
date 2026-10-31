using UnityEngine;
using UnityEngine.UI;

public class pESQButton : MonoBehaviour
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
        pESQQuestionnaire.Instance.LogAnswer(questionIndex, answerValue);
    }
}
