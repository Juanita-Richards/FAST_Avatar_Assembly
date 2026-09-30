using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using PlayStructure;
using Unity.VisualScripting;
using UnityEngine.UIElements;

/// <summary>
/// Grades a reconstructed participant build against a target build
/// 
/// AttachPoint indices are not compared.
/// 
/// The grading process:
/// 
/// 1. NxM piece comparison
///     - Piece typw
///     - Piece color
///     
/// 2. Establish one-to-one piece correspondence
/// 
/// 3. Compare connectivity between the matched pieces
/// 
/// 4. Compare relative spatial orientation
/// 
/// </summary>

public class BuidGrader2 : MonoBehaviour
{
    //Build
    [Header("Build Containers")]

    [Tooltip("Parent GameObject containing the target BuildingPieces")]
    [SerializeField]
    private Transform targetBuildRoot;

    [Tooltip("Parent GameObject containing the reconstructed participant BuildingPieces.")]
    [SerializeField]
    private Transform participantBuildRoot;

    //Wieght
    [Header("Piece Matching Weights")]

    [Tooltip("Weight given to matching PieceType")]
    [SerializeField]
    private float pieceTypeWeight = 1.0f;

    [Tooltip("Weight given to matching PieceColor")]
    [SerializeField]
    private float pieceColorWeight = 1.0f;

    [Header("Final Accuracy Weights")]

    [Range(0f, 1f)]
    [SerializeField]
    private float pieceAccuracyWeight = 0.40f;

    [Range(0f, 1f)]
    [SerializeField]
    private float connectionAccuracyWeight = 0.40f;

    [Range(0f, 1f)]
    [SerializeField]
    private float orientationAccuracyWeight = 0.20f;

    [Header("Orientation")]
    [SerializeField]
    private float orientationTolerance = 15f;


    /// <summary>
    /// Represents one matched participant piece and target piece
    /// </summary>
    /// 

    [Serializable]

    public class PieceMatch
    {
        public BuildingPiece participantPiece;
        public BuildingPiece targetPiece;

        public float pieceMatchScore;

        public bool typeMatches, colorMatches;

        public bool connectionCorrect;
        public bool orientationCorrect;
    }

    /// <summary>
    /// Complete grading result
    /// </summary>
    /// 
    [Serializable]
    public class GradeResult
    {
        public int targetPieceCount;
        public int participantPieceCount;

        public int matchedPieceCount;

        public int correctTypes, correctColors;

        public int targetConnectionCount;
        public int correctConnectionCount;

        public int orientationChecks;
        public int correctOrientation;

        public int missingPieces, extraPieces;

        public float pieceAccuracy;
        public float connectionAccuracy;
        public float orientationAccuracy;

        public float finalAccuracy;

        public List<PieceMatch> matches = new List<PieceMatch>();

    }

    /// <summary>
    /// Used internally during N x M comparison.
    /// </summary>

    private class CandidateMatch
    {
        public BuildingPiece participant;
        public BuildingPiece target;


        public float score;

        public bool typeMatches;
        public bool colorMatches;
    }

    /// <summary>
    /// Public Grading Function
    /// </summary>

    public GradeResult GradeBuild()
    {
        Debug.Log("=================================");
        Debug.Log("STARTING STRUCTURE GRADING");
        Debug.Log("=================================");

        //--------------------------------------------
        // Step 1: Get all pieces from both structures
        //--------------------------------------------

        List<BuildingPiece> targetPieces = targetBuildRoot.GetComponentsInChildren<BuildingPiece>(true).ToList();

        List<BuildingPiece> participantPieces = participantBuildRoot.GetComponentsInChildren<BuildingPiece>(true).ToList();

        Debug.Log($"Target contains {targetPieces.Count} pieces");
        Debug.Log($"Target contains {participantPieces.Count} pieces");

        GradeResult result = new GradeResult();

        result.targetPieceCount = targetPieces.Count;
        result.participantPieceCount = participantPieces.Count;

        //--------------------------------------------
        // Step 2: N x M Piece Comparison
        //--------------------------------------------

        List<CandidateMatch> candidates = GenerateCandidateMatches(participantPieces, targetPieces);

        //--------------------------------------------
        // Step 3: one-to-one comparison
        //--------------------------------------------

        result.matches = EstablishPieceCorrespondence(candidates);

        result.matchedPieceCount = result.matches.Count;

        //--------------------------------------------
        // Step 4: Determine missing / extra pieces.
        //--------------------------------------------

        result.missingPieces = Mathf.Max(0, targetPieces.Count - result.matches.Count);
        result.extraPieces = Mathf.Max(0, participantPieces.Count - result.matchedPieceCount);

        //--------------------------------------------
        // Step 5: Calculate piece accuracy
        //--------------------------------------------

        CalculatePieceAccuracy(result, targetPieces.Count);

        //--------------------------------------------
        // Step 6: Compare connectivity.
        //--------------------------------------------

        CalculateConnectionAccuracy(result);


        //--------------------------------------------
        // Step 7: Compare orientation
        //--------------------------------------------
        CalculateOrientationAccuracy(result);

        //--------------------------------------------
        // Step 8: Compare orientation
        //--------------------------------------------
        CalculateFinalAccuracy(result);

        //--------------------------------------------
        // Step 9: Compare orientation
        //--------------------------------------------
        PrintResult(result);

        return result;
    }

    //============================================
    // N x M comparison
    //============================================

    private List<CandidateMatch> GenerateCandidateMatches(List<BuildingPiece> targetPieces, List<BuildingPiece> participantPieces)
    {
        List<CandidateMatch> candidates = new List<CandidateMatch>();

        //Participant piece N
        foreach (BuildingPiece participant in participantPieces)
        {
            //compared against every target piece M
            foreach (BuildingPiece target in targetPieces)
            {
                CandidateMatch candidateMatch = ComparePiece(participant, target);

                candidates.Add(candidateMatch);
            }
        }

        Debug.Log($"Generated {candidates.Count} N x M comparisons");

        return candidates;

    }

    //============================================
    // Individual Piece Comparison
    //============================================
    private CandidateMatch ComparePiece(BuildingPiece participant, BuildingPiece target)
    {
        CandidateMatch candidate = new CandidateMatch();
        candidate.participant = participant;
        candidate.target = target;

        float score = 0f;

        // --------------------------
        // Piece Type
        // --------------------------

        candidate.typeMatches = participant.pieceType == target.pieceType;

        if (candidate.typeMatches)
        {
            score += pieceTypeWeight;
        }

        // --------------------------
        // Piece Color
        // --------------------------

        candidate.colorMatches = participant.pieceColor == target.pieceColor;

        if (candidate.colorMatches)
        {
            score += pieceColorWeight;
        }

        candidate.score = score;

        return candidate;

    }

    //============================================
    // One-to-One Piece Matching
    //============================================

    private List<PieceMatch> EstablishPieceCorrespondence(List<CandidateMatch> candidates)
    {
        List<PieceMatch> matches = new List<PieceMatch>();

        HashSet<BuildingPiece> usedParticipantPieces = new HashSet<BuildingPiece>();

        HashSet<BuildingPiece> usedTargetPieces = new HashSet<BuildingPiece>();

        //Highest scoring candidate first
        List<CandidateMatch> sortedCandidates = candidates.OrderByDescending(c => c.score).ToList();

        foreach (CandidateMatch candidate in sortedCandidates)
        {
            // Participant piece already assigned
            if (usedParticipantPieces.Contains(candidate.participant)) continue;

            // Target piece already assigned
            if (usedTargetPieces.Contains(candidate.target)) continue;

            // A piece with completely different type and color should NOT automatically become a match.
            if (candidate.score <= 0f) continue;

            PieceMatch match = new PieceMatch();

            match.participantPiece = candidate.participant;
            match.targetPiece = candidate.target;
            match.pieceMatchScore = candidate.score;
            match.typeMatches = candidate.typeMatches;
            match.colorMatches = candidate.colorMatches;

            matches.Add(match);

            usedParticipantPieces.Add(candidate.participant);
            usedTargetPieces.Add(candidate.target);

        }

        return matches;
    }
    //============================================
    // PIECE ACCURACY
    //============================================

    private void CalculatePieceAccuracy(GradeResult result, int targetPieceCount)
    {
        result.correctTypes = result.matches.Count(m => m.typeMatches);
        result.correctColors = result.matches.Count(m => m.colorMatches);

        if (targetPieceCount == 0)
        {
            result.pieceAccuracy = 0f;
            return;
        }

        float typeAccuracy = (float)result.correctTypes / targetPieceCount;
        float colorAccuracy = (float)result.correctColors / targetPieceCount;

        //Type and color contribute equally here
        result.pieceAccuracy = (typeAccuracy + colorAccuracy) / 2f;

    }

    private void CalculateConnectionAccuracy(GradeResult result)
    {
        int correctConnections = 0;
        int totalTargetConnections = 0;

        //Used so that A-B and B-A aren't counted twice
        HashSet<string> checkedTargetConnections = new HashSet<string>();

        foreach (PieceMatch match in result.matches)
        {
            BuildingPiece targetPiece = match.targetPiece;

            foreach (AttachPoint targetAP in targetPiece.attachPoints)
            {
                // No connection here??
                if (targetAP.attachedAttachPoint == null) continue;

                BuildingPiece connectedTargetPiece = targetAP.attachedAttachPoint.owningPiece;

                //create an order-independent connection key.
                string connectionKey = CreateConnectionKey(targetPiece, connectedTargetPiece);

                //Don't count the same connection twice
                if (checkedTargetConnections.Contains(connectionKey)) continue;

                checkedTargetConnections.Add(connectionKey);

                totalTargetConnections++;

                //Find which participant piece corresponds to the other target piece
                PieceMatch connectedMatch =
                    result.matches.FirstOrDefault(m => m.targetPiece == connectedTargetPiece);

                if (connectedMatch == null) continue;


                BuildingPiece expectedParticipantNeighbor = connectedMatch.participantPiece;

                // ------------------------------------------------
                // IMPORTANT:
                //
                // We DO NOT care which AttachPoint index was used.
                //
                // We only ask:
                //
                // "Is this participant piece connected to the
                // participant piece corresponding to the target
                // neighbor?"
                // ------------------------------------------------

                bool participantHasConnection = ArePiecesConnected(match.participantPiece, expectedParticipantNeighbor);

                if (participantHasConnection)
                {
                    correctConnections++;
                }

            }
        }

        result.targetConnectionCount = totalTargetConnections;
        result.correctConnectionCount = correctConnections;

        if (totalTargetConnections == 0)
        {
            result.connectionAccuracy = 1f;
        }
        else
        {
            result.connectionAccuracy = (float)correctConnections / totalTargetConnections;
        }
    }


    private bool ArePiecesConnected(BuildingPiece pieceA, BuildingPiece pieceB)
    {
        foreach (AttachPoint attachPoint in pieceA.attachPoints)
        {
            if (attachPoint.attachedAttachPoint == null) continue;

            BuildingPiece connectedPiece = attachPoint.attachedAttachPoint.owningPiece;

            if (connectedPiece == pieceB) return true;

        }

        return false;
    }

    // ============================
    // Connection Key
    // ============================

    private string CreateConnectionKey(BuildingPiece a, BuildingPiece b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();

        if (idA < idB)
        {
            return idA + "_" + idB;
        }

        return idB + "_" + idA;
    }


    // ============================
    // Orientation Accuracy
    // ============================
    private void CalculateOrientationAccuracy(GradeResult result)
    {
        int correct = 0;
        int checks = 0;

        HashSet<string> checkConnections = new HashSet<string>();

        foreach (PieceMatch match in result.matches)
        {
            BuildingPiece targetPiece = match.targetPiece;

            foreach (AttachPoint targetAP in targetPiece.attachPoints)
            {
                if (targetAP.attachedAttachPoint == null) continue;

                BuildingPiece targetNeighbor = targetAP.attachedAttachPoint.owningPiece;

                string key = CreateConnectionKey(targetPiece, targetNeighbor);

                if (checkConnections.Contains(key)) continue;

                checkConnections.Add(key);

                PieceMatch neighborMatch = result.matches.FirstOrDefault(m => m.targetPiece == targetNeighbor);

                if (neighborMatch == null) continue;

                BuildingPiece participantPiece = match.participantPiece;
                BuildingPiece participantNeighbor = neighborMatch.participantPiece;

                //They must be connected before orientation makes sense
                if (!ArePiecesConnected(participantPiece, participantNeighbor)) continue;

                checks++;

                bool orientationCorrect = CompareRelativeOrientation(participantPiece, participantNeighbor, targetPiece, targetNeighbor);

                if (orientationCorrect)
                {
                    correct++;
                }

            }
        }

        result.orientationChecks = checks;
        result.correctOrientation = correct;

        if (checks == 0)
        {
            result.orientationAccuracy = 0f;
        }

        else
        {
            result.orientationAccuracy = (float)correct / checks;
        }
    }


    // ============================
    // Relative Orientation
    // ============================
    private bool CompareRelativeOrientation(BuildingPiece participantA, BuildingPiece participantB, BuildingPiece targetA, BuildingPiece targetB)
    {
        // ---------------------------------------
        // Calculate B's rotationn relative to A
        // This means rotating the entire structure in world space
        // will not automatically cause an error
        // --------------------------------------

        Quaternion participantRelativeRotation = Quaternion.Inverse(participantA.transform.rotation) * participantB.transform.rotation;

        Quaternion targetRelativeRotation = Quaternion.Inverse(targetA.transform.rotation) * targetB.transform.rotation;

        float angleDifference = Quaternion.Angle(participantRelativeRotation, targetRelativeRotation);

        return angleDifference <= orientationTolerance;
    }

    // ============================
    // Final Score
    // ============================
    private void CalculateFinalAccuracy(GradeResult result)
    {
        float totalWeight = pieceAccuracyWeight + connectionAccuracyWeight + orientationAccuracyWeight;

        if (totalWeight <= 0f)
        {
            result.finalAccuracy = 0f;
            return;
        }

        float weightedScore = result.pieceAccuracy * pieceAccuracyWeight;

        weightedScore += result.pieceAccuracy * connectionAccuracyWeight;

        weightedScore += result.connectionAccuracy * orientationAccuracyWeight;

        result.finalAccuracy = weightedScore / totalWeight;
    }

    // ============================
    // Debug Report
    // ============================

    private void PrintResult(GradeResult result)
    {
        Debug.Log("=======================================");

        Debug.Log("STRUCTURE GRADING RESULTS");

        Debug.Log("=======================================");

        Debug.Log($"Target Pieces: {result.targetPieceCount}");
        Debug.Log($"Participant Pieces: {result.participantPieceCount}");
        Debug.Log($"Matched Pieces: {result.matchedPieceCount}");
        Debug.Log($"Missing Pieces: {result.missingPieces}");
        Debug.Log($"Extra Pieces: {result.extraPieces}");
        Debug.Log($"Correct Types: {result.correctTypes} / {result.targetPieceCount}");
        Debug.Log($"Correct Colors: {result.correctColors} / {result.targetPieceCount}");
        Debug.Log($"Connections: {result.correctConnectionCount} / {result.targetConnectionCount}");
        Debug.Log($"Orientation: {result.correctOrientation} / {result.orientationChecks}");
        Debug.Log($"Piece Accuracy: {result.pieceAccuracy * 100f:F2}% ");
        Debug.Log($"Connection Accuracy: {result.connectionAccuracy * 100f:F2}% ");
        Debug.Log($"Orientation Accuracy: {result.orientationAccuracy * 100f:F2}% ");
        Debug.Log($"FINAL STRUCTURAL ACCURACY: {result.finalAccuracy * 100f:F2}%");
        Debug.Log("===========================================");

        //Print individual correspondence
        foreach (PieceMatch match in result.matches)
        {
            Debug.Log($"Participant [{match.participantPiece.name}]" +
                $"-> Target [{match.targetPiece.name}]" +
                $"Type: [{match.typeMatches}]" +
                $"Color: [{match.colorMatches}]");

        }

    }

    [ContextMenu("Grade Structure")]

    public void GradeFromInspector()
    {
        GradeBuild();
    }

    //private void Start()
    //{
    //    GradeBuild();
    //}
}