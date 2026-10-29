using Poopsweeper.Core;
using Xunit.Abstractions;

namespace Poopsweeper.Core.Tests;

public class SolverTests
{
    private readonly ITestOutputHelper _out;
    public SolverTests(ITestOutputHelper output) => _out = output;

    private static Board Meadow(int w, int h)
    {
        var b = new Board(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                b.At(x, y).Kind = CellKind.Meadow;
        return b;
    }

    [Fact]
    public void SinglePoint_FindsMine()
    {
        var b = Meadow(5, 1);
        b.At(4, 0).Poop = 1;
        b.Reveal(0, 0); // Zahl 1 an Position 3
        var d = Solver.Deduce(b, false);
        Assert.Contains((4, 0), d.Mines.Keys);
        Assert.Empty(d.Safe);
    }

    [Fact]
    public void OneTwoPattern_FindsSafeCell()
    {
        // Spalte 0 aufgedeckt: 1 / 2 ... klassisches Teilmengen-Muster in einer 3x3-Ecke
        var b = Meadow(4, 3);
        b.At(3, 0).Poop = 1;
        b.At(3, 2).Poop = 1;
        b.Reveal(0, 1);
        var d = Solver.Deduce(b, false);
        // Die Mitte der rechten Spalte (3,1) liegt zwischen beiden Haufen und ist sicher.
        Assert.Contains((3, 1), d.Safe);
    }

    [Fact]
    public void Deduce_IgnoresPlayerFlags()
    {
        var b = Meadow(5, 1);
        b.At(4, 0).Poop = 1;
        b.Reveal(0, 0);
        b.At(3, 0).State = CellState.Hidden; // Zahl-Feld wieder verdecken → keine Info
        b.At(2, 0).State = CellState.Hidden;
        b.At(2, 0).Flags = 1;
        var d = Solver.Deduce(b, false);
        Assert.DoesNotContain((4, 0), d.Mines.Keys);
    }

    [Fact]
    public void Puddle_RangeDoesNotProduceWrongDeductions()
    {
        // Für viele Seeds: alles, was der Solver als sicher meldet, ist es wirklich.
        for (int seed = 0; seed < 40; seed++)
        {
            var p = LevelCurve.For(10, seed);
            var s = new GameSession(p with { NoGuess = false });
            var (x, y) = s.Board.MeadowCells().First();
            s.Reveal(x, y);
            var d = Solver.Deduce(s.Board, s.AllowDoubleFlags);
            foreach (var (sx, sy) in d.Safe) Assert.Equal(0, s.Board.At(sx, sy).Poop);
            foreach (var kv in d.Mines) Assert.Equal(kv.Value, s.Board.At(kv.Key.x, kv.Key.y).Poop);
        }
    }

    [Fact]
    public void FindHint_ReturnsSafeCellAfterOpening()
    {
        var s = new GameSession(LevelCurve.For(2) with { DogCharges = 2 });
        var (x, y) = s.Board.MeadowCells().First();
        s.Reveal(x, y);
        var hint = Solver.FindHint(s.Board, false);
        if (hint is { } h && h.Kind == HintKind.Safe) Assert.Equal(0, s.Board.At(h.X, h.Y).Poop);
    }

    [Fact]
    public void UseHint_CostsChargeOnlyWhenHintExists()
    {
        var s = new GameSession(LevelCurve.For(2) with { DogCharges = 3, NoGuess = true });
        Assert.False(s.UseHint()); // Spiel noch nicht gestartet
        var (x, y) = s.Board.MeadowCells().First();
        s.Reveal(x, y);
        int before = s.DogCharges;
        bool used = s.UseHint();
        Assert.Equal(used ? before - 1 : before, s.DogCharges);
        if (used) Assert.NotNull(s.CurrentHint);
    }

    [Fact]
    public void NoGuess_SameSeedSameClick_SameField()
    {
        var p = LevelCurve.For(6) with { NoGuess = true };
        var a = new GameSession(p);
        var b = new GameSession(p);
        var (x, y) = a.Board.MeadowCells().First();
        a.Reveal(x, y);
        b.Reveal(x, y);
        foreach (var (cx, cy) in a.Board.MeadowCells())
            Assert.Equal(a.Board.At(cx, cy).Poop, b.Board.At(cx, cy).Poop);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    [InlineData(8)]
    [InlineData(10)]
    [InlineData(12)]
    public void CampaignLevels_UpToLimit_AreGuessFree(int level)
    {
        Assert.True(level <= LevelCurve.NoGuessUntilLevel);
        for (int seed = 0; seed < 20; seed++)
        {
            var s = new GameSession(LevelCurve.For(level, seed));
            Assert.True(s.Params.NoGuess);
            var (x, y) = s.Board.MeadowCells().ElementAt(s.Board.MeadowCount / 2);
            s.Reveal(x, y);
            var rep = Solver.Simulate(s.Board, x, y, s.AllowDoubleFlags);
            Assert.True(rep.Solved, $"Level {level}, Seed {seed}: {rep.Remaining} Felder nur mit Raten");
        }
    }

    [Fact]
    public void LaterLevels_DoNotForceNoGuess()
    {
        Assert.False(LevelCurve.For(LevelCurve.NoGuessUntilLevel + 1).NoGuess);
    }
}

public class TutorialTests
{
    [Fact]
    public void ScriptedSolution_WinsAndEachStepCompletes()
    {
        var s = Tutorial.CreateSession();
        var steps = Tutorial.Steps;

        s.Reveal(1, 2);
        Assert.True(Tutorial.IsDone(steps[0], s));
        Assert.Equal(1, s.Board.Number(3, 1).Min);
        Assert.Equal(2, s.Board.Number(3, 2).Min);
        Assert.Equal(1, s.Board.Number(3, 3).Min);

        s.Reveal(4, 2);
        Assert.True(Tutorial.IsDone(steps[2], s));
        Assert.False(Tutorial.IsDone(steps[3], s));

        s.ToggleFlag(4, 1);
        s.ToggleFlag(4, 3);
        Assert.True(Tutorial.IsDone(steps[3], s));

        Assert.False(Tutorial.IsDone(steps[4], s));
        s.Reveal(4, 2); // Chord
        Assert.True(Tutorial.IsDone(steps[4], s));
        Assert.Equal(3, s.Lives);

        s.Reveal(6, 2);
        Assert.True(Tutorial.IsDone(steps[5], s));
        Assert.Equal(GameState.Won, s.State);
    }

    [Fact]
    public void Solver_FindsTheOneTwoOneDeduction()
    {
        var s = Tutorial.CreateSession();
        s.Reveal(1, 2);
        var d = Solver.Deduce(s.Board, false);
        Assert.Contains((4, 2), d.Safe);
        Assert.Contains((4, 1), d.Mines.Keys);
        Assert.Contains((4, 3), d.Mines.Keys);
    }
}
