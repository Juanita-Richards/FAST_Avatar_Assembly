using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using PlayStructure;

public class OptimalStructureComparison : MonoBehaviour
{
    // ============================================================
    // INSPECTOR
    // ============================================================

    [Header("Builds")]
    [SerializeField]
    private Transform targetBuildRoot;

    [SerializeField]
    private Transform participantBuildRoot;


    [Header("Structure Grading")]
    [Tooltip("Maximum distance from the target position for a piece to be position-correct.")]
    [SerializeField]
    private float structurePositionTolerance = 0.05f;

    [Tooltip("Maximum orientation difference in degrees for a piece to be orientation-correct.")]
    [SerializeField]
    private float orientationToleranceDegrees = 10f;


    [Header("Output")]
    [SerializeField]
    private string comparisonFolderName = "FAST_Comparisons";

    [SerializeField]
    private bool addTimestampToFileName = false;


    // ============================================================
    // DATA CLASSES
    // ============================================================

    [Serializable]
    public class PieceMatch
    {
        public BuildingPiece targetPiece;
        public BuildingPiece participantPiece;

        public Vector3 targetPosition;
        public Quaternion targetRotation;

        public Vector3 originalParticipantPosition;
        public Quaternion originalParticipantRotation;

        public Vector3 alignedParticipantPosition;
        public Quaternion alignedParticipantRotation;

        public float positionError;
        public float orientationError;

        public bool positionCorrect;
        public bool orientationCorrect;

        public bool structuralPositionCorrect;

        public bool colorCorrect;
        public bool connectorCorrect;
    }


    [Serializable]
    public class ComparisonResult
    {
        // Counts
        public int targetPieceCount;
        public int participantPieceCount;
        public int matchedPieceCount;
        public int missingPieceCount;
        public int extraPieceCount;

        // Structure
        public int targetStructuralPositions;
        public int correctStructuralPositions;
        public float structureAccuracy;

        public float meanPositionError;
        public float rmsPositionError;
        public float normalizedStructureError;

        // Color
        public int targetTubeCount;
        public int correctTubeColors;
        public float colorAccuracy;

        // Connectors
        public int targetConnectorCount;
        public int correctConnectors;
        public float connectorAccuracy;

        // Orientation
        public int orientationTargetCount;
        public int correctOrientations;
        public float orientationAccuracy;

        // Connections
        public int targetConnectionCount;
        public int correctConnections;
        public float connectionAccuracy;

        // Alignment
        public Quaternion alignmentRotation;
        public Vector3 alignmentTranslation;

        // Matches
        public List<PieceMatch> matches =
            new List<PieceMatch>();
    }


    private class PiecePair
    {
        public BuildingPiece target;
        public BuildingPiece participant;

        public PiecePair(
            BuildingPiece targetPiece,
            BuildingPiece participantPiece)
        {
            target = targetPiece;
            participant = participantPiece;
        }
    }


    private class AssignmentSolution
    {
        public float totalDistance =
            float.PositiveInfinity;

        public List<PiecePair> pairs =
            new List<PiecePair>();
    }


    private struct TargetConnection
    {
        public BuildingPiece tube;
        public BuildingPiece connector;

        public TargetConnection(
            BuildingPiece tubePiece,
            BuildingPiece connectorPiece)
        {
            tube = tubePiece;
            connector = connectorPiece;
        }
    }


    // ============================================================
    // RUN COMPARISON
    // ============================================================

    [ContextMenu("Compare Structures")]
    public void CompareStructures()
    {
        Debug.Log("Compare Structures started.");

        ComparisonResult result =
            GenerateComparison();

        if (result == null)
        {
            Debug.LogError(
                "Comparison failed.");

            return;
        }

        PrintResult(result);
        SaveResult(result);
    }


    // ============================================================
    // GENERATE COMPARISON
    // ============================================================

    public ComparisonResult GenerateComparison()
    {
        // --------------------------------------------------------
        // Validate
        // --------------------------------------------------------

        if (targetBuildRoot == null)
        {
            Debug.LogError(
                "Target Build Root is not assigned.");

            return null;
        }

        if (participantBuildRoot == null)
        {
            Debug.LogError(
                "Participant Build Root is not assigned.");

            return null;
        }


        // --------------------------------------------------------
        // Get pieces
        // --------------------------------------------------------

        List<BuildingPiece> targetPieces =
            GetGradablePieces(targetBuildRoot);

        List<BuildingPiece> participantPieces =
            GetGradablePieces(participantBuildRoot);


        if (targetPieces.Count == 0)
        {
            Debug.LogError(
                "No gradable target pieces found.");

            return null;
        }

        if (participantPieces.Count == 0)
        {
            Debug.LogError(
                "No gradable participant pieces found.");

            return null;
        }


        // ========================================================
        // ALIGNMENT
        //
        // IMPORTANT:
        // No global rotational alignment.
        //
        // The participant's original rotations are preserved.
        // Only translation is removed.
        // ========================================================

        Quaternion alignmentRotation =
            Quaternion.identity;


        Vector3 targetCenter =
            GetCentroid(
                targetPieces,
                targetBuildRoot);

        Vector3 participantCenter =
            GetCentroid(
                participantPieces,
                participantBuildRoot);


        Vector3 alignmentTranslation =
            targetCenter -
            participantCenter;


        // --------------------------------------------------------
        // First correspondence
        // --------------------------------------------------------

        List<PiecePair> pairs =
            FindOptimalCorrespondence(
                targetPieces,
                participantPieces,
                alignmentTranslation);


        // --------------------------------------------------------
        // Improve translation from matched pieces
        // --------------------------------------------------------

        if (pairs.Count > 0)
        {
            alignmentTranslation =
                CalculateTranslation(pairs);

            pairs =
                FindOptimalCorrespondence(
                    targetPieces,
                    participantPieces,
                    alignmentTranslation);

            if (pairs.Count > 0)
            {
                alignmentTranslation =
                    CalculateTranslation(pairs);
            }
        }


        // ========================================================
        // RESULT
        // ========================================================

        ComparisonResult result =
            new ComparisonResult();


        result.targetPieceCount =
            targetPieces.Count;

        result.participantPieceCount =
            participantPieces.Count;

        result.matchedPieceCount =
            pairs.Count;

        result.missingPieceCount =
            Mathf.Max(
                0,
                targetPieces.Count -
                pairs.Count);

        result.extraPieceCount =
            Mathf.Max(
                0,
                participantPieces.Count -
                pairs.Count);

        result.alignmentRotation =
            alignmentRotation;

        result.alignmentTranslation =
            alignmentTranslation;


        // ========================================================
        // CREATE MATCH DATA
        // ========================================================

        foreach (PiecePair pair in pairs)
        {
            PieceMatch match =
                new PieceMatch();


            match.targetPiece =
                pair.target;

            match.participantPiece =
                pair.participant;


            // ----------------------------------------------------
            // Target transform
            // ----------------------------------------------------

            match.targetPosition =
                GetLocalPosition(
                    pair.target,
                    targetBuildRoot);

            match.targetRotation =
                GetLocalRotation(
                    pair.target,
                    targetBuildRoot);


            // ----------------------------------------------------
            // Original participant transform
            // ----------------------------------------------------

            match.originalParticipantPosition =
                GetLocalPosition(
                    pair.participant,
                    participantBuildRoot);

            match.originalParticipantRotation =
                GetLocalRotation(
                    pair.participant,
                    participantBuildRoot);


            // ----------------------------------------------------
            // Generated / aligned participant
            //
            // Translation ONLY.
            // ----------------------------------------------------

            match.alignedParticipantPosition =
                match.originalParticipantPosition +
                alignmentTranslation;

            match.alignedParticipantRotation =
                match.originalParticipantRotation;


            // ====================================================
            // POSITION
            // ====================================================

            match.positionError =
                Vector3.Distance(
                    match.targetPosition,
                    match.alignedParticipantPosition);


            match.positionCorrect =
                match.positionError <=
                structurePositionTolerance;


            // ====================================================
            // ORIENTATION
            //
            // IMPORTANT:
            // Calculate orientation BEFORE structural correctness.
            // ====================================================

            match.orientationError =
                CalculateOrientationDifference(
                    pair.target,
                    pair.participant,
                    match.targetRotation,
                    match.alignedParticipantRotation);


            match.orientationCorrect =
                match.orientationError <=
                orientationToleranceDegrees;


            // ====================================================
            // STRUCTURAL CORRECTNESS
            //
            // A structural position is correct ONLY when:
            //
            // 1. Position is correct
            // AND
            // 2. Orientation is correct
            //
            // Color is NOT involved.
            // ====================================================

            match.structuralPositionCorrect =
                match.positionCorrect &&
                match.orientationCorrect;


            // ====================================================
            // COLOR
            // ====================================================

            match.colorCorrect =
                IsTube(pair.target) &&
                IsTube(pair.participant) &&
                pair.target.pieceColor ==
                pair.participant.pieceColor;


            // ====================================================
            // CONNECTOR TYPE
            // ====================================================

            match.connectorCorrect =
                IsConnector(pair.target) &&
                IsConnector(pair.participant) &&
                pair.target.pieceType ==
                pair.participant.pieceType;


            result.matches.Add(match);
        }


        // ========================================================
        // SCORES
        // ========================================================

        CalculateStructureScore(
            result,
            targetPieces);

        CalculateColorScore(
            result,
            targetPieces);

        CalculateConnectorScore(
            result,
            targetPieces);

        CalculateOrientationScore(
            result,
            targetPieces);

        CalculateConnectionScore(
            result,
            targetPieces);


        // ========================================================
        // QUICK CONSOLE OUTPUT
        // ========================================================

        Debug.Log(
            $"STRUCTURAL GRADE: " +
            $"{result.correctStructuralPositions}/" +
            $"{result.targetStructuralPositions} = " +
            $"{result.structureAccuracy:F2}%");


        Debug.Log(
            $"COLOR GRADE: " +
            $"{result.correctTubeColors}/" +
            $"{result.targetTubeCount} = " +
            $"{result.colorAccuracy:F2}%");


        Debug.Log(
            $"CONNECTOR GRADE: " +
            $"{result.correctConnectors}/" +
            $"{result.targetConnectorCount} = " +
            $"{result.connectorAccuracy:F2}%");


        Debug.Log(
            $"ORIENTATION GRADE: " +
            $"{result.correctOrientations}/" +
            $"{result.orientationTargetCount} = " +
            $"{result.orientationAccuracy:F2}%");


        Debug.Log(
            $"CONNECTION GRADE: " +
            $"{result.correctConnections}/" +
            $"{result.targetConnectionCount} = " +
            $"{result.connectionAccuracy:F2}%");


        return result;
    }


    // ============================================================
    // PIECES
    // ============================================================

    private List<BuildingPiece> GetGradablePieces(
        Transform root)
    {
        return root
            .GetComponentsInChildren<BuildingPiece>(true)
            .Where(piece =>
                IsTube(piece) ||
                IsConnector(piece))
            .ToList();
    }


    private bool IsTube(
        BuildingPiece piece)
    {
        return
            piece != null &&
            piece.pieceType ==
            PieceType.PIPE;
    }


    private bool IsConnector(
        BuildingPiece piece)
    {
        if (piece == null)
            return false;

        switch (piece.pieceType)
        {
            case PieceType.C_2WAY:
            case PieceType.C_ELBOW:
            case PieceType.C_TEE:
            case PieceType.C_3WAY:
            case PieceType.C_4WAY:
            case PieceType.C_5WAY:

                return true;

            default:

                return false;
        }
    }


    // ============================================================
    // LOCAL TRANSFORMS
    // ============================================================

    private Vector3 GetLocalPosition(
        BuildingPiece piece,
        Transform root)
    {
        return root.InverseTransformPoint(
            piece.transform.position);
    }


    private Quaternion GetLocalRotation(
        BuildingPiece piece,
        Transform root)
    {
        return
            Quaternion.Inverse(root.rotation) *
            piece.transform.rotation;
    }


    // ============================================================
    // CENTROID
    // ============================================================

    private Vector3 GetCentroid(
        List<BuildingPiece> pieces,
        Transform root)
    {
        if (pieces == null ||
            pieces.Count == 0)
        {
            return Vector3.zero;
        }


        Vector3 center =
            Vector3.zero;


        foreach (BuildingPiece piece
                 in pieces)
        {
            center +=
                GetLocalPosition(
                    piece,
                    root);
        }


        return center /
               pieces.Count;
    }


    // ============================================================
    // TRANSLATION
    // ============================================================

    private Vector3 CalculateTranslation(
        List<PiecePair> pairs)
    {
        if (pairs == null ||
            pairs.Count == 0)
        {
            return Vector3.zero;
        }


        Vector3 targetCenter =
            Vector3.zero;

        Vector3 participantCenter =
            Vector3.zero;


        foreach (PiecePair pair in pairs)
        {
            targetCenter +=
                GetLocalPosition(
                    pair.target,
                    targetBuildRoot);

            participantCenter +=
                GetLocalPosition(
                    pair.participant,
                    participantBuildRoot);
        }


        targetCenter /=
            pairs.Count;

        participantCenter /=
            pairs.Count;


        return
            targetCenter -
            participantCenter;
    }


    // ============================================================
    // CORRESPONDENCE
    //
    // Position determines correspondence.
    //
    // Color is NOT used.
    // Orientation is NOT used.
    // Exact connector type is NOT used.
    //
    // Tubes can only match tubes.
    // Connectors can only match connectors.
    // ============================================================

    private List<PiecePair> FindOptimalCorrespondence(
        List<BuildingPiece> targetPieces,
        List<BuildingPiece> participantPieces,
        Vector3 translation)
    {
        List<PiecePair> result =
            new List<PiecePair>();


        // Tubes

        List<BuildingPiece> targetTubes =
            targetPieces
                .Where(IsTube)
                .ToList();

        List<BuildingPiece> participantTubes =
            participantPieces
                .Where(IsTube)
                .ToList();


        result.AddRange(
            FindBestAssignment(
                targetTubes,
                participantTubes,
                translation));


        // Connectors

        List<BuildingPiece> targetConnectors =
            targetPieces
                .Where(IsConnector)
                .ToList();

        List<BuildingPiece> participantConnectors =
            participantPieces
                .Where(IsConnector)
                .ToList();


        result.AddRange(
            FindBestAssignment(
                targetConnectors,
                participantConnectors,
                translation));


        return result;
    }


    // ============================================================
    // OPTIMAL ASSIGNMENT
    // ============================================================

    private List<PiecePair> FindBestAssignment(
        List<BuildingPiece> targets,
        List<BuildingPiece> participants,
        Vector3 translation)
    {
        if (targets.Count == 0 ||
            participants.Count == 0)
        {
            return new List<PiecePair>();
        }


        if (participants.Count <=
            targets.Count)
        {
            AssignmentSolution solution =
                new AssignmentSolution();

            bool[] usedTargets =
                new bool[targets.Count];

            List<PiecePair> currentPairs =
                new List<PiecePair>();


            SearchParticipantAssignments(
                0,
                targets,
                participants,
                translation,
                usedTargets,
                currentPairs,
                0f,
                solution);


            return solution.pairs;
        }


        AssignmentSolution reverseSolution =
            new AssignmentSolution();

        bool[] usedParticipants =
            new bool[participants.Count];

        List<PiecePair> reversePairs =
            new List<PiecePair>();


        SearchTargetAssignments(
            0,
            targets,
            participants,
            translation,
            usedParticipants,
            reversePairs,
            0f,
            reverseSolution);


        return reverseSolution.pairs;
    }


    // ============================================================
    // PARTICIPANT -> TARGET SEARCH
    // ============================================================

    private void SearchParticipantAssignments(
        int participantIndex,
        List<BuildingPiece> targets,
        List<BuildingPiece> participants,
        Vector3 translation,
        bool[] usedTargets,
        List<PiecePair> currentPairs,
        float currentDistance,
        AssignmentSolution best)
    {
        if (currentDistance >=
            best.totalDistance)
        {
            return;
        }


        if (participantIndex >=
            participants.Count)
        {
            best.totalDistance =
                currentDistance;

            best.pairs =
                new List<PiecePair>(
                    currentPairs);

            return;
        }


        BuildingPiece participant =
            participants[participantIndex];


        Vector3 participantPosition =
            GetLocalPosition(
                participant,
                participantBuildRoot)
            + translation;


        for (int targetIndex = 0;
             targetIndex < targets.Count;
             targetIndex++)
        {
            if (usedTargets[targetIndex])
                continue;


            BuildingPiece target =
                targets[targetIndex];


            Vector3 targetPosition =
                GetLocalPosition(
                    target,
                    targetBuildRoot);


            float distance =
                Vector3.Distance(
                    participantPosition,
                    targetPosition);


            usedTargets[targetIndex] =
                true;


            currentPairs.Add(
                new PiecePair(
                    target,
                    participant));


            SearchParticipantAssignments(
                participantIndex + 1,
                targets,
                participants,
                translation,
                usedTargets,
                currentPairs,
                currentDistance + distance,
                best);


            currentPairs.RemoveAt(
                currentPairs.Count - 1);


            usedTargets[targetIndex] =
                false;
        }
    }


    // ============================================================
    // TARGET -> PARTICIPANT SEARCH
    // ============================================================

    private void SearchTargetAssignments(
        int targetIndex,
        List<BuildingPiece> targets,
        List<BuildingPiece> participants,
        Vector3 translation,
        bool[] usedParticipants,
        List<PiecePair> currentPairs,
        float currentDistance,
        AssignmentSolution best)
    {
        if (currentDistance >=
            best.totalDistance)
        {
            return;
        }


        if (targetIndex >=
            targets.Count)
        {
            best.totalDistance =
                currentDistance;

            best.pairs =
                new List<PiecePair>(
                    currentPairs);

            return;
        }


        BuildingPiece target =
            targets[targetIndex];


        Vector3 targetPosition =
            GetLocalPosition(
                target,
                targetBuildRoot);


        for (int participantIndex = 0;
             participantIndex <
             participants.Count;
             participantIndex++)
        {
            if (usedParticipants[
                    participantIndex])
            {
                continue;
            }


            BuildingPiece participant =
                participants[
                    participantIndex];


            Vector3 participantPosition =
                GetLocalPosition(
                    participant,
                    participantBuildRoot)
                + translation;


            float distance =
                Vector3.Distance(
                    participantPosition,
                    targetPosition);


            usedParticipants[
                participantIndex] =
                true;


            currentPairs.Add(
                new PiecePair(
                    target,
                    participant));


            SearchTargetAssignments(
                targetIndex + 1,
                targets,
                participants,
                translation,
                usedParticipants,
                currentPairs,
                currentDistance + distance,
                best);


            currentPairs.RemoveAt(
                currentPairs.Count - 1);


            usedParticipants[
                participantIndex] =
                false;
        }
    }


    // ============================================================
    // STRUCTURE SCORE
    //
    // Correct structure =
    //
    // position correct
    // AND
    // orientation correct
    //
    // Color does not affect this score.
    // ============================================================

    private void CalculateStructureScore(
        ComparisonResult result,
        List<BuildingPiece> targetPieces)
    {
        result.targetStructuralPositions =
            targetPieces.Count;


        result.correctStructuralPositions =
            result.matches.Count(
                match =>
                    match.structuralPositionCorrect);


        if (result.targetStructuralPositions > 0)
        {
            result.structureAccuracy =
                (float)
                result.correctStructuralPositions /
                result.targetStructuralPositions *
                100f;
        }
        else
        {
            result.structureAccuracy =
                0f;
        }


        // --------------------------------------------------------
        // Error statistics
        // --------------------------------------------------------

        if (result.matches.Count == 0)
        {
            result.meanPositionError = 0f;
            result.rmsPositionError = 0f;
            result.normalizedStructureError = 0f;

            return;
        }


        result.meanPositionError =
            result.matches.Average(
                match =>
                    match.positionError);


        float squaredErrorSum =
            0f;


        foreach (PieceMatch match
                 in result.matches)
        {
            squaredErrorSum +=
                match.positionError *
                match.positionError;
        }


        result.rmsPositionError =
            Mathf.Sqrt(
                squaredErrorSum /
                result.matches.Count);


        // --------------------------------------------------------
        // Target radius
        // --------------------------------------------------------

        Vector3 targetCenter =
            GetCentroid(
                targetPieces,
                targetBuildRoot);


        float maxRadius =
            0f;


        foreach (BuildingPiece piece
                 in targetPieces)
        {
            Vector3 position =
                GetLocalPosition(
                    piece,
                    targetBuildRoot);


            float radius =
                Vector3.Distance(
                    position,
                    targetCenter);


            if (radius > maxRadius)
            {
                maxRadius =
                    radius;
            }
        }


        if (maxRadius > 0.000001f)
        {
            result.normalizedStructureError =
                result.rmsPositionError /
                maxRadius;
        }
        else
        {
            result.normalizedStructureError =
                0f;
        }
    }


    // ============================================================
    // COLOR SCORE
    // ============================================================

    private void CalculateColorScore(
        ComparisonResult result,
        List<BuildingPiece> targetPieces)
    {
        result.targetTubeCount =
            targetPieces.Count(
                IsTube);


        result.correctTubeColors =
            result.matches.Count(
                match =>
                    IsTube(
                        match.targetPiece) &&
                    match.colorCorrect);


        result.colorAccuracy =
            result.targetTubeCount > 0
                ? (float)
                  result.correctTubeColors /
                  result.targetTubeCount *
                  100f
                : 0f;
    }


    // ============================================================
    // CONNECTOR SCORE
    // ============================================================

    private void CalculateConnectorScore(
        ComparisonResult result,
        List<BuildingPiece> targetPieces)
    {
        result.targetConnectorCount =
            targetPieces.Count(
                IsConnector);


        result.correctConnectors =
            result.matches.Count(
                match =>
                    IsConnector(
                        match.targetPiece) &&
                    match.connectorCorrect);


        result.connectorAccuracy =
            result.targetConnectorCount > 0
                ? (float)
                  result.correctConnectors /
                  result.targetConnectorCount *
                  100f
                : 0f;
    }


    // ============================================================
    // ORIENTATION SCORE
    //
    // Denominator is TARGET pieces, not just matched pieces.
    //
    // Therefore a missing piece cannot receive orientation credit.
    // ============================================================

    private void CalculateOrientationScore(
        ComparisonResult result,
        List<BuildingPiece> targetPieces)
    {
        result.orientationTargetCount =
            targetPieces.Count;


        result.correctOrientations =
            result.matches.Count(
                match =>
                    match.orientationCorrect);


        result.orientationAccuracy =
            result.orientationTargetCount > 0
                ? (float)
                  result.correctOrientations /
                  result.orientationTargetCount *
                  100f
                : 0f;
    }


    // ============================================================
    // ORIENTATION DIFFERENCE
    // ============================================================

    private float CalculateOrientationDifference(
        BuildingPiece targetPiece,
        BuildingPiece participantPiece,
        Quaternion targetRotation,
        Quaternion participantRotation)
    {
        if (targetPiece == null ||
            participantPiece == null)
        {
            return 180f;
        }


        // Different exact piece types are not considered
        // orientation-equivalent.

        if (targetPiece.pieceType !=
            participantPiece.pieceType)
        {
            return 180f;
        }


        List<Quaternion>
            targetEquivalentRotations =
                GetEquivalentRotations(
                    targetPiece,
                    targetRotation);


        List<Quaternion>
            participantEquivalentRotations =
                GetEquivalentRotations(
                    participantPiece,
                    participantRotation);


        float smallestAngle =
            180f;


        foreach (Quaternion targetEquivalent
                 in targetEquivalentRotations)
        {
            foreach (
                Quaternion participantEquivalent
                in participantEquivalentRotations)
            {
                float angle =
                    Quaternion.Angle(
                        targetEquivalent,
                        participantEquivalent);


                if (angle <
                    smallestAngle)
                {
                    smallestAngle =
                        angle;
                }
            }
        }


        return smallestAngle;
    }


    // ============================================================
    // SYMMETRY
    // ============================================================

    private List<Quaternion>
        GetEquivalentRotations(
            BuildingPiece piece,
            Quaternion baseRotation)
    {
        List<Quaternion> rotations =
            new List<Quaternion>();


        rotations.Add(
            baseRotation);


        if (piece.symmetries != null)
        {
            foreach (Symmetry symmetry
                     in piece.symmetries)
            {
                if (symmetry == null)
                    continue;


                Quaternion equivalent =
                    symmetry
                        .rotationalOffsetFromCanonical *
                    baseRotation;


                rotations.Add(
                    equivalent);
            }
        }


        return rotations;
    }


    // ============================================================
    // CONNECTION SCORE
    // ============================================================

    private void CalculateConnectionScore(
        ComparisonResult result,
        List<BuildingPiece> targetPieces)
    {
        List<TargetConnection>
            targetConnections =
                GetTargetTubeConnectorConnections(
                    targetPieces);


        result.targetConnectionCount =
            targetConnections.Count;

        result.correctConnections =
            0;


        Dictionary<
            BuildingPiece,
            PieceMatch> targetToMatch =
                new Dictionary<
                    BuildingPiece,
                    PieceMatch>();


        foreach (PieceMatch match
                 in result.matches)
        {
            if (match.targetPiece != null)
            {
                targetToMatch[
                    match.targetPiece] =
                    match;
            }
        }


        foreach (TargetConnection connection
                 in targetConnections)
        {
            if (!targetToMatch.TryGetValue(
                    connection.tube,
                    out PieceMatch tubeMatch))
            {
                continue;
            }


            if (!targetToMatch.TryGetValue(
                    connection.connector,
                    out PieceMatch connectorMatch))
            {
                continue;
            }


            BuildingPiece participantTube =
                tubeMatch.participantPiece;


            BuildingPiece participantConnector =
                connectorMatch.participantPiece;


            if (participantTube == null ||
                participantConnector == null)
            {
                continue;
            }


            // Correct tube color

            bool correctTube =
                IsTube(participantTube) &&
                participantTube.pieceColor ==
                connection.tube.pieceColor;


            // Correct connector type

            bool correctConnector =
                IsConnector(
                    participantConnector) &&
                participantConnector.pieceType ==
                connection.connector.pieceType;


            // Must actually be connected

            bool actuallyConnected =
                ArePiecesConnected(
                    participantTube,
                    participantConnector);


            if (correctTube &&
                correctConnector &&
                actuallyConnected)
            {
                result.correctConnections++;
            }
        }


        result.connectionAccuracy =
            result.targetConnectionCount > 0
                ? (float)
                  result.correctConnections /
                  result.targetConnectionCount *
                  100f
                : 0f;
    }


    // ============================================================
    // TARGET CONNECTIONS
    // ============================================================

    private List<TargetConnection>
        GetTargetTubeConnectorConnections(
            List<BuildingPiece> targetPieces)
    {
        List<TargetConnection> connections =
            new List<TargetConnection>();


        HashSet<string> seen =
            new HashSet<string>();


        foreach (BuildingPiece piece
                 in targetPieces)
        {
            if (!IsTube(piece))
                continue;


            if (piece.attachPoints == null)
                continue;


            foreach (AttachPoint attachPoint
                     in piece.attachPoints)
            {
                if (attachPoint == null)
                    continue;


                if (attachPoint
                        .attachedAttachPoint ==
                    null)
                {
                    continue;
                }


                BuildingPiece otherPiece =
                    attachPoint
                        .attachedAttachPoint
                        .owningPiece;


                if (!IsConnector(otherPiece))
                    continue;


                string key =
                    piece.GetInstanceID() +
                    "_" +
                    otherPiece.GetInstanceID();


                if (seen.Contains(key))
                    continue;


                seen.Add(key);


                connections.Add(
                    new TargetConnection(
                        piece,
                        otherPiece));
            }
        }


        return connections;
    }


    // ============================================================
    // ACTUAL CONNECTION CHECK
    // ============================================================

    private bool ArePiecesConnected(
        BuildingPiece a,
        BuildingPiece b)
    {
        if (a == null ||
            b == null)
        {
            return false;
        }


        if (a.attachPoints == null)
            return false;


        foreach (AttachPoint attachPoint
                 in a.attachPoints)
        {
            if (attachPoint == null)
                continue;


            if (attachPoint
                    .attachedAttachPoint ==
                null)
            {
                continue;
            }


            BuildingPiece connectedPiece =
                attachPoint
                    .attachedAttachPoint
                    .owningPiece;


            if (connectedPiece == b)
            {
                return true;
            }
        }


        return false;
    }


    // ============================================================
    // PRINT RESULT
    // ============================================================

    private void PrintResult(
        ComparisonResult result)
    {
        StringBuilder sb =
            new StringBuilder();


        sb.AppendLine(
            "======================================");

        sb.AppendLine(
            "FAST OPTIMAL STRUCTURE COMPARISON");

        sb.AppendLine(
            "======================================");


        sb.AppendLine(
            $"Target: {targetBuildRoot.name}");

        sb.AppendLine(
            $"Participant: {participantBuildRoot.name}");

        sb.AppendLine();


        sb.AppendLine(
            $"Matched Pieces: {result.matchedPieceCount}");

        sb.AppendLine(
            $"Missing Pieces: {result.missingPieceCount}");

        sb.AppendLine(
            $"Extra Pieces: {result.extraPieceCount}");

        sb.AppendLine();


        sb.AppendLine(
            $"Structure: " +
            $"{result.correctStructuralPositions}/" +
            $"{result.targetStructuralPositions} = " +
            $"{result.structureAccuracy:F2}%");

        sb.AppendLine(
            $"Mean Position Error: " +
            $"{result.meanPositionError:F6}");

        sb.AppendLine(
            $"RMS Position Error: " +
            $"{result.rmsPositionError:F6}");

        sb.AppendLine(
            $"Normalized Structure Error: " +
            $"{result.normalizedStructureError:F6}");

        sb.AppendLine();


        sb.AppendLine(
            $"Tube Color: " +
            $"{result.correctTubeColors}/" +
            $"{result.targetTubeCount} = " +
            $"{result.colorAccuracy:F2}%");

        sb.AppendLine(
            $"Connectors: " +
            $"{result.correctConnectors}/" +
            $"{result.targetConnectorCount} = " +
            $"{result.connectorAccuracy:F2}%");

        sb.AppendLine(
            $"Orientation: " +
            $"{result.correctOrientations}/" +
            $"{result.orientationTargetCount} = " +
            $"{result.orientationAccuracy:F2}%");

        sb.AppendLine(
            $"Connections: " +
            $"{result.correctConnections}/" +
            $"{result.targetConnectionCount} = " +
            $"{result.connectionAccuracy:F2}%");

        sb.AppendLine();


        sb.AppendLine(
            "GLOBAL ROTATION DISABLED");

        sb.AppendLine(
            "Alignment Rotation: Identity");

        sb.AppendLine(
            $"Alignment Translation: " +
            $"{result.alignmentTranslation}");


        Debug.Log(
            sb.ToString());
    }


    // ============================================================
    // SAVE RESULT
    // ============================================================

    private void SaveResult(
        ComparisonResult result)
    {
        //string folderPath =
        //    Path.Combine(
        //        Application.persistentDataPath,
        //        comparisonFolderName);

        string directory = $"FAST_Structure/{"Grade 1 "} ";
        string folderPath = Path.Combine(directory, comparisonFolderName);
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }


        string targetName =
            MakeSafeFileName(
                targetBuildRoot.name);


        string participantName =
            MakeSafeFileName(
                participantBuildRoot.name);


        string fileName =
            targetName +
            "_vs_" +
            participantName;


        if (addTimestampToFileName)
        {
            //fileName +=
            //    "_" +
            //    DateTime.Now.ToString(
            //        "yyyyMMdd_HHmmss");
        }


        fileName +=
            ".txt";


        string filePath =
            Path.Combine(
                folderPath,
                fileName);


        StringBuilder sb =
            new StringBuilder();


        // ========================================================
        // HEADER
        // ========================================================

        sb.AppendLine(
            "FAST_OPTIMAL_STRUCTURE_COMPARISON");

        sb.AppendLine(
            $"TargetRoot={targetBuildRoot.name}");

        sb.AppendLine(
            $"ParticipantRoot={participantBuildRoot.name}");

        sb.AppendLine();


        // ========================================================
        // COUNTS
        // ========================================================

        sb.AppendLine(
            $"TargetPieces={result.targetPieceCount}");

        sb.AppendLine(
            $"ParticipantPieces={result.participantPieceCount}");

        sb.AppendLine(
            $"MatchedPieces={result.matchedPieceCount}");

        sb.AppendLine(
            $"MissingPieces={result.missingPieceCount}");

        sb.AppendLine(
            $"ExtraPieces={result.extraPieceCount}");

        sb.AppendLine();


        // ========================================================
        // STRUCTURE
        // ========================================================

        sb.AppendLine(
            $"StructurePositionTolerance=" +
            $"{FloatToString(structurePositionTolerance)}");

        sb.AppendLine(
            $"OrientationToleranceDegrees=" +
            $"{FloatToString(orientationToleranceDegrees)}");

        sb.AppendLine(
            $"CorrectStructuralPositions=" +
            $"{result.correctStructuralPositions}");

        sb.AppendLine(
            $"TargetStructuralPositions=" +
            $"{result.targetStructuralPositions}");

        sb.AppendLine(
            $"StructureAccuracyPercent=" +
            $"{FloatToString(result.structureAccuracy)}");

        sb.AppendLine(
            $"MeanPositionError=" +
            $"{FloatToString(result.meanPositionError)}");

        sb.AppendLine(
            $"RMSPositionError=" +
            $"{FloatToString(result.rmsPositionError)}");

        sb.AppendLine(
            $"NormalizedStructureError=" +
            $"{FloatToString(result.normalizedStructureError)}");

        sb.AppendLine();


        // ========================================================
        // OTHER GRADES
        // ========================================================

        sb.AppendLine(
            $"TubeColorCorrect=" +
            $"{result.correctTubeColors}");

        sb.AppendLine(
            $"TubeColorTotal=" +
            $"{result.targetTubeCount}");

        sb.AppendLine(
            $"TubeColorAccuracyPercent=" +
            $"{FloatToString(result.colorAccuracy)}");


        sb.AppendLine(
            $"ConnectorCorrect=" +
            $"{result.correctConnectors}");

        sb.AppendLine(
            $"ConnectorTotal=" +
            $"{result.targetConnectorCount}");

        sb.AppendLine(
            $"ConnectorAccuracyPercent=" +
            $"{FloatToString(result.connectorAccuracy)}");


        sb.AppendLine(
            $"OrientationCorrect=" +
            $"{result.correctOrientations}");

        sb.AppendLine(
            $"OrientationTotal=" +
            $"{result.orientationTargetCount}");

        sb.AppendLine(
            $"OrientationAccuracyPercent=" +
            $"{FloatToString(result.orientationAccuracy)}");


        sb.AppendLine(
            $"ConnectionCorrect=" +
            $"{result.correctConnections}");

        sb.AppendLine(
            $"ConnectionTotal=" +
            $"{result.targetConnectionCount}");

        sb.AppendLine(
            $"ConnectionAccuracyPercent=" +
            $"{FloatToString(result.connectionAccuracy)}");

        sb.AppendLine();


        // ========================================================
        // ALIGNMENT
        // ========================================================

        sb.AppendLine(
            "AlignmentMode=TranslationOnly");

        sb.AppendLine(
            "AlignmentRotation=0,0,0,1");

        sb.AppendLine(
            $"AlignmentTranslation=" +
            $"{VectorToString(result.alignmentTranslation)}");

        sb.AppendLine();


        // ========================================================
        // MATCHES
        // ========================================================

        foreach (PieceMatch match
                 in result.matches)
        {
            sb.AppendLine(
                "MATCH");


            sb.AppendLine(
                $"TargetName=" +
                $"{match.targetPiece.name}");

            sb.AppendLine(
                $"ParticipantName=" +
                $"{match.participantPiece.name}");


            sb.AppendLine(
                $"TargetType=" +
                $"{match.targetPiece.pieceType}");

            sb.AppendLine(
                $"ParticipantType=" +
                $"{match.participantPiece.pieceType}");


            sb.AppendLine(
                $"TargetColor=" +
                $"{match.targetPiece.pieceColor}");

            sb.AppendLine(
                $"ParticipantColor=" +
                $"{match.participantPiece.pieceColor}");


            sb.AppendLine(
                $"TargetPosition=" +
                $"{VectorToString(match.targetPosition)}");

            sb.AppendLine(
                $"OriginalParticipantPosition=" +
                $"{VectorToString(match.originalParticipantPosition)}");

            sb.AppendLine(
                $"AlignedParticipantPosition=" +
                $"{VectorToString(match.alignedParticipantPosition)}");


            sb.AppendLine(
                $"TargetRotation=" +
                $"{QuaternionToString(match.targetRotation)}");

            sb.AppendLine(
                $"OriginalParticipantRotation=" +
                $"{QuaternionToString(match.originalParticipantRotation)}");

            sb.AppendLine(
                $"AlignedParticipantRotation=" +
                $"{QuaternionToString(match.alignedParticipantRotation)}");


            sb.AppendLine(
                $"PositionError=" +
                $"{FloatToString(match.positionError)}");

            sb.AppendLine(
                $"PositionCorrect=" +
                $"{match.positionCorrect}");


            sb.AppendLine(
                $"OrientationErrorDegrees=" +
                $"{FloatToString(match.orientationError)}");

            sb.AppendLine(
                $"OrientationCorrect=" +
                $"{match.orientationCorrect}");


            sb.AppendLine(
                $"StructuralPositionCorrect=" +
                $"{match.structuralPositionCorrect}");


            sb.AppendLine(
                $"ColorCorrect=" +
                $"{match.colorCorrect}");

            sb.AppendLine(
                $"ConnectorCorrect=" +
                $"{match.connectorCorrect}");


            sb.AppendLine(
                "END_MATCH");

            sb.AppendLine();
        }


        File.WriteAllText(
            filePath,
            sb.ToString());


        Debug.Log(
            "Comparison saved to:\n" +
            filePath);
    }


    // ============================================================
    // FILE HELPERS
    // ============================================================

    private string MakeSafeFileName(
        string value)
    {
        foreach (char invalidChar
                 in Path.GetInvalidFileNameChars())
        {
            value =
                value.Replace(
                    invalidChar,
                    '_');
        }


        return value.Replace(
            ' ',
            '_');
    }


    private string FloatToString(
        float value)
    {
        return value.ToString(
            "R",
            CultureInfo.InvariantCulture);
    }


    private string VectorToString(
        Vector3 value)
    {
        return
            FloatToString(value.x) +
            "," +
            FloatToString(value.y) +
            "," +
            FloatToString(value.z);
    }


    private string QuaternionToString(
        Quaternion value)
    {
        return
            FloatToString(value.x) +
            "," +
            FloatToString(value.y) +
            "," +
            FloatToString(value.z) +
            "," +
            FloatToString(value.w);
    }


    // ============================================================
    // SHOW FOLDER
    // ============================================================

    [ContextMenu("Show Comparison Folder")]
    public void ShowComparisonFolder()
    {
        string folderPath =
            Path.Combine(
                Application.persistentDataPath,
                comparisonFolderName);


        Debug.Log(
            "FAST Comparison Folder:\n" +
            folderPath);
    }
}