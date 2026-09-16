using PlayStructure;
using UnityEditor.PackageManager;
using UnityEngine;
using UnityEngine.Events;

public class TurnOffRespawn : MonoBehaviour
{
   
    private BuildingPiece buildingPiece;
    public GameObject Hint;

    /// <summary>
    /// Respawn will happen when the transform moves below this World Y position.
    /// </summary>
    [SerializeField]
    [Tooltip("Respawn will happen when the transform moves below this World Y position.")]
    private float _yThresholdForRespawn;

    /// <summary>
    /// UnityEvent triggered when a respawn occurs.
    /// </summary>
    [SerializeField]
    [Tooltip("UnityEvent triggered when a respawn occurs.")]
    private UnityEvent _whenRespawned = new UnityEvent();

    /// <summary>
    /// If the transform has an associated rigidbody, make it kinematic during this
    /// number of frames after a respawn, in order to avoid ghost collisions.
    /// </summary>
    [SerializeField]
    [Tooltip("If the transform has an associated rigidbody, make it kinematic during this number of frames after a respawn, in order to avoid ghost collisions.")]
    private int _sleepFrames = 0;

    public UnityEvent WhenRespawned => _whenRespawned;

    // cached starting transform
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Vector3 _initialScale;

#pragma warning restore CS0618 // Type or member is obsolete
    private Rigidbody _rigidBody;
    private int _sleepCountDown;

    protected virtual void OnEnable()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        _initialScale = transform.localScale;
#pragma warning restore CS0618 // Type or member is obsolete
        _rigidBody = GetComponent<Rigidbody>();
    }

    protected virtual void Update()
    {
        if (transform.position.y < _yThresholdForRespawn)
        {
            Respawn();
        }
    }

    protected virtual void FixedUpdate()
    {
        if (_sleepCountDown > 0)
        {
            if (--_sleepCountDown == 0)
            {
                _rigidBody.isKinematic = false;
            }
        }

        bool isAttached = isAttachedBuildingPiece();
    }

    private bool isAttachedBuildingPiece()
    {

        if (buildingPiece == null)
            return false;

        if (buildingPiece.attachPoints == null)
            return false;

        foreach (AttachPoint attachPoint in buildingPiece.attachPoints)
        {
            if (attachPoint == null) continue;
            Debug.Log("Turnedd OFF");
            if (attachPoint.currentAttachState == AttachPoint.AttachState.LOOSE_LOCK || attachPoint.currentAttachState == AttachPoint.AttachState.FULL_LOCK || attachPoint.currentAttachState == AttachPoint.AttachState.PARTIAL_LOCK)
            {
                this.GetComponent<TurnOffRespawn>().enabled = false;
                this.GetComponent<ReplaceDropped>().enabled = false;
            }
        }

        return false;
    }

    public void Respawn()
    {
        transform.position = Hint.transform.position;
        transform.rotation = Hint.transform.rotation;
        transform.localScale = _initialScale;

        if (_rigidBody)
        {
#pragma warning disable CS0618 // Type or member is obsolete
            _rigidBody.linearVelocity = Vector3.zero;
#pragma warning restore CS0618 // Type or member is obsolete
            _rigidBody.angularVelocity = Vector3.zero;

            if (!_rigidBody.isKinematic && _sleepFrames > 0)
            {
                _sleepCountDown = _sleepFrames;
                _rigidBody.isKinematic = true;
            }
        }

        _whenRespawned.Invoke();
    }

}
