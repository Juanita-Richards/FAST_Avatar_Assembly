using System;
using System.Collections.Generic;
using UnityEngine;

public class BuildGrader2 : MonoBehaviour
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
        public bool rotationAlignmentApplied;
        public float rotationAlignmentDegrees;
        public FASTSharedGrading.Result sharedResult;
    }

    public GradeResult GradeBuild()
    {
        var r = FASTSharedGrading.Grade(targetBuildRoot, participantBuildRoot, gradingSettings);
        if (r == null) return null;
        FASTSharedGrading.Log(r, verboseLog);
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
            rotationAlignmentApplied=gradingSettings.alignWholeBuildRotation,
            rotationAlignmentDegrees=Quaternion.Angle(Quaternion.identity,r.alignmentRotation),
            sharedResult=r
        };
    }
    [ContextMenu("Grade Structure")]
    public void GradeFromInspector() { GradeBuild(); }
}
