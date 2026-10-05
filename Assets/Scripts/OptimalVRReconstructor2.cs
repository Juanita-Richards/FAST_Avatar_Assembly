// ============================================================================
//  OptimalStructureVRReconstructor.cs  --  reconstruct + SCORE the reconstruction
//
//  TWO JOBS
//  --------
//  1. RECONSTRUCT  (unchanged behaviour)
//       Load the comparison file written by BuildGrader3, instantiate the
//       participant pieces under generatedStructureRoot at their aligned
//       positions/rotations.
//
//  2. GRADE THE RECONSTRUCTION  (grade B -- added)
//       A separate pass, run AFTER reconstruction, that scores the generated
//       geometry against the comparison file:
//
//         B1 position fidelity   generated.localPosition vs
//                                AlignedParticipantPosition * structureScale
//         B2 axis signature      generated build direction set vs the target's
//                                (positions only, sign-free axes, bottleneck)
//         B3 rotation drift      angle(generated.localRotation,
//                                AlignedParticipantRotation)
//
//  WHY THE SCORE LIVES HERE
//  ------------------------
//  Only this script knows what the generated structure is. BuildGrader3 knows
//  the participant build and the target; this knows the rebuild. Keeping B here
//  means one implementation of the measurement, and no dependency on the
//  grader's in-memory correspondence surviving a domain reload.
//
//  PAIRING IS BY NAME
//  ------------------
//  Each instantiated piece is named  Reconstructed_{participantName}_{n},
//  so a generated piece is paired to its file record by parsing that name.
//  Grade B therefore does NOT depend on BuildGrader3's correspondence.
//
//  RUN ORDER
//  ---------
//      BuildGrader3.GradeBuild()        -> participant scores + writes the file
//      this.LoadAndReconstruct()        -> builds the pieces
//      this.GradeReconstruction()       -> grade B   [or Grade And Reconstruct]
// ============================================================================

using UnityEngine;
using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using PlayStructure;

public class OptimalStructureVRReconstructor2 : MonoBehaviour
{
    // ========================================================================
    // FILE
    // ========================================================================

    [Header("Comparison File")]

    [Tooltip("Name of the comparison file, resolved under " +
             "Application.persistentDataPath/FAST_Structure/Grade 1/...")]
    [SerializeField]
    private string fileName = "";

    [Tooltip("Leave true to resolve under Application.persistentDataPath " +
             "(absolute and identical for every script). Set false to use the " +
             "legacy project-relative path -- only for debugging a mismatch.")]
    [SerializeField]
    private bool usePersistentDataPath = true;

    [Tooltip("Folder name under FAST_Structure/Grade 1/. Must match the " +
             "grader's comparisonFolderName exactly, including no trailing " +
             "spaces.")]
    [SerializeField]
    private string comparisonFolderName = "FAST_Comparisons";


    // ========================================================================
    // TUBE PREFABS
    // ========================================================================

    [Header("Tube Prefabs")]

    [SerializeField]
    private GameObject redTubePrefab;

    [SerializeField]
    private GameObject yellowTubePrefab;

    [SerializeField]
    private GameObject greenTubePrefab;

    [SerializeField]
    private GameObject blueTubePrefab;


    // ========================================================================
    // CONNECTOR PREFABS
    // ========================================================================

    [Header("Connector Prefabs")]

    [SerializeField]
    private GameObject elbowConnectorPrefab;

    [SerializeField]
    private GameObject threeWayConnectorPrefab;


    // ========================================================================
    // OUTPUT
    // ========================================================================

    [Header("Generated Structure")]

    [Tooltip("All reconstructed pieces become children of this Transform.")]
    [SerializeField]
    private Transform generatedStructureRoot;

    [Tooltip("Uniform visual scale for the reconstructed structure. Grade B " +
             "divides this back out, so changing it does not change the score " +
             "-- only the visual size.")]
    [SerializeField]
    private float structureScale = 1f;

    [Header("Options")]

    [Tooltip("Automatically load and reconstruct when the scene starts.")]
    [SerializeField]
    private bool reconstructOnStart = false;


    // ========================================================================
    // GRADE B SETTINGS
    // ========================================================================

    [Header("Grade B - Reconstruction Scoring")]

    [Tooltip("Max positional deviation, in NORMALISED units " +
             "(1.0 == one median tube<->connector edge), for a generated piece " +
             "to count as faithful to the file.")]
    [SerializeField]
    private float reconstructionPositionTolerance = 0.35f;

    [Tooltip("Max rotation drift, in degrees, for a generated piece to count " +
             "as faithful to the file. Note: if the reconstructor places the " +
             "rotation and nobody adjusts it, this is identically zero.")]
    [SerializeField]
    private float reconstructionRotationToleranceDegrees = 10f;

    [Tooltip("Max bottleneck axis angle, in degrees, for the generated build's " +
             "direction signature to match the target's.")]
    [SerializeField]
    private float reconstructionAxisToleranceDegrees = 10f;

    [Tooltip("The target structure, for the B2 axis comparison. Optional: " +
             "leave empty and B2 is skipped.")]
    [SerializeField]
    private Transform targetBuildRoot;

    [Tooltip("Log the per-piece reconstruction table.")]
    [SerializeField]
    private bool verboseReconstructionLog = true;


    // ========================================================================
    // LOADED DATA
    // ========================================================================

    [Serializable]
    private class SavedPiece
    {
        public string targetName;
        public string participantName;

        public string targetType;
        public string participantType;

        public string targetColor;
        public string participantColor;

        public Vector3 targetPosition;
        public Vector3 originalParticipantPosition;
        public Vector3 alignedParticipantPosition;

        public Quaternion targetRotation;
        public Quaternion originalParticipantRotation;
        public Quaternion alignedParticipantRotation;

        public float positionError;
        public float orientationError;
    }

    [Serializable]
    public class ReconstructionPieceReport
    {
        public string generatedName = "";
        public string fileParticipantName = "";
        public string pieceType = "";

        public Vector3 expectedPosition;
        public Vector3 actualPosition;

        public float positionDeviation;
        public bool positionFaithful;

        public float rotationDrift;
        public bool rotationFaithful;
    }

    [Serializable]
    public class ReconstructionResult
    {
        public int fileRecordCount;
        public int matchedByNameCount;
        public int missingGeneratedCount;
        public int extraGeneratedCount;

        public int positionFaithfulCount;
        public float meanPositionDeviation;
        public float maxPositionDeviation;
        public string worstPositionName = "";

        public int rotationFaithfulCount;
        public float meanRotationDrift;
        public float maxRotationDrift;

        public float axisBottleneckDegrees;
        public bool axisCorrect;
        public bool axisEvaluated;
        public int targetAxisCount;
        public int generatedAxisCount;

        public float scaleAuditFileUnit;
        public float scaleAuditGeneratedUnit;
        public bool scaleAuditWarning;

        public float reconstructionScore;

        public List<ReconstructionPieceReport> pieces =
            new List<ReconstructionPieceReport>();
    }

    private readonly List<SavedPiece> loadedPieces = new List<SavedPiece>();

    private readonly List<GameObject> generatedPieces = new List<GameObject>();

    private ReconstructionResult lastReconstructionResult;

    public ReconstructionResult LastReconstructionResult =>
        lastReconstructionResult;


    // ========================================================================
    // START
    // ========================================================================

    private void Start()
    {
        if (reconstructOnStart)
        {
            LoadAndReconstruct();
        }
    }


    // ========================================================================
    // COMPLETE WORKFLOW
    // ========================================================================

    [ContextMenu("Load And Reconstruct")]
    public void LoadAndReconstruct()
    {
        if (!LoadComparison())
        {
            return;
        }

        ReconstructParticipant();
    }


    [ContextMenu("Grade And Reconstruct")]
    public void GradeAndReconstruct()
    {
        LoadAndReconstruct();

        if (loadedPieces.Count > 0)
        {
            GradeReconstruction();
        }
    }


    // ========================================================================
    // LOAD FILE
    // ========================================================================

    [ContextMenu("Load Comparison")]
    public bool LoadComparison()
    {
        loadedPieces.Clear();

        string path = ResolveComparisonPath();

        if (!File.Exists(path))
        {
            Debug.LogError(
                "Optimal structure comparison file was not found:\n" + path +
                "\n\nCheck that usePersistentDataPath and " +
                "comparisonFolderName match the grader's, and that the grader " +
                "has been run to write the file.");
            return false;
        }

        string[] lines = File.ReadAllLines(path);

        SavedPiece currentPiece = null;

        foreach (string rawLine in lines)
        {
            string line = rawLine.Trim();

            if (line == "MATCH")
            {
                currentPiece = new SavedPiece();
                continue;
            }

            if (line == "END_MATCH")
            {
                if (currentPiece != null)
                {
                    loadedPieces.Add(currentPiece);
                }

                currentPiece = null;
                continue;
            }

            if (currentPiece == null)
            {
                continue;
            }

            ParsePieceLine(currentPiece, line);
        }

        Debug.Log($"Loaded {loadedPieces.Count} pieces from:\n{path}");

        return loadedPieces.Count > 0;
    }


    private string ResolveComparisonPath()
    {
        if (usePersistentDataPath)
        {
            //string folder = Path.Combine(
            //    Application.persistentDataPath,
            //    "FAST_Structure",
            //    "Grade 1",
            //    comparisonFolderName);
            string directory = $"FAST_Structure/{"Grade 1 "} ";
            string folder = Path.Combine(directory, comparisonFolderName);

            Directory.CreateDirectory(folder);

            return Path.Combine(folder, fileName);
        }

        // Legacy project-relative form. Kept only for diagnosing a mismatch.
        return Path.Combine($"FAST_Structure/{"Grade 1 "}/{"FAST_Comparisons"}", fileName);
    }


    // ========================================================================
    // RECONSTRUCT PARTICIPANT
    // ========================================================================

    [ContextMenu("Reconstruct Participant")]
    public void ReconstructParticipant()
    {
        if (loadedPieces.Count == 0)
        {
            Debug.LogWarning(
                "No comparison data is loaded. Run Load Comparison first.");
            return;
        }

        if (generatedStructureRoot == null)
        {
            Debug.LogError("Generated Structure Root has not been assigned.");
            return;
        }

        ClearGeneratedStructure();

        int createdCount = 0;

        foreach (SavedPiece piece in loadedPieces)
        {
            GameObject prefab = GetParticipantPrefab(piece);

            if (prefab == null)
            {
                Debug.LogWarning(
                    $"No prefab found for " +
                    $"{piece.participantName} | " +
                    $"Type: {piece.participantType} | " +
                    $"Color: {piece.participantColor}");
                continue;
            }

            GameObject newPiece = Instantiate(prefab, generatedStructureRoot);

            newPiece.name =
                $"Reconstructed_{piece.participantName}_{createdCount}";

            // Aligned position, local to the reconstructed structure root.
            newPiece.transform.localPosition =
                piece.alignedParticipantPosition * structureScale;

            newPiece.transform.localRotation =
                piece.alignedParticipantRotation;

            newPiece.transform.localScale =
                prefab.transform.localScale * structureScale;

            generatedPieces.Add(newPiece);

            createdCount++;
        }

        Debug.Log($"VR reconstruction complete. Created {createdCount} pieces.");
    }


    // ========================================================================
    // CLEAR GENERATED STRUCTURE
    // ========================================================================

    [ContextMenu("Clear Reconstructed Structure")]
    public void ClearGeneratedStructure()
    {
        if (generatedStructureRoot == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            for (int i = generatedStructureRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(generatedStructureRoot.GetChild(i).gameObject);
            }
        }
        else
        {
            for (int i = generatedStructureRoot.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(generatedStructureRoot.GetChild(i).gameObject);
            }
        }

        generatedPieces.Clear();
    }


    // ========================================================================
    // PREFAB SELECTION
    // ========================================================================

    private GameObject GetParticipantPrefab(SavedPiece piece)
    {
        if (piece.participantType == "PIPE")
        {
            switch (piece.participantColor)
            {
                case "RED": return redTubePrefab;
                case "YELLOW": return yellowTubePrefab;
                case "GREEN": return greenTubePrefab;
                case "BLUE": return blueTubePrefab;

                default:
                    Debug.LogWarning(
                        "Unknown pipe color: " + piece.participantColor);
                    return null;
            }
        }

        switch (piece.participantType)
        {
            case "C_ELBOW": return elbowConnectorPrefab;
            case "C_3WAY": return threeWayConnectorPrefab;

            // Uncomment and assign as you add prefab slots:
            // case "C_2WAY": return twoWayConnectorPrefab;
            // case "C_TEE":  return teeConnectorPrefab;
            // case "C_4WAY": return fourWayConnectorPrefab;
            // case "C_5WAY": return fiveWayConnectorPrefab;

            default:
                Debug.LogWarning(
                    "Unknown piece type: " + piece.participantType);
                return null;
        }
    }


    // ========================================================================
    // GRADE B - SCORE THE RECONSTRUCTION
    //
    //  Run AFTER ReconstructParticipant, or after hand-editing the generated
    //  pieces. Reads the file, pairs by name, scores.
    // ========================================================================

    [ContextMenu("Grade Reconstruction")]
    public ReconstructionResult GradeReconstruction()
    {
        Debug.Log("================================================");
        Debug.Log("GRADE B -- VR RECONSTRUCTION SCORE");
        Debug.Log("Measures the generated geometry against the comparison file.");
        Debug.Log("================================================");

        if (generatedStructureRoot == null)
        {
            Debug.LogError("Generated Structure Root has not been assigned.");
            return null;
        }

        if (generatedStructureRoot.childCount == 0)
        {
            Debug.LogWarning(
                "Generated structure is empty. Run Load And Reconstruct first.");
            return null;
        }

        if (loadedPieces.Count == 0 && !LoadComparison())
        {
            return null;
        }

        ReconstructionResult result = new ReconstructionResult();

        result.fileRecordCount = loadedPieces.Count;

        // ---- index generated children by participant name ------------------

        Dictionary<string, Transform> byBaseName =
            new Dictionary<string, Transform>();

        foreach (Transform child in generatedStructureRoot)
        {
            string baseName = ExtractParticipantName(child.name);

            if (!string.IsNullOrEmpty(baseName) &&
                !byBaseName.ContainsKey(baseName))
            {
                byBaseName[baseName] = child;
            }
        }

        // ---- scale audit ---------------------------------------------------
        //
        //  Compare the file's tube spacing against the generated geometry's.
        //  If these disagree, structureScale here no longer matches the
        //  reconstructor's setting, or a prefab is scaled differently -- and
        //  EVERY position will read as wrong.

        float fileUnit = MedianEdgeFromFile();
        float generatedUnit = MedianEdgeFromGenerated(byBaseName);

        result.scaleAuditFileUnit = fileUnit;
        result.scaleAuditGeneratedUnit = generatedUnit;

        if (fileUnit > 0.0001f && generatedUnit > 0.0001f)
        {
            result.scaleAuditWarning =
                Mathf.Abs(generatedUnit / fileUnit - 1f) > 0.05f;
        }

        Debug.Log($"[B SCALE AUDIT] file unit={fileUnit:F4} | " +
                  $"generated unit={generatedUnit:F4} | " +
                  $"ratio={(fileUnit > 0.0001f ? generatedUnit / fileUnit : 0f):F4}");

        if (result.scaleAuditWarning)
        {
            Debug.LogWarning(
                "[B SCALE AUDIT] Tube spacing differs by more than 5%. Every " +
                "position would read as wrong. Check that structureScale here " +
                "matches the reconstructor's, and that piece prefabs are not " +
                "scaled differently.");
        }

        // ---- B1 / B3 : per piece -------------------------------------------

        HashSet<Transform> used = new HashSet<Transform>();

        foreach (SavedPiece record in loadedPieces)
        {
            Transform generated = FindGenerated(record, byBaseName);

            if (generated == null)
            {
                result.missingGeneratedCount++;
                Debug.Log($"  [B1] '{record.participantName}' " +
                          $"({record.participantType}) -> NOT GENERATED");
                continue;
            }

            used.Add(generated);
            result.matchedByNameCount++;

            Vector3 expected =
                record.alignedParticipantPosition * structureScale;

            Vector3 actual = generated.localPosition;

            // Normalise by the generated unit where available, so the score is
            // scale-independent.
            float divisor = generatedUnit > 0.0001f ? generatedUnit : 1f;

            float deviation = Vector3.Distance(expected, actual) / divisor;

            float drift = Quaternion.Angle(
                generated.localRotation, record.alignedParticipantRotation);

            bool posOk = deviation <= reconstructionPositionTolerance;
            bool rotOk = drift <= reconstructionRotationToleranceDegrees;

            if (posOk) result.positionFaithfulCount++;
            if (rotOk) result.rotationFaithfulCount++;

            result.pieces.Add(new ReconstructionPieceReport
            {
                generatedName = generated.name,
                fileParticipantName = record.participantName,
                pieceType = record.participantType,
                expectedPosition = expected,
                actualPosition = actual,
                positionDeviation = deviation,
                positionFaithful = posOk,
                rotationDrift = drift,
                rotationFaithful = rotOk
            });
        }

        foreach (Transform child in generatedStructureRoot)
        {
            if (!used.Contains(child))
            {
                result.extraGeneratedCount++;
                Debug.Log($"  [B1] EXTRA generated '{child.name}' has no " +
                          $"counterpart in the file (not graded)");
            }
        }

        if (result.pieces.Count > 0)
        {
            result.meanPositionDeviation =
                result.pieces.Average(p => p.positionDeviation);

            ReconstructionPieceReport worst = result.pieces
                .OrderByDescending(p => p.positionDeviation).First();

            result.maxPositionDeviation = worst.positionDeviation;
            result.worstPositionName = worst.fileParticipantName;

            result.meanRotationDrift =
                result.pieces.Average(p => p.rotationDrift);

            result.maxRotationDrift =
                result.pieces.Max(p => p.rotationDrift);
        }

        // ---- B2 : axis signature -------------------------------------------

        List<AxisRecord> generatedAxes =
            BuildAxisSetFromGenerated(generatedStructureRoot);

        result.generatedAxisCount = generatedAxes.Count;

        if (targetBuildRoot != null)
        {
            List<AxisRecord> targetAxes =
                BuildAxisSetFromTarget(targetBuildRoot);

            result.targetAxisCount = targetAxes.Count;

            if (targetAxes.Count > 0 && generatedAxes.Count > 0)
            {
                result.axisEvaluated = true;

                result.axisBottleneckDegrees =
                    ComputeBottleneckAxisAngle(targetAxes, generatedAxes);

                result.axisCorrect =
                    result.axisBottleneckDegrees <=
                    reconstructionAxisToleranceDegrees;
            }
        }
        else
        {
            Debug.Log("[B2] targetBuildRoot not assigned - axis signature " +
                      "comparison skipped (assign it to enable B2).");
        }

        // ---- score ---------------------------------------------------------

        float posFraction = result.matchedByNameCount == 0
            ? 0f
            : (float)result.positionFaithfulCount /
              result.matchedByNameCount;

        float axisFraction = 1f;

        if (result.axisEvaluated)
        {
            axisFraction = reconstructionAxisToleranceDegrees <= 0f
                ? (result.axisCorrect ? 1f : 0f)
                : Mathf.Clamp01(
                    1f - result.axisBottleneckDegrees /
                         reconstructionAxisToleranceDegrees);
        }

        result.reconstructionScore = posFraction * axisFraction;

        lastReconstructionResult = result;

        PrintReconstructionResult(result);

        return result;
    }


    // ========================================================================
    // GRADE B HELPERS
    // ========================================================================

    private Transform FindGenerated(
        SavedPiece record, Dictionary<string, Transform> byBaseName)
    {
        if (!string.IsNullOrEmpty(record.participantName) &&
            byBaseName.TryGetValue(
                record.participantName, out Transform direct))
        {
            return direct;
        }

        foreach (Transform child in generatedStructureRoot)
        {
            if (!string.IsNullOrEmpty(record.participantName) &&
                child.name.Contains(record.participantName))
            {
                return child;
            }
        }

        return null;
    }


    private string ExtractParticipantName(string generatedName)
    {
        if (string.IsNullOrEmpty(generatedName))
        {
            return "";
        }

        const string prefix = "Reconstructed_";

        string working = generatedName.StartsWith(prefix)
            ? generatedName.Substring(prefix.Length)
            : generatedName;

        int lastUnderscore = working.LastIndexOf('_');

        if (lastUnderscore > 0)
        {
            string tail = working.Substring(lastUnderscore + 1);

            if (int.TryParse(tail, out int _))
            {
                working = working.Substring(0, lastUnderscore);
            }
        }

        return working;
    }


    private float MedianEdge(List<Vector3> tubes, List<Vector3> connectors)
    {
        if (tubes.Count == 0 || connectors.Count == 0)
        {
            return 0f;
        }

        List<float> nearest = new List<float>();

        foreach (Vector3 t in tubes)
        {
            float best = float.MaxValue;

            foreach (Vector3 c in connectors)
            {
                float d = Vector3.Distance(t, c);

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
            return 0f;
        }

        nearest.Sort();

        int mid = nearest.Count / 2;

        return (nearest.Count % 2 == 1)
            ? nearest[mid]
            : 0.5f * (nearest[mid - 1] + nearest[mid]);
    }


    private float MedianEdgeFromFile()
    {
        List<Vector3> tubes = new List<Vector3>();
        List<Vector3> connectors = new List<Vector3>();

        foreach (SavedPiece p in loadedPieces)
        {
            Vector3 pos = p.alignedParticipantPosition * structureScale;

            if (p.participantType == "PIPE")
            {
                tubes.Add(pos);
            }
            else if (IsConnectorType(p.participantType))
            {
                connectors.Add(pos);
            }
        }

        return MedianEdge(tubes, connectors);
    }


    private float MedianEdgeFromGenerated(
        Dictionary<string, Transform> byBaseName)
    {
        List<Vector3> tubes = new List<Vector3>();
        List<Vector3> connectors = new List<Vector3>();

        foreach (Transform child in generatedStructureRoot)
        {
            string baseName = ExtractParticipantName(child.name);

            SavedPiece record = loadedPieces.FirstOrDefault(
                p => p.participantName == baseName);

            if (record == null)
            {
                continue;
            }

            if (record.participantType == "PIPE")
            {
                tubes.Add(child.localPosition);
            }
            else if (IsConnectorType(record.participantType))
            {
                connectors.Add(child.localPosition);
            }
        }

        return MedianEdge(tubes, connectors);
    }


    private bool IsConnectorType(string type)
    {
        switch (type)
        {
            case "C_2WAY":
            case "C_ELBOW":
            case "C_TEE":
            case "C_3WAY":
            case "C_4WAY":
            case "C_5WAY":
                return true;

            default:
                return false;
        }
    }


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
        return $"{Mathf.Round(v.x * 1000f)}_" +
               $"{Mathf.Round(v.y * 1000f)}_" +
               $"{Mathf.Round(v.z * 1000f)}";
    }

    private class AxisRecord
    {
        public Vector3 axis;
        public string name;
    }


    private List<AxisRecord> BuildAxisSetFromTransforms(
        List<Transform> pieces, Vector3 centre, string label)
    {
        List<AxisRecord> axes = new List<AxisRecord>();
        HashSet<string> seen = new HashSet<string>();

        foreach (Transform t in pieces)
        {
            Vector3 dir = t.position - centre;

            if (dir.sqrMagnitude < 1e-10f)
            {
                continue;
            }

            Vector3 axis = CanonicalAxis(dir.normalized);
            string key = AxisKey(axis);

            if (seen.Contains(key))
            {
                continue;
            }

            seen.Add(key);

            axes.Add(new AxisRecord { axis = axis, name = t.name });
        }

        return axes;
    }


    private List<AxisRecord> BuildAxisSetFromGenerated(
        Transform root)
    {
        List<Transform> children =
            root.Cast<Transform>().ToList();

        if (children.Count == 0)
        {
            return new List<AxisRecord>();
        }

        Vector3 centre = Vector3.zero;

        foreach (Transform t in children)
        {
            centre += t.position;
        }

        centre /= children.Count;

        return BuildAxisSetFromTransforms(children, centre, "generated");
    }


    private List<AxisRecord> BuildAxisSetFromTarget(Transform root)
    {
        List<Transform> pieces =
            root.GetComponentsInChildren<BuildingPiece>(true)
                .Where(p => p != null &&
                            (p.pieceType == PieceType.PIPE ||
                             IsConnectorType(p.pieceType.ToString())))
                .Select(p => p.transform)
                .ToList();

        if (pieces.Count == 0)
        {
            return new List<AxisRecord>();
        }

        // Centre on the connectors where possible: the structural skeleton.
        List<Transform> connectors = pieces
            .Where(t => t.GetComponent<BuildingPiece>() != null &&
                        IsConnectorType(
                            t.GetComponent<BuildingPiece>()
                                .pieceType.ToString()))
            .ToList();

        List<Transform> anchor = connectors.Count > 0 ? connectors : pieces;

        Vector3 centre = Vector3.zero;

        foreach (Transform t in anchor)
        {
            centre += t.position;
        }

        centre /= anchor.Count;

        return BuildAxisSetFromTransforms(pieces, centre, "target");
    }


    private float ComputeBottleneckAxisAngle(
        List<AxisRecord> a, List<AxisRecord> b)
    {
        int n = a.Count;
        int m = b.Count;

        if (n == 0 || m == 0)
        {
            return 180f;
        }

        float[,] cost = new float[n, m];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                cost[i, j] = Vector3.Angle(a[i].axis, b[j].axis);
            }
        }

        int size = Mathf.Min(n, m);

        List<float> distinct = new List<float>();

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < m; j++)
            {
                distinct.Add(cost[i, j]);
            }
        }

        distinct.Sort();

        for (int k = 0; k < distinct.Count; k++)
        {
            if (k > 0 && Mathf.Abs(distinct[k] - distinct[k - 1]) <= 1e-4f)
            {
                continue;
            }

            if (HasAngleMatching(cost, n, m, distinct[k], size))
            {
                return distinct[k];
            }
        }

        return distinct[distinct.Count - 1];
    }


    private bool HasAngleMatching(
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

            if (TryAugmentAngle(cost, m, threshold, i, visited, matchTarget))
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
                TryAugmentAngle(cost, m, threshold, matchTarget[j],
                                visited, matchTarget))
            {
                matchTarget[j] = participant;
                return true;
            }
        }

        return false;
    }


    // ========================================================================
    // GRADE B - PRINT
    // ========================================================================

    private void PrintReconstructionResult(ReconstructionResult result)
    {
        Debug.Log("================================================");
        Debug.Log("GRADE B -- RECONSTRUCTION RESULTS");
        Debug.Log("================================================");

        Debug.Log($"Records in file:    {result.fileRecordCount}");
        Debug.Log($"Paired by name:     {result.matchedByNameCount}");
        Debug.Log($"Missing generated:  {result.missingGeneratedCount}");
        Debug.Log($"Extra generated:    {result.extraGeneratedCount}");

        if (result.matchedByNameCount > 0)
        {
            Debug.Log($"B1 POSITION FAITHFUL: " +
                      $"{result.positionFaithfulCount} / " +
                      $"{result.matchedByNameCount} " +
                      $"(tol {reconstructionPositionTolerance:F4} units) " +
                      $"| mean {result.meanPositionDeviation:F4} " +
                      $"| worst {result.maxPositionDeviation:F4} " +
                      $"('{result.worstPositionName}')");

            Debug.Log($"B3 ROTATION FAITHFUL: " +
                      $"{result.rotationFaithfulCount} / " +
                      $"{result.matchedByNameCount} " +
                      $"(tol {reconstructionRotationToleranceDegrees:F2} deg) " +
                      $"| mean {result.meanRotationDrift:F2} " +
                      $"| worst {result.maxRotationDrift:F2} deg");
        }

        if (result.axisEvaluated)
        {
            Debug.Log($"B2 AXIS SIGNATURE: " +
                      $"{result.axisBottleneckDegrees:F2} deg " +
                      $"(tol {reconstructionAxisToleranceDegrees:F2}) -> " +
                      $"{(result.axisCorrect ? "MATCHES" : "DIFFERS")} " +
                      $"| target axes {result.targetAxisCount}, " +
                      $"generated axes {result.generatedAxisCount}");
        }

        Debug.Log($"RECONSTRUCTION SCORE: {result.reconstructionScore:F4}");

        if (verboseReconstructionLog && result.pieces.Count > 0)
        {
            Debug.Log("------------------------------------------------");
            Debug.Log("PER-PIECE RECONSTRUCTION TABLE (worst deviation first)");
            Debug.Log("------------------------------------------------");

            foreach (ReconstructionPieceReport p in result.pieces
                         .OrderByDescending(x => x.positionDeviation))
            {
                Debug.Log(
                    $"  [B1] '{p.fileParticipantName}' ({p.pieceType}) " +
                    $"| expected={Vector3ToString(p.expectedPosition)} " +
                    $"| actual={Vector3ToString(p.actualPosition)} " +
                    $"| dev={p.positionDeviation:F4} " +
                    $"| pos_ok={p.positionFaithful} " +
                    $"| rot_drift={p.rotationDrift:F2}deg " +
                    $"| rot_ok={p.rotationFaithful}");
            }
        }

        Debug.Log("================================================");
    }

    private string Vector3ToString(Vector3 v)
    {
        return v.x.ToString("R", CultureInfo.InvariantCulture) + "," +
               v.y.ToString("R", CultureInfo.InvariantCulture) + "," +
               v.z.ToString("R", CultureInfo.InvariantCulture);
    }


    // ========================================================================
    // PARSE FILE
    // ========================================================================

    private void ParsePieceLine(SavedPiece piece, string line)
    {
        if (line.StartsWith("TargetName="))
            piece.targetName = GetValue(line);
        else if (line.StartsWith("ParticipantName="))
            piece.participantName = GetValue(line);
        else if (line.StartsWith("TargetType="))
            piece.targetType = GetValue(line);
        else if (line.StartsWith("ParticipantType="))
            piece.participantType = GetValue(line);
        else if (line.StartsWith("TargetColor="))
            piece.targetColor = GetValue(line);
        else if (line.StartsWith("ParticipantColor="))
            piece.participantColor = GetValue(line);
        else if (line.StartsWith("TargetPosition="))
            piece.targetPosition = ParseVector3(GetValue(line));
        else if (line.StartsWith("OriginalParticipantPosition="))
            piece.originalParticipantPosition =
                ParseVector3(GetValue(line));
        else if (line.StartsWith("AlignedParticipantPosition="))
            piece.alignedParticipantPosition =
                ParseVector3(GetValue(line));
        else if (line.StartsWith("TargetRotation="))
            piece.targetRotation = ParseQuaternion(GetValue(line));
        else if (line.StartsWith("OriginalParticipantRotation="))
            piece.originalParticipantRotation =
                ParseQuaternion(GetValue(line));
        else if (line.StartsWith("AlignedParticipantRotation="))
            piece.alignedParticipantRotation =
                ParseQuaternion(GetValue(line));
        else if (line.StartsWith("PositionError="))
            piece.positionError = ParseFloat(GetValue(line));
        else if (line.StartsWith("OrientationErrorDegrees="))
            piece.orientationError = ParseFloat(GetValue(line));
    }


    // ========================================================================
    // STRING HELPERS
    // ========================================================================

    private string GetValue(string line)
    {
        int equalsIndex = line.IndexOf('=');

        return equalsIndex < 0 ? string.Empty : line.Substring(equalsIndex + 1);
    }


    private Vector3 ParseVector3(string value)
    {
        string[] values = value.Split(',');

        if (values.Length != 3)
        {
            Debug.LogWarning("Invalid Vector3: " + value);
            return Vector3.zero;
        }

        return new Vector3(
            ParseFloat(values[0]), ParseFloat(values[1]), ParseFloat(values[2]));
    }


    private Quaternion ParseQuaternion(string value)
    {
        string[] values = value.Split(',');

        if (values.Length != 4)
        {
            Debug.LogWarning("Invalid Quaternion: " + value);
            return Quaternion.identity;
        }

        Quaternion q = new Quaternion(
            ParseFloat(values[0]), ParseFloat(values[1]),
            ParseFloat(values[2]), ParseFloat(values[3]));

        return NormalizeQuaternion(q);
    }


    private float ParseFloat(string value)
    {
        if (float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result))
        {
            return result;
        }

        Debug.LogWarning("Could not parse float: " + value);

        return 0f;
    }


    private Quaternion NormalizeQuaternion(Quaternion q)
    {
        float magnitude = Mathf.Sqrt(
            q.x * q.x + q.y * q.y + q.z * q.z + q.w * q.w);

        if (magnitude < Mathf.Epsilon)
        {
            return Quaternion.identity;
        }

        return new Quaternion(
            q.x / magnitude, q.y / magnitude,
            q.z / magnitude, q.w / magnitude);
    }


    // ========================================================================
    // FILE LOCATION
    // ========================================================================

    [ContextMenu("Show Comparison File Location")]
    public void ShowComparisonFileLocation()
    {
        Debug.Log("Comparison file location:\n" + ResolveComparisonPath());
    }
}
