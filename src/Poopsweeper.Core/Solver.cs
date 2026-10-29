namespace Poopsweeper.Core;

public enum HintKind { Safe, Mine }

public readonly record struct Hint(HintKind Kind, int X, int Y);

/// <summary>Ergebnis einer Ableitung: sichere Felder und Felder mit festem Haufenwert.</summary>
public sealed record Deduction(List<(int x, int y)> Safe, Dictionary<(int x, int y), int> Mines, bool Truncated);

public sealed record SolveReport(bool Solved, int Remaining, int Steps, Board? Final = null);

/// <summary>
/// Logik-Löser. Liest nur, was ein Spieler sieht (aufgedeckte Zahlen, Pfützen-Spannen, getretene Haufen),
/// und leitet daraus per Bedingungs-Suche je Frontier-Gruppe ab, was in allen Lösungen gleich ist.
/// Tüten des Spielers werden bewusst ignoriert.
/// </summary>
public static class Solver
{
    private const int NodeLimit = 150_000;
    private const int MaxSteps = 4000;

    private sealed class Con
    {
        public int[] Vars = Array.Empty<int>(); // Variablen-Indizes dieses Aufrufs
        public int Lo, Hi;
    }

    public static Deduction Deduce(Board b, bool allowDouble, IReadOnlyDictionary<(int x, int y), int>? known = null)
    {
        int maxVal = allowDouble ? 2 : 1;
        int w = b.Width;

        var knownVal = new Dictionary<int, int>();
        if (known != null)
            foreach (var kv in known) knownVal[kv.Key.y * w + kv.Key.x] = kv.Value;

        var varIndex = new Dictionary<int, int>(); // Zell-Id → Variable
        var varCell = new List<int>();
        var cons = new List<Con>();

        foreach (var (x, y) in b.MeadowCells())
        {
            ref var c = ref b.At(x, y);
            if (c.State != CellState.Revealed || c.Hit) continue;
            var info = b.Number(x, y);
            if (info.Unknown) continue;

            int sum = 0;
            var unknown = new List<int>();
            foreach (var (nx, ny) in b.Neighbors(x, y))
            {
                int id = ny * w + nx;
                if (b.At(nx, ny).State == CellState.Revealed) sum += b.At(nx, ny).Poop;
                else if (knownVal.TryGetValue(id, out int kv)) sum += kv;
                else unknown.Add(id);
            }
            if (unknown.Count == 0) continue;

            int lo = Math.Max(0, info.Min - sum);
            int hi = Math.Min(unknown.Count * maxVal, info.Max - sum);
            if (lo > hi) continue;

            var vars = new int[unknown.Count];
            for (int i = 0; i < vars.Length; i++)
            {
                if (!varIndex.TryGetValue(unknown[i], out int vi))
                {
                    vi = varCell.Count;
                    varIndex[unknown[i]] = vi;
                    varCell.Add(unknown[i]);
                }
                vars[i] = vi;
            }
            cons.Add(new Con { Vars = vars, Lo = lo, Hi = hi });
        }

        var safe = new List<(int x, int y)>();
        var mines = new Dictionary<(int x, int y), int>();
        bool truncated = false;
        if (varCell.Count == 0) return new Deduction(safe, mines, false);

        // Zusammenhangskomponenten
        var parent = Enumerable.Range(0, varCell.Count).ToArray();
        int Find(int a)
        {
            while (parent[a] != a) a = parent[a] = parent[parent[a]];
            return a;
        }
        foreach (var con in cons)
            for (int i = 1; i < con.Vars.Length; i++) parent[Find(con.Vars[i])] = Find(con.Vars[0]);

        var compVars = new Dictionary<int, List<int>>();
        for (int v = 0; v < varCell.Count; v++)
        {
            int r = Find(v);
            if (!compVars.TryGetValue(r, out var list)) compVars[r] = list = new List<int>();
            list.Add(v);
        }
        var compCons = new Dictionary<int, List<Con>>();
        foreach (var con in cons)
        {
            int r = Find(con.Vars[0]);
            if (!compCons.TryGetValue(r, out var list)) compCons[r] = list = new List<Con>();
            list.Add(con);
        }

        foreach (var (root, vars) in compVars)
        {
            var seen = Search(vars, compCons[root], maxVal, out bool aborted);
            if (aborted) { truncated = true; continue; }
            if (seen == null) continue;
            for (int i = 0; i < vars.Count; i++)
            {
                int cell = varCell[vars[i]];
                var pos = (cell % w, cell / w);
                int mask = seen[i];
                if (mask == 1) safe.Add(pos);
                else if ((mask & (mask - 1)) == 0) mines[pos] = mask == 2 ? 1 : 2; // genau ein Wert, > 0
            }
        }
        safe.Sort((a, b2) => a.y != b2.y ? a.y.CompareTo(b2.y) : a.x.CompareTo(b2.x));
        return new Deduction(safe, mines, truncated);
    }

    /// <summary>Liefert je Variable die Bitmaske der in Lösungen vorkommenden Werte; null = keine Lösung.</summary>
    private static int[]? Search(List<int> globalVars, List<Con> cons, int maxVal, out bool aborted)
    {
        int n = globalVars.Count;
        var local = new Dictionary<int, int>();
        for (int i = 0; i < n; i++) local[globalVars[i]] = i;

        var varCons = new List<int>[n];
        for (int i = 0; i < n; i++) varCons[i] = new List<int>();
        int m = cons.Count;
        var lo = new int[m];
        var hi = new int[m];
        var rem = new int[m];
        var sum = new int[m];
        for (int ci = 0; ci < m; ci++)
        {
            lo[ci] = cons[ci].Lo;
            hi[ci] = cons[ci].Hi;
            rem[ci] = cons[ci].Vars.Length;
            foreach (int gv in cons[ci].Vars) varCons[local[gv]].Add(ci);
        }

        // BFS-Reihenfolge: benachbarte Variablen nacheinander → früheres Abschneiden
        var order = new List<int>(n);
        var queued = new bool[n];
        var conDone = new bool[m];
        for (int start = 0; start < n; start++)
        {
            if (queued[start]) continue;
            var q = new Queue<int>();
            q.Enqueue(start);
            queued[start] = true;
            while (q.Count > 0)
            {
                int v = q.Dequeue();
                order.Add(v);
                foreach (int ci in varCons[v])
                {
                    if (conDone[ci]) continue;
                    conDone[ci] = true;
                    foreach (int gv in cons[ci].Vars)
                    {
                        int lv = local[gv];
                        if (!queued[lv]) { queued[lv] = true; q.Enqueue(lv); }
                    }
                }
            }
        }

        var val = new int[n];
        var seen = new int[n];
        long nodes = 0;
        bool abort = false, done = false;
        int solutions = 0;

        void Rec(int p)
        {
            if (abort || done) return;
            if (++nodes > NodeLimit) { abort = true; return; }
            if (p == n)
            {
                solutions++;
                bool allMulti = true;
                for (int i = 0; i < n; i++)
                {
                    seen[i] |= 1 << val[i];
                    if ((seen[i] & (seen[i] - 1)) == 0) allMulti = false;
                }
                if (allMulti) done = true;
                return;
            }
            int v = order[p];
            var cs = varCons[v];
            for (int a = 0; a <= maxVal; a++)
            {
                foreach (int ci in cs) { sum[ci] += a; rem[ci]--; }
                bool ok = true;
                foreach (int ci in cs)
                    if (sum[ci] > hi[ci] || sum[ci] + rem[ci] * maxVal < lo[ci]) { ok = false; break; }
                if (ok) { val[v] = a; Rec(p + 1); }
                foreach (int ci in cs) { sum[ci] -= a; rem[ci]++; }
                if (abort || done) return;
            }
        }

        Rec(0);
        aborted = abort;
        return solutions == 0 ? null : seen;
    }

    /// <summary>
    /// Spielt das Feld wie ein perfekter Logik-Spieler ab dem ersten Klick durch.
    /// Remaining = Zahl der sicheren Felder, die ohne Raten nicht erreichbar sind.
    /// </summary>
    public static SolveReport Simulate(Board populated, int startX, int startY, bool allowDouble)
    {
        var b = populated.Clone();
        b.Reveal(startX, startY);
        var known = new Dictionary<(int x, int y), int>();
        int steps = 0;

        while (steps++ < MaxSteps && b.SafeHiddenCount > 0)
        {
            var d = Deduce(b, allowDouble, known);
            foreach (var kv in d.Mines) known[kv.Key] = kv.Value;

            int opened = 0;
            foreach (var (x, y) in d.Safe)
            {
                if (b.At(x, y).State != CellState.Hidden) continue;
                if (b.Reveal(x, y) > 0) return new SolveReport(false, -1, steps); // Logikfehler: nie erwartet
                opened++;
            }
            if (opened == 0) break;
        }
        return new SolveReport(b.SafeHiddenCount == 0, b.SafeHiddenCount, steps, b);
    }

    /// <summary>
    /// Macht ein festgefahrenes Feld lösbar (Hill-Climbing): verschiebt einen Haufen vom Rand des
    /// offenen Bereichs auf ein anderes sicheres Feld und behält die Änderung, wenn die Zahl der
    /// nicht ohne Raten erreichbaren Felder dadurch nicht steigt. Verändert <paramref name="b"/>.
    /// </summary>
    public static bool Repair(Board b, int startX, int startY, bool allowDouble, Random rng, int maxIterations)
    {
        var rep = Simulate(b, startX, startY, allowDouble);
        for (int it = 0; it < maxIterations && !rep.Solved; it++)
        {
            var f = rep.Final;
            if (f == null || rep.Remaining < 0) return false;

            var frontierMines = new List<(int x, int y)>();
            var targets = new List<(int x, int y)>();
            foreach (var (x, y) in f.MeadowCells())
            {
                ref var c = ref f.At(x, y);
                if (c.State != CellState.Hidden) continue;
                bool touchesOpen = f.Neighbors(x, y).Any(n => f.At(n.x, n.y).State == CellState.Revealed);
                if (touchesOpen && c.Poop > 0) frontierMines.Add((x, y));
                if (c.Poop == 0 && c.Special == SpecialKind.None) targets.Add((x, y));
            }
            if (frontierMines.Count == 0 || targets.Count == 0) return false;

            var from = frontierMines[rng.Next(frontierMines.Count)];
            var to = targets[rng.Next(targets.Count)];
            b.MovePoop(from, to);
            var next = Simulate(b, startX, startY, allowDouble);
            if (next.Remaining >= 0 && next.Remaining <= rep.Remaining) rep = next;
            else b.MovePoop(to, from);
        }
        return rep.Solved;
    }

    /// <summary>Ein sicheres Feld (bevorzugt) oder ein sicherer Haufen für den Tipp des Hundes.</summary>
    public static Hint? FindHint(Board b, bool allowDouble)
    {
        var d = Deduce(b, allowDouble);
        foreach (var (x, y) in d.Safe)
            if (b.At(x, y).State == CellState.Hidden) return new Hint(HintKind.Safe, x, y);
        foreach (var kv in d.Mines)
        {
            ref var c = ref b.At(kv.Key.x, kv.Key.y);
            if (c.State == CellState.Hidden && c.Flags != kv.Value) return new Hint(HintKind.Mine, kv.Key.x, kv.Key.y);
        }
        return null;
    }
}
