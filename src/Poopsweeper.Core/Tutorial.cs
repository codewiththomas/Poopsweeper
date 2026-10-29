namespace Poopsweeper.Core;

public enum TutorialKind { Info, Dig, Flag, Chord, Finish }

/// <summary>Ein Schritt des geführten Tutorials. Targets werden hervorgehoben.</summary>
public sealed record TutorialStep(
    string TextKey, TutorialKind Kind, (int x, int y)[] Targets, (int x, int y)[]? DoneCells = null);

/// <summary>
/// Festes Lehrfeld (8x5, Wiese 6x3) mit dem klassischen 1-2-1-Muster:
/// Spalte 3 zeigt 1-2-1, daraus folgt "Mitte sicher, beide Ränder Haufen".
/// </summary>
public static class Tutorial
{
    public static readonly TutorialStep[] Steps =
    {
        new("tut.s1", TutorialKind.Dig, new[] { (1, 2) }),
        new("tut.s2", TutorialKind.Info, new[] { (4, 2) }),
        new("tut.s3", TutorialKind.Dig, new[] { (4, 2) }),
        new("tut.s4", TutorialKind.Flag, new[] { (4, 1), (4, 3) }),
        new("tut.s5", TutorialKind.Chord, new[] { (4, 2) }, new[] { (5, 1), (5, 2), (5, 3) }),
        new("tut.s6", TutorialKind.Finish, new[] { (6, 2) }),
    };

    public static GameSession CreateSession()
    {
        var board = new Board(8, 5);
        for (int y = 1; y <= 3; y++)
            for (int x = 1; x <= 6; x++)
                board.At(x, y).Kind = CellKind.Meadow;
        board.At(4, 1).Poop = 1;
        board.At(4, 3).Poop = 1;
        board.MarkPopulated();
        return new GameSession(new LevelParams { Level = 0, Width = 8, Height = 5, Lives = 3 }, board);
    }

    public static bool IsDone(TutorialStep step, GameSession s)
    {
        switch (step.Kind)
        {
            case TutorialKind.Info:
                return false; // geht per "Weiter" weiter
            case TutorialKind.Dig:
                return step.Targets.All(t => s.Board.At(t.x, t.y).State == CellState.Revealed);
            case TutorialKind.Flag:
                return step.Targets.All(t => s.Board.At(t.x, t.y).Flags > 0);
            case TutorialKind.Chord:
                return (step.DoneCells ?? step.Targets).All(t => s.Board.At(t.x, t.y).State == CellState.Revealed);
            default:
                return s.State == GameState.Won;
        }
    }
}
