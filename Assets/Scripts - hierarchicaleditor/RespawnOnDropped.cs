//using UnityEngine;
//using UnityEngine.Events;

//public class RespawnOnDropped : MonoBehaviour
//{

//    /// <summary>
//    /// Respawn will happen when the transform moves below this World Y position.
//    /// </summary>
//    [SerializeField]
//    [Tooltip("Respawn will happen when the transform moves below this World Y position.")]
//    private float _yThresholdForRespawn;


//    /// <summary>
//    /// UnityEvent triggered when a respawn occurs.
//    /// </summary>
//    [SerializeField]
//    [Tooltip("UnityEvent triggered when a respawn occurs.")]
//    private UnityEvent _whenRespawned = new UnityEvent();

//    /// <summary>
//    /// If the transform has an associated rigidbody, make it kinematic during this
//    /// number of frames after a respawn, in order to avoid ghost collisions.
//    /// </summary>
//    [SerializeField]
//    [Tooltip("If the transform has an associated rigidbody, make it kinematic during this number of frames after a respawn, in order to avoid ghost collisions.")]
//    private int _sleepFrames = 0;

//    public UnityEvent WhenRespawned => _whenRespawned;

//    // cached starting transform
//    private Vector3 _initialPosition;
//    private Quaternion _initialRotation;
//    private Vector3 _initialScale;

//#pragma warning disable CS0618 // Type or member is obsolete
//    //private TwoGrabFreeTransformer[] _freeTransformers;
//#pragma warning restore CS0618 // Type or member is obsolete
//    private Rigidbody _rigidBody;
//    private int _sleepCountDown;



//}
