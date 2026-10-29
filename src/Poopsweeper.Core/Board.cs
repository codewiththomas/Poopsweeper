namespace Poopsweeper.Core;

public struct Cell
{
    public CellKind Kind;
    public ObstacleType Obstacle;
    /// <summary>Größe des Hindernisses in Zellen und Position dieser Zelle darin (0,0 = oben links).</summary>
    public byte ObsW, ObsH, ObsPx, ObsPy;
    public SpecialKind Special;
    public CellState State;
    /// <summary>0 = kein Haufen, 1 = Haufen, 2 = Doppelhaufen.</summary>
    public byte Poop;
    /// <summary>Gesetzte Tüten (0..2).</summary>
    public byte Flags;
    /// <summary>Fragezeichen: "vielleicht ein Haufen" – zählt nicht als Tüte.</summary>
    public bool Question;
    /// <summary>Haufen, in den getreten wurde.</summary>
    public bool Hit;
    /// <summary>Sprinkler: Zahl wurde per Hund freigelegt.</summary>
    public bool Sniffed;
    /// <summary>Pfütze: untere Grenze der angezeigten Spanne (obere = +1).</summary>
    public byte PuddleLow;
}

/// <summary>Was die Zahl einer aufgedeckten Zelle anzeigt.</summary>
public readonly record struct NumberInfo(int Min, int Max, bool Unknown)
{
    public bool IsExact => !Unknown && Min == Max;
}

public sealed class Board
{
    private static readonly (int dx, int dy)[] Dirs =
        { (-1, -1), (0, -1), (1, -1), (-1, 0), (1, 0), (-1, 1), (0, 1), (1, 1) };

    private readonly Cell[] _cells;

    public int Width { get; }
    public int Height { get; }
    public bool Populated { get; private set; }

    public Board(int width, int height)
    {
        Width = width;
        Height = height;
        _cells = new Cell[width * height];
    }

    public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

    public ref Cell At(int x, int y) => ref _cells[y * Width + x];

    public bool IsMeadow(int x, int y) => InBounds(x, y) && At(x, y).Kind == CellKind.Meadow;

    public IEnumerable<(int x, int y)> Neighbors(int x, int y)
    {
        foreach (var (dx, dy) in Dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (IsMeadow(nx, ny)) yield return (nx, ny);
        }
    }

    public IEnumerable<(int x, int y)> MeadowCells()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                if (At(x, y).Kind == CellKind.Meadow) yield return (x, y);
    }

    public int MeadowCount => MeadowCells().Count();

    /// <summary>Echte Summe der Haufen um die Zelle.</summary>
    public int Count(int x, int y)
    {
        int n = 0;
        foreach (var (nx, ny) in Neighbors(x, y)) n += At(nx, ny).Poop;
        return n;
    }

    public NumberInfo Number(int x, int y)
    {
        ref var c = ref At(x, y);
        int n = Count(x, y);
        return c.Special switch
        {
            SpecialKind.Puddle => new NumberInfo(c.PuddleLow, c.PuddleLow + 1, false),
            SpecialKind.Sprinkler when !c.Sniffed => new NumberInfo(0, 0, true),
            _ => new NumberInfo(n, n, false),
        };
    }

    public int TotalPoopValue => MeadowCells().Sum(p => At(p.x, p.y).Poop);

    public int SafeHiddenCount =>
        MeadowCells().Count(p => At(p.x, p.y).Poop == 0 && At(p.x, p.y).State == CellState.Hidden);

    /// <summary>
    /// Legt Haufen und Spezialfelder. Die angeklickte Zelle samt Nachbarn bleibt frei,
    /// damit der erste Klick immer eine Öffnung ergibt.
    /// </summary>
    public void Populate(Random rng, LevelParams p, int safeX, int safeY)
    {
        var safe = new HashSet<(int, int)> { (safeX, safeY) };
        foreach (var n in Neighbors(safeX, safeY)) safe.Add(n);

        var candidates = MeadowCells().Where(c => !safe.Contains(c)).ToList();
        Shuffle(candidates, rng);

        int meadow = MeadowCount;
        int poopCells = Math.Min(p.PlannedPoopCells(meadow), Math.Max(1, candidates.Count - 1));
        int doubles = Math.Min(p.PlannedDoubles(meadow), poopCells);

        for (int i = 0; i < poopCells; i++)
        {
            var (x, y) = candidates[i];
            At(x, y).Poop = (byte)(i < doubles ? 2 : 1);
        }

        var free = candidates.Skip(poopCells).ToList();
        int puddles = Math.Min(free.Count, (int)Math.Round(meadow * p.PuddleFraction));
        int sprinklers = Math.Min(free.Count - puddles, p.SprinklerCount);
        for (int i = 0; i < puddles; i++) At(free[i].x, free[i].y).Special = SpecialKind.Puddle;
        for (int i = puddles; i < puddles + sprinklers; i++) At(free[i].x, free[i].y).Special = SpecialKind.Sprinkler;

        // Pfützen-Spanne erst nach den Haufen festlegen.
        foreach (var (x, y) in free.Take(puddles))
        {
            int n = Count(x, y);
            int off = rng.Next(2);
            At(x, y).PuddleLow = (byte)Math.Max(0, n - off);
        }

        Populated = true;
    }

    /// <summary>Deckt auf. Gibt die Zahl neu getretener Haufen zurück.</summary>
    public int Reveal(int x, int y)
    {
        if (!IsMeadow(x, y)) return 0;
        ref var start = ref At(x, y);
        if (start.State == CellState.Revealed || start.Flags > 0) return 0;
        start.Question = false;

        if (start.Poop > 0)
        {
            start.State = CellState.Revealed;
            start.Hit = true;
            return 1;
        }

        var stack = new Stack<(int x, int y)>();
        stack.Push((x, y));
        while (stack.Count > 0)
        {
            var (cx, cy) = stack.Pop();
            ref var c = ref At(cx, cy);
            if (c.State == CellState.Revealed) continue;
            c.State = CellState.Revealed;
            c.Question = false;
            if (c.Special == SpecialKind.None && Count(cx, cy) == 0)
            {
                foreach (var (nx, ny) in Neighbors(cx, cy))
                {
                    ref var n = ref At(nx, ny);
                    if (n.State == CellState.Hidden && n.Flags == 0 && n.Poop == 0) stack.Push((nx, ny));
                }
            }
        }
        return 0;
    }

    /// <summary>Klick auf eine Zahl: deckt alle Nachbarn auf, wenn genug Tüten liegen.</summary>
    public int Chord(int x, int y)
    {
        if (!IsMeadow(x, y)) return 0;
        ref var c = ref At(x, y);
        if (c.State != CellState.Revealed) return 0;
        var info = Number(x, y);
        if (!info.IsExact || info.Min == 0) return 0;

        int marked = 0;
        foreach (var (nx, ny) in Neighbors(x, y))
        {
            ref var n = ref At(nx, ny);
            marked += n.Hit ? n.Poop : n.Flags;
        }
        if (marked != info.Min) return 0;

        int hits = 0;
        foreach (var (nx, ny) in Neighbors(x, y).ToList()) hits += Reveal(nx, ny);
        return hits;
    }

    /// <summary>Markierung wechseln: leer → Tüte → (2. Tüte bei Doppelhaufen-Levels) → Fragezeichen → leer.</summary>
    public bool CycleFlag(int x, int y, bool allowDouble)
    {
        if (!IsMeadow(x, y)) return false;
        ref var c = ref At(x, y);
        if (c.State != CellState.Hidden) return false;
        int max = allowDouble ? 2 : 1;
        if (c.Question)
        {
            c.Question = false;
        }
        else if (c.Flags >= max)
        {
            c.Flags = 0;
            c.Question = true;
        }
        else
        {
            c.Flags++;
        }
        return true;
    }

    public void RevealAllPoop()
    {
        foreach (var (x, y) in MeadowCells())
        {
            ref var c = ref At(x, y);
            if (c.Poop > 0 && c.State == CellState.Hidden) c.State = CellState.Revealed;
        }
    }

    private static void Shuffle<T>(IList<T> list, Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
