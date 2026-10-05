using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using PlayStructure;

public class OptimalStructureVRReconstructor : MonoBehaviour
{
    // ============================================================
    // FILE
    // ============================================================

    [Header("Comparison File")]

    [Tooltip(
        "Name of the comparison file inside Application.persistentDataPath.")]
    [SerializeField]
    private string fileName = "";


    // ============================================================
    // TUBE PREFABS
    // ============================================================

    [Header("Tube Prefabs")]

    [SerializeField]
    private GameObject redTubePrefab;

    [SerializeField]
    private GameObject yellowTubePrefab;

    [SerializeField]
    private GameObject greenTubePrefab;

    [SerializeField]
    private GameObject blueTubePrefab;



    // ============================================================
    // CONNECTOR PREFABS
    // ============================================================

    [Header("Connector Prefabs")]

    [SerializeField]
    private GameObject elbowConnectorPrefab;


    [SerializeField]
    private GameObject threeWayConnectorPrefab;



    // ============================================================
    // OUTPUT
    // ============================================================

    [Header("Generated Structure")]

    [Tooltip(
        "All reconstructed pieces will become children of this Transform.")]
    [SerializeField]
    private Transform generatedStructureRoot;


    [Tooltip(
        "Optional uniform visual scale for the reconstructed structure.")]
    [SerializeField]
    private float structureScale = 1f;


    [Header("Options")]

    [Tooltip(
        "Automatically load and reconstruct when the scene starts.")]
    [SerializeField]
    private bool reconstructOnStart = false;


    // ============================================================
    // LOADED DATA
    // ============================================================

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


    private readonly List<SavedPiece> loadedPieces =
        new List<SavedPiece>();


    private readonly List<GameObject> generatedPieces =
        new List<GameObject>();


    // ============================================================
    // START
    // ============================================================

    private void Start()
    {
        if (reconstructOnStart)
        {
            LoadAndReconstruct();
        }
    }


    // ============================================================
    // COMPLETE WORKFLOW
    // ============================================================

    [ContextMenu("Load And Reconstruct")]
    public void LoadAndReconstruct()
    {
        if (!LoadComparison())
        {
            return;
        }

        ReconstructParticipant();
    }


    // ============================================================
    // LOAD FILE
    // ============================================================

    [ContextMenu("Load Comparison")]
    public bool LoadComparison()
    {
        loadedPieces.Clear();
        string directory = $"FAST_Structure/{"Grade 1 "}/{"FAST_Comparisons"}";
        string path = Path.Combine(directory, fileName);
        //string path = Path.Combine(Application.persistentDataPath,fileName);


        if (!File.Exists(path))
        {
            Debug.LogError(
                "Optimal structure comparison file was not found:\n" +
                path);

            return false;
        }


        string[] lines =
            File.ReadAllLines(path);


        SavedPiece currentPiece =
            null;


        foreach (string rawLine in lines)
        {
            string line =
                rawLine.Trim();


            if (line == "MATCH")
            {
                currentPiece =
                    new SavedPiece();

                continue;
            }


            if (line == "END_MATCH")
            {
                if (currentPiece != null)
                {
                    loadedPieces.Add(
                        currentPiece);
                }

                currentPiece =
                    null;

                continue;
            }


            if (currentPiece == null)
            {
                continue;
            }


            ParsePieceLine(
                currentPiece,
                line);
        }


        Debug.Log(
            $"Loaded {loadedPieces.Count} pieces from:\n{path}");


        return loadedPieces.Count > 0;
    }


    // ============================================================
    // RECONSTRUCT PARTICIPANT
    // ============================================================

    [ContextMenu("Reconstruct Participant")]
    public void ReconstructParticipant()
    {
        if (loadedPieces.Count == 0)
        {
            Debug.LogWarning(
                "No comparison data is loaded. " +
                "Run Load Comparison first.");

            return;
        }


        if (generatedStructureRoot == null)
        {
            Debug.LogError(
                "Generated Structure Root has not been assigned.");

            return;
        }


        ClearGeneratedStructure();


        int createdCount = 0;


        foreach (SavedPiece piece in loadedPieces)
        {
            GameObject prefab =
                GetParticipantPrefab(piece);


            if (prefab == null)
            {
                Debug.LogWarning(
                    $"No prefab found for " +
                    $"{piece.participantName} | " +
                    $"Type: {piece.participantType} | " +
                    $"Color: {piece.participantColor}");

                continue;
            }


            // ----------------------------------------------------
            // Create actual FAST piece.
            // ----------------------------------------------------

            GameObject newPiece =
                Instantiate(
                    prefab,
                    generatedStructureRoot);


            newPiece.name =
                $"Reconstructed_{piece.participantName}_{createdCount}";


            // ----------------------------------------------------
            // Apply the ALIGNED position calculated by the
            // optimal structure comparison.
            //
            // These coordinates are local to the reconstructed
            // structure root.
            // ----------------------------------------------------

            newPiece.transform.localPosition =
                piece.alignedParticipantPosition
                * structureScale;


            // ----------------------------------------------------
            // Apply the ALIGNED participant orientation.
            // ----------------------------------------------------

            newPiece.transform.localRotation =
                piece.alignedParticipantRotation;


            // ----------------------------------------------------
            // Scale.
            //
            // Leave structureScale at 1 if you want the same
            // physical size as the source structures.
            // ----------------------------------------------------

            newPiece.transform.localScale =
                prefab.transform.localScale
                * structureScale;


            generatedPieces.Add(
                newPiece);


            createdCount++;
        }


        Debug.Log(
            $"VR reconstruction complete. " +
            $"Created {createdCount} pieces.");
    }


    // ============================================================
    // CLEAR GENERATED STRUCTURE
    // ============================================================

    [ContextMenu("Clear Reconstructed Structure")]
    public void ClearGeneratedStructure()
    {
        if (generatedStructureRoot == null)
        {
            return;
        }


        // --------------------------------------------------------
        // Play mode.
        // --------------------------------------------------------

        if (Application.isPlaying)
        {
            for (int i =
                     generatedStructureRoot.childCount - 1;
                 i >= 0;
                 i--)
            {
                Destroy(
                    generatedStructureRoot
                        .GetChild(i)
                        .gameObject);
            }
        }

        // --------------------------------------------------------
        // Editor mode.
        // --------------------------------------------------------

        else
        {
            for (int i =
                     generatedStructureRoot.childCount - 1;
                 i >= 0;
                 i--)
            {
                DestroyImmediate(
                    generatedStructureRoot
                        .GetChild(i)
                        .gameObject);
            }
        }


        generatedPieces.Clear();
    }


    // ============================================================
    // PREFAB SELECTION
    // ============================================================

    private GameObject GetParticipantPrefab(
        SavedPiece piece)
    {
        // --------------------------------------------------------
        // PIPE
        // --------------------------------------------------------

        if (piece.participantType == "PIPE")
        {
            switch (piece.participantColor)
            {
                case "RED":
                    return redTubePrefab;

                case "YELLOW":
                    return yellowTubePrefab;

                case "GREEN":
                    return greenTubePrefab;

                case "BLUE":
                    return blueTubePrefab;

                //case "BLACK":
                //    return blackTubePrefab;

                default:
                    Debug.LogWarning(
                        "Unknown pipe color: " +
                        piece.participantColor);

                    return null;
            }
        }


        // --------------------------------------------------------
        // CONNECTORS
        // --------------------------------------------------------

        switch (piece.participantType)
        {
            //case "C_2WAY":
            //    return twoWayConnectorPrefab;

            case "C_ELBOW":
                return elbowConnectorPrefab;

            //case "C_TEE":
            //    return teeConnectorPrefab;

            case "C_3WAY":
                return threeWayConnectorPrefab;

            //case "C_4WAY":
            //    return fourWayConnectorPrefab;

            //case "C_5WAY":
            //    return fiveWayConnectorPrefab;

            default:

                Debug.LogWarning(
                    "Unknown piece type: " +
                    piece.participantType);

                return null;
        }
    }


    // ============================================================
    // PARSE FILE
    // ============================================================

    private void ParsePieceLine(
        SavedPiece piece,
        string line)
    {
        if (line.StartsWith("TargetName="))
        {
            piece.targetName =
                GetValue(line);
        }

        else if (line.StartsWith("ParticipantName="))
        {
            piece.participantName =
                GetValue(line);
        }

        else if (line.StartsWith("TargetType="))
        {
            piece.targetType =
                GetValue(line);
        }

        else if (line.StartsWith("ParticipantType="))
        {
            piece.participantType =
                GetValue(line);
        }

        else if (line.StartsWith("TargetColor="))
        {
            piece.targetColor =
                GetValue(line);
        }

        else if (line.StartsWith("ParticipantColor="))
        {
            piece.participantColor =
                GetValue(line);
        }

        else if (line.StartsWith("TargetPosition="))
        {
            piece.targetPosition =
                ParseVector3(
                    GetValue(line));
        }

        else if (line.StartsWith(
                     "OriginalParticipantPosition="))
        {
            piece.originalParticipantPosition =
                ParseVector3(
                    GetValue(line));
        }

        else if (line.StartsWith(
                     "AlignedParticipantPosition="))
        {
            piece.alignedParticipantPosition =
                ParseVector3(
                    GetValue(line));
        }

        else if (line.StartsWith("TargetRotation="))
        {
            piece.targetRotation =
                ParseQuaternion(
                    GetValue(line));
        }

        else if (line.StartsWith(
                     "OriginalParticipantRotation="))
        {
            piece.originalParticipantRotation =
                ParseQuaternion(
                    GetValue(line));
        }

        else if (line.StartsWith(
                     "AlignedParticipantRotation="))
        {
            piece.alignedParticipantRotation =
                ParseQuaternion(
                    GetValue(line));
        }

        else if (line.StartsWith("PositionError="))
        {
            piece.positionError =
                ParseFloat(
                    GetValue(line));
        }

        else if (line.StartsWith(
                     "OrientationErrorDegrees="))
        {
            piece.orientationError =
                ParseFloat(
                    GetValue(line));
        }
    }


    // ============================================================
    // STRING HELPERS
    // ============================================================

    private string GetValue(
        string line)
    {
        int equalsIndex =
            line.IndexOf('=');


        if (equalsIndex < 0)
        {
            return string.Empty;
        }


        return line.Substring(
            equalsIndex + 1);
    }


    private Vector3 ParseVector3(
        string value)
    {
        string[] values =
            value.Split(',');


        if (values.Length != 3)
        {
            Debug.LogWarning(
                "Invalid Vector3: " +
                value);

            return Vector3.zero;
        }


        return new Vector3(
            ParseFloat(values[0]),
            ParseFloat(values[1]),
            ParseFloat(values[2]));
    }


    private Quaternion ParseQuaternion(
        string value)
    {
        string[] values =
            value.Split(',');


        if (values.Length != 4)
        {
            Debug.LogWarning(
                "Invalid Quaternion: " +
                value);

            return Quaternion.identity;
        }


        Quaternion q =
            new Quaternion(
                ParseFloat(values[0]),
                ParseFloat(values[1]),
                ParseFloat(values[2]),
                ParseFloat(values[3]));


        return NormalizeQuaternion(q);
    }


    private float ParseFloat(
        string value)
    {
        if (float.TryParse(
                value,
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out float result))
        {
            return result;
        }


        Debug.LogWarning(
            "Could not parse float: " +
            value);


        return 0f;
    }


    private Quaternion NormalizeQuaternion(
        Quaternion q)
    {
        float magnitude =
            Mathf.Sqrt(
                q.x * q.x +
                q.y * q.y +
                q.z * q.z +
                q.w * q.w);


        if (magnitude < Mathf.Epsilon)
        {
            return Quaternion.identity;
        }


        return new Quaternion(
            q.x / magnitude,
            q.y / magnitude,
            q.z / magnitude,
            q.w / magnitude);
    }


    // ============================================================
    // FILE LOCATION
    // ============================================================

    [ContextMenu("Show Comparison File Location")]
    public void ShowComparisonFileLocation()
    {
        //string path = Path.Combine(Application.persistentDataPath,fileName);
        string directory = $"FAST_Structure/{"Grade 1 "}/{"FAST_Comparisons"}";
        string path = Path.Combine(directory, fileName);

        Debug.Log("Comparison file location:\n" +path);
    }
}