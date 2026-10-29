namespace Poopsweeper.Core;

/// <summary>Ein laufendes Level: Leben, Tüten, Hund, Zeit, Sieg/Niederlage.</summary>
public sealed class GameSession
{
    private readonly Random _populateRng;

    public LevelParams Params { get; }
    public Board Board { get; }
    public GameState State { get; private set; } = GameState.Ready;
    public int Lives { get; private set; }
    public int DogCharges { get; private set; }
    public double Elapsed { get; private set; }
    public double ParSeconds { get; }

    /// <summary>Aktueller Hunde-Tipp (wird bei der nächsten Aktion gelöscht).</summary>
    public Hint? CurrentHint { get; private set; }

    public event Action? Changed;

    public GameSession(LevelParams p) : this(p, LevelGenerator.Generate(p))
    {
    }

    /// <summary>Mit vorgegebenem Feld (Tutorial). Ist es schon bevölkert, läuft das Spiel sofort.</summary>
    public GameSession(LevelParams p, Board board)
    {
        Params = p;
        Board = board;
        if (board.Populated) State = GameState.Playing;
        Lives = p.Lives;
        DogCharges = p.DogCharges;
        _populateRng = new Random(p.Seed ^ 0x5EED);
        ParSeconds = 20 + Board.MeadowCount * p.ParSecondsPerCell;
    }

    public bool AllowDoubleFlags => Params.DoubleFraction > 0;

    /// <summary>Noch benötigte Tüten (kann negativ werden, wenn zu viele liegen).</summary>
    public int PoopRemaining
    {
        get
        {
            int total = Board.Populated ? Board.TotalPoopValue : Params.PlannedPoopValue(Board.MeadowCount);
            int used = 0;
            foreach (var (x, y) in Board.MeadowCells())
            {
                ref var c = ref Board.At(x, y);
                used += c.Hit ? c.Poop : c.Flags;
            }
            return total - used;
        }
    }

    public void Tick(double dt)
    {
        if (State != GameState.Playing) return;
        Elapsed += dt;
    }

    /// <summary>Linksklick/Tap: aufdecken; auf eine aufgedeckte Zahl = Chord.</summary>
    public void Reveal(int x, int y)
    {
        if (!CanAct(x, y)) return;
        CurrentHint = null;
        EnsureStarted(x, y);

        int hits = Board.At(x, y).State == CellState.Revealed ? Board.Chord(x, y) : Board.Reveal(x, y);
        Lives -= hits;
        Finish();
    }

    /// <summary>Rechtsklick/Langdruck: Tüte setzen bzw. wieder entfernen.</summary>
    public void ToggleFlag(int x, int y)
    {
        if (State is GameState.Won or GameState.Lost) return;
        CurrentHint = null;
        if (Board.CycleFlag(x, y, AllowDoubleFlags)) Changed?.Invoke();
    }

    /// <summary>Schnüffelhund: Haufen → wird eingetütet, sicheres Feld → wird aufgedeckt, Sprinkler → Zahl sichtbar.</summary>
    public bool Sniff(int x, int y)
    {
        if (DogCharges <= 0 || !CanAct(x, y)) return false;
        ref var c = ref Board.At(x, y);
        bool sniffableSprinkler = c.State == CellState.Revealed && c.Special == SpecialKind.Sprinkler && !c.Sniffed;
        if (c.State == CellState.Revealed && !sniffableSprinkler) return false;

        CurrentHint = null;
        EnsureStarted(x, y);
        DogCharges--;
        if (sniffableSprinkler)
        {
            c.Sniffed = true;
        }
        else if (c.Poop > 0)
        {
            c.Flags = c.Poop;
            c.Question = false;
        }
        else
        {
            c.Flags = 0;
            Board.Reveal(x, y);
        }
        Finish();
        return true;
    }

    /// <summary>Hund-Tipp: zeigt ein per Logik sicheres Feld (oder einen sicheren Haufen). Kostet nur dann einen Einsatz, wenn es etwas zu zeigen gibt.</summary>
    public bool UseHint()
    {
        if (State != GameState.Playing || DogCharges <= 0) return false;
        var hint = Solver.FindHint(Board, AllowDoubleFlags);
        if (hint == null) return false;
        DogCharges--;
        CurrentHint = hint;
        Changed?.Invoke();
        return true;
    }

    /// <summary>★ geschafft, ★★ in Par-Zeit, ★★★ zusätzlich kein Leben verloren.</summary>
    public int Stars
    {
        get
        {
            if (State != GameState.Won) return 0;
            if (Elapsed > ParSeconds) return 1;
            return Lives == Params.Lives ? 3 : 2;
        }
    }

    private bool CanAct(int x, int y) =>
        State is GameState.Ready or GameState.Playing && Board.IsMeadow(x, y);

    private void EnsureStarted(int x, int y)
    {
        if (State != GameState.Ready) return;
        if (Params.NoGuess) PopulateNoGuess(x, y);
        else Board.Populate(_populateRng, Params, x, y);
        State = GameState.Playing;
    }

    private const int MaxNoGuessTries = 80;
    private const int RepairIterations = 200;

    /// <summary>
    /// Verteilt die Haufen so lange neu (deterministisch pro Level-Seed), bis der Solver das Feld ohne Raten schafft.
    /// Gelingt das nicht, wird der beste Versuch genommen.
    /// </summary>
    private void PopulateNoGuess(int x, int y)
    {
        int bestSeed = 0, bestRemaining = int.MaxValue;
        for (int attempt = 0; attempt < MaxNoGuessTries; attempt++)
        {
            int seed = unchecked(Params.Seed * 7919 + attempt * 104729 + 17);
            Board.ClearPopulation();
            Board.Populate(new Random(seed), Params, x, y);
            var report = Solver.Simulate(Board, x, y, AllowDoubleFlags);
            if (report.Solved) return;
            if (report.Remaining >= 0 && report.Remaining < bestRemaining)
            {
                bestRemaining = report.Remaining;
                bestSeed = seed;
            }
        }
        Board.ClearPopulation();
        var repairRng = new Random(bestSeed);
        Board.Populate(repairRng, Params, x, y);
        Solver.Repair(Board, x, y, AllowDoubleFlags, repairRng, RepairIterations);
    }

    private void Finish()
    {
        if (Lives <= 0)
        {
            Lives = 0;
            State = GameState.Lost;
            Board.RevealAllPoop();
        }
        else if (Board.SafeHiddenCount == 0)
        {
            State = GameState.Won;
            foreach (var (x, y) in Board.MeadowCells())
            {
                ref var c = ref Board.At(x, y);
                if (c.Poop > 0 && !c.Hit) c.Flags = c.Poop;
            }
        }
        Changed?.Invoke();
    }
}
