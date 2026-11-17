using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildGrader : MonoBehaviour
{
    [Header("Build Containers")]
    [SerializeField] private Transform targetBuildRoot;
    [SerializeField] private Transform participantBuildRoot;
    [Header("Shared Grading Settings")]
    [SerializeField] private FASTSharedGrading.Settings gradingSettings = new FASTSharedGrading.Settings();
    [SerializeField] private bool verboseLog = true;

    [Serializable] public class GradeResult
    {
        public int targetPieceCount, participantPieceCount, matchedPieceCount, missingPieces, extraPieces;
        public int targetStructuralPositions, correctStructuralPositions;
        public float structureAccuracy;
        public int targetTubeCount, correctTubeColors;
        public float colorAccuracy;
        public int targetConnectorCount, correctConnectors;
        public float connectorAccuracy;
        public int orientationTargetCount, correctOrientations;
        public float orientationAccuracy;
        public float structuralOrientationErrorDegrees, structuralOrientationToleranceDegrees, structuralOrientationScore;
        public bool structuralOrientationCorrect;
        public int targetConnectionCount, correctConnections;
        public float connectionAccuracy;
        public bool unitsComparable;
        public bool rotationAlignmentApplied;
        public float rotationAlignmentDegrees;
        public int alignmentCandidatesEvaluated, winningOrientationIndex;
        public string winningTargetAnchor, winningParticipantAnchor;
        public string winningTargetAnchorPath, winningParticipantAnchorPath;
        public int winningTargetConnectorIndex, winningParticipantConnectorIndex;
        public int winningTargetChildIndex, winningParticipantChildIndex;
        public FASTSharedGrading.Result sharedResult;
    }

    public GradeResult GradeBuild()
    {
        var r = FASTSharedGrading.Grade(targetBuildRoot, participantBuildRoot, gradingSettings);
        if (r == null)
        {
            Debug.LogError("BuildGrader: one of the build roots is unassigned.");
            return null;
        }
        FASTSharedGrading.Log(r, verboseLog);
        // Grade() returns a Result even when the build could not be framed; check the flag
        // before reading any of the percentages, which are all zero in that case.
        if (!r.unitsComparable)
            Debug.LogWarning("BuildGrader: result is NOT GRADED (unitsComparable is false). " +
                "The percentages below are meaningless.");
        return new GradeResult {
            targetPieceCount=r.targetCount, participantPieceCount=r.participantCount,
            matchedPieceCount=r.matches.Count, missingPieces=r.missing, extraPieces=r.extra,
            targetStructuralPositions=r.targetCount, correctStructuralPositions=r.correctPositions,
            structureAccuracy=r.PositionPercent,
            targetTubeCount=r.targetTubes, correctTubeColors=r.correctColors,
            colorAccuracy=r.ColorPercent,
            targetConnectorCount=r.targetConnectors, correctConnectors=r.correctConnectors,
            connectorAccuracy=r.ConnectorPercent,
            orientationTargetCount=r.targetCount,correctOrientations=r.correctOrientations,
            orientationAccuracy=r.OrientationPercent,
            structuralOrientationErrorDegrees=r.structuralAxisError,
            structuralOrientationToleranceDegrees=gradingSettings.axisToleranceDegrees,
            structuralOrientationCorrect=r.structuralAxisCorrect,
            structuralOrientationScore=r.axisEvaluated&&gradingSettings.axisToleranceDegrees>0?
                Mathf.Clamp01(1f-r.structuralAxisError/gradingSettings.axisToleranceDegrees):
                (r.structuralAxisCorrect?1f:0f),
            targetConnectionCount=r.targetConnections, correctConnections=r.correctConnections,
            connectionAccuracy=r.ConnectionPercent,
            unitsComparable=r.unitsComparable,
            rotationAlignmentApplied=r.anchorAlignmentApplied,
            alignmentCandidatesEvaluated=r.alignmentCandidatesEvaluated,
            winningOrientationIndex=r.winningOrientationIndex,
            winningTargetAnchor=r.winningTargetAnchor,
            winningParticipantAnchor=r.winningParticipantAnchor,
            winningTargetAnchorPath=r.winningTargetAnchorPath,
            winningParticipantAnchorPath=r.winningParticipantAnchorPath,
            winningTargetConnectorIndex=r.winningTargetConnectorIndex,
            winningTargetChildIndex=r.winningTargetChildIndex,
            winningParticipantConnectorIndex=r.winningParticipantConnectorIndex,
            winningParticipantChildIndex=r.winningParticipantChildIndex,
            rotationAlignmentDegrees=Quaternion.Angle(Quaternion.identity,r.alignmentRotation),
            sharedResult=r
        };
    }
    [ContextMenu("Grade Structure")]
    public void GradeFromInspector() { GradeBuild(); }
}
