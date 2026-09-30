using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using PlayStructure;

public class BuildGrader : MonoBehaviour
{
    // ============================================================
    // BUILD ROOTS
    // ============================================================

    [Header("Build Containers")]

    [Tooltip("Parent GameObject containing the TARGET structure")]
    [SerializeField] private Transform targetBuildRoot;

    [Tooltip("Parent GameObject containing the PARTICIPANT structure")]
    [SerializeField] private Transform participantBuildRoot;


    // ============================================================
    // PIECE MATCH
    // ============================================================

    [Serializable]
    public class PieceMatch
    {
        public BuildingPiece participantPiece;
        public BuildingPiece targetPiece;

        // Distance between the two normalized structural positions.
        public float positionDistance;

        public bool colorCorrect;
        public bool connectorCorrect;
    }


    // ============================================================
    // GRADE RESULT
    // ============================================================

    [Serializable]
    public class GradeResult
    {
        public int targetPieceCount;
        public int participantPieceCount;

        public int matchedPieceCount;

        public int missingPieces;
        public int extraPieces;


        // --------------------------------------------------------
        // TUBE COLOR
        // --------------------------------------------------------

        public int targetTubeCount;
        public int correctTubeColors;
        public float colorAccuracy;


        // --------------------------------------------------------
        // CONNECTORS
        // --------------------------------------------------------

        public int targetConnectorCount;
        public int correctConnectors;
        public float connectorAccuracy;


        // --------------------------------------------------------
        // CONNECTIONS
        // --------------------------------------------------------

        public int targetConnectionCount;
        public int correctConnections;
        public float connectionAccuracy;


        // --------------------------------------------------------
        // MATCHES
        // --------------------------------------------------------

        public List<PieceMatch> matches = new List<PieceMatch>();
    }


    // ============================================================
    // N x M POSITION CANDIDATE
    // ============================================================

    private class PositionCandidate
    {
        public BuildingPiece participant;
        public BuildingPiece target;

        public Vector3 participantPosition;
        public Vector3 targetPosition;

        public float distance;
    }


    // ============================================================
    // MAIN GRADING FUNCTION
    // ============================================================

    public GradeResult GradeBuild()
    {
        Debug.Log("================================================");

        Debug.Log("FAST STRUCTURE GRADING");

        Debug.Log("================================================");


        // ========================================================
        // VALIDATION
        // ========================================================

        if (targetBuildRoot == null)
        {
            Debug.LogError("Target Build Root has not been assigned.");

            return null;
        }


        if (participantBuildRoot == null)
        {
            Debug.LogError("Participant Build Root has not been assigned.");

            return null;
        }


        // ========================================================
        // GET PIECES
        // ========================================================

        List<BuildingPiece> targetPieces =targetBuildRoot.GetComponentsInChildren<BuildingPiece>(true).Where(IsGradablePiece).ToList();


        List<BuildingPiece> participantPieces = participantBuildRoot.GetComponentsInChildren<BuildingPiece>(true).Where(IsGradablePiece).ToList();


        GradeResult result = new GradeResult();


        result.targetPieceCount = targetPieces.Count;


        result.participantPieceCount = participantPieces.Count;


        Debug.Log($"Target Pieces: {targetPieces.Count}");


        Debug.Log($"Participant Pieces: {participantPieces.Count}");


        // ========================================================
        // STEP 1: ESTABLISH CORRESPONDENCE USING RELATIVE SPATIAL POSITION
        // ========================================================

        result.matches = EstablishSpatialCorrespondence(participantPieces,targetPieces);


        result.matchedPieceCount =result.matches.Count;


        result.missingPieces = Mathf.Max(0, targetPieces.Count - result.matchedPieceCount);


        result.extraPieces = Mathf.Max(0,participantPieces.Count - result.matchedPieceCount);


        // ========================================================
        // STEP 2: GRADE COLORS
        // ========================================================

        CalculateColorAccuracy(result,targetPieces);


        // ========================================================
        // STEP 3: GRADE CONNECTORS
        // ========================================================

        CalculateConnectorAccuracy(result,targetPieces);


        // ========================================================
        // STEP 4: GRADE CONNECTIONS
        // ========================================================

        CalculateConnectionAccuracy(result,targetPieces);

        // ========================================================
        // PRINT RESULTS
        // ========================================================

        PrintResult(result);


        return result;
    }


    // ============================================================
    // SPATIAL CORRESPONDENCE
    //
    // IMPORTANT:
    //
    // This is where "respective structural position" is determined.
    //
    // We do NOT use:
    //
    //      Tube color
    //      Connector PieceType
    //      Correctness
    //
    // We use relative spatial position.
    // ============================================================

    private List<PieceMatch> EstablishSpatialCorrespondence(List<BuildingPiece> participantPieces,List<BuildingPiece> targetPieces)
    {
        List<PieceMatch> matches = new List<PieceMatch>();


        // ========================================================
        // Calculate normalized positions for each structure.
        // ========================================================

        Dictionary<BuildingPiece, Vector3> targetPositions = CalculateNormalizedPositions(targetPieces, targetBuildRoot);

        Dictionary<BuildingPiece, Vector3> participantPositions = CalculateNormalizedPositions(participantPieces, participantBuildRoot);

        // ========================================================
        // Match tubes independently.
        // ========================================================

        List<BuildingPiece> targetTubes = targetPieces.Where(IsTube).ToList();


        List<BuildingPiece> participantTubes = participantPieces.Where(IsTube).ToList();


        List<PieceMatch> tubeMatches = MatchCategoryByPosition(participantTubes, targetTubes, participantPositions, targetPositions);
 
        matches.AddRange(tubeMatches);


        // ========================================================
        // Match connectors independently.
        //
        // IMPORTANT:
        //
        // C_ELBOW and C_3WAY are NOT separated here.
        //
        // They are all just connector positions.
        //
        // Exact connector type is graded AFTER correspondence.
        // ========================================================

        List<BuildingPiece> targetConnectors = targetPieces.Where(IsConnector).ToList();


        List<BuildingPiece> participantConnectors = participantPieces.Where(IsConnector).ToList();


        List<PieceMatch> connectorMatches = MatchCategoryByPosition(participantConnectors,targetConnectors,participantPositions,targetPositions);


        matches.AddRange(connectorMatches);


        return matches;
    }


    // ============================================================
    // NORMALIZED POSITIONS
    //
    // Raw Unity world positions cannot be compared directly because
    // the target and participant structures may be located in
    // different places in the scene.
    //
    // Therefore:
    //
    // 1. Convert each piece to build-root local space.
    // 2. Find the center of that structure.
    // 3. Center all positions around zero.
    // 4. Normalize by the structure's overall size.
    //
    // This means:
    //
    // "upper-left position in Build A"
    //
    // can be compared with
    //
    // "upper-left position in P1"
    //
    // even if the two build roots are far apart in world space.
    // ============================================================

    private Dictionary<BuildingPiece, Vector3>CalculateNormalizedPositions(List<BuildingPiece> pieces,Transform buildRoot)
    {
        Dictionary<BuildingPiece, Vector3> result = new Dictionary<BuildingPiece, Vector3>();


        if (pieces.Count == 0)
        {
            return result;
        }


        // ========================================================
        // Convert every piece to local build coordinates.
        // ========================================================

        Dictionary<BuildingPiece, Vector3> localPositions = new Dictionary<BuildingPiece, Vector3>();

        foreach (BuildingPiece piece in pieces)
        {
            Vector3 localPosition = buildRoot.InverseTransformPoint(piece.transform.position);


            localPositions.Add(piece,localPosition);
        }

        // ========================================================
        // Find center of structure.
        // ========================================================

        Vector3 center = Vector3.zero;


        foreach (Vector3 position in localPositions.Values)
        {
            center += position;
        }

        center /= localPositions.Count;


        // ========================================================
        // Find structure size.
        //
        // We use the maximum distance from the center.
        // ========================================================

        float structureRadius = 0f;


        foreach (Vector3 position in localPositions.Values)
        {
            float distance = Vector3.Distance(position, center);


            if (distance > structureRadius)
            {
                structureRadius =  distance;
            }
        }


        // Prevent division by zero.
        if (structureRadius < 0.0001f)
        {
            structureRadius = 1f;
        }


        // ========================================================
        // Normalize positions.
        // ========================================================

        foreach (KeyValuePair<BuildingPiece, Vector3> pair in localPositions)
        {
            Vector3 normalizedPosition = (pair.Value - center) / structureRadius;


            result.Add(pair.Key, normalizedPosition);
        }


        return result;
    }


    // ============================================================
    // N x M CATEGORY POSITION MATCHING
    //
    // Every participant position is compared against every target
    // position in the SAME GENERAL CATEGORY.
    //
    // Tube -> tube
    //
    // Connector -> connector
    //
    // We then find the global one-to-one assignment with the
    // smallest TOTAL positional distance.
    //
    // This is different from greedy matching.
    // ============================================================

    private List<PieceMatch> MatchCategoryByPosition(List<BuildingPiece> participantPieces, List<BuildingPiece> targetPieces, Dictionary<BuildingPiece, Vector3> participantPositions,
        Dictionary<BuildingPiece, Vector3> targetPositions)
    {
        List<PieceMatch> bestMatches = new List<PieceMatch>();


        if (participantPieces.Count == 0 || targetPieces.Count == 0)
        {
            return bestMatches;
        }


        // ========================================================
        // If participant has more pieces than target, we assign
        // target positions to a subset of participant pieces.
        //
        // If target has more pieces than participant, missing target
        // positions simply remain unmatched.
        // ========================================================

        int requiredMatches = Mathf.Min(participantPieces.Count, targetPieces.Count);


        float bestTotalDistance = float.MaxValue;


        List<PieceMatch> currentMatches = new List<PieceMatch>();


        HashSet<BuildingPiece> usedTargets = new HashSet<BuildingPiece>();


        SearchBestSpatialAssignment(participantPieces,targetPieces,participantPositions,targetPositions,0,requiredMatches,
            usedTargets,currentMatches,0f,ref bestTotalDistance,ref bestMatches);


        return bestMatches;
    }


    // ============================================================
    // GLOBAL ASSIGNMENT SEARCH
    //
    // Because FAST structures contain a small number of pieces,
    // we can search possible one-to-one mappings and choose the
    // mapping with the lowest TOTAL normalized spatial distance.
    //
    // No color or connector type enters this calculation.
    // ============================================================

    private void SearchBestSpatialAssignment(List<BuildingPiece> participantPieces,List<BuildingPiece> targetPieces,Dictionary<BuildingPiece, Vector3> participantPositions,
        Dictionary<BuildingPiece, Vector3> targetPositions, int participantIndex, int requiredMatches, HashSet<BuildingPiece> usedTargets, List<PieceMatch> currentMatches,
        float currentDistance, ref float bestTotalDistance, ref List<PieceMatch> bestMatches)
    {
        // ========================================================
        // PRUNING
        //
        // If this partial assignment is already worse than our
        // best complete assignment, stop searching this branch.
        // ========================================================

        if (currentDistance >= bestTotalDistance)
        {
            return;
        }


        // ========================================================
        // Complete assignment.
        // ========================================================

        if (currentMatches.Count == requiredMatches)
        {
            if (currentDistance < bestTotalDistance)
            {
                bestTotalDistance = currentDistance;


                bestMatches = currentMatches.Select(match => new PieceMatch{
                                    participantPiece = match.participantPiece, targetPiece = match.targetPiece,positionDistance = match.positionDistance
                }).ToList();
            }


            return;
        }


        // No participant pieces remain.
        if (participantIndex >= participantPieces.Count)
        {
            return;
        }


        // ========================================================
        // Check whether enough participant pieces remain to reach
        // requiredMatches.
        // ========================================================

        int participantPiecesRemaining = participantPieces.Count - participantIndex;


        int matchesStillNeeded = requiredMatches - currentMatches.Count;


        if (participantPiecesRemaining < matchesStillNeeded)
        {
            return;
        }


        BuildingPiece participant = participantPieces[participantIndex];


        Vector3 participantPosition = participantPositions[participant];


        // ========================================================
        // OPTION 1
        //
        // Match this participant piece with each unused target.
        // ========================================================

        foreach (BuildingPiece target in targetPieces)
        {
            if (usedTargets.Contains(target)) continue;

            Vector3 targetPosition = targetPositions[target];


            float distance = Vector3.Distance(participantPosition, targetPosition);


            PieceMatch match = new PieceMatch();


            match.participantPiece = participant;


            match.targetPiece = target;


            match.positionDistance = distance;


            usedTargets.Add(target);


            currentMatches.Add(match);


            SearchBestSpatialAssignment(participantPieces,targetPieces,participantPositions,targetPositions,participantIndex + 1,requiredMatches,usedTargets,
                currentMatches,currentDistance + distance,ref bestTotalDistance,ref bestMatches);


            currentMatches.RemoveAt(currentMatches.Count - 1);


            usedTargets.Remove(target);
        }


        // ========================================================
        // OPTION 2
        //
        // If there are extra participant pieces, this participant
        // piece may be skipped.
        // ========================================================

        if (participantPieces.Count > targetPieces.Count)
        {
            SearchBestSpatialAssignment(participantPieces,targetPieces,participantPositions,targetPositions,
                participantIndex + 1,requiredMatches,usedTargets,currentMatches,currentDistance,ref bestTotalDistance,ref bestMatches);
        }
    }


    // ============================================================
    // TUBE COLOR ACCURACY
    // ============================================================

    private void CalculateColorAccuracy(GradeResult result,List<BuildingPiece> targetPieces)
    {
        List<BuildingPiece> targetTubes = targetPieces.Where(IsTube).ToList();

        result.targetTubeCount =targetTubes.Count;

        result.correctTubeColors = 0;

        foreach (BuildingPiece targetTube in targetTubes)
        {
            PieceMatch match = FindMatchForTarget(result,targetTube);


            if (match == null) continue;


            if (!IsTube(match.participantPiece)) continue;


            bool correct = match.participantPiece.pieceColor == targetTube.pieceColor;


            match.colorCorrect = correct;


            if (correct)
            {
                result.correctTubeColors++;
            }
        }


        if (result.targetTubeCount == 0)
        {
            result.colorAccuracy = 0f;
        }
        else
        {
            result.colorAccuracy = (float)result.correctTubeColors / result.targetTubeCount;
        }
    }


    // ============================================================
    // CONNECTOR ACCURACY
    // ============================================================

    private void CalculateConnectorAccuracy(GradeResult result,List<BuildingPiece> targetPieces)
    {
        List<BuildingPiece> targetConnectors =targetPieces.Where(IsConnector).ToList();


        result.targetConnectorCount =targetConnectors.Count;


        result.correctConnectors =0;


        foreach (BuildingPiece targetConnector in targetConnectors)
        {
            PieceMatch match = FindMatchForTarget(result,targetConnector);


            if (match == null) continue;


            if (!IsConnector(match.participantPiece)) continue;


            bool correct = match.participantPiece.pieceType == targetConnector.pieceType;


            match.connectorCorrect = correct;


            if (correct) result.correctConnectors++;
        }


        if (result.targetConnectorCount == 0)
        {
            result.connectorAccuracy = 0f;
        }
        else
        {
            result.connectorAccuracy = (float)result.correctConnectors / result.targetConnectorCount;
        }
    }


    // ============================================================
    // CONNECTION ACCURACY
    //
    // A target tube <-> connector connection receives credit only
    // when:
    //
    // 1. Corresponding participant tube exists.
    // 2. Tube color is correct.
    // 3. Corresponding participant connector exists.
    // 4. Connector type is correct.
    // 5. Those participant pieces are actually connected.
    // ============================================================

    private void CalculateConnectionAccuracy(GradeResult result, List<BuildingPiece> targetPieces)
    {
        int correct = 0;


        int total = 0;


        HashSet<string> checkedTargetConnections = new HashSet<string>();


        foreach (BuildingPiece targetPiece in targetPieces)
        {
            foreach (AttachPoint targetAP in targetPiece.attachPoints)
            {
                if (targetAP == null) continue;


                if (targetAP.attachedAttachPoint == null) continue;


                BuildingPiece targetNeighbor =targetAP.attachedAttachPoint.owningPiece;


                if (targetNeighbor == null) continue;


                string key = CreateConnectionKey(targetPiece,targetNeighbor);


                if (checkedTargetConnections.Contains(key)) continue;
               


                checkedTargetConnections.Add(key);


                BuildingPiece targetTube = null;


                BuildingPiece targetConnector = null;


                // =================================================
                // Target piece is tube, neighbor is connector.
                // =================================================

                if (IsTube(targetPiece) && IsConnector(targetNeighbor))
                {
                    targetTube = targetPiece;


                    targetConnector = targetNeighbor;
                }


                // =================================================
                // Target piece is connector, neighbor is tube.
                // =================================================

                else if (IsConnector(targetPiece) && IsTube(targetNeighbor))
                {
                    targetTube = targetNeighbor;


                    targetConnector = targetPiece;
                }


                else
                {
                    continue;
                }


                // =================================================
                // This is one required target connection.
                // =================================================

                total++;


                PieceMatch tubeMatch = FindMatchForTarget(result,targetTube);


                PieceMatch connectorMatch = FindMatchForTarget(result,targetConnector);


                if (tubeMatch == null ||connectorMatch == null) continue;


                BuildingPiece participantTube = tubeMatch.participantPiece;


                BuildingPiece participantConnector = connectorMatch.participantPiece;


                // =================================================
                // CORRECT TUBE CATEGORY
                // =================================================

                if (!IsTube(participantTube)) continue;

                // =================================================
                // CORRECT TUBE COLOR
                // =================================================

                if (participantTube.pieceColor != targetTube.pieceColor) continue;

                // =================================================
                // CORRECT CONNECTOR CATEGORY
                // =================================================

                if (!IsConnector(participantConnector)) continue;


                // =================================================
                // CORRECT CONNECTOR TYPE
                // =================================================

                if (participantConnector.pieceType != targetConnector.pieceType) continue;


                // =================================================
                // ACTUAL CONNECTION
                // =================================================

                if (!ArePiecesConnected(participantTube,participantConnector)) continue;


                // =================================================
                // EVERYTHING IS CORRECT.
                // =================================================

                correct++;
            }
        }


        result.targetConnectionCount = total;


        result.correctConnections = correct;


        result.connectionAccuracy = total == 0 ? 0f: (float)correct / total;
    }


    // ============================================================
    // IS GRADABLE PIECE
    // ============================================================

    private bool IsGradablePiece(BuildingPiece piece)
    {
        return IsTube(piece) ||  IsConnector(piece);
    }


    // ============================================================
    // IS TUBE
    // ============================================================

    private bool IsTube(BuildingPiece piece)
    {
        if (piece == null) return false;


        return piece.pieceType == PieceType.PIPE;
    }


    // ============================================================
    // IS CONNECTOR
    // ============================================================

    private bool IsConnector(BuildingPiece piece)
    {
        if (piece == null) return false;


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
    // FIND MATCH FOR TARGET
    // ============================================================

    private PieceMatch FindMatchForTarget(GradeResult result,BuildingPiece target)
    {
        return result.matches.FirstOrDefault(match => match.targetPiece == target);
    }


    // ============================================================
    // ARE PIECES CONNECTED
    // ============================================================

    private bool ArePiecesConnected(BuildingPiece pieceA, BuildingPiece pieceB)
    {
        if (pieceA == null || pieceB == null) return false;

        foreach (AttachPoint attachPoint in pieceA.attachPoints)
        {
            if (attachPoint == null) continue;


            if (attachPoint.attachedAttachPoint == null) continue;


            BuildingPiece connectedPiece = attachPoint.attachedAttachPoint.owningPiece;


            if (connectedPiece == pieceB) return true;

        }


        return false;
    }


    // ============================================================
    // CONNECTION KEY
    // ============================================================

    private string CreateConnectionKey(BuildingPiece a,BuildingPiece b)
    {
        int idA = a.GetInstanceID();


        int idB = b.GetInstanceID();


        if (idA < idB)
        {
            return idA + "_" + idB;
        }


        return idB + "_" + idA;
    }


    // ============================================================
    // PRINT RESULTS
    // ============================================================

    private void PrintResult(GradeResult result)
    {
        Debug.Log("================================================");


        Debug.Log("STRUCTURE GRADING RESULTS");


        Debug.Log("================================================");


        Debug.Log($"TUBE COLORS: {result.correctTubeColors} / {result.targetTubeCount} {result.colorAccuracy * 100f:F2}%)");


        Debug.Log($"CONNECTORS: {result.correctConnectors} / {result.targetConnectorCount} ({result.connectorAccuracy * 100f:F2}%)");


        Debug.Log($"CONNECTIONS: {result.correctConnections} / {result.targetConnectionCount} ({result.connectionAccuracy * 100f:F2}%)");


        Debug.Log("================================================");


        Debug.Log("N x M SPATIAL CORRESPONDENCE:");


        foreach (PieceMatch match in result.matches.OrderBy(match => match.positionDistance))
        {
            if (IsTube(match.targetPiece))
            {
                Debug.Log($"[TUBE] " +
                    $"Participant: {match.participantPiece.name} " +
                    $"({match.participantPiece.pieceColor}) " +
                    $"-> " +
                    $"Target: {match.targetPiece.name} " +
                    $"({match.targetPiece.pieceColor}) " +
                    $"| Position Distance: " +
                    $"{match.positionDistance:F4} " +
                    $"| Color Correct: " +
                    $"{match.colorCorrect}");
            }


            else if (IsConnector(match.targetPiece))
            {
                Debug.Log(
                    $"[CONNECTOR] " +
                    $"Participant: {match.participantPiece.name} " +
                    $"({match.participantPiece.pieceType}) " +
                    $"-> " +
                    $"Target: {match.targetPiece.name} " +
                    $"({match.targetPiece.pieceType}) " +
                    $"| Position Distance: " +
                    $"{match.positionDistance:F4} " +
                    $"| Connector Correct: " +
                    $"{match.connectorCorrect}");
            }
        }


        Debug.Log("================================================");
    }


    // ============================================================
    // RUN FROM INSPECTOR
    // ============================================================

    [ContextMenu("Grade Structure")]
    public void GradeFromInspector()
    {
        GradeBuild();
    }
}