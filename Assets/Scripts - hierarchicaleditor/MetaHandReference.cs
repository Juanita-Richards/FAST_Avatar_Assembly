using UnityEngine;

namespace PlayStructure
{
    public class MetaHandReference : MonoBehaviour
    {
        public static MetaHandReference Instance { get; private set; }

        [Header("Assign the real moving hand/controller anchors here")]
        public Transform leftAnchor;
        public Transform rightAnchor;

        private void Awake()
        {
            Instance = this;
        }
    }
}