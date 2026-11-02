using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using PlayStructure;

// Shared source of truth for OSC and BuildGrader.
public static class FASTSharedGrading
{
    [Serializable] public class Pair
    {
        public BuildingPiece target, participant;
        public Vector3 targetPosition, originalParticipantPosition, alignedParticipantPosition;
        public Quaternion targetRotation, originalParticipantRotation, alignedParticipantRotation;
        public float positionError, orientationError, nextBestDistance;
        public bool positionCorrect, orientationCorrect, colorCorrect, connectorCorrect, ambiguous;
    }

    [Serializable] public class Result
    {
        public int targetCount, participantCount, missing, extra;
        public int targetTubes, correctColors, targetConnectors, correctConnectors;
        public int correctPositions, correctOrientations;
        public int targetConnections, correctConnections;
        public float structuralAxisError;
        public bool structuralAxisCorrect, axisEvaluated;
        public int targetAxisCount, participantAxisCount;
        public Quaternion alignmentRotation = Quaternion.identity;
        public Vector3 alignmentTranslation;
        // Similarity fitted from the correspondences (co-alignment). 
        // coAlignmentApplied is false only when there were too few pairs to fit.
        public bool coAlignmentApplied;
        public bool coAlignmentRotated;
        public float coAlignmentScale = 1f;
        public float coAlignmentRotationDegrees;
        public Vector3 coAlignmentTranslation;
        public float coAlignmentResidualMax;
        public float coAlignmentResidualMean;
        public int coAlignmentIterations;
        public int coAlignmentPairs;
        public float targetUnit, participantUnit;
        public bool unitsComparable;
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
        // Co-alignment: fit one similarity from the correspondences and score the
        // residual, so a rigid grid-phase offset between the two builds is absorbed
        // rather than counted against the participant piece by piece.
        public bool coAlignCorrespondences = true;
        public bool coAlignSolveRotation = true;
        [Range(0f, 2f)] public float coAlignScaleClamp = 0.25f;
        public int coAlignMaxIterations = 8;

        // Clamped on read so a bad inspector value degrades loudly rather than silently.
        public float PositionTolerance => Mathf.Max(0f, normalizedPositionTolerance);
        public float OrientationTolerance => Mathf.Clamp(orientationToleranceDegrees, 0f, 180f);
        public float AxisTolerance => Mathf.Clamp(axisToleranceDegrees, 0f, 180f);
        public float AmbiguityThreshold => Mathf.Max(0f, correspondenceAmbiguityThreshold);
        public float ScaleClamp => Mathf.Clamp(coAlignScaleClamp, 0f, 2f);
        public int CoAlignIterations => Mathf.Clamp(coAlignMaxIterations, 1, 32);
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
        while (t != null && t != root)
        {
            parts.Add(t.GetSiblingIndex().ToString("D6") + ":" + t.name);
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts.ToArray());
    }

    static Vector3 Local(Transform root, BuildingPiece p) => root.InverseTransformPoint(p.transform.position);
    static Quaternion Rotation(Transform root, BuildingPiece p) => Quaternion.Inverse(root.rotation) * p.transform.rotation;

    // Anchors are the connectors; this is the point the normalised frame is built around.
    static bool TryCentre(List<BuildingPiece> pieces, Transform root, out Vector3 centre)
    {
        centre = Vector3.zero;
        var anchors = pieces.Where(Connector).ToList();
        if (anchors.Count == 0) return false;
        foreach (var p in anchors) centre += Local(root, p);
        centre /= anchors.Count;
        return true;
    }

    // Median tube-to-nearest-connector distance: the build's own notion of one grid step.
    static bool TryUnit(List<BuildingPiece> pieces, Transform root, out float unit)
    {
        unit = 1f;
        var ts = pieces.Where(Tube).ToList();
        var cs = pieces.Where(Connector).ToList();
        if (ts.Count == 0 || cs.Count == 0) return false;
        var distances = new List<float>();
        foreach (var t in ts)
        {
            float best = float.PositiveInfinity;
            foreach (var c in cs)
                best = Mathf.Min(best, Vector3.Distance(Local(root, t), Local(root, c)));
            if (best > 0.000001f && !float.IsInfinity(best)) distances.Add(best);
        }
        if (distances.Count == 0) return false;
        distances.Sort();
        int m = distances.Count / 2;
        float median = distances.Count % 2 == 1 ? distances[m] : (distances[m - 1] + distances[m]) * 0.5f;
        if (median <= 0.000001f) return false;
        unit = median;
        return true;
    }

    // Kuhn's augmenting-path search. visitedRows is what keeps this terminating: without it a
    // dense tolerance band can re-enter the same row through different columns and recurse forever.
    static bool Augment(int i, float[,] d, float bound, bool[] visitedCols, bool[] visitedRows, int[] owner)
    {
        if (visitedRows[i]) return false;
        visitedRows[i] = true;
        for (int j = 0; j < owner.Length; j++)
        {
            if (visitedCols[j] || d[i, j] > bound + 1e-5f) continue;
            visitedCols[j] = true;
            if (owner[j] < 0 || Augment(owner[j], d, bound, visitedCols, visitedRows, owner))
            {
                owner[j] = i;
                return true;
            }
        }
        return false;
    }

    static bool Feasible(float[,] d, float bound)
    {
        int n = d.GetLength(0), m = d.GetLength(1);
        int[] owner = Enumerable.Repeat(-1, m).ToArray();
        int count = 0;
        for (int i = 0; i < n; i++)
            if (Augment(i, d, bound, new bool[m], new bool[n], owner)) count++;
        return count == Mathf.Min(n, m);
    }

    // Square-assignment Hungarian (Jonker-Volgenant style potentials). Requires a square matrix.
    static int[] Hungarian(double[,] a)
    {
        int n = a.GetLength(0);
        if (n == 0) return new int[0];
        if (a.GetLength(1) != n) throw new ArgumentException("Hungarian requires a square matrix.");
        var u = new double[n + 1]; var v = new double[n + 1];
        var p = new int[n + 1]; var way = new int[n + 1];
        for (int i = 1; i <= n; i++)
        {
            p[0] = i;
            int j0 = 0;
            var minv = Enumerable.Repeat(double.PositiveInfinity, n + 1).ToArray();
            var used = new bool[n + 1];
            do
            {
                used[j0] = true;
                int i0 = p[j0], j1 = 0;
                double delta = double.PositiveInfinity;
                for (int j = 1; j <= n; j++) if (!used[j])
                {
                    double cur = a[i0 - 1, j - 1] - u[i0] - v[j];
                    if (cur < minv[j]) { minv[j] = cur; way[j] = j0; }
                    if (minv[j] < delta) { delta = minv[j]; j1 = j; }
                }
                for (int j = 0; j <= n; j++)
                    if (used[j]) { u[p[j]] += delta; v[j] -= delta; }
                    else minv[j] -= delta;
                j0 = j1;
            } while (p[j0] != 0);
            do { int j1 = way[j0]; p[j0] = p[j1]; j0 = j1; } while (j0 != 0);
        }
        var answer = Enumerable.Repeat(-1, n).ToArray();
        for (int j = 1; j <= n; j++) if (p[j] > 0) answer[p[j] - 1] = j - 1;
        return answer;
    }

    static List<Tuple<int, int>> Assign(List<BuildingPiece> ps, List<BuildingPiece> ts,
        Transform pRoot, Transform tRoot, Vector3 pc, Vector3 tc, float pu, float tu, Quaternion align)
    {
        int n = ps.Count, m = ts.Count;
        var result = new List<Tuple<int, int>>();
        if (n == 0 || m == 0) return result;
        // d stays in "participant rows x target columns" order; the padding matrix below is not.
        float[,] d = new float[n, m];
        var candidates = new List<float>();
        for (int i = 0; i < n; i++) for (int j = 0; j < m; j++)
        {
            Vector3 p = align * ((Local(pRoot, ps[i]) - pc) / pu);
            Vector3 t = (Local(tRoot, ts[j]) - tc) / tu;
            d[i, j] = Vector3.Distance(p, t);
            candidates.Add(d[i, j]);
        }
        candidates.Sort();
        int lo = 0, hi = candidates.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (Feasible(d, candidates[mid])) hi = mid;
            else lo = mid + 1;
        }
        float bottleneck = candidates[lo];

        // Pad to square, minimising total distance among the pairs inside the bottleneck.
        int size = Mathf.Max(n, m);
        var costs = new double[size, size];
        for (int i = 0; i < size; i++) for (int j = 0; j < size; j++)
            costs[i, j] = (i < n && j < m && d[i, j] <= bottleneck + 1e-5f)
                ? d[i, j] + 1e-9 * (i * (double)size + j)   // stable tie-break
                : 1e7;
        var map = Hungarian(costs);
        for (int i = 0; i < n; i++)
            if (map[i] >= 0 && map[i] < m && d[i, map[i]] <= bottleneck + 1e-4f)
                result.Add(Tuple.Create(i, map[i]));
        return result;
    }

    // Thin wrapper kept for the alignment seed search: identity similarity over the
    // raw q. All the assignment weight now lives in AllAssignmentsBetween.
    static List<Tuple<int, int>> AllAssignments(List<BuildingPiece> ps, List<BuildingPiece> ts,
        Transform pr, Transform tr, Vector3 pc, Vector3 tc, float pu, float tu, Quaternion q)
    {
        return AllAssignmentsBetween(ps, ts, pr, tr, pc, tc, pu, tu, q,
            out _, out _);
    }

    // Assignment solver over normalised points, optionally scored through a fitted similarity.
    // When 'fit' is supplied every participant point is pushed through it before the distance
    // is taken, which is what lets co-alignment absorb a rigid offset instead of penalising it.
    static List<Tuple<int, int>> AllAssignmentsBetween(List<BuildingPiece> ps, List<BuildingPiece> ts,
        Transform pr, Transform tr, Vector3 pc, Vector3 tc, float pu, float tu, Quaternion q,
        out List<Vector3> participantPoints, out List<Vector3> targetPoints)
    {
        return AllAssignmentsBetween(ps, ts, pr, tr, pc, tc, pu, tu, q,
            out participantPoints, out targetPoints,
            new Similarity { rotation = q, scale = 1f, translation = Vector3.zero }, false);
    }

    static List<Tuple<int, int>> AllAssignmentsBetween(List<BuildingPiece> ps, List<BuildingPiece> ts,
        Transform pr, Transform tr, Vector3 pc, Vector3 tc, float pu, float tu, Quaternion q,
        out List<Vector3> participantPoints, out List<Vector3> targetPoints,
        Similarity fit, bool applyFit)
    {
        participantPoints = ps.Select(p => (Local(pr, p) - pc) / pu).ToList();
        targetPoints = ts.Select(t => (Local(tr, t) - tc) / tu).ToList();

        var result = new List<Tuple<int, int>>();
        foreach (bool tube in new[] { true, false })
        {
            var aIdx = Enumerable.Range(0, ps.Count).Where(i => Tube(ps[i]) == tube).ToList();
            var bIdx = Enumerable.Range(0, ts.Count).Where(i => Tube(ts[i]) == tube).ToList();
            if (aIdx.Count == 0 || bIdx.Count == 0) continue;

            int n = aIdx.Count, m = bIdx.Count;
            var d = new float[n, m];
            var candidates = new List<float>();
            for (int i = 0; i < n; i++) for (int j = 0; j < m; j++)
            {
                Vector3 pv = applyFit ? Apply(fit, participantPoints[aIdx[i]]) : q * participantPoints[aIdx[i]];
                d[i, j] = Vector3.Distance(pv, targetPoints[bIdx[j]]);
                candidates.Add(d[i, j]);
            }

            candidates.Sort();
            int lo = 0, hi = candidates.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (Feasible(d, candidates[mid])) hi = mid;
                else lo = mid + 1;
            }
            float bottleneck = candidates[lo];

            int size = Mathf.Max(n, m);
            var costs = new double[size, size];
            for (int i = 0; i < size; i++) for (int j = 0; j < size; j++)
                costs[i, j] = (i < n && j < m && d[i, j] <= bottleneck + 1e-5f)
                    ? d[i, j] + 1e-9 * (i * (double)size + j)
                    : 1e7;

            var map = Hungarian(costs);
            for (int i = 0; i < n; i++)
                if (map[i] >= 0 && map[i] < m && d[i, map[i]] <= bottleneck + 1e-4f)
                    result.Add(Tuple.Create(aIdx[i], bIdx[map[i]]));
        }
        return result;
    }


    // Horn's quaternion fit: Jacobi diagonalization of a symmetric 4x4 matrix.
    static Quaternion FitRotation(List<Tuple<Vector3, Vector3>> pairs)
    {
        if (pairs.Count < 3) return Quaternion.identity;
        Vector3 pa = Vector3.zero, pb = Vector3.zero;
        foreach (var pair in pairs) { pa += pair.Item1; pb += pair.Item2; }
        pa /= pairs.Count; pb /= pairs.Count;
        float sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
        foreach (var pair in pairs)
        {
            Vector3 a = pair.Item1 - pa, b = pair.Item2 - pb;
            sxx += a.x * b.x; sxy += a.x * b.y; sxz += a.x * b.z;
            syx += a.y * b.x; syy += a.y * b.y; syz += a.y * b.z;
            szx += a.z * b.x; szy += a.z * b.y; szz += a.z * b.z;
        }
        float[,] matrix = {
            {sxx+syy+szz, syz-szy,     szx-sxz,     sxy-syx},
            {syz-szy,     sxx-syy-szz, sxy+syx,     szx+sxz},
            {szx-sxz,     sxy+syx,     -sxx+syy-szz, syz+szy},
            {sxy-syx,     szx+sxz,     syz+szy,     -sxx-syy+szz}
        };
        float[,] vectors = new float[4, 4];
        for (int i = 0; i < 4; i++) vectors[i, i] = 1f;
        for (int iteration = 0; iteration < 64; iteration++)
        {
            int p = 0, q = 1;
            float largest = 0f;
            for (int i = 0; i < 4; i++) for (int j = i + 1; j < 4; j++)
                if (Mathf.Abs(matrix[i, j]) > largest)
                {
                    largest = Mathf.Abs(matrix[i, j]); p = i; q = j;
                }
            if (largest < 1e-7f) break;
            float pp = matrix[p, p], qq = matrix[q, q], pq = matrix[p, q];
            float angle = 0.5f * Mathf.Atan2(2f * pq, qq - pp);
            float c = Mathf.Cos(angle), s = Mathf.Sin(angle);
            for (int k = 0; k < 4; k++) if (k != p && k != q)
            {
                float kp = matrix[k, p], kq = matrix[k, q];
                matrix[k, p] = matrix[p, k] = c * kp - s * kq;
                matrix[k, q] = matrix[q, k] = s * kp + c * kq;
            }
            matrix[p, p] = c * c * pp - 2f * c * s * pq + s * s * qq;
            matrix[q, q] = s * s * pp + 2f * c * s * pq + c * c * qq;
            matrix[p, q] = matrix[q, p] = 0f;
            for (int k = 0; k < 4; k++)
            {
                float vp = vectors[k, p], vq = vectors[k, q];
                vectors[k, p] = c * vp - s * vq;
                vectors[k, q] = s * vp + c * vq;
            }
        }
        int best = 0;
        for (int i = 1; i < 4; i++) if (matrix[i, i] > matrix[best, best]) best = i;
        return Quaternion.Normalize(new Quaternion(vectors[1, best], vectors[2, best],
            vectors[3, best], vectors[0, best]));
    }

    // Full similarity fit from correspondences: rotation + uniform scale + translation.
    // Horn's method gives the rotation; the scale is the ratio of the pair spreads, which
    // is exact for a similarity and needs no extra unknowns in the eigenproblem.
    struct Similarity
    {
        public Quaternion rotation;
        public float scale;
        public Vector3 translation;
    }

    static Similarity FitSimilarity(List<Tuple<Vector3, Vector3>> pairs, bool solveRotation, float scaleClamp)
    {
        var fit = new Similarity { rotation = Quaternion.identity, scale = 1f, translation = Vector3.zero };
        if (pairs == null || pairs.Count == 0) return fit;

        Vector3 a0 = Vector3.zero, b0 = Vector3.zero;
        foreach (var pair in pairs) { a0 += pair.Item1; b0 += pair.Item2; }
        a0 /= pairs.Count; b0 /= pairs.Count;

        Quaternion q = solveRotation && pairs.Count >= 3 ? FitRotation(pairs) : Quaternion.identity;
        if (q.w < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // canonical hemisphere

        // Least-squares uniform scale for the chosen rotation: <rotated a, b> / <a, a>.
        float num = 0f, den = 0f;
        foreach (var pair in pairs)
        {
            Vector3 a = pair.Item1 - a0;
            Vector3 b = pair.Item2 - b0;
            num += Vector3.Dot(q * a, b);
            den += a.sqrMagnitude;
        }
        float scale = den > 1e-12f ? num / den : 1f;
        if (float.IsNaN(scale) || float.IsInfinity(scale)) scale = 1f;
        scale = Mathf.Clamp(scale, 1f - scaleClamp, 1f + scaleClamp);
        if (scale <= 1e-6f) scale = 1f;

        fit.rotation = q;
        fit.scale = scale;
        // Translation is whatever puts the rotated+scaled participant centroid on the target's.
        fit.translation = b0 - (q * a0) * scale;
        return fit;
    }

    // Apply a similarity to a normalised participant point.
    static Vector3 Apply(Similarity fit, Vector3 v) => (fit.rotation * v) * fit.scale + fit.translation;

    // Co-alignment: fit the similarity from the current correspondences, apply it, re-solve
    // the assignment, repeat. Returns the final transform and the mapping it was scored under.
    static void CoAlign(List<BuildingPiece> ps, List<BuildingPiece> ts, Transform pRoot, Transform tRoot,
        Vector3 pc, Vector3 tc, float pu, float tu, Quaternion seed,
        Settings settings, out Similarity fit, out List<Tuple<int, int>> mappings, out int iterations)
    {
        fit = new Similarity { rotation = seed, scale = 1f, translation = Vector3.zero };

        // Work in normalised participant units, mapping onto normalised target units, so the
        // fitted scale measures a genuine structural size difference rather than the raw prefab
        // scale. Points are fed in as (participant - pc)/pu and (target - tc)/tu.
        mappings = AllAssignmentsBetween(ps, ts, pRoot, tRoot, pc, tc, pu, tu, seed, out var participantPoints, out var targetPoints);
        iterations = 0;

        for (int pass = 0; pass < settings.CoAlignIterations; pass++)
        {
            iterations = pass + 1;
            if (mappings.Count < 2) break;

            var pairs = new List<Tuple<Vector3, Vector3>>();
            foreach (var ij in mappings)
                pairs.Add(Tuple.Create(participantPoints[ij.Item1], targetPoints[ij.Item2]));

            var candidate = FitSimilarity(pairs, settings.coAlignSolveRotation, settings.ScaleClamp);

            var next = AllAssignmentsBetween(ps, ts, pRoot, tRoot, pc, tc, pu, tu,
                candidate.rotation, out participantPoints, out targetPoints,
                candidate, true);

            bool converged = next.Count == mappings.Count;
            fit = candidate;
            mappings = next;
            if (converged && pass > 0) break;
        }
    }


    // Smallest angle over the symmetries of both pieces. INFINITE when the types differ, so the
    // caller reports it as "not comparable" rather than as a 50-degree error.
    public const float IncomparableAngle = 180f + 1f;
    static float ErrorAngle(BuildingPiece t, BuildingPiece p, Quaternion tr, Quaternion pr)
    {
        if (t.pieceType != p.pieceType) return IncomparableAngle;
        var ta = new List<Quaternion> { tr };
        var pb = new List<Quaternion> { pr };
        // Offsets are assumed to be in piece-local (canonical) coordinates.
        if (t.symmetries != null)
            foreach (var s in t.symmetries) if (s != null) ta.Add(tr * s.rotationalOffsetFromCanonical);
        if (p.symmetries != null)
            foreach (var s in p.symmetries) if (s != null) pb.Add(pr * s.rotationalOffsetFromCanonical);
        float best = 180f;
        foreach (var a in ta) foreach (var b in pb) best = Mathf.Min(best, Quaternion.Angle(a, b));
        return best;
    }

    // Snap a unit direction to a canonical sign, resolving negative zero and axis ties
    // deterministically so the same physical axis always yields the same vector.
    static Vector3 Canonical(Vector3 v)
    {
        const float eps = 1e-4f;
        float ax = Mathf.Abs(v.x), ay = Mathf.Abs(v.y), az = Mathf.Abs(v.z);
        if (ax <= eps && ay <= eps && az <= eps) return Vector3.zero;
        if (ax > eps) return v.x < 0f ? -v : v;
        if (ay > eps) return v.y < 0f ? -v : v;
        return v.z < 0f ? -v : v;
    }

    static List<Vector3> Axes(List<BuildingPiece> pieces, Transform root, Vector3 centre, float unit, Quaternion q)
    {
        var axes = new List<Vector3>();
        foreach (var piece in pieces)
        {
            Vector3 v = q * ((Local(root, piece) - centre) / unit);
            if (v.sqrMagnitude < 1e-8f) continue;
            v = Canonical(v.normalized);
            if (v == Vector3.zero) continue;
            if (!axes.Any(a => Vector3.Dot(a, v) > 0.999999f)) axes.Add(v);
        }
        return axes;
    }

    static float AxisError(List<Vector3> a, List<Vector3> b)
    {
        if (a.Count == 0 || b.Count == 0 || a.Count != b.Count) return 180f;
        int n = a.Count;
        var d = new float[n, n];
        var values = new List<float>();
        for (int i = 0; i < n; i++) for (int j = 0; j < n; j++)
        {
            d[i, j] = Mathf.Acos(Mathf.Clamp(Mathf.Abs(Vector3.Dot(a[i], b[j])), -1, 1)) * Mathf.Rad2Deg;
            values.Add(d[i, j]);
        }
        values.Sort();
        int lo = 0, hi = values.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (Feasible(d, values[mid])) hi = mid;
            else lo = mid + 1;
        }
        return values[lo];
    }

    // Count attached point pairs rather than just distinct pairs of pieces.
    // NOTE: this reads attachPoints, which the participant's build may not populate the same
    // way the target does. It grades the TARGET's topology and checks the participant matches it.
    static IEnumerable<Tuple<BuildingPiece, BuildingPiece>> TargetConnections(List<BuildingPiece> pieces)
    {
        var seen = new HashSet<object>();
        foreach (var piece in pieces)
        {
            if (piece.attachPoints == null) continue;
            foreach (var ap in piece.attachPoints)
            {
                if (ap == null || ap.attachedAttachPoint == null) continue;
                var other = ap.attachedAttachPoint.owningPiece;
                if (other == null || !pieces.Contains(other)) continue;
                var tube = Tube(piece) && Connector(other) ? piece :
                    Connector(piece) && Tube(other) ? other : null;
                if (tube == null || seen.Contains(ap) || seen.Contains(ap.attachedAttachPoint)) continue;
                seen.Add(ap);
                seen.Add(ap.attachedAttachPoint);
                yield return Tuple.Create(tube, tube == piece ? other : piece);
            }
        }
    }

    // Count distinct physical attachments even when only one side records the link.
    static int AttachmentCount(BuildingPiece tube, BuildingPiece connector)
    {
        var seen = new HashSet<object>();
        int count = 0;
        foreach (var piece in new[] { tube, connector })
        {
            if (piece.attachPoints == null) continue;
            foreach (var ap in piece.attachPoints)
            {
                if (ap == null || ap.attachedAttachPoint == null ||
                    ap.attachedAttachPoint.owningPiece != (piece == tube ? connector : tube) ||
                    seen.Contains(ap) || seen.Contains(ap.attachedAttachPoint)) continue;
                seen.Add(ap);
                seen.Add(ap.attachedAttachPoint);
                count++;
            }
        }
        return count;
    }


    // Deterministic seed list. Identity is produced by forward/up, so it is not listed twice.
    static IEnumerable<Quaternion> AlignmentSeeds()
    {
        Vector3[] directions = { Vector3.right, Vector3.left, Vector3.up, Vector3.down,
            Vector3.forward, Vector3.back };
        foreach (var forward in directions)
            foreach (var up in directions)
            {
                if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.5f) continue;
                Quaternion seed = Quaternion.LookRotation(forward, up);
                yield return seed;
            }
    }

    public static Result Grade(Transform targetRoot, Transform participantRoot, Settings settings)
    {
        if (targetRoot == null || participantRoot == null)
        {
            Debug.LogError("FAST: assign both build roots.");
            return null;
        }
        if (settings == null) settings = new Settings();
        var ts = Pieces(targetRoot);
        var ps = Pieces(participantRoot);
        var r = new Result { targetCount = ts.Count, participantCount = ps.Count };
        r.targetTubes = ts.Count(Tube);
        r.targetConnectors = ts.Count(Connector);

        // Both frames need a connector centroid and a unit; without them the two builds are not
        // comparable, and reporting large pair errors would blame the participant for it.
        // Every out parameter is assigned unconditionally: '&&' short-circuits, so a
        // failed TryCentre would leave tu/pu definitely-unassigned to the compiler.
        Vector3 tc, pc;
        float tu, pu;
        bool tCentre = TryCentre(ts, targetRoot, out tc);
        bool tUnit = TryUnit(ts, targetRoot, out tu);
        bool pCentre = TryCentre(ps, participantRoot, out pc);
        bool pUnit = TryUnit(ps, participantRoot, out pu);
        bool tFramed = tCentre && tUnit;
        bool pFramed = pCentre && pUnit;
        r.unitsComparable = tFramed && pFramed;
        if (!tCentre) tc = Vector3.zero;
        if (!pCentre) pc = Vector3.zero;
        if (!tUnit) tu = 1f;
        if (!pUnit) pu = 1f;
        if (!r.unitsComparable)
        {
            Debug.LogWarning("FAST: cannot build a comparable frame - " +
                (tFramed ? "" : "target ") + (pFramed ? "" : "participant ") +
                "is missing connectors or tubes. Position, orientation and axis are not graded.");
            r.diagnosticLog.Add("[ABORT] " + (tFramed ? "target" : "participant") +
                " build has no connectors or no tubes; position/orientation/axis not graded.");
            return r;
        }
        r.targetUnit = tu;
        r.participantUnit = pu;

        Quaternion q = Quaternion.identity;
        var mappings = AllAssignments(ps, ts, participantRoot, targetRoot, pc, tc, pu, tu, q);

        // Co-alignment runs first: it fits one similarity from the correspondences so a
        // rigid offset between the two grids is absorbed. The whole-build rotation search
        // below then only has to find the seed, not the phase.
        Similarity coFit = new Similarity { rotation = q, scale = 1f, translation = Vector3.zero };
        if (settings.coAlignCorrespondences && ps.Count > 0 && ts.Count > 0)
        {
            CoAlign(ps, ts, participantRoot, targetRoot, pc, tc, pu, tu, q,
                settings, out coFit, out mappings, out int coIterations);
            r.coAlignmentApplied = true;
            r.coAlignmentIterations = coIterations;
            r.coAlignmentPairs = mappings.Count;
            r.coAlignmentRotated = settings.coAlignSolveRotation;
            r.coAlignmentScale = coFit.scale;
            r.coAlignmentRotationDegrees = Quaternion.Angle(Quaternion.identity, coFit.rotation);
            r.coAlignmentTranslation = coFit.translation;
        }

        if (settings.alignWholeBuildRotation)
        {
            // Rotation fitting remains dependent on correspondences; try multiple starts.
            foreach (var seed in AlignmentSeeds())
            {
                Quaternion candidate = seed;
                var candidateMappings = AllAssignments(ps, ts, participantRoot, targetRoot, pc, tc, pu, tu, candidate);
                for (int iter = 0; iter < 8; iter++)
                {
                    var pairs = candidateMappings.Where(x => Connector(ps[x.Item1]))
                        .Select(x => Tuple.Create((Local(participantRoot, ps[x.Item1]) - pc) / pu,
                            (Local(targetRoot, ts[x.Item2]) - tc) / tu)).ToList();
                    if (pairs.Count < 3) break;
                    candidate = FitRotation(pairs);
                    candidateMappings = AllAssignments(ps, ts, participantRoot, targetRoot, pc, tc, pu, tu, candidate);
                }
                if (BetterAlignment(candidateMappings, candidate, mappings, q, ps, ts,
                    participantRoot, targetRoot, pc, tc, pu, tu))
                {
                    q = candidate;
                    mappings = candidateMappings;
                }
            }
        }

        r.alignmentRotation = q;
        // (q * pc) is rotated first; the unit ratio then rescales into target units.
        r.alignmentTranslation = tc - (q * pc) * (tu / pu);
        foreach (var ij in mappings)
        {
            var p = ps[ij.Item1];
            var t = ts[ij.Item2];
            Vector3 tp = Local(targetRoot, t), pp = Local(participantRoot, p);
            Vector3 aligned = tc + q * ((pp - pc) / pu) * tu;
            Quaternion tr = Rotation(targetRoot, t), pr = q * Rotation(participantRoot, p);
            // Residual AFTER the fitted similarity: this is the placement error that is
            // actually the participant's, not the grid-phase offset between the two builds.
            Vector3 participantNormalised = (pp - pc) / pu;
            Vector3 scoredParticipant = r.coAlignmentApplied
                ? Apply(coFit, participantNormalised)
                : q * participantNormalised;
            float distance = Vector3.Distance((tp - tc) / tu, scoredParticipant);

            // Thirds of a unit step: within tolerance wins outright, the 2x band is reported as
            // genuinely close, beyond that there is nothing to be ambiguous about.
            const float ambiguousBand = 2f;
            var alternatives = ts.Where(x => Tube(x) == Tube(p) && x != t)
                .Select(x => Vector3.Distance((Local(targetRoot, x) - tc) / tu,
                    scoredParticipant))
                .Where(x => x <= Mathf.Max(settings.PositionTolerance, ambiguousBand))
                .OrderBy(x => x).ToList();
            float next = alternatives.Count > 0 ? alternatives[0] : float.PositiveInfinity;
            bool ambiguous = !float.IsPositiveInfinity(next) && !float.IsNaN(next) &&
                next - distance <= settings.AmbiguityThreshold;

            r.matches.Add(new Pair
            {
                target = t, participant = p,
                targetPosition = tp, originalParticipantPosition = pp, alignedParticipantPosition = aligned,
                targetRotation = tr, originalParticipantRotation = Rotation(participantRoot, p),
                alignedParticipantRotation = pr, positionError = distance,
                positionCorrect = distance <= settings.PositionTolerance,
                nextBestDistance = next,
                ambiguous = ambiguous
            });
        }

        if (r.coAlignmentApplied && r.matches.Count > 0)
        {
            r.coAlignmentResidualMax = r.matches.Max(x => x.positionError);
            r.coAlignmentResidualMean = r.matches.Average(x => x.positionError);
        }

        r.missing = ts.Count - r.matches.Count;
        r.extra = ps.Count - r.matches.Count;
        r.correctPositions = r.matches.Count(x => x.positionCorrect);
        foreach (var t in ts.Where(t => !r.matches.Any(x => x.target == t)))
            r.diagnosticLog.Add("[MISSING] " + t.name);
        foreach (var p in ps.Where(p => !r.matches.Any(x => x.participant == p)))
            r.diagnosticLog.Add("[EXTRA] " + p.name);

        // The participant axes are view-invariant; the target axes are a rigid template.
        var ta = Axes(ts, targetRoot, tc, tu, Quaternion.identity);
        var pa = Axes(ps, participantRoot, pc, pu, Quaternion.identity);
        r.targetAxisCount = ta.Count;
        r.participantAxisCount = pa.Count;
        r.axisEvaluated = ta.Count > 0 && pa.Count > 0;
        r.structuralAxisError = r.axisEvaluated ? AxisError(ta, pa) : 180f;
        r.structuralAxisCorrect = r.axisEvaluated &&
            r.structuralAxisError <= settings.AxisTolerance;
        if (r.axisEvaluated && ta.Count != pa.Count)
            r.diagnosticLog.Add($"Axis counts differ ({ta.Count} target / {pa.Count} participant): " +
                "axis error reported as 180 deg because the two sets cannot be paired.");

        foreach (var match in r.matches)
            match.colorCorrect = Tube(match.target) && Tube(match.participant) &&
                match.target.pieceColor == match.participant.pieceColor;
        r.correctColors = r.matches.Count(x => x.colorCorrect);

        foreach (var match in r.matches)
            match.connectorCorrect = Connector(match.target) && Connector(match.participant) &&
                match.target.pieceType == match.participant.pieceType;
        r.correctConnectors = r.matches.Count(x => x.connectorCorrect);

        foreach (var match in r.matches)
        {
            match.orientationError = ErrorAngle(match.target, match.participant,
                match.targetRotation, match.alignedParticipantRotation);
            match.orientationCorrect = match.orientationError <= settings.OrientationTolerance;
        }
        r.correctOrientations = r.matches.Count(x => x.orientationCorrect);

        foreach (var match in r.matches)
            r.diagnosticLog.Add($"[PAIR] target={match.target.name} ({match.target.pieceType}/{match.target.pieceColor}) " +
                $"participant={match.participant.name} ({match.participant.pieceType}/{match.participant.pieceColor}) " +
                $"posErrNormalized={match.positionError:F4} posOK={match.positionCorrect} " +
                $"rotErr={(match.orientationError > 180f ? "n/a" : match.orientationError.ToString("F2"))} " +
                $"rotOK={match.orientationCorrect} " +
                $"colorOK={match.colorCorrect} connectorOK={match.connectorCorrect} " +
                $"ambiguous={match.ambiguous} " +
                $"nextAlternative={(float.IsPositiveInfinity(match.nextBestDistance) ? "none" : match.nextBestDistance.ToString("F4"))}");

        var usedAttachments = new Dictionary<string, int>();
        foreach (var edge in TargetConnections(ts))
        {
            var tube = edge.Item1;
            var connector = edge.Item2;
            r.targetConnections++;
            var tm = r.matches.FirstOrDefault(x => x.target == tube);
            var cm = r.matches.FirstOrDefault(x => x.target == connector);
            string reason = tm == null ? "missing mapped tube" :
                cm == null ? "missing mapped connector" :
                !tm.colorCorrect ? "wrong tube color" :
                !cm.connectorCorrect ? "wrong connector type" : null;
            if (reason == null)
            {
                string key = tm.participant.GetInstanceID() + ":" + cm.participant.GetInstanceID();
                int used;
                usedAttachments.TryGetValue(key, out used);
                if (used < AttachmentCount(tm.participant, cm.participant))
                {
                    usedAttachments[key] = used + 1;
                    reason = "PASS";
                    r.correctConnections++;
                }
                else reason = "not connected";
            }
            r.connectionLog.Add($"[CONNECTION] {tube.name} <-> {connector.name}: {reason}");
        }

        return r;
    }

    static bool BetterAlignment(List<Tuple<int, int>> candidate, Quaternion cq,
        List<Tuple<int, int>> current, Quaternion q, List<BuildingPiece> ps, List<BuildingPiece> ts,
        Transform pr, Transform tr, Vector3 pc, Vector3 tc, float pu, float tu)
    {
        if (candidate.Count != current.Count) return candidate.Count > current.Count;
        float candidateMax = 0f, currentMax = 0f;
        float candidateSum = 0f, currentSum = 0f;
        foreach (var pair in candidate)
        {
            float d = Vector3.Distance(cq * ((Local(pr, ps[pair.Item1]) - pc) / pu),
                (Local(tr, ts[pair.Item2]) - tc) / tu);
            candidateMax = Mathf.Max(candidateMax, d);
            candidateSum += d;
        }
        foreach (var pair in current)
        {
            float d = Vector3.Distance(q * ((Local(pr, ps[pair.Item1]) - pc) / pu),
                (Local(tr, ts[pair.Item2]) - tc) / tu);
            currentMax = Mathf.Max(currentMax, d);
            currentSum += d;
        }
        return candidateMax < currentMax - 1e-5f ||
            (Mathf.Abs(candidateMax - currentMax) <= 1e-5f && candidateSum < currentSum - 1e-5f);
    }

    public static void Log(Result r, bool details)
    {
        if (r == null) return;
        if (!r.unitsComparable)
        {
            Debug.Log("========== FAST GRADING ==========\n" +
                "NOT GRADED: the target or the participant build has no connectors or no tubes, " +
                "so the two cannot be put in a common frame.\n" +
                $"Matched: {r.matches.Count}, Missing: {r.missing}, Extra: {r.extra}");
            if (details) foreach (var s in r.diagnosticLog) Debug.Log(s);
            return;
        }
        string coLine = r.coAlignmentApplied
            ? ($"STAGE 0: CO-ALIGNMENT\n" +
               $"Fitted from {r.coAlignmentPairs} correspondences in {r.coAlignmentIterations} pass(es): " +
               $"{(r.coAlignmentRotated ? r.coAlignmentRotationDegrees.ToString("F2") + " deg" : "rotation fixed")}, " +
               $"scale {r.coAlignmentScale:F4}\n" +
               $"Offset absorbed: ({r.coAlignmentTranslation.x:F4}, {r.coAlignmentTranslation.y:F4}, " +
               $"{r.coAlignmentTranslation.z:F4}) in target units\n" +
               $"Residual after fit: mean {r.coAlignmentResidualMean:F4}, max {r.coAlignmentResidualMax:F4} " +
               $"(tolerance 0.35 of a unit step)\n\n")
            : "STAGE 0: CO-ALIGNMENT\nno fit (too few correspondences)\n\n";

        Debug.Log("========== FAST GRADING ==========\n" +
            coLine +
            "STAGE 1: STRUCTURAL ANALYSIS\n" +
            $"Structural Position: {r.correctPositions}/{r.targetCount} ({r.PositionPercent:F2}%)\n" +
            $"Structural Axis: {(r.axisEvaluated ? r.structuralAxisError.ToString("F2") + " deg" : "n/a")} " +
            $"({(r.axisEvaluated ? (r.structuralAxisCorrect ? "CORRECT" : "INCORRECT") : "NOT EVALUATED")}; " +
            $"axes {r.targetAxisCount}/{r.participantAxisCount})\n" +
            $"Matched: {r.matches.Count}, Missing: {r.missing}, Extra: {r.extra}\n\n" +
            "STAGE 2: TUBE COLORS\n" +
            $"Tube Colors: {r.correctColors}/{r.targetTubes} ({r.ColorPercent:F2}%)\n\n" +
            "STAGE 3: CONNECTOR TYPES\n" +
            $"Connectors: {r.correctConnectors}/{r.targetConnectors} ({r.ConnectorPercent:F2}%)\n\n" +
            "STAGE 4: INDIVIDUAL ROTATION\n" +
            $"Individual Rotation: {r.correctOrientations}/{r.targetCount} ({r.OrientationPercent:F2}%)\n\n" +
            "STAGE 5: CONNECTIONS\n" +
            $"Connections: {r.correctConnections}/{r.targetConnections} ({r.ConnectionPercent:F2}%)");
        if (details)
        {
            foreach (var s in r.diagnosticLog) Debug.Log(s);
            foreach (var s in r.connectionLog) Debug.Log(s);
        }
    }
}
