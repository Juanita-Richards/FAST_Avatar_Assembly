using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;

public class OptimalStructureVisualizer : MonoBehaviour
{
    [Header("File")]
    [SerializeField]
    private string fileName = "";

    [Header("Visualization")]
    [SerializeField]
    private float targetMarkerSize = 0.04f;

    [SerializeField]
    private float participantMarkerSize = 0.03f;

    [SerializeField]
    private float orientationLineLength = 0.15f;

    [SerializeField]
    private bool showTargetPositions = true;

    [SerializeField]
    private bool showAlignedParticipantPositions = true;

    [SerializeField]
    private bool showMatchLines = true;

    [SerializeField]
    private bool showOrientation = true;

    [Header("Loaded Result")]
    [SerializeField]
    private int loadedMatchCount;

    [SerializeField]
    private float meanPositionError;

    [SerializeField]
    private float rmsPositionError;

    [SerializeField]
    private float normalizedStructureError;

    [Serializable]
    private class SavedMatch
    {
        public string targetName;
        public string participantName;

        public Vector3 targetPosition;
        public Vector3 originalParticipantPosition;
        public Vector3 alignedParticipantPosition;

        public Quaternion targetRotation;
        public Quaternion originalParticipantRotation;
        public Quaternion alignedParticipantRotation;

        public float positionError;
        public float orientationError;
    }

    private List<SavedMatch> matches =
        new List<SavedMatch>();

    // ============================================================
    // LOAD
    // ============================================================

    [ContextMenu("Load Comparison")]
    public void LoadComparison()
    {
        string directory = $"FAST_Structure/{"Grade 1 "}/{"FAST_Comparisons"} ";
        string path = Path.Combine(directory, fileName);
        //string path =
        //    Path.Combine(
        //        Application.persistentDataPath,
        //        fileName);

        if (!File.Exists(path))
        {
            Debug.LogError(
                "Comparison file was not found:\n" +
                path);

            return;
        }

        matches.Clear();

        string[] lines =
            File.ReadAllLines(path);

        SavedMatch current =
            null;

        foreach (string rawLine in lines)
        {
            string line =
                rawLine.Trim();

            if (line.StartsWith(
                    "MeanPositionError="))
            {
                meanPositionError =
                    ParseFloat(
                        Value(line));
            }
            else if (line.StartsWith(
                         "RMSPositionError="))
            {
                rmsPositionError =
                    ParseFloat(
                        Value(line));
            }
            else if (line.StartsWith(
                         "NormalizedStructureError="))
            {
                normalizedStructureError =
                    ParseFloat(
                        Value(line));
            }
            else if (line == "MATCH")
            {
                current =
                    new SavedMatch();
            }
            else if (line == "END_MATCH")
            {
                if (current != null)
                {
                    matches.Add(current);
                    current = null;
                }
            }
            else if (current != null)
            {
                ParseMatchLine(
                    current,
                    line);
            }
        }

        loadedMatchCount =
            matches.Count;

        Debug.Log(
            $"Loaded {loadedMatchCount} optimal matches.\n" +
            $"Mean Error: {meanPositionError:F5}\n" +
            $"RMS Error: {rmsPositionError:F5}\n" +
            $"Normalized Structure Error: " +
            $"{normalizedStructureError:F5}");
    }

    // ============================================================
    // PARSE MATCH
    // ============================================================

    private void ParseMatchLine(
        SavedMatch match,
        string line)
    {
        if (line.StartsWith(
                "TargetName="))
        {
            match.targetName =
                Value(line);
        }
        else if (line.StartsWith(
                     "ParticipantName="))
        {
            match.participantName =
                Value(line);
        }
        else if (line.StartsWith(
                     "TargetPosition="))
        {
            match.targetPosition =
                ParseVector3(
                    Value(line));
        }
        else if (line.StartsWith(
                     "OriginalParticipantPosition="))
        {
            match.originalParticipantPosition =
                ParseVector3(
                    Value(line));
        }
        else if (line.StartsWith(
                     "AlignedParticipantPosition="))
        {
            match.alignedParticipantPosition =
                ParseVector3(
                    Value(line));
        }
        else if (line.StartsWith(
                     "TargetRotation="))
        {
            match.targetRotation =
                ParseQuaternion(
                    Value(line));
        }
        else if (line.StartsWith(
                     "OriginalParticipantRotation="))
        {
            match.originalParticipantRotation =
                ParseQuaternion(
                    Value(line));
        }
        else if (line.StartsWith(
                     "AlignedParticipantRotation="))
        {
            match.alignedParticipantRotation =
                ParseQuaternion(
                    Value(line));
        }
        else if (line.StartsWith(
                     "PositionError="))
        {
            match.positionError =
                ParseFloat(
                    Value(line));
        }
        else if (line.StartsWith(
                     "OrientationErrorDegrees="))
        {
            match.orientationError =
                ParseFloat(
                    Value(line));
        }
    }

    // ============================================================
    // UNITY GIZMO VISUALIZATION
    // ============================================================

    private void OnDrawGizmos()
    {
        if (matches == null ||
            matches.Count == 0)
        {
            return;
        }

        foreach (SavedMatch match
                 in matches)
        {
            Vector3 targetWorld =
                transform.TransformPoint(
                    match.targetPosition);

            Vector3 participantWorld =
                transform.TransformPoint(
                    match.alignedParticipantPosition);

            // ----------------------------------------------------
            // TARGET
            // ----------------------------------------------------

            if (showTargetPositions)
            {
                Gizmos.color =
                    Color.green;

                Gizmos.DrawSphere(
                    targetWorld,
                    targetMarkerSize);

                if (showOrientation)
                {
                    Quaternion worldRotation =
                        transform.rotation
                        * match.targetRotation;

                    Vector3 forward =
                        worldRotation
                        * Vector3.forward;

                    Gizmos.DrawLine(
                        targetWorld,
                        targetWorld +
                        forward
                        * orientationLineLength);
                }
            }

            // ----------------------------------------------------
            // ALIGNED PARTICIPANT
            // ----------------------------------------------------

            if (showAlignedParticipantPositions)
            {
                Gizmos.color =
                    Color.yellow;

                Gizmos.DrawWireSphere(
                    participantWorld,
                    participantMarkerSize);

                if (showOrientation)
                {
                    Quaternion worldRotation =
                        transform.rotation
                        * match.alignedParticipantRotation;

                    Vector3 forward =
                        worldRotation
                        * Vector3.forward;

                    Gizmos.DrawLine(
                        participantWorld,
                        participantWorld +
                        forward
                        * orientationLineLength);
                }
            }

            // ----------------------------------------------------
            // CORRESPONDENCE
            // ----------------------------------------------------

            if (showMatchLines)
            {
                Gizmos.color =
                    Color.white;

                Gizmos.DrawLine(
                    targetWorld,
                    participantWorld);
            }
        }
    }

    // ============================================================
    // PARSING
    // ============================================================

    private string Value(
        string line)
    {
        int index =
            line.IndexOf('=');

        if (index < 0)
        {
            return "";
        }

        return line.Substring(
            index + 1);
    }

    private Vector3 ParseVector3(
        string value)
    {
        string[] parts =
            value.Split(',');

        if (parts.Length != 3)
        {
            return Vector3.zero;
        }

        return new Vector3(
            ParseFloat(parts[0]),
            ParseFloat(parts[1]),
            ParseFloat(parts[2]));
    }

    private Quaternion ParseQuaternion(
        string value)
    {
        string[] parts =
            value.Split(',');

        if (parts.Length != 4)
        {
            return Quaternion.identity;
        }

        return new Quaternion(
            ParseFloat(parts[0]),
            ParseFloat(parts[1]),
            ParseFloat(parts[2]),
            ParseFloat(parts[3]));
    }

    private float ParseFloat(
        string value)
    {
        float.TryParse(
            value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out float result);

        return result;
    }
}