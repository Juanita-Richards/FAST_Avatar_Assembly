using UnityEngine;

public class ReplaceDropped : MonoBehaviour
{
    [Header("Reset Settings")]
    public float minYPosition = -1f;

    public Vector3 startPosition;
    public Quaternion startRotation;
    private Rigidbody rb;

    private bool isResetting = false;

    void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        if (isResetting)
            return;

        if (transform.position.y < minYPosition)
        {
            Debug.Log($"{gameObject.name} fell too far. Current Y: {transform.position.y}");
            ResetObject();
        }
    }

    private void ResetObject()
    {
        isResetting = true;

        if (rb != null)
        {
            // Stop physics movement first
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            // Temporarily make it kinematic so physics does not fight the reset
            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;

            transform.position = startPosition;
            transform.rotation = startRotation;

            rb.position = startPosition;
            rb.rotation = startRotation;

            rb.isKinematic = wasKinematic;
            rb.Sleep();
        }
        else
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }

        isResetting = false;
    }
}