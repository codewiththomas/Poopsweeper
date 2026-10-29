namespace Poopsweeper.Core;

/// <summary>
/// Erzeugt das Wiesen-Layout (ohne Haufen): ausgefranster Waldrand + feste Hindernisse.
/// Deterministisch pro Seed.
/// </summary>
public static class LevelGenerator
{
    private const int MaxAttempts = 60;

    public static Board Generate(LevelParams p)
    {
        var rng = new Random(p.Seed);
        for (int attempt = 0; attempt < MaxAttempts; attempt++)
        {
            var board = BuildForest(p, rng);
            PlaceObstacles(board, p, rng);
            if (IsConnected(board) && board.MeadowCount >= MinMeadow(p)) return board;
        }
        // Notlösung: schlichtes Rechteck mit Zaun.
        var plain = BuildForest(p with { Roughness = 0 }, rng);
        return plain;
    }

    private static int MinMeadow(LevelParams p) => Math.Max(30, p.Width * p.Height / 2);

    private static Board BuildForest(LevelParams p, Random rng)
    {
        int w = p.Width, h = p.Height;
        var board = new Board(w, h);
        int maxExtra = Math.Min((int)Math.Round(p.Roughness * 3), (Math.Min(w, h) - 6) / 3);
        maxExtra = Math.Max(0, maxExtra);

        int[] top = Walk(w, maxExtra, p.Roughness, rng);
        int[] bottom = Walk(w, maxExtra, p.Roughness, rng);
        int[] left = Walk(h, maxExtra, p.Roughness, rng);
        int[] right = Walk(h, maxExtra, p.Roughness, rng);

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool forest = y < top[x] || y >= h - bottom[x] || x < left[y] || x >= w - right[y];
                board.At(x, y).Kind = forest ? CellKind.Void : CellKind.Meadow;
            }
        }

        // Sackgassen-Zipfel (Wiesenzellen mit < 2 Nachbarn) in Wald verwandeln.
        bool changed = true;
        while (changed)
        {
            changed = false;
            foreach (var (x, y) in board.MeadowCells().ToList())
            {
                if (board.Neighbors(x, y).Count() < 2)
                {
                    board.At(x, y).Kind = CellKind.Void;
                    changed = true;
                }
            }
        }
        return board;
    }

    /// <summary>Waldtiefe entlang einer Kante als Random Walk in [1, 1+maxExtra].</summary>
    private static int[] Walk(int length, int maxExtra, double roughness, Random rng)
    {
        var depth = new int[length];
        int d = 1 + rng.Next(maxExtra + 1);
        double changeChance = 0.3 + 0.5 * roughness;
        for (int i = 0; i < length; i++)
        {
            if (rng.NextDouble() < changeChance) d += rng.Next(2) == 0 ? -1 : 1;
            d = Math.Clamp(d, 1, 1 + maxExtra);
            depth[i] = d;
        }
        return depth;
    }

    private static void PlaceObstacles(Board board, LevelParams p, Random rng)
    {
        for (int i = 0; i < p.TreeCount; i++) TryPlace(board, rng, ObstacleType.Tree, 2, 2);
        for (int i = 0; i < p.BenchCount; i++)
        {
            bool horizontal = rng.Next(2) == 0;
            TryPlace(board, rng, ObstacleType.Bench, horizontal ? 2 : 1, horizontal ? 1 : 2);
        }
        for (int i = 0; i < p.BushCount; i++) TryPlace(board, rng, ObstacleType.Bush, 1, 1);
        for (int i = 0; i < p.RockCount; i++) TryPlace(board, rng, ObstacleType.Rock, 1, 1);
    }

    private static void TryPlace(Board board, Random rng, ObstacleType type, int ow, int oh)
    {
        for (int attempt = 0; attempt < 40; attempt++)
        {
            int x = rng.Next(board.Width - ow + 1);
            int y = rng.Next(board.Height - oh + 1);
            if (!Fits(board, x, y, ow, oh)) continue;

            Stamp(board, x, y, ow, oh, type);
            if (IsConnected(board)) return;
            Stamp(board, x, y, ow, oh, ObstacleType.None); // zerschneidet die Wiese → zurück
        }
    }

    private static bool Fits(Board board, int x, int y, int ow, int oh)
    {
        for (int dy = 0; dy < oh; dy++)
            for (int dx = 0; dx < ow; dx++)
                if (board.At(x + dx, y + dy).Kind != CellKind.Meadow) return false;
        return true;
    }

    private static void Stamp(Board board, int x, int y, int ow, int oh, ObstacleType type)
    {
        for (int dy = 0; dy < oh; dy++)
        {
            for (int dx = 0; dx < ow; dx++)
            {
                ref var c = ref board.At(x + dx, y + dy);
                c.Kind = type == ObstacleType.None ? CellKind.Meadow : CellKind.Obstacle;
                c.Obstacle = type;
                bool placed = type != ObstacleType.None;
                c.ObsW = (byte)(placed ? ow : 0);
                c.ObsH = (byte)(placed ? oh : 0);
                c.ObsPx = (byte)(placed ? dx : 0);
                c.ObsPy = (byte)(placed ? dy : 0);
            }
        }
    }

    /// <summary>Alle Wiesenzellen hängen (8er-Nachbarschaft) zusammen und keine ist isoliert.</summary>
    public static bool IsConnected(Board board)
    {
        var cells = board.MeadowCells().ToList();
        if (cells.Count == 0) return false;

        var seen = new HashSet<(int, int)> { cells[0] };
        var stack = new Stack<(int x, int y)>();
        stack.Push(cells[0]);
        while (stack.Count > 0)
        {
            var (x, y) = stack.Pop();
            foreach (var n in board.Neighbors(x, y))
                if (seen.Add(n)) stack.Push(n);
        }
        return seen.Count == cells.Count;
    }
}
