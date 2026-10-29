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

    public event Action? Changed;

    public GameSession(LevelParams p)
    {
        Params = p;
        Board = LevelGenerator.Generate(p);
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
        EnsureStarted(x, y);

        int hits = Board.At(x, y).State == CellState.Revealed ? Board.Chord(x, y) : Board.Reveal(x, y);
        Lives -= hits;
        Finish();
    }

    /// <summary>Rechtsklick/Langdruck: Tüte setzen bzw. wieder entfernen.</summary>
    public void ToggleFlag(int x, int y)
    {
        if (State is GameState.Won or GameState.Lost) return;
        if (Board.CycleFlag(x, y, AllowDoubleFlags)) Changed?.Invoke();
    }

    /// <summary>Schnüffelhund: Haufen → wird eingetütet, sicheres Feld → wird aufgedeckt, Sprinkler → Zahl sichtbar.</summary>
    public bool Sniff(int x, int y)
    {
        if (DogCharges <= 0 || !CanAct(x, y)) return false;
        ref var c = ref Board.At(x, y);
        bool sniffableSprinkler = c.State == CellState.Revealed && c.Special == SpecialKind.Sprinkler && !c.Sniffed;
        if (c.State == CellState.Revealed && !sniffableSprinkler) return false;

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
        Board.Populate(_populateRng, Params, x, y);
        State = GameState.Playing;
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
