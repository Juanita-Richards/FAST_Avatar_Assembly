using UnityEngine;

public class MetaHandReference : MonoBehaviour
{
    public Transform leftHand;
    public Transform rightHand;

    public static MetaHandReference Instance { get; private set; }

    private void Awake()
    {
        Instance = this;
    }
}
