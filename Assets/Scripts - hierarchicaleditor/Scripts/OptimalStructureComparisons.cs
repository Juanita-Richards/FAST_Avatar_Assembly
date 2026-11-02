using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEngine;

// Filename can remain OptimalStructureComparisons.cs; existing Unity component class stays unchanged.
public class OptimalStructureComparisons : MonoBehaviour
{
    [Header("Builds")]
    [SerializeField] private Transform targetBuildRoot;
    [SerializeField] private Transform participantBuildRoot;
    [Header("Shared Grading Settings - must match BuildGrader")]
    [SerializeField] private FASTSharedGrading.Settings gradingSettings = new FASTSharedGrading.Settings();
    [Header("Output")]
    [SerializeField] private string comparisonFolderName = "FAST_Comparisons";
    [SerializeField] private bool addTimestampToFileName = false;
    [SerializeField] private bool verboseLog = true;
    public FASTSharedGrading.Result LastResult {get;private set;}
    static string F(float f) => f.ToString("R",CultureInfo.InvariantCulture);
    static string V(Vector3 v) => F(v.x)+","+F(v.y)+","+F(v.z);
    static string Q(Quaternion q) => F(q.x)+","+F(q.y)+","+F(q.z)+","+F(q.w);
    static string Safe(string s)
    {
        foreach(char c in Path.GetInvalidFileNameChars())s=s.Replace(c,'_');
        return s.Replace(' ','_');
    }
    // Application.persistentDataPath is where SaveResult writes; both the save and the
    // "show folder" menu item resolve through here so they can never disagree.
    private string ComparisonFolderPath()
    {
        string name = string.IsNullOrEmpty(comparisonFolderName)
            ? "FAST_Comparisons"
            : comparisonFolderName.Trim();

        return Path.Combine(Application.persistentDataPath,
            "FAST_Structure", "Grade 1", name);
    }


    public FASTSharedGrading.Result GenerateComparison()
    {
        if (targetBuildRoot == null || participantBuildRoot == null)
        {
            Debug.LogError("FAST: assign both build roots before comparing.");
            LastResult = null;
            return null;
        }

        LastResult=FASTSharedGrading.Grade(targetBuildRoot,participantBuildRoot,gradingSettings);

        if (LastResult != null && !LastResult.unitsComparable)
            Debug.LogWarning("FAST: result is NOT GRADED - one of the builds has no " +
                "connectors or no tubes, so the two share no common frame. The file " +
                "will be written with zeros.");

        return LastResult;
    }
    [ContextMenu("Compare Structures")]
    public void CompareStructures()
    {
        var r=GenerateComparison();
        if(r==null)return;
        FASTSharedGrading.Log(r,verboseLog);
        SaveResult(r);
    }
    private void SaveResult(FASTSharedGrading.Result r)
    {
        if (r == null)
        {
            Debug.LogError("FAST: nothing to save - the comparison returned no result.");
            return;
        }

        //string folder=ComparisonFolderPath();
        string folder = Path.Combine("FAST_Structure", "Grade 1", comparisonFolderName.Trim());
        Directory.CreateDirectory(folder);
        string file=Safe(targetBuildRoot.name)+"_vs_"+Safe(participantBuildRoot.name);
        if(addTimestampToFileName)file+="_"+DateTime.Now.ToString("yyyyMMdd_HHmmss");
        file+=".txt";
        var sb=new StringBuilder();
        sb.AppendLine("FAST_OPTIMAL_STRUCTURE_COMPARISON");
        sb.AppendLine("TargetRoot="+targetBuildRoot.name);
        sb.AppendLine("ParticipantRoot="+participantBuildRoot.name);
        sb.AppendLine("AlignmentMode="+(gradingSettings.alignWholeBuildRotation?"BestFitRotation":"TranslationOnly"));
        sb.AppendLine("AlignmentRotation="+Q(r.alignmentRotation));
        sb.AppendLine("AlignmentTranslation="+V(r.alignmentTranslation));
        sb.AppendLine("TargetUnit="+F(r.targetUnit));
        sb.AppendLine("ParticipantUnit="+F(r.participantUnit));
        sb.AppendLine("UnitsComparable="+r.unitsComparable);
        sb.AppendLine("TargetPieces="+r.targetCount);
        sb.AppendLine("ParticipantPieces="+r.participantCount);
        sb.AppendLine("MatchedPieces="+r.matches.Count);
        sb.AppendLine("MissingPieces="+r.missing);
        sb.AppendLine("ExtraPieces="+r.extra);
        sb.AppendLine("CorrectStructuralPositions="+r.correctPositions);
        sb.AppendLine("TargetStructuralPositions="+r.targetCount);
        sb.AppendLine("StructureAccuracyPercent="+F(r.PositionPercent));
        sb.AppendLine("TubeColorAccuracyPercent="+F(r.ColorPercent));
        sb.AppendLine("ConnectorAccuracyPercent="+F(r.ConnectorPercent));
        sb.AppendLine("OrientationAccuracyPercent="+F(r.OrientationPercent));
        sb.AppendLine("ConnectionAccuracyPercent="+F(r.ConnectionPercent));
        foreach(var m in r.matches)
        {
            sb.AppendLine("MATCH");
            sb.AppendLine("TargetName="+m.target.name);
            sb.AppendLine("ParticipantName="+m.participant.name);
            sb.AppendLine("TargetType="+m.target.pieceType);
            sb.AppendLine("ParticipantType="+m.participant.pieceType);
            sb.AppendLine("TargetColor="+m.target.pieceColor);
            sb.AppendLine("ParticipantColor="+m.participant.pieceColor);
            sb.AppendLine("TargetPosition="+V(m.targetPosition));
            sb.AppendLine("OriginalParticipantPosition="+V(m.originalParticipantPosition));
            sb.AppendLine("AlignedParticipantPosition="+V(m.alignedParticipantPosition));
            sb.AppendLine("TargetRotation="+Q(m.targetRotation));
            sb.AppendLine("OriginalParticipantRotation="+Q(m.originalParticipantRotation));
            sb.AppendLine("AlignedParticipantRotation="+Q(m.alignedParticipantRotation));
            sb.AppendLine("PositionError="+F(m.positionError));
            sb.AppendLine("PositionCorrect="+m.positionCorrect);
            sb.AppendLine("OrientationErrorDegrees="+F(m.orientationError));
            sb.AppendLine("OrientationCorrect="+m.orientationCorrect);
            sb.AppendLine("StructuralPositionCorrect="+m.positionCorrect);
            sb.AppendLine("ColorCorrect="+m.colorCorrect);
            sb.AppendLine("ConnectorCorrect="+m.connectorCorrect);
            sb.AppendLine("CorrespondenceAmbiguous="+m.ambiguous);
            sb.AppendLine("END_MATCH");
        }
        string path=Path.Combine(folder,file);
        try
        {
            File.WriteAllText(path,sb.ToString());
            Debug.Log("FAST comparison saved to: "+path);
        }
        catch(Exception e)
        {
            // A failed write must not look like a successful comparison.
            Debug.LogError("FAST comparison could not be written to "+path+" - "+e.Message);
        }
    }
    [ContextMenu("Show Comparison Folder")]
    public void ShowComparisonFolder()
    {
        // Must match what SaveResult() actually writes to, and must be the full path:
        // a relative path here would resolve against the editor's working directory,
        // not against Application.persistentDataPath where the file really lands.
        string folder = ComparisonFolderPath();

        Debug.Log("FAST comparison folder: " + folder);
        Debug.Log("Exists: " + Directory.Exists(folder));
    }
}
