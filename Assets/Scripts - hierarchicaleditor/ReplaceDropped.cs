using NUnit.Framework;
using PlayStructure;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.PlayerLoop;

public class ReplaceDropped : MonoBehaviour
{
    //[Header("Reset Settings")]
    public float minYPosition = -1f;
    public float maxYPosition = 0.2f;

    public Vector3 startPosition;
    public Quaternion startRotation;
    private Rigidbody rb;

    private Vector3 snappedPosition;
    private Quaternion snappedRotation;
    private bool hasSnappedResetPoint = false;

    private BuildingPiece buildingPiece;

    private bool wasAttachedLastFrame = false;
    private bool isResetting = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        ///<Summary>
        /// The below is good to use when you want certain prefabs (i.e., screws)
        /// to ignore the Building Piece script
        /// </Summary>
        /// 

        TryGetComponent(out buildingPiece);


    }

    private void Start()
    {
        startPosition = transform.position;
        startRotation = transform.rotation;

    }

    private void FixedUpdate()
    {
        if (isResetting)
        {
            return;
        }

        bool isAttchedNow = isAttachedBuildingPiece();

        // Only save snapped position when it changes from not attached to attached.
        // This prevents saving the falling position as the new reset point.

        if (isAttchedNow && !wasAttachedLastFrame && !HasFallenFar())
        {
            StartCoroutine(SaveSnapPointAfterPhysics());
        }

        wasAttachedLastFrame = isAttchedNow;

        if (HasFallenFar())
        {
            ResetObject();
        }
    }

    private bool HasFallenFar()
    {
        return transform.position.y < minYPosition || transform.position.y < maxYPosition;
    }

    private IEnumerator SaveSnapPointAfterPhysics()
    {

        yield return new WaitForFixedUpdate();

        if (isAttachedBuildingPiece() && !HasFallenFar())
        {
            SaveCurrentSnappedResetPoint();

        }
    }


    private void SaveCurrentSnappedResetPoint()
    {
        if (buildingPiece == null)
        {
            return;
        }

        snappedPosition = transform.position;
        snappedRotation = transform.rotation;

        hasSnappedResetPoint = true;


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

            if (attachPoint.currentAttachState == AttachPoint.AttachState.LOOSE_LOCK || attachPoint.currentAttachState == AttachPoint.AttachState.FULL_LOCK || attachPoint.currentAttachState == AttachPoint.AttachState.PARTIAL_LOCK)
            {
                //GetComponent<ReplaceDropped>().enabled = false;
                return true;
            }
        }

        return false;
    }

    private void ResetObject()
    {
        isResetting = true;

        Vector3 targetPosition;
        Quaternion targetRotation;

        if (hasSnappedResetPoint)
        {
            targetPosition = snappedPosition;
            targetRotation = snappedRotation;
        }

        else
        {
            targetPosition = startPosition;
            targetRotation = startRotation;
        }

        MoveObjectTo(targetPosition, targetRotation);

    }
    private void MoveObjectTo(Vector3 targetPosition, Quaternion targetRotation)
    {

        if (rb != null)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = Vector3.zero;
#else
            rb.velocity = Vector3.zero;
#endif
            rb.angularVelocity = Vector3.zero;

            bool wasKinematic = rb.isKinematic;
            rb.isKinematic = true;

            transform.position = targetPosition;
            transform.rotation = targetRotation;

            rb.position = targetPosition;
            rb.rotation = targetRotation;

            Physics.SyncTransforms();

            rb.isKinematic = wasKinematic;
            rb.Sleep();
        }
        else
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
            Physics.SyncTransforms();
        }
    }
}

