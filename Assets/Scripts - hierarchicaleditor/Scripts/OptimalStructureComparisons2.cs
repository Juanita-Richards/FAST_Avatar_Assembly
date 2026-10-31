using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using UnityEngine;

// Filename can remain OptimalStructureComparisons.cs; existing Unity component class stays unchanged.
public class OptimalStructureComparison2 : MonoBehaviour
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
    public FASTSharedGrading.Result GenerateComparison()
    {
        LastResult=FASTSharedGrading.Grade(targetBuildRoot,participantBuildRoot,gradingSettings);
        return LastResult;
    }
    [ContextMenu("Compare Structures")]
    public void CompareStructures()
    {
        var r=GenerateComparison();if(r==null)return;
        FASTSharedGrading.Log(r,verboseLog);
        SaveResult(r);
    }
    private void SaveResult(FASTSharedGrading.Result r)
    {
        string folder=Path.Combine("FAST_Structure","Grade 1",comparisonFolderName.Trim());
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
        File.WriteAllText(path,sb.ToString());
        Debug.Log("FAST comparison saved to: "+path);
    }
    [ContextMenu("Show Comparison Folder")]
    public void ShowComparisonFolder()
    {
        //Debug.Log(Path.Combine(Application.persistentDataPath,"FAST_Structure","Grade 1",comparisonFolderName.Trim()));

        Debug.Log(Path.Combine(
            $"FAST_Structure/{"Grade 1 "} ",
            comparisonFolderName
        ));

    }
}
