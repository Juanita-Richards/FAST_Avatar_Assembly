using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ReplaceDropped : MonoBehaviour
{


    [Header("Reset Settings")]
    public float minYPosition = 0.2f;          // If object falls below this height, start timer
    public float secondsBeforeReset = 1f;     // How long it must stay below before resetting

    [Header("Optional")]
    public bool onlyResetWhenNotHeld = true;  // Uses parent check as a simple held check
    public bool restoreOriginalParent = true;

    public Vector3 startPosition;
    public Quaternion startRotation;
    public Transform startParent;

    private Rigidbody rb;
    private float timeBelowMin = 0f;

    void Awake()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;
        startParent = transform.parent;

        rb = GetComponent<Rigidbody>();
    }

    void Update()
    {
        // If the object is currently being held by a controller/hand,
        // it may have a different parent. Do not reset while held.
        if (onlyResetWhenNotHeld && transform.parent != startParent)
        {
            timeBelowMin = 0f;
            return;
        }

        // If object is below the floor/drop height, start counting.
        if (transform.position.y < minYPosition)
        {
            timeBelowMin += Time.deltaTime;
        }
        else
        {
            timeBelowMin = 0f;
        }

        // Reset after it has been below the minimum height long enough.
        if (timeBelowMin >= secondsBeforeReset)
        {
            ResetObject();
        }
    }

    private void ResetObject()
    {
        if (restoreOriginalParent)
        {
            transform.SetParent(startParent);
        }

        if (rb != null)
        {
            // Stop all movement first.
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            // Move it back.
            rb.position = startPosition;
            rb.rotation = startRotation;

            rb.Sleep();
        }
        else
        {
            transform.position = startPosition;
            transform.rotation = startRotation;
        }

        timeBelowMin = 0f;
    }
}
