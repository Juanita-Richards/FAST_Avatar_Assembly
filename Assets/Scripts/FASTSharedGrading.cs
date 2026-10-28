using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayStructure;

// Shared source of truth for BOTH OSC and BuildGrader. Do not duplicate these scores.
public static class FASTSharedGrading
{
    [Serializable] public class Pair
    {
        public BuildingPiece target, participant;
        public Vector3 targetPosition, originalParticipantPosition, alignedParticipantPosition;
        public Quaternion targetRotation, originalParticipantRotation, alignedParticipantRotation;
        public float positionError, orientationError, nextBestDistance;
        public bool positionCorrect, orientationCorrect, colorCorrect, connectorCorrect;
        public bool ambiguous;
    }
    [Serializable] public class Result
    {
        public int targetCount, participantCount, missing, extra;
        public int targetTubes, correctColors, targetConnectors, correctConnectors;
        public int correctPositions, correctOrientations, targetConnections, correctConnections;
        public float structuralAxisError;
        public bool structuralAxisCorrect, axisEvaluated;
        public int targetAxisCount, participantAxisCount;
        public Quaternion alignmentRotation = Quaternion.identity;
        public Vector3 alignmentTranslation;
        public float targetUnit, participantUnit;
        public List<Pair> matches = new List<Pair>();
        public List<string> connectionLog = new List<string>();
        public List<string> diagnosticLog = new List<string>();
        public float PositionPercent => Percent(correctPositions, targetCount);
        public float ColorPercent => Percent(correctColors, targetTubes);
        public float ConnectorPercent => Percent(correctConnectors, targetConnectors);
        public float OrientationPercent => Percent(correctOrientations, targetCount);
        public float ConnectionPercent => Percent(correctConnections, targetConnections);
    }
    [Serializable] public class Settings
    {
        public float normalizedPositionTolerance = 0.35f;
        public float orientationToleranceDegrees = 10f;
        public float axisToleranceDegrees = 10f;
        public bool alignWholeBuildRotation = false;
        public float correspondenceAmbiguityThreshold = 0.03f;
    }
    public static float Percent(int numerator, int denominator) => denominator > 0 ? 100f * numerator / denominator : 0f;
    public static bool Tube(BuildingPiece p) => p != null && p.pieceType == PieceType.PIPE;
    public static bool Connector(BuildingPiece p)
    {
        if (p == null) return false;
        switch (p.pieceType)
        {
            case PieceType.C_2WAY: case PieceType.C_ELBOW: case PieceType.C_TEE:
            case PieceType.C_3WAY: case PieceType.C_4WAY: case PieceType.C_5WAY: return true;
            default: return false;
        }
    }
    public static bool Gradable(BuildingPiece p) => Tube(p) || Connector(p);
    static List<BuildingPiece> Pieces(Transform root) => root.GetComponentsInChildren<BuildingPiece>(true)
        .Where(Gradable).OrderBy(p => HierarchyKey(p.transform, root), StringComparer.Ordinal).ToList();
    static string HierarchyKey(Transform t, Transform root)
    {
        var parts = new List<string>();
        while (t != null && t != root) { parts.Add(t.GetSiblingIndex().ToString("D6") + ":" + t.name); t = t.parent; }
        parts.Reverse(); return string.Join("/", parts.ToArray());
    }
    static Vector3 Local(Transform root, BuildingPiece p) => root.InverseTransformPoint(p.transform.position);
    static Quaternion Rotation(Transform root, BuildingPiece p) => Quaternion.Inverse(root.rotation) * p.transform.rotation;
    static Vector3 Centre(List<BuildingPiece> pieces, Transform root)
    {
        var anchors = pieces.Where(Connector).ToList();
        if (anchors.Count == 0) anchors = pieces;
        if (anchors.Count == 0) return Vector3.zero;
        Vector3 result = Vector3.zero;
        foreach (var p in anchors) result += Local(root, p);
        return result / anchors.Count;
    }
    static float Unit(List<BuildingPiece> pieces, Transform root)
    {
        var ts = pieces.Where(Tube).ToList(); var cs = pieces.Where(Connector).ToList();
        if (ts.Count == 0 || cs.Count == 0) return 1f;
        var d = new List<float>();
        foreach (var t in ts)
        {
            float best = float.PositiveInfinity;
            foreach (var c in cs) best = Mathf.Min(best, Vector3.Distance(Local(root,t),Local(root,c)));
            if (best > 0.000001f && !float.IsInfinity(best)) d.Add(best);
        }
        if (d.Count == 0) return 1f;
        d.Sort(); int m = d.Count / 2;
        float v = d.Count % 2 == 1 ? d[m] : (d[m-1]+d[m]) * 0.5f;
        return v > 0.000001f ? v : 1f;
    }
    // Feasibility for the smallest possible maximum normalized distance.
    static bool Augment(int i, float[,] d, float bound, bool[] visited, int[] owner)
    {
        for (int j=0;j<owner.Length;j++)
        {
            if (visited[j] || d[i,j] > bound + 1e-5f) continue;
            visited[j]=true;
            if (owner[j] < 0 || Augment(owner[j],d,bound,visited,owner)) {owner[j]=i;return true;}
        }
        return false;
    }
    static bool Feasible(float[,] d, float bound)
    {
        int n=d.GetLength(0), m=d.GetLength(1);
        int[] owner=Enumerable.Repeat(-1,m).ToArray();
        int count=0;
        for(int i=0;i<n;i++) if(Augment(i,d,bound,new bool[m],owner)) count++;
        return count==Mathf.Min(n,m);
    }
    // Hungarian (minimum sum under the optimal bottleneck). Square matrix with dummy rows/columns.
    static int[] Hungarian(double[,] a)
    {
        int n=a.GetLength(0); var u=new double[n+1];var v=new double[n+1];
        var p=new int[n+1];var way=new int[n+1];
        for(int i=1;i<=n;i++)
        {
            p[0]=i; int j0=0;var minv=Enumerable.Repeat(double.PositiveInfinity,n+1).ToArray();var used=new bool[n+1];
            do {
                used[j0]=true;int i0=p[j0], j1=0;double delta=double.PositiveInfinity;
                for(int j=1;j<=n;j++) if(!used[j]) {
                    double cur=a[i0-1,j-1]-u[i0]-v[j];
                    if(cur<minv[j]) {minv[j]=cur;way[j]=j0;}
                    if(minv[j]<delta) {delta=minv[j];j1=j;}
                }
                for(int j=0;j<=n;j++) if(used[j]){u[p[j]]+=delta;v[j]-=delta;}
                    else minv[j]-=delta;
                j0=j1;
            } while(p[j0]!=0);
            do {int j1=way[j0];p[j0]=p[j1];j0=j1;} while(j0!=0);
        }
        var answer=Enumerable.Repeat(-1,n).ToArray();
        for(int j=1;j<=n;j++) if(p[j]>0) answer[p[j]-1]=j-1;
        return answer;
    }
    static List<Tuple<int,int>> Assign(List<BuildingPiece> ps, List<BuildingPiece> ts,
        Transform pRoot, Transform tRoot, Vector3 pc, Vector3 tc, float pu, float tu, Quaternion align)
    {
        int n=ps.Count,m=ts.Count;
        var result=new List<Tuple<int,int>>();if(n==0||m==0)return result;
        // Use participant rows if fewer; transpose otherwise for feasibility.
        bool transpose=n>m;int rows=transpose?m:n,cols=transpose?n:m;
        float[,] d=new float[rows,cols];var candidates=new List<float>();
        for(int i=0;i<rows;i++) for(int j=0;j<cols;j++) {
            var pp=ps[transpose?j:i];var tt=ts[transpose?i:j];
            Vector3 p=align*((Local(pRoot,pp)-pc)/pu);
            Vector3 t=(Local(tRoot,tt)-tc)/tu;
            d[i,j]=Vector3.Distance(p,t); candidates.Add(d[i,j]);
        }
        candidates.Sort();int lo=0,hi=candidates.Count-1;
        while(lo<hi){int mid=(lo+hi)/2;if(Feasible(d,candidates[mid]))hi=mid;else lo=mid+1;}
        float bottleneck=candidates[lo];int size=cols;
        var costs=new double[size,size];
        for(int i=0;i<size;i++)for(int j=0;j<size;j++)
            costs[i,j]=i>=rows?0:(d[i,j]<=bottleneck+1e-5f?d[i,j]+1e-9*(i*size+j):1e7);
        var map=Hungarian(costs);
        for(int i=0;i<rows;i++)if(map[i]>=0&&map[i]<cols&&d[i,map[i]]<=bottleneck+1e-4f)
            result.Add(transpose?Tuple.Create(map[i],i):Tuple.Create(i,map[i]));
        return result;
    }
    static List<Tuple<int,int>> AllAssignments(List<BuildingPiece> ps,List<BuildingPiece> ts,
        Transform pr,Transform tr,Vector3 pc,Vector3 tc,float pu,float tu,Quaternion q)
    {
        var result=new List<Tuple<int,int>>();
        foreach(bool tube in new[]{true,false}) {
            var a=ps.Where(p=>Tube(p)==tube).ToList();var b=ts.Where(p=>Tube(p)==tube).ToList();
            var pairs=Assign(a,b,pr,tr,pc,tc,pu,tu,q);
            foreach(var pair in pairs)result.Add(Tuple.Create(ps.IndexOf(a[pair.Item1]),ts.IndexOf(b[pair.Item2])));
        }
        return result;
    }
    // Best-fit quaternion for already established pairings; optional, never used unless requested.
    static Quaternion FitRotation(List<Tuple<Vector3,Vector3>> pairs)
    {
        if(pairs.Count<3)return Quaternion.identity;
        Vector3 pa=Vector3.zero,pb=Vector3.zero;
        foreach(var pair in pairs){pa+=pair.Item1;pb+=pair.Item2;}
        pa/=pairs.Count;pb/=pairs.Count;
        float sxx=0,sxy=0,sxz=0,syx=0,syy=0,syz=0,szx=0,szy=0,szz=0;
        foreach(var pair in pairs) {
            Vector3 a=pair.Item1-pa,b=pair.Item2-pb;
            sxx+=a.x*b.x;sxy+=a.x*b.y;sxz+=a.x*b.z;
            syx+=a.y*b.x;syy+=a.y*b.y;syz+=a.y*b.z;
            szx+=a.z*b.x;szy+=a.z*b.y;szz+=a.z*b.z;
        }
        float[,] N={
            {sxx+syy+szz,syz-szy,szx-sxz,sxy-syx},
            {syz-szy,sxx-syy-szz,sxy+syx,szx+sxz},
            {szx-sxz,sxy+syx,-sxx+syy-szz,syz+szy},
            {sxy-syx,szx+sxz,syz+szy,-sxx-syy+szz}};
        float[] v={1f,0f,0f,0f};
        // Positive diagonal shift makes power iteration seek the largest algebraic eigenvalue.
        for(int k=0;k<80;k++) {
            var w=new float[4];float norm=0;
            for(int i=0;i<4;i++){w[i]=1000f*v[i];for(int j=0;j<4;j++)w[i]+=N[i,j]*v[j];norm+=w[i]*w[i];}
            if(norm<1e-12f)break;norm=Mathf.Sqrt(norm);
            for(int i=0;i<4;i++)v[i]=w[i]/norm;
        }
        return Quaternion.Normalize(new Quaternion(v[1],v[2],v[3],v[0]));
    }
    static float ErrorAngle(BuildingPiece t,BuildingPiece p,Quaternion tr,Quaternion pr)
    {
        if(t.pieceType!=p.pieceType)return 180f;
        var ta=new List<Quaternion>{tr};var pb=new List<Quaternion>{pr};
        if(t.symmetries!=null)foreach(var s in t.symmetries)if(s!=null)ta.Add(s.rotationalOffsetFromCanonical*tr);
        if(p.symmetries!=null)foreach(var s in p.symmetries)if(s!=null)pb.Add(s.rotationalOffsetFromCanonical*pr);
        float best=180f;foreach(var a in ta)foreach(var b in pb)best=Mathf.Min(best,Quaternion.Angle(a,b));
        return best;
    }
    static string ConnectionKey(BuildingPiece a,BuildingPiece b)
    {
        int x=a.GetInstanceID(),y=b.GetInstanceID();return x<y?x+":"+y:y+":"+x;
    }
    static bool Connected(BuildingPiece a,BuildingPiece b)
    {
        if(a==null||b==null||a.attachPoints==null)return false;
        return a.attachPoints.Any(ap=>ap!=null&&ap.attachedAttachPoint!=null&&ap.attachedAttachPoint.owningPiece==b);
    }
    static Vector3 Canonical(Vector3 v)
    {
        if(v.x < -1e-4f || (Mathf.Abs(v.x)<=1e-4f && v.y< -1e-4f) ||
           (Mathf.Abs(v.x)<=1e-4f && Mathf.Abs(v.y)<=1e-4f&&v.z<0)) return -v;
        return v;
    }
    static List<Vector3> Axes(List<BuildingPiece> pieces,Transform root,Vector3 centre,float unit,Quaternion q)
    {
        var axes=new List<Vector3>();
        foreach(var piece in pieces){Vector3 v=q*((Local(root,piece)-centre)/unit);
            if(v.sqrMagnitude<1e-8f)continue;v=Canonical(v.normalized);
            if(!axes.Any(a=>Mathf.Abs(Vector3.Dot(a,v))>0.999999f)) axes.Add(v);
        }
        return axes;
    }
    static float AxisError(List<Vector3> a,List<Vector3> b)
    {
        if(a.Count==0||b.Count==0||a.Count!=b.Count)return 180f;
        int n=a.Count;var d=new float[n,n];var values=new List<float>();
        for(int i=0;i<n;i++)for(int j=0;j<n;j++){
            d[i,j]=Mathf.Acos(Mathf.Clamp(Mathf.Abs(Vector3.Dot(a[i],b[j])),-1,1))*Mathf.Rad2Deg;
            values.Add(d[i,j]);
        }
        values.Sort();int lo=0,hi=values.Count-1;
        while(lo<hi){int mid=(lo+hi)/2;if(Feasible(d,values[mid]))hi=mid;else lo=mid+1;}
        return values[lo];
    }
    public static Result Grade(Transform targetRoot,Transform participantRoot,Settings settings)
    {
        if(targetRoot==null||participantRoot==null){Debug.LogError("FAST: assign both build roots.");return null;}
        var ts=Pieces(targetRoot);var ps=Pieces(participantRoot);
        var r=new Result {targetCount=ts.Count,participantCount=ps.Count};
        r.targetTubes=ts.Count(Tube);r.targetConnectors=ts.Count(Connector);
        if(ts.Count==0||ps.Count==0){Debug.LogError("FAST: both builds need gradable BuildingPiece objects.");return null;}
        Vector3 tc=Centre(ts,targetRoot),pc=Centre(ps,participantRoot);
        float tu=Unit(ts,targetRoot),pu=Unit(ps,participantRoot);r.targetUnit=tu;r.participantUnit=pu;
        Quaternion q=Quaternion.identity;
        var mappings=AllAssignments(ps,ts,participantRoot,targetRoot,pc,tc,pu,tu,q);
        if(settings.alignWholeBuildRotation)
        {
            // Iterative best-fit refinement of the initial position-based mapping.
            // For symmetric or strongly rotated builds this is heuristic: inspect ambiguity logs.
            for(int iter=0;iter<8;iter++) {
                var pairs=mappings.Where(x=>Connector(ps[x.Item1])).Select(x=>Tuple.Create(
                    (Local(participantRoot,ps[x.Item1])-pc)/pu,
                    (Local(targetRoot,ts[x.Item2])-tc)/tu)).ToList();
                if(pairs.Count<3)break;
                q=FitRotation(pairs);
                mappings=AllAssignments(ps,ts,participantRoot,targetRoot,pc,tc,pu,tu,q);
            }
        }
        r.alignmentRotation=q;
        // From the participant original coordinate space to target local coordinate space.
        r.alignmentTranslation=tc-q*pc*(tu/pu);
        foreach(var ij in mappings)
        {
            var p=ps[ij.Item1];var t=ts[ij.Item2];
            Vector3 tp=Local(targetRoot,t),pp=Local(participantRoot,p);
            Vector3 aligned=tc+q*((pp-pc)/pu)*tu;
            Quaternion tr=Rotation(targetRoot,t),pr=q*Rotation(participantRoot,p);
            float distance=Vector3.Distance((tp-tc)/tu,q*((pp-pc)/pu));
            var sameTypeTargets=ts.Where(x=>Tube(x)==Tube(p)).ToList();
            var alternatives=sameTypeTargets.Where(x=>x!=t)
                .Select(x=>Vector3.Distance((Local(targetRoot,x)-tc)/tu,q*((pp-pc)/pu))).OrderBy(x=>x).ToList();
            float next=alternatives.Count>0?alternatives[0]:float.PositiveInfinity;
            var match=new Pair {
                target=t,participant=p,targetPosition=tp,originalParticipantPosition=pp,
                alignedParticipantPosition=aligned,targetRotation=tr,
                originalParticipantRotation=Rotation(participantRoot,p),alignedParticipantRotation=pr,
                positionError=distance,positionCorrect=distance<=settings.normalizedPositionTolerance,
                orientationError=ErrorAngle(t,p,tr,pr),nextBestDistance=next,
                ambiguous=next-distance<=settings.correspondenceAmbiguityThreshold,
                colorCorrect=Tube(t)&&Tube(p)&&t.pieceColor==p.pieceColor,
                connectorCorrect=Connector(t)&&Connector(p)&&t.pieceType==p.pieceType
            };
            match.orientationCorrect=match.orientationError<=settings.orientationToleranceDegrees;
            r.matches.Add(match);
            r.diagnosticLog.Add($"[PAIR] target={t.name} ({t.pieceType}/{t.pieceColor}) participant={p.name} ({p.pieceType}/{p.pieceColor}) posErrNormalized={distance:F4} posOK={match.positionCorrect} rotErr={match.orientationError:F2} rotOK={match.orientationCorrect} colorOK={match.colorCorrect} connectorOK={match.connectorCorrect} ambiguous={match.ambiguous} nextAlternative={next:F4}");
        }
        r.missing=ts.Count-r.matches.Count;r.extra=ps.Count-r.matches.Count;
        foreach(var t in ts.Where(t=>!r.matches.Any(x=>x.target==t)))r.diagnosticLog.Add("[MISSING] "+t.name);
        foreach(var p in ps.Where(p=>!r.matches.Any(x=>x.participant==p)))r.diagnosticLog.Add("[EXTRA] "+p.name);
        r.correctPositions=r.matches.Count(x=>x.positionCorrect);
        r.correctOrientations=r.matches.Count(x=>x.orientationCorrect);
        r.correctColors=r.matches.Count(x=>Tube(x.target)&&x.colorCorrect);
        r.correctConnectors=r.matches.Count(x=>Connector(x.target)&&x.connectorCorrect);
        var ta=Axes(ts,targetRoot,tc,tu,Quaternion.identity);
        var pa=Axes(ps,participantRoot,pc,pu,q);
        r.targetAxisCount=ta.Count;r.participantAxisCount=pa.Count;
        r.axisEvaluated=ta.Count>0&&pa.Count>0;
        r.structuralAxisError=r.axisEvaluated?AxisError(ta,pa):180f;
        r.structuralAxisCorrect=r.axisEvaluated&&r.structuralAxisError<=settings.axisToleranceDegrees;
        var seen=new HashSet<string>();
        foreach(var t in ts)if(t.attachPoints!=null)foreach(var ap in t.attachPoints) {
            if(ap==null||ap.attachedAttachPoint==null)continue;
            var other=ap.attachedAttachPoint.owningPiece;
            if(other==null||!ts.Contains(other))continue;
            var tube=Tube(t)&&Connector(other)?t:Connector(t)&&Tube(other)?other:null;
            var conn=tube==t?other:t;
            if(tube==null)continue;
            string key=ConnectionKey(tube,conn);if(!seen.Add(key))continue;
            r.targetConnections++;
            var tm=r.matches.FirstOrDefault(x=>x.target==tube);
            var cm=r.matches.FirstOrDefault(x=>x.target==conn);
            string reason=tm==null?"missing mapped tube":cm==null?"missing mapped connector":
                !tm.colorCorrect?"wrong tube color":!cm.connectorCorrect?"wrong connector type":
                !Connected(tm.participant,cm.participant)?"not connected":"PASS";
            if(reason=="PASS")r.correctConnections++;
            r.connectionLog.Add($"[CONNECTION] {tube.name} <-> {conn.name}: {reason}");
        }
        return r;
    }
    public static void Log(Result r,bool details)
    {
        if(r==null)return;
        Debug.Log($"STRUCTURAL POSITION: {r.correctPositions}/{r.targetCount} ({r.PositionPercent:F2}%)\n"+
            $"TUBE COLORS: {r.correctColors}/{r.targetTubes} ({r.ColorPercent:F2}%)\n"+
            $"CONNECTORS: {r.correctConnectors}/{r.targetConnectors} ({r.ConnectorPercent:F2}%)\n"+
            $"INDIVIDUAL ROTATION: {r.correctOrientations}/{r.targetCount} ({r.OrientationPercent:F2}%)\n"+
            $"STRUCTURAL AXIS: {r.structuralAxisError:F2} deg ({(r.structuralAxisCorrect?"CORRECT":"INCORRECT")}; axes {r.targetAxisCount}/{r.participantAxisCount})\n"+
            $"CONNECTIONS: {r.correctConnections}/{r.targetConnections} ({r.ConnectionPercent:F2}%)\n"+
            $"MATCHED: {r.matches.Count}, MISSING: {r.missing}, EXTRA: {r.extra}");
        if(details){foreach(var s in r.diagnosticLog)Debug.Log(s);foreach(var s in r.connectionLog)Debug.Log(s);}
    }
}
