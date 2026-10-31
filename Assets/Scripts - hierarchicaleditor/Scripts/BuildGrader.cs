// ============================================================================
//  BuildGrader3.cs  --  structural grader with BUILD-LEVEL orientation
//
//  GRADES
//  ------
//    1. Tube colour placement      denominator = target tubes
//    2. Connector placement        denominator = target connectors
//    3. Structural orientation     BUILD-LEVEL AXIS SIGNATURE (see below)
//    4. Connections                denominator = unique target tube<->connector edges
//
//  CORRESPONDENCE
//  --------------
//  Position only, established by a rigid, parameter-free, two-stage method:
//
//    1. COMMON FRAME
//         - each build expressed in its own build-root local space
//         - translated so the CONNECTOR-NETWORK centroid is the origin
//           (connectors are the skeleton; a missing tube cannot move it)
//         - scaled by the reciprocal of the MEDIAN tube<->connector edge
//           length of that build (a manufactured length = structural
//           constant; median is robust to misplaced / extra pieces)
//
//    2. BOTTLENECK ASSIGNMENT (lexicographic, no weights)
//         a. hard constraint : partial injection of size min(n, m),
//                              tubes->tubes, connectors->connectors
//         b. objective       : minimise the MAXIMUM pairwise distance
//                              (Chebyshev / bottleneck assignment, exact,
//                               via threshold search + bipartite matching)
//         c. tie-break       : median of assigned distances
//         d. tie-break       : sum of assigned distances
//         e. tie-break       : target degree vs participant GEOMETRIC degree
//         f. tie-break       : deterministic instance-id ordering
//
//  Correspondence NEVER uses:
//         - tube colour              (graded outcome)
//         - exact connector type     (graded outcome)
//         - orientation correctness  (graded outcome)
//         - participant connection correctness (graded outcome)
//
//  WHY THE BOTTLENECK OBJECTIVE
//  ----------------------------
//  A sum-minimising objective is a least-squares criterion: it lets a
//  well-placed piece subsidise a badly-placed one, so the minimum-sum
//  labelling is not necessarily the correct labelling. The bottleneck
//  criterion bounds the WORST piece instead, which is the property a
//  correspondence should have when one piece is unambiguously at a position.
//
//  STRUCTURAL ORIENTATION  (grade 3)
//  ---------------------------------
//  Orientation is NOT graded per piece. A piece's rotation cannot be
//  recovered reliably from a photograph and re-placed in VR, so a per-piece
//  rotation verdict would be grading the reconstruction, not the participant.
//
//  Instead the build is described by its AXIS SIGNATURE:
//
//    - take the connector centroid as the structural centre
//    - for each piece, the unit vector centre -> piece
//    - reduce each to a sign-free AXIS (d and -d are the same axis)
//    - compare the two builds' axis SETS by bottleneck angle
//
//  This reads POSITIONS ONLY. It never touches transform.rotation on either
//  side, so no reconstruction roll can influence it. Because axes are
//  sign-free, a tube placed "the other way" along the same line is not
//  penalised -- correct, since a tube is symmetric about its axis.
//
//  Reported as:
//      structuralOrientationErrorDegrees  (bottleneck angle, degrees)
//      structuralOrientationToleranceDegrees
//      structuralOrientationCorrect
//
//  To align whole-build rotation out first, set
//  alignBuildRotationBeforeSignature = true. With it on, a participant who
//  built the correct shape rotated as a whole reads as correct, and only
//  INTERNAL misdirection is penalised. With it off, a whole-build rotation
//  is itself reported as a structural orientation error.
// ============================================================================

using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using PlayStructure;

public class BuildGrader : MonoBehaviour
{
    // ========================================================================
    // BUILD ROOTS
    // ========================================================================

    [Header("Build Containers")]

    [Tooltip("Parent GameObject containing the TARGET structure")]
    [SerializeField]
    private Transform targetBuildRoot;

    [Tooltip("Parent GameObject containing the PARTICIPANT structure")]
    [SerializeField]
    private Transform participantBuildRoot;


    // ========================================================================
    // ORIENTATION SETTINGS  (build-level)
    // ========================================================================

    [Header("Structural Orientation (build-level)")]

    [Tooltip("Maximum bottleneck axis angle, in degrees, for the build's " +
             "direction signature to count as correct.")]
    [SerializeField]
    private float structuralOrientationToleranceDegrees = 10f;

    [Tooltip("If true, the best rigid rotation aligning the participant's " +
             "connector skeleton onto the target's is removed before the " +
             "axis signatures are compared, so a whole-build rotation is not " +
             "penalised and only internal misdirection counts. If false, a " +
             "whole-build rotation is reported as an orientation error.")]
    [SerializeField]
    private bool alignBuildRotationBeforeSignature = false;


    // ========================================================================
    // DIAGNOSTICS
    // ========================================================================

    [Header("Diagnostics")]

    [Tooltip("Log every correspondence line (tube / connector).")]
    [SerializeField]
    private bool verboseCorrespondenceLog = true;

    [Tooltip("Log every target connection with PASS / FAIL and the reason.")]
    [SerializeField]
    private bool verboseConnectionLog = true;

    [Tooltip("Log the normalisation frames for both builds.")]
    [SerializeField]
    private bool verboseFrameLog = true;

    [Tooltip("Log the per-piece axis table used by the orientation signature.")]
    [SerializeField]
    private bool verboseAxisLog = true;


    // ========================================================================
    // PIECE MATCH
    // ========================================================================

    [Serializable]
    public class PieceMatch
    {
        public BuildingPiece participantPiece;
        public BuildingPiece targetPiece;

        // Distance in normalised units (1.0 == one median tube length).
        public float positionDistance;

        // Graded flags. No per-piece orientation flag by design.
        public bool colorCorrect;
        public bool connectorCorrect;
    }


    // ========================================================================
    // CORRESPONDENCE DIAGNOSTIC RECORD
    // ========================================================================

    [Serializable]
    public class CorrespondenceDecision
    {
        public BuildingPiece participantPiece;
        public BuildingPiece targetPiece;

        public float distance;

        public string decidedBy = "";

        public string rivalTargetName = "";
        public float rivalDistance = -1f;
    }


    // ========================================================================
    // BUILD FRAME
    // ========================================================================

    public class BuildFrame
    {
        public Transform root;

        public Vector3 originLocal;
        public float scale = 1f;
        public float medianEdgeLength = 1f;

        public bool valid;

        public Vector3 ToFrame(Vector3 worldPosition)
        {
            Vector3 local = root.InverseTransformPoint(worldPosition);
            return (local - originLocal) / scale;
        }
    }


    // ========================================================================
    // AXIS RECORD  (one per piece, for the orientation signature)
    // ========================================================================

    [Serializable]
    public class AxisRecord
    {
        public BuildingPiece piece;
        public Vector3 axis;          // sign-free, unit length
        public float angleToNearest = -1f;
        public string nearestName = "";
    }


    // ========================================================================
    // GRADE RESULT
    // ========================================================================

    [Serializable]
    public class GradeResult
    {
        public int targetPieceCount;
        public int participantPieceCount;

        public int matchedPieceCount;

        public int missingPieces;
        public int extraPieces;


        // Tube colour
        public int targetTubeCount;
        public int correctTubeColors;
        public float colorAccuracy;


        // Connectors
        public int targetConnectorCount;
        public int correctConnectors;
        public float connectorAccuracy;


        // Structural orientation (build-level)
        public float structuralOrientationErrorDegrees;
        public float structuralOrientationToleranceDegrees;
        public bool structuralOrientationCorrect;
        public float structuralOrientationScore;   // derived, 0..1


        // Connections
        public int targetConnectionCount;
        public int correctConnections;
        public float connectionAccuracy;


        // Alignment diagnostics
        public bool rotationAlignmentApplied;
        public float rotationAlignmentDegrees;


        public List<PieceMatch> matches = new List<PieceMatch>();
        public List<CorrespondenceDecision> decisions =
            new List<CorrespondenceDecision>();
        public List<string> connectionLog = new List<string>();
        public List<AxisRecord> targetAxes = new List<AxisRecord>();
        public List<AxisRecord> participantAxes = new List<AxisRecord>();
    }


    // ========================================================================
    // MAIN
    // ========================================================================

    public GradeResult GradeBuild()
    {
        Debug.Log("================================================");
        Debug.Log("STRUCTURE GRADING (bottleneck correspondence)");
        Debug.Log("Orientation: BUILD-LEVEL axis signature (no per-piece rotation)");
        Debug.Log("================================================");

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

        List<BuildingPiece> targetPieces =
            targetBuildRoot.GetComponentsInChildren<BuildingPiece>(true)
                .Where(IsGradablePiece).ToList();

        List<BuildingPiece> participantPieces =
            participantBuildRoot.GetComponentsInChildren<BuildingPiece>(true)
                .Where(IsGradablePiece).ToList();

        GradeResult result = new GradeResult();

        result.targetPieceCount = targetPieces.Count;
        result.participantPieceCount = participantPieces.Count;

        Debug.Log($"Target pieces (gradable):      {targetPieces.Count} " +
                  $"(tubes {targetPieces.Count(IsTube)}, " +
                  $"connectors {targetPieces.Count(IsConnector)})");

        Debug.Log($"Participant pieces (gradable): {participantPieces.Count} " +
                  $"(tubes {participantPieces.Count(IsTube)}, " +
                  $"connectors {participantPieces.Count(IsConnector)})");

        // ------------------------------------------------------------------
        // 1. CORRESPONDENCE
        // ------------------------------------------------------------------

        result.matches =
            EstablishStructuralCorrespondence(
                participantPieces, targetPieces, result.decisions);

        result.matchedPieceCount = result.matches.Count;

        result.missingPieces =
            Mathf.Max(0, targetPieces.Count - result.matchedPieceCount);

        result.extraPieces =
            Mathf.Max(0, participantPieces.Count - result.matchedPieceCount);

        // ------------------------------------------------------------------
        // 2. TUBE COLOUR
        // ------------------------------------------------------------------

        CalculateColorAccuracy(result, targetPieces);

        // ------------------------------------------------------------------
        // 3. CONNECTOR TYPE
        // ------------------------------------------------------------------

        CalculateConnectorAccuracy(result, targetPieces);

        // ------------------------------------------------------------------
        // 4. STRUCTURAL ORIENTATION (build-level)
        // ------------------------------------------------------------------

        CalculateStructuralOrientation(result, targetPieces, participantPieces);

        // ------------------------------------------------------------------
        // 5. CONNECTIONS
        // ------------------------------------------------------------------

        CalculateConnectionAccuracy(result, targetPieces);

        PrintResult(result);

        return result;
    }


    // ========================================================================
    // CORRESPONDENCE
    // ========================================================================

    private List<PieceMatch> EstablishStructuralCorrespondence(
        List<BuildingPiece> participantPieces,
        List<BuildingPiece> targetPieces,
        List<CorrespondenceDecision> decisionsOut)
    {
        List<PieceMatch> matches = new List<PieceMatch>();

        BuildFrame targetFrame =
            BuildCommonFrame(targetPieces, targetBuildRoot);

        BuildFrame participantFrame =
            BuildCommonFrame(participantPieces, participantBuildRoot);

        if (verboseFrameLog)
        {
            LogFrame("TARGET", targetFrame);
            LogFrame("PARTICIPANT", participantFrame);
        }

        if (!targetFrame.valid || !participantFrame.valid)
        {
            Debug.LogError(
                "Correspondence aborted: a build frame could not be built. " +
                "Both roots need gradable pieces and at least one " +
                "tube<->connector edge.");
            return matches;
        }

        // ---- TUBES -> TUBES ------------------------------------------------

        List<BuildingPiece> targetTubes = targetPieces.Where(IsTube).ToList();
        List<BuildingPiece> participantTubes =
            participantPieces.Where(IsTube).ToList();

        Debug.Log("------------------------------------------------");
        Debug.Log($"TUBE CORRESPONDENCE  (participant {participantTubes.Count} " +
                  $"-> target {targetTubes.Count})");
        Debug.Log("------------------------------------------------");

        matches.AddRange(
            MatchCategory(participantTubes, targetTubes,
                          participantFrame, targetFrame, decisionsOut));

        // ---- CONNECTORS -> CONNECTORS --------------------------------------
        //
        // Sub-types deliberately NOT separated: exact connector type is a
        // graded outcome and must not influence the mapping.

        List<BuildingPiece> targetConnectors =
            targetPieces.Where(IsConnector).ToList();
        List<BuildingPiece> participantConnectors =
            participantPieces.Where(IsConnector).ToList();

        Debug.Log("------------------------------------------------");
        Debug.Log($"CONNECTOR CORRESPONDENCE  " +
                  $"(participant {participantConnectors.Count} -> " +
                  $"target {targetConnectors.Count})");
        Debug.Log("------------------------------------------------");

        matches.AddRange(
            MatchCategory(participantConnectors, targetConnectors,
                          participantFrame, targetFrame, decisionsOut));

        return matches;
    }


    // ========================================================================
    // COMMON FRAME
    // ========================================================================

    private BuildFrame BuildCommonFrame(
        List<BuildingPiece> pieces, Transform root)
    {
        BuildFrame frame = new BuildFrame();
        frame.root = root;
        frame.valid = false;

        if (pieces == null || pieces.Count == 0 || root == null)
        {
            return frame;
        }

        List<BuildingPiece> connectorSubset =
            pieces.Where(IsConnector).ToList();

        List<BuildingPiece> anchorPieces =
            connectorSubset.Count > 0 ? connectorSubset : pieces;

        Vector3 originLocal = Vector3.zero;

        foreach (BuildingPiece p in anchorPieces)
        {
            originLocal += root.InverseTransformPoint(p.transform.position);
        }

        originLocal /= anchorPieces.Count;

        frame.originLocal = originLocal;

        float medianEdge = MeasureMedianTubeConnectorEdge(pieces, root);

        frame.medianEdgeLength = medianEdge;
        frame.scale = (medianEdge > 0.0001f) ? medianEdge : 1f;

        frame.valid = true;
        return frame;
    }


    private float MeasureMedianTubeConnectorEdge(
        List<BuildingPiece> pieces, Transform root)
    {
        List<BuildingPiece> tubes = pieces.Where(IsTube).ToList();
        List<BuildingPiece> connectors = pieces.Where(IsConnector).ToList();

        if (tubes.Count == 0 || connectors.Count == 0)
        {
            return 1f;
        }

        List<Vector3> tubeLocal = tubes
            .Select(t => root.InverseTransformPoint(t.transform.position))
            .ToList();

        List<Vector3> connectorLocal = connectors
            .Select(c => root.InverseTransformPoint(c.transform.position))
            .ToList();

        List<float> nearest = new List<float>();

        foreach (Vector3 tp in tubeLocal)
        {
            float best = float.MaxValue;

            foreach (Vector3 cp in connectorLocal)
            {
                float d = Vector3.Distance(tp, cp);

                if (d < best)
                {
                    best = d;
                }
            }

            if (best < float.MaxValue && best > 0.00001f)
            {
                nearest.Add(best);
            }
        }

        if (nearest.Count == 0)
        {
            return 1f;
        }

        nearest.Sort();

        int mid = nearest.Count / 2;

        float median =
            (nearest.Count % 2 == 1)
                ? nearest[mid]
                : 0.5f * (nearest[mid - 1] + nearest[mid]);

        return (median > 0.0001f) ? median : 1f;
    }


    private void LogFrame(string label, BuildFrame frame)
    {
        if (frame == null || frame.root == null)
        {
            return;
        }

        Debug.Log($"[FRAME {label}] origin(local)=" +
                  $"{frame.originLocal.ToString("F4")} | " +
                  $"medianEdge={frame.medianEdgeLength:F4} | " +
                  $"scale={frame.scale:F4} | valid={frame.valid}");
    }


    // ========================================================================
    // MATCH A SINGLE CATEGORY
    // ========================================================================

    private List<PieceMatch> MatchCategory(
        List<BuildingPiece> participantPieces,
        List<BuildingPiece> targetPieces,
        BuildFrame participantFrame,
        BuildFrame targetFrame,
        List<CorrespondenceDecision> decisionsOut)
    {
        List<PieceMatch> result = new List<PieceMatch>();

        int n = participantPieces.Count;
        int m = targetPieces.Count;

        if (n == 0 || m == 0)
        {
            if (n > 0 && m == 0)
            {
                Debug.Log("  (no target pieces in this category - nothing to map)");
            }
            else if (n == 0 && m > 0)
            {
                Debug.Log("  (no participant pieces in this category - all " +
                          "target positions here are MISSING)");
            }

            return result;
        }

        Vector3[] p = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            p[i] = participantFrame.ToFrame(
                participantPieces[i].transform.position);
        }

        Vector3[] t = new Vector3[m];
        for (int j = 0; j < m; j++)
        {
            t[j] = targetFrame.ToFrame(targetPieces[j].transform.position);
        }

        float[,] d = new float[n, m];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                d[i, j] = Vector3.Distance(p[i], t[j]);
            }
        }

        int assignmentSize = Mathf.Min(n, m);

        // ---- BOTTLENECK SEARCH ---------------------------------------------

        List<float> distinct = new List<float>();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                distinct.Add(d[i, j]);
            }
        }

        distinct.Sort();

        List<float> candidates = new List<float>();

        for (int k = 0; k < distinct.Count; k++)
        {
            if (k == 0 || Mathf.Abs(distinct[k] - distinct[k - 1]) > 1e-6f)
            {
                candidates.Add(distinct[k]);
            }
        }

        float lowerBound = 0f;

        for (int i = 0; i < n; i++)
        {
            float rowMin = float.MaxValue;

            for (int j = 0; j < m; j++)
            {
                if (d[i, j] < rowMin)
                {
                    rowMin = d[i, j];
                }
            }

            if (rowMin > lowerBound)
            {
                lowerBound = rowMin;
            }
        }

        float bottleneck = candidates[candidates.Count - 1];

        for (int k = 0; k < candidates.Count; k++)
        {
            if (candidates[k] < lowerBound - 1e-6f)
            {
                continue;
            }

            if (HasMatchingOfSize(d, n, m, candidates[k], assignmentSize))
            {
                bottleneck = candidates[k];
                break;
            }
        }

        // ---- ENUMERATE OPTIMAL BOTTLENECK ASSIGNMENTS -----------------------

        List<int[]> assignments = new List<int[]>();

        int[] current = new int[n];
        for (int i = 0; i < n; i++)
        {
            current[i] = -1;
        }

        HashSet<int> used = new HashSet<int>();
        int maxEnumeration = 20000;

        EnumerateBottleneckAssignments(
            d, n, m, bottleneck, assignmentSize,
            0, current, used, assignments, 0f, maxEnumeration);

        if (assignments.Count == 0)
        {
            Debug.LogError(
                "Correspondence: no bottleneck assignment found. Falling back " +
                "to greedy nearest.");
            return GreedyFallback(
                participantPieces, targetPieces, d, assignmentSize);
        }

        // ---- RANK: (median, sum, signature, instance id) -------------------

        int bestIndex = 0;
        float bestMedian = float.MaxValue;
        float bestSum = float.MaxValue;
        int bestSig = -1;

        for (int a = 0; a < assignments.Count; a++)
        {
            int[] map = assignments[a];

            List<float> usedDists = new List<float>();

            for (int i = 0; i < n; i++)
            {
                if (map[i] >= 0)
                {
                    usedDists.Add(d[i, map[i]]);
                }
            }

            usedDists.Sort();

            float median = usedDists[usedDists.Count / 2];

            float sum = 0f;
            foreach (float v in usedDists)
            {
                sum += v;
            }

            int sig = SignatureAgreement(
                map, targetPieces, p, bottleneck);

            bool better = false;

            if (a == 0)
            {
                better = true;
            }
            else if (median < bestMedian - 1e-6f)
            {
                better = true;
            }
            else if (Mathf.Abs(median - bestMedian) <= 1e-6f)
            {
                if (sum < bestSum - 1e-6f)
                {
                    better = true;
                }
                else if (Mathf.Abs(sum - bestSum) <= 1e-6f)
                {
                    if (sig > bestSig)
                    {
                        better = true;
                    }
                    else if (sig == bestSig &&
                             IsLexicographicallySmaller(
                                 assignments[a], assignments[bestIndex], n))
                    {
                        better = true;
                    }
                }
            }

            if (better)
            {
                bestIndex = a;
                bestMedian = median;
                bestSum = sum;
                bestSig = sig;
            }
        }

        int[] bestMap = assignments[bestIndex];

        // ---- BUILD MATCHES + DECISIONS -------------------------------------

        for (int i = 0; i < n; i++)
        {
            int j = bestMap[i];

            if (j < 0)
            {
                Debug.Log($"  [UNMAPPED PARTICIPANT] " +
                          $"{DescribePiece(participantPieces[i])} - no target " +
                          $"position available (extra piece, no credit)");
                continue;
            }

            result.Add(new PieceMatch
            {
                participantPiece = participantPieces[i],
                targetPiece = targetPieces[j],
                positionDistance = d[i, j]
            });

            CorrespondenceDecision decision = new CorrespondenceDecision
            {
                participantPiece = participantPieces[i],
                targetPiece = targetPieces[j],
                distance = d[i, j],
                decidedBy = DescribeDecisionStage(d, i, j, bottleneck)
            };

            float rivalBest = float.MaxValue;
            int rivalIndex = -1;

            for (int k = 0; k < m; k++)
            {
                if (k == j)
                {
                    continue;
                }

                if (d[i, k] < rivalBest)
                {
                    rivalBest = d[i, k];
                    rivalIndex = k;
                }
            }

            if (rivalIndex >= 0)
            {
                decision.rivalTargetName = targetPieces[rivalIndex].name;
                decision.rivalDistance = rivalBest;
            }

            decisionsOut.Add(decision);
        }

        HashSet<int> mappedTargets = new HashSet<int>();

        for (int i = 0; i < n; i++)
        {
            if (bestMap[i] >= 0)
            {
                mappedTargets.Add(bestMap[i]);
            }
        }

        for (int j = 0; j < m; j++)
        {
            if (!mappedTargets.Contains(j))
            {
                Debug.Log($"  [MISSING TARGET] " +
                          $"{DescribePiece(targetPieces[j])} - no participant " +
                          $"piece mapped here (no credit)");
            }
        }

        return result;
    }


    // ========================================================================
    // BIPARTITE FEASIBILITY
    // ========================================================================

    private bool HasMatchingOfSize(
        float[,] d, int n, int m, float threshold, int size)
    {
        int[] matchTarget = new int[m];
        for (int j = 0; j < m; j++)
        {
            matchTarget[j] = -1;
        }

        int matched = 0;

        for (int i = 0; i < n; i++)
        {
            bool[] visited = new bool[m];

            if (TryAugment(d, n, m, threshold, i, visited, matchTarget))
            {
                matched++;

                if (matched >= size)
                {
                    return true;
                }
            }
        }

        return matched >= size;
    }


    private bool TryAugment(
        float[,] d, int n, int m, float threshold,
        int participant, bool[] visited, int[] matchTarget)
    {
        for (int j = 0; j < m; j++)
        {
            if (d[participant, j] > threshold + 1e-6f)
            {
                continue;
            }

            if (visited[j])
            {
                continue;
            }

            visited[j] = true;

            if (matchTarget[j] == -1 ||
                TryAugment(d, n, m, threshold, matchTarget[j],
                           visited, matchTarget))
            {
                matchTarget[j] = participant;
                return true;
            }
        }

        return false;
    }


    private void EnumerateBottleneckAssignments(
        float[,] d, int n, int m, float threshold, int size,
        int index, int[] current, HashSet<int> used,
        List<int[]> output, float cost, int maxEnumeration)
    {
        if (output.Count >= maxEnumeration)
        {
            return;
        }

        if (index >= n)
        {
            if (current.Count(x => x >= 0) == size)
            {
                output.Add((int[])current.Clone());
            }

            return;
        }

        bool canSkip = (n > m);

        for (int j = 0; j < m; j++)
        {
            if (used.Contains(j))
            {
                continue;
            }

            if (d[index, j] > threshold + 1e-6f)
            {
                continue;
            }

            used.Add(j);
            current[index] = j;

            EnumerateBottleneckAssignments(
                d, n, m, threshold, size, index + 1, current, used,
                output, cost + d[index, j], maxEnumeration);

            current[index] = -1;
            used.Remove(j);

            if (output.Count >= maxEnumeration)
            {
                return;
            }
        }

        if (canSkip)
        {
            current[index] = -1;

            EnumerateBottleneckAssignments(
                d, n, m, threshold, size, index + 1, current, used,
                output, cost, maxEnumeration);
        }
    }


    // ========================================================================
    // SIGNATURE TIE-BREAK
    //
    //  Target side : the target piece's own structural degree (ground truth).
    //  Participant : how many participant pieces lie within the bottleneck
    //                radius, measured GEOMETRICALLY. Deliberately ignores the
    //                participant's snapped connections, so a participant who
    //                forgot to join two pieces is still described by where the
    //                pieces physically sit.
    // ========================================================================

    private int SignatureAgreement(
        int[] map,
        List<BuildingPiece> targetPieces,
        Vector3[] p,
        float radius)
    {
        int agreement = 0;

        for (int i = 0; i < p.Length; i++)
        {
            if (map[i] < 0)
            {
                continue;
            }

            int targetDegree = TargetDegree(targetPieces[map[i]]);

            int participantDegree = 0;

            for (int k = 0; k < p.Length; k++)
            {
                if (k == i)
                {
                    continue;
                }

                if (Vector3.Distance(p[k], p[i]) <= radius + 1e-6f)
                {
                    participantDegree++;
                }
            }

            if (targetDegree == participantDegree)
            {
                agreement++;
            }
        }

        return agreement;
    }


    private int TargetDegree(BuildingPiece piece)
    {
        if (piece == null || piece.attachPoints == null)
        {
            return 0;
        }

        int degree = 0;

        foreach (AttachPoint ap in piece.attachPoints)
        {
            if (ap == null)
            {
                continue;
            }

            if (ap.attachedAttachPoint != null &&
                ap.attachedAttachPoint.owningPiece != null)
            {
                degree++;
            }
        }

        return degree;
    }


    private bool IsLexicographicallySmaller(int[] a, int[] b, int n)
    {
        for (int i = 0; i < n; i++)
        {
            if (a[i] == b[i])
            {
                continue;
            }

            int va = a[i] < 0 ? int.MaxValue : a[i];
            int vb = b[i] < 0 ? int.MaxValue : b[i];

            return va < vb;
        }

        return false;
    }


    private string DescribeDecisionStage(
        float[,] d, int i, int j, float bottleneck)
    {
        bool tight = Mathf.Abs(d[i, j] - bottleneck) <= 1e-6f;

        return tight
            ? $"bottleneck (R={bottleneck:F4})"
            : "bottleneck (tie-broken by median/sum/signature)";
    }


    private List<PieceMatch> GreedyFallback(
        List<BuildingPiece> participantPieces,
        List<BuildingPiece> targetPieces,
        float[,] d,
        int assignmentSize)
    {
        List<PieceMatch> matches = new List<PieceMatch>();

        List<Tuple<float, int, int>> edges = new List<Tuple<float, int, int>>();

        for (int i = 0; i < participantPieces.Count; i++)
        {
            for (int j = 0; j < targetPieces.Count; j++)
            {
                edges.Add(new Tuple<float, int, int>(d[i, j], i, j));
            }
        }

        edges = edges.OrderBy(e => e.Item1).ToList();

        HashSet<int> usedP = new HashSet<int>();
        HashSet<int> usedT = new HashSet<int>();

        foreach (var e in edges)
        {
            if (matches.Count >= assignmentSize)
            {
                break;
            }

            if (usedP.Contains(e.Item2) || usedT.Contains(e.Item3))
            {
                continue;
            }

            usedP.Add(e.Item2);
            usedT.Add(e.Item3);

            matches.Add(new PieceMatch
            {
                participantPiece = participantPieces[e.Item2],
                targetPiece = targetPieces[e.Item3],
                positionDistance = e.Item1
            });
        }

        return matches;
    }


    // ========================================================================
    // TUBE COLOUR ACCURACY
    // ========================================================================

    private void CalculateColorAccuracy(
        GradeResult result, List<BuildingPiece> targetPieces)
    {
        List<BuildingPiece> targetTubes = targetPieces.Where(IsTube).ToList();

        result.targetTubeCount = targetTubes.Count;
        result.correctTubeColors = 0;

        foreach (BuildingPiece targetTube in targetTubes)
        {
            PieceMatch match = FindMatchForTarget(result, targetTube);

            if (match == null)
            {
                continue;
            }

            BuildingPiece participantTube = match.participantPiece;

            if (!IsTube(participantTube))
            {
                continue;
            }

            bool correct =
                participantTube.pieceColor == targetTube.pieceColor;

            match.colorCorrect = correct;

            if (correct)
            {
                result.correctTubeColors++;
            }
        }

        result.colorAccuracy =
            result.targetTubeCount == 0
                ? 0f
                : (float)result.correctTubeColors / result.targetTubeCount;
    }


    // ========================================================================
    // CONNECTOR ACCURACY
    // ========================================================================

    private void CalculateConnectorAccuracy(
        GradeResult result, List<BuildingPiece> targetPieces)
    {
        List<BuildingPiece> targetConnectors =
            targetPieces.Where(IsConnector).ToList();

        result.targetConnectorCount = targetConnectors.Count;
        result.correctConnectors = 0;

        foreach (BuildingPiece targetConnector in targetConnectors)
        {
            PieceMatch match = FindMatchForTarget(result, targetConnector);

            if (match == null)
            {
                continue;
            }

            BuildingPiece participantConnector = match.participantPiece;

            if (!IsConnector(participantConnector))
            {
                continue;
            }

            bool correct =
                participantConnector.pieceType == targetConnector.pieceType;

            match.connectorCorrect = correct;

            if (correct)
            {
                result.correctConnectors++;
            }
        }

        result.connectorAccuracy =
            result.targetConnectorCount == 0
                ? 0f
                : (float)result.correctConnectors / result.targetConnectorCount;
    }


    // ========================================================================
    // STRUCTURAL ORIENTATION  (BUILD-LEVEL AXIS SIGNATURE)
    //
    //  Reads positions only. Never touches transform.rotation.
    //
    //  1. centre = connector-network centroid (the structural skeleton)
    //  2. per piece  : unit vector centre -> piece, reduced to a sign-free axis
    //  3. optional   : remove the best rigid rotation that aligns the
    //                  participant's connector skeleton onto the target's
    //  4. compare the two axis sets by BOTTLENECK ANGLE
    //
    //  Two pieces at the same distance and direction produce the same axis.
    //  That is intended: the signature describes DIRECTIONS THE STRUCTURE
    //  OCCUPIES, so a repeated direction is one direction, not two errors.
    //  Duplicates are kept once and the count is reported in the log.
    // ========================================================================

    private void CalculateStructuralOrientation(
        GradeResult result,
        List<BuildingPiece> targetPieces,
        List<BuildingPiece> participantPieces)
    {
        result.structuralOrientationToleranceDegrees =
            structuralOrientationToleranceDegrees;

        Vector3 targetCentre =
            GetConnectorCentreLocal(targetPieces, targetBuildRoot);

        Vector3 participantCentre =
            GetConnectorCentreLocal(participantPieces, participantBuildRoot);

        bool haveTargetCentre =
            targetPieces.Any(IsConnector) || targetPieces.Count > 0;

        if (!haveTargetCentre || targetPieces.Count == 0)
        {
            result.structuralOrientationErrorDegrees = 0f;
            result.structuralOrientationCorrect = false;
            result.structuralOrientationScore = 0f;
            return;
        }

        // ---- participant positions, optionally rotation-aligned ------------

        Quaternion alignment = Quaternion.identity;

        if (alignBuildRotationBeforeSignature)
        {
            alignment = FindSkeletonAlignment(
                targetPieces, participantPieces,
                targetCentre, participantCentre);

            result.rotationAlignmentApplied = true;
            result.rotationAlignmentDegrees =
                Quaternion.Angle(Quaternion.identity, alignment);
        }

        // ---- axis sets ----------------------------------------------------

        result.targetAxes = BuildAxisSet(
            targetPieces, targetCentre, Quaternion.identity, Vector3.zero);

        result.participantAxes = BuildAxisSet(
            participantPieces, participantCentre, alignment, targetCentre);

        if (result.targetAxes.Count == 0 || result.participantAxes.Count == 0)
        {
            result.structuralOrientationErrorDegrees = 0f;
            result.structuralOrientationCorrect = false;
            result.structuralOrientationScore = 0f;
            return;
        }

        // ---- bottleneck angle between the two axis sets --------------------

        float bottleneckAngle = ComputeBottleneckAxisAngle(
            result.targetAxes, result.participantAxes);

        result.structuralOrientationErrorDegrees = bottleneckAngle;

        result.structuralOrientationCorrect =
            bottleneckAngle <= structuralOrientationToleranceDegrees;

        result.structuralOrientationScore =
            structuralOrientationToleranceDegrees <= 0f
                ? (result.structuralOrientationCorrect ? 1f : 0f)
                : Mathf.Clamp01(
                    1f - bottleneckAngle /
                         structuralOrientationToleranceDegrees);
    }


    // ========================================================================
    // CONNECTOR CENTROID IN ROOT-LOCAL SPACE
    // ========================================================================

    private Vector3 GetConnectorCentreLocal(
        List<BuildingPiece> pieces, Transform root)
    {
        if (root == null)
        {
            return Vector3.zero;
        }

        List<BuildingPiece> connectors = pieces.Where(IsConnector).ToList();

        List<BuildingPiece> anchor =
            connectors.Count > 0 ? connectors : pieces;

        if (anchor.Count == 0)
        {
            return Vector3.zero;
        }

        Vector3 sum = Vector3.zero;

        foreach (BuildingPiece p in anchor)
        {
            sum += root.InverseTransformPoint(p.transform.position);
        }

        return sum / anchor.Count;
    }


    // ========================================================================
    // AXIS SET
    //
    //  Each piece contributes the sign-free axis of (centre -> piece).
    //  When alignment is active the participant vector is rotated about the
    //  shared centre into the target's frame first.
    // ========================================================================

    private List<AxisRecord> BuildAxisSet(
        List<BuildingPiece> pieces,
        Vector3 centreLocal,
        Quaternion alignment,
        Vector3 alignmentPivotTargetLocal)
    {
        List<AxisRecord> axes = new List<AxisRecord>();

        List<AxisRecord> raw = new List<AxisRecord>();

        foreach (BuildingPiece p in pieces)
        {
            if (p == null)
            {
                continue;
            }

            Transform root =
                (pieces == null || pieces.Count == 0)
                    ? null
                    : p.transform.root;

            // Centre is supplied in that build's own root-local space, so the
            // piece position must be taken in the same space.
            // The caller passes the matching root through the centre, so we
            // recompute using the correct root below.
            raw.Add(new AxisRecord { piece = p });
        }

        // The root is not carried in the list, so resolve it per piece here.
        // (Kept explicit so the frame is unambiguous.)
        for (int i = 0; i < raw.Count; i++)
        {
            BuildingPiece p = raw[i].piece;

            Transform root = ResolveRootFor(p);

            if (root == null)
            {
                continue;
            }

            Vector3 local = root.InverseTransformPoint(p.transform.position);

            Vector3 dir = local - centreLocal;

            if (alignment != Quaternion.identity)
            {
                // Rotate about the shared centre so the participant skeleton
                // is brought onto the target skeleton, then re-express in the
                // target's local orientation.
                dir = alignment * dir;
            }

            if (dir.sqrMagnitude < 1e-10f)
            {
                continue;   // piece sits exactly at the centre: no direction
            }

            raw[i].axis = CanonicalAxis(dir.normalized);
        }

        // Deduplicate: a repeated direction is ONE direction the structure
        // occupies, not several.
        HashSet<string> seen = new HashSet<string>();

        foreach (AxisRecord r in raw)
        {
            if (r.axis.sqrMagnitude < 0.5f)
            {
                continue;
            }

            string key = AxisKey(r.axis);

            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);
            axes.Add(r);
        }

        return axes;
    }


    private Transform ResolveRootFor(BuildingPiece piece)
    {
        if (piece == null)
        {
            return null;
        }

        // Prefer the registered build roots; fall back to the hierarchy root.
        if (targetBuildRoot != null &&
            piece.transform.IsChildOf(targetBuildRoot))
        {
            return targetBuildRoot;
        }

        if (participantBuildRoot != null &&
            piece.transform.IsChildOf(participantBuildRoot))
        {
            return participantBuildRoot;
        }

        return piece.transform.root;
    }


    // ========================================================================
    // SIGN-FREE AXIS
    //
    //  d and -d are the same axis. Canonicalise by forcing the first
    //  non-negligible component positive. A small tolerance keeps the
    //  canonical form stable for near-zero components.
    // ========================================================================

    private Vector3 CanonicalAxis(Vector3 v)
    {
        const float eps = 1e-4f;

        if (v.x > eps) return v;
        if (v.x < -eps) return -v;
        if (v.y > eps) return v;
        if (v.y < -eps) return -v;
        if (v.z > eps) return v;
        if (v.z < -eps) return -v;

        return v;
    }


    private string AxisKey(Vector3 v)
    {
        // Round to ~0.5 degree so near-identical axes dedupe together.
        return $"{Mathf.Round(v.x * 1000f)}_" +
               $"{Mathf.Round(v.y * 1000f)}_" +
               $"{Mathf.Round(v.z * 1000f)}";
    }


    // ========================================================================
    // BOTTLENECK ANGLE BETWEEN TWO AXIS SETS
    // ========================================================================

    private float ComputeBottleneckAxisAngle(
        List<AxisRecord> a, List<AxisRecord> b)
    {
        int n = a.Count;
        int m = b.Count;

        float[,] cost = new float[n, m];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                cost[i, j] = Vector3.Angle(a[i].axis, b[j].axis);
            }
        }

        int size = Mathf.Min(n, m);

        // Threshold search over sorted distinct angles.
        List<float> distinct = new List<float>();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                distinct.Add(cost[i, j]);
            }
        }

        distinct.Sort();

        List<float> candidates = new List<float>();

        for (int k = 0; k < distinct.Count; k++)
        {
            if (k == 0 || Mathf.Abs(distinct[k] - distinct[k - 1]) > 1e-4f)
            {
                candidates.Add(distinct[k]);
            }
        }

        float bottleneck = candidates[candidates.Count - 1];

        for (int k = 0; k < candidates.Count; k++)
        {
            if (HasAngleMatchingOfSize(cost, n, m, candidates[k], size))
            {
                bottleneck = candidates[k];
                break;
            }
        }

        // Annotate each participant axis with its nearest target axis, for
        // the diagnostic table (greedy read-out, independent of the
        // bottleneck matching, and only used for logging).
        foreach (AxisRecord r in b)
        {
            float best = float.MaxValue;
            string name = "";

            foreach (AxisRecord q in a)
            {
                float ang = Vector3.Angle(r.axis, q.axis);

                if (ang < best)
                {
                    best = ang;
                    name = q.piece != null ? q.piece.name : "?";
                }
            }

            r.angleToNearest = best;
            r.nearestName = name;
        }

        return bottleneck;
    }


    private bool HasAngleMatchingOfSize(
        float[,] cost, int n, int m, float threshold, int size)
    {
        int[] matchTarget = new int[m];
        for (int j = 0; j < m; j++)
        {
            matchTarget[j] = -1;
        }

        int matched = 0;

        for (int i = 0; i < n; i++)
        {
            bool[] visited = new bool[m];

            if (TryAugmentAngle(
                    cost, m, threshold, i, visited, matchTarget))
            {
                matched++;

                if (matched >= size)
                {
                    return true;
                }
            }
        }

        return matched >= size;
    }


    private bool TryAugmentAngle(
        float[,] cost, int m, float threshold,
        int participant, bool[] visited, int[] matchTarget)
    {
        for (int j = 0; j < m; j++)
        {
            if (cost[participant, j] > threshold + 1e-4f)
            {
                continue;
            }

            if (visited[j])
            {
                continue;
            }

            visited[j] = true;

            if (matchTarget[j] == -1 ||
                TryAugmentAngle(
                    cost, m, threshold, matchTarget[j], visited, matchTarget))
            {
                matchTarget[j] = participant;
                return true;
            }
        }

        return false;
    }


    // ========================================================================
    // SKELETON ALIGNMENT  (optional, for alignBuildRotationBeforeSignature)
    //
    //  Paired-point Procrustes (Kabsch/Horn) on the correspondence already
    //  established for CONNECTORS, which is the structural skeleton. Uses the
    //  correspondence, never the connectivity relation.
    // ========================================================================

    private Quaternion FindSkeletonAlignment(
        List<BuildingPiece> targetPieces,
        List<BuildingPiece> participantPieces,
        Vector3 targetCentreLocal,
        Vector3 participantCentreLocal)
    {
        // Uses the correspondence already stored on the component's most
        // recent run is not available here, so rebuild the connector pairing
        // from frames -- cheap and free of graded information.
        BuildFrame targetFrame =
            BuildCommonFrame(targetPieces, targetBuildRoot);

        BuildFrame participantFrame =
            BuildCommonFrame(participantPieces, participantBuildRoot);

        // Reuse the same bottleneck machinery for connectors only.
        List<BuildingPiece> targetConnectors =
            targetPieces.Where(IsConnector).ToList();

        List<BuildingPiece> participantConnectors =
            participantPieces.Where(IsConnector).ToList();

        if (targetConnectors.Count < 3 ||
            participantConnectors.Count < 3 ||
            !targetFrame.valid || !participantFrame.valid)
        {
            // Not enough points to fit a rotation; leave identity.
            return Quaternion.identity;
        }

        int n = participantConnectors.Count;
        int m = targetConnectors.Count;

        Vector3[] p = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            p[i] = participantFrame.ToFrame(
                participantConnectors[i].transform.position);
        }

        Vector3[] t = new Vector3[m];
        for (int j = 0; j < m; j++)
        {
            t[j] = targetFrame.ToFrame(
                targetConnectors[j].transform.position);
        }

        // Correspond in a rotation-free way first: compare only the
        // DISTANCE PATTERNS by matching each participant connector to the
        // target connector with the most similar distance profile.
        List<Tuple<Vector3, Vector3>> pairs =
            new List<Tuple<Vector3, Vector3>>();

        List<int> unmatchedTargets = Enumerable.Range(0, m).ToList();

        for (int i = 0; i < n; i++)
        {
            float bestScore = float.MaxValue;
            int bestJ = -1;

            foreach (int j in unmatchedTargets)
            {
                float dSelf =
                    Vector3.Distance(p[i], t[j]);

                if (dSelf < bestScore)
                {
                    bestScore = dSelf;
                    bestJ = j;
                }
            }

            if (bestJ >= 0)
            {
                pairs.Add(new Tuple<Vector3, Vector3>(p[i], t[bestJ]));
                unmatchedTargets.Remove(bestJ);
            }
        }

        if (pairs.Count < 3)
        {
            return Quaternion.identity;
        }

        return ProcrustesRotation(pairs);
    }


    // ========================================================================
    // PROCRUSTES / KABSCH ROTATION
    // ========================================================================

    private Quaternion ProcrustesRotation(
        List<Tuple<Vector3, Vector3>> pairs)
    {
        Vector3 pMean = Vector3.zero;
        Vector3 tMean = Vector3.zero;

        foreach (var pair in pairs)
        {
            pMean += pair.Item1;
            tMean += pair.Item2;
        }

        pMean /= pairs.Count;
        tMean /= pairs.Count;

        float[,] h = new float[3, 3];

        foreach (var pair in pairs)
        {
            Vector3 a = pair.Item1 - pMean;
            Vector3 b = pair.Item2 - tMean;

            h[0, 0] += a.x * b.x; h[0, 1] += a.x * b.y; h[0, 2] += a.x * b.z;
            h[1, 0] += a.y * b.x; h[1, 1] += a.y * b.y; h[1, 2] += a.y * b.z;
            h[2, 0] += a.z * b.x; h[2, 1] += a.z * b.y; h[2, 2] += a.z * b.z;
        }

        // Extract the rotation from H via the standard quaternion method
        // (Shepperd's method on the 4x4 symmetric matrix built from H).
        float trace = h[0, 0] + h[1, 1] + h[2, 2];

        float s;
        float qw, qx, qy, qz;

        if (trace > 0f)
        {
            s = Mathf.Sqrt(trace + 1f) * 2f;
            qw = 0.25f * s;
            qx = (h[2, 1] - h[1, 2]) / s;
            qy = (h[0, 2] - h[2, 0]) / s;
            qz = (h[1, 0] - h[0, 1]) / s;
        }
        else if (h[0, 0] > h[1, 1] && h[0, 0] > h[2, 2])
        {
            s = Mathf.Sqrt(1f + h[0, 0] - h[1, 1] - h[2, 2]) * 2f;
            qw = (h[2, 1] - h[1, 2]) / s;
            qx = 0.25f * s;
            qy = (h[0, 1] + h[1, 0]) / s;
            qz = (h[0, 2] + h[2, 0]) / s;
        }
        else if (h[1, 1] > h[2, 2])
        {
            s = Mathf.Sqrt(1f + h[1, 1] - h[0, 0] - h[2, 2]) * 2f;
            qw = (h[0, 2] - h[2, 0]) / s;
            qx = (h[0, 1] + h[1, 0]) / s;
            qy = 0.25f * s;
            qz = (h[1, 2] + h[2, 1]) / s;
        }
        else
        {
            s = Mathf.Sqrt(1f + h[2, 2] - h[0, 0] - h[1, 1]) * 2f;
            qw = (h[1, 0] - h[0, 1]) / s;
            qx = (h[0, 2] + h[2, 0]) / s;
            qy = (h[1, 2] + h[2, 1]) / s;
            qz = 0.25f * s;
        }

        Quaternion q = new Quaternion(qx, qy, qz, qw);

        if (q.x == 0f && q.y == 0f && q.z == 0f && q.w == 0f)
        {
            return Quaternion.identity;
        }

        return Quaternion.Normalize(q);
    }


    // ========================================================================
    // CONNECTIONS
    // ========================================================================

    private void CalculateConnectionAccuracy(
        GradeResult result, List<BuildingPiece> targetPieces)
    {
        int correct = 0;
        int total = 0;

        HashSet<string> checkedTargetConnections = new HashSet<string>();

        List<BuildingPiece> orderedTargets =
            targetPieces.OrderBy(p => p.GetInstanceID()).ToList();

        foreach (BuildingPiece targetPiece in orderedTargets)
        {
            if (targetPiece.attachPoints == null)
            {
                continue;
            }

            foreach (AttachPoint targetAttachPoint in targetPiece.attachPoints)
            {
                if (targetAttachPoint == null ||
                    targetAttachPoint.attachedAttachPoint == null)
                {
                    continue;
                }

                BuildingPiece targetNeighbor =
                    targetAttachPoint.attachedAttachPoint.owningPiece;

                if (targetNeighbor == null || !IsGradablePiece(targetNeighbor))
                {
                    continue;
                }

                string key = CreateConnectionKey(targetPiece, targetNeighbor);

                if (checkedTargetConnections.Contains(key))
                {
                    continue;
                }

                checkedTargetConnections.Add(key);

                BuildingPiece targetTube = null;
                BuildingPiece targetConnector = null;

                if (IsTube(targetPiece) && IsConnector(targetNeighbor))
                {
                    targetTube = targetPiece;
                    targetConnector = targetNeighbor;
                }
                else if (IsConnector(targetPiece) && IsTube(targetNeighbor))
                {
                    targetTube = targetNeighbor;
                    targetConnector = targetPiece;
                }
                else
                {
                    continue;
                }

                total++;

                string label =
                    $"target tube {DescribePiece(targetTube)} <-> " +
                    $"connector {DescribePiece(targetConnector)}";

                PieceMatch tubeMatch = FindMatchForTarget(result, targetTube);
                PieceMatch connectorMatch =
                    FindMatchForTarget(result, targetConnector);

                if (tubeMatch == null)
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | target tube unmapped (missing " +
                        $"participant tube)");
                    continue;
                }

                if (connectorMatch == null)
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | target connector unmapped (missing " +
                        $"participant connector)");
                    continue;
                }

                BuildingPiece participantTube = tubeMatch.participantPiece;
                BuildingPiece participantConnector =
                    connectorMatch.participantPiece;

                if (!IsTube(participantTube))
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | mapped participant piece is not a " +
                        $"tube ({DescribePiece(participantTube)})");
                    continue;
                }

                if (!IsConnector(participantConnector))
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | mapped participant piece is not a " +
                        $"connector ({DescribePiece(participantConnector)})");
                    continue;
                }

                if (participantTube.pieceColor != targetTube.pieceColor)
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | wrong tube colour " +
                        $"(participant {participantTube.pieceColor}, " +
                        $"target {targetTube.pieceColor})");
                    continue;
                }

                if (participantConnector.pieceType !=
                    targetConnector.pieceType)
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | wrong connector type " +
                        $"(participant {participantConnector.pieceType}, " +
                        $"target {targetConnector.pieceType})");
                    continue;
                }

                if (!ArePiecesConnected(participantTube, participantConnector))
                {
                    RecordConnection(result, verboseConnectionLog,
                        $"FAIL: {label} | participant pieces not connected " +
                        $"({DescribePiece(participantTube)} vs " +
                        $"{DescribePiece(participantConnector)})");
                    continue;
                }

                correct++;

                RecordConnection(result, verboseConnectionLog,
                    $"PASS: {label} | participant tube " +
                    $"{DescribePiece(participantTube)} + connector " +
                    $"{DescribePiece(participantConnector)} connected");
            }
        }

        result.targetConnectionCount = total;
        result.correctConnections = correct;

        result.connectionAccuracy =
            total == 0 ? 0f : (float)correct / total;
    }


    private void RecordConnection(
        GradeResult result, bool log, string line)
    {
        result.connectionLog.Add(line);

        if (log)
        {
            Debug.Log("  " + line);
        }
    }


    // ========================================================================
    // PIECE PREDICATES
    // ========================================================================

    private bool IsGradablePiece(BuildingPiece piece)
    {
        return IsTube(piece) || IsConnector(piece);
    }


    private bool IsTube(BuildingPiece piece)
    {
        if (piece == null)
        {
            return false;
        }

        return piece.pieceType == PieceType.PIPE;
    }


    private bool IsConnector(BuildingPiece piece)
    {
        if (piece == null)
        {
            return false;
        }

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


    private string DescribePiece(BuildingPiece piece)
    {
        if (piece == null)
        {
            return "<null>";
        }

        if (IsTube(piece))
        {
            return $"'{piece.name}' [PIPE/{piece.pieceColor}]";
        }

        return $"'{piece.name}' [{piece.pieceType}]";
    }


    // ========================================================================
    // HELPERS
    // ========================================================================

    private PieceMatch FindMatchForTarget(
        GradeResult result, BuildingPiece target)
    {
        return result.matches.FirstOrDefault(m => m.targetPiece == target);
    }


    private bool ArePiecesConnected(
        BuildingPiece pieceA, BuildingPiece pieceB)
    {
        if (pieceA == null || pieceB == null || pieceA.attachPoints == null)
        {
            return false;
        }

        foreach (AttachPoint attachPoint in pieceA.attachPoints)
        {
            if (attachPoint == null ||
                attachPoint.attachedAttachPoint == null)
            {
                continue;
            }

            if (attachPoint.attachedAttachPoint.owningPiece == pieceB)
            {
                return true;
            }
        }

        return false;
    }


    private string CreateConnectionKey(BuildingPiece a, BuildingPiece b)
    {
        int idA = a.GetInstanceID();
        int idB = b.GetInstanceID();

        return idA < idB ? idA + "_" + idB : idB + "_" + idA;
    }


    // ========================================================================
    // PRINT
    // ========================================================================

    private void PrintResult(GradeResult result)
    {
        Debug.Log("================================================");
        Debug.Log("STRUCTURE GRADING RESULTS");
        Debug.Log("================================================");

        Debug.Log($"TUBE COLORS: {result.correctTubeColors} / " +
                  $"{result.targetTubeCount} " +
                  $"({result.colorAccuracy * 100f:F2}%)");

        Debug.Log($"CONNECTORS:  {result.correctConnectors} / " +
                  $"{result.targetConnectorCount} " +
                  $"({result.connectorAccuracy * 100f:F2}%)");

        Debug.Log($"STRUCTURAL ORIENTATION: " +
                  $"bottleneck axis error " +
                  $"{result.structuralOrientationErrorDegrees:F2} deg " +
                  $"(tolerance " +
                  $"{result.structuralOrientationToleranceDegrees:F2} deg) -> " +
                  $"{(result.structuralOrientationCorrect ? "CORRECT" : "INCORRECT")} " +
                  $"| score {result.structuralOrientationScore:F3}");

        if (result.rotationAlignmentApplied)
        {
            Debug.Log($"  (whole-build rotation alignment applied: " +
                      $"{result.rotationAlignmentDegrees:F2} deg)");
        }

        Debug.Log($"CONNECTIONS: {result.correctConnections} / " +
                  $"{result.targetConnectionCount} " +
                  $"({result.connectionAccuracy * 100f:F2}%)");

        //Debug.Log($"Target pieces: {result.targetPieceCount} | " +
        //          $"Participant pieces: {result.participantPieceCount} | " +
        //          $"Matched: {result.matchedPieceCount} | " +
        //          $"Missing: {result.missingPieces} | " +
        //          $"Extra: {result.extraPieces}");

        //if (verboseCorrespondenceLog)
        //{
        //    Debug.Log("================================================");
        //    Debug.Log("CORRESPONDENCE DUMP - TUBES");
        //    Debug.Log("================================================");

        //    foreach (PieceMatch match in result.matches
        //                 .Where(m => IsTube(m.targetPiece))
        //                 .OrderBy(m => m.positionDistance))
        //    {
        //        CorrespondenceDecision decision =
        //            result.decisions.FirstOrDefault(
        //                x => x.targetPiece == match.targetPiece);

        //        string decide = decision != null ? decision.decidedBy : "<none>";

        //        string rival =
        //            decision != null && decision.rivalTargetName != ""
        //                ? $"{decision.rivalTargetName} @ " +
        //                  $"{decision.rivalDistance:F4}"
        //                : "<none>";

        //        Debug.Log(
        //            $"[TUBE] Participant '{match.participantPiece.name}' " +
        //            $"({match.participantPiece.pieceColor}) -> " +
        //            $"Target '{match.targetPiece.name}' " +
        //            $"({match.targetPiece.pieceColor}) " +
        //            $"| distance={match.positionDistance:F4} " +
        //            $"| decided_by={decide} " +
        //            $"| nearest_rival={rival} " +
        //            $"| COLOR CORRECT={match.colorCorrect}");
        //    }

        //    Debug.Log("================================================");
        //    Debug.Log("CORRESPONDENCE DUMP - CONNECTORS");
        //    Debug.Log("================================================");

        //    foreach (PieceMatch match in result.matches
        //                 .Where(m => IsConnector(m.targetPiece))
        //                 .OrderBy(m => m.positionDistance))
        //    {
        //        CorrespondenceDecision decision =
        //            result.decisions.FirstOrDefault(
        //                x => x.targetPiece == match.targetPiece);

        //        string decide = decision != null ? decision.decidedBy : "<none>";

        //        string rival =
        //            decision != null && decision.rivalTargetName != ""
        //                ? $"{decision.rivalTargetName} @ " +
        //                  $"{decision.rivalDistance:F4}"
        //                : "<none>";

        //        Debug.Log(
        //            $"[CONNECTOR] Participant '{match.participantPiece.name}' " +
        //            $"({match.participantPiece.pieceType}) -> " +
        //            $"Target '{match.targetPiece.name}' " +
        //            $"({match.targetPiece.pieceType}) " +
        //            $"| distance={match.positionDistance:F4} " +
        //            $"| decided_by={decide} " +
        //            $"| nearest_rival={rival} " +
        //            $"| CONNECTOR CORRECT={match.connectorCorrect}");
        //    }
        //}

        //if (verboseAxisLog)
        //{
        //    Debug.Log("================================================");
        //    Debug.Log("AXIS TABLE - TARGET (direction from connector centroid)");
        //    Debug.Log("================================================");

        //    foreach (AxisRecord r in result.targetAxes)
        //    {
        //        Debug.Log($"  [TARGET AXIS] {DescribePiece(r.piece)} " +
        //                  $"| axis=({r.axis.x:F3}, {r.axis.y:F3}, {r.axis.z:F3})");
        //    }

        //    Debug.Log("================================================");
        //    Debug.Log("AXIS TABLE - PARTICIPANT (nearest target axis + angle)");
        //    Debug.Log("================================================");

        //    foreach (AxisRecord r in result.participantAxes
        //                 .OrderByDescending(x => x.angleToNearest))
        //    {
        //        Debug.Log($"  [PARTICIPANT AXIS] {DescribePiece(r.piece)} " +
        //                  $"| axis=({r.axis.x:F3}, {r.axis.y:F3}, {r.axis.z:F3}) " +
        //                  $"| nearest target axis '{r.nearestName}' " +
        //                  $"| angle={r.angleToNearest:F2} deg");
        //    }

        //    Debug.Log($"  Distinct target axes: {result.targetAxes.Count} | " +
        //              $"distinct participant axes: " +
        //              $"{result.participantAxes.Count}");
        //}

        //if (verboseConnectionLog)
        //{
        //    Debug.Log("================================================");
        //    Debug.Log("CONNECTION DUMP (unique target tube<->connector edges)");
        //    Debug.Log("================================================");

        //    foreach (string line in result.connectionLog)
        //    {
        //        Debug.Log("  " + line);
        //    }
        //}

        Debug.Log("================================================");
    }


    // ========================================================================
    // RUN FROM INSPECTOR
    // ========================================================================

    [ContextMenu("Grade Structure")]
    public void GradeFromInspector()
    {
        GradeBuild();
    }
}


