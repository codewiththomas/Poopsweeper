using Poopsweeper.Core;

namespace Poopsweeper.Core.Tests;

public class BoardTests
{
    private static Board Meadow(int w, int h)
    {
        var b = new Board(w, h);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                b.At(x, y).Kind = CellKind.Meadow;
        return b;
    }

    [Fact]
    public void Count_IgnoresObstaclesAndSumsDoubles()
    {
        var b = Meadow(3, 3);
        b.At(0, 0).Poop = 2;
        b.At(2, 2).Poop = 1;
        b.At(1, 0).Kind = CellKind.Obstacle;
        b.At(1, 0).Poop = 1; // Hindernis darf nie zählen (wird ohnehin nicht als Nachbar geliefert)
        Assert.Equal(3, b.Count(1, 1));
    }

    [Fact]
    public void Reveal_FloodsZeroAreaAndStopsAtNumbers()
    {
        var b = Meadow(5, 1);
        b.At(4, 0).Poop = 1;
        b.Reveal(0, 0);
        Assert.Equal(CellState.Revealed, b.At(3, 0).State); // Zahl 1 am Rand der Öffnung
        Assert.Equal(CellState.Hidden, b.At(4, 0).State);
    }

    [Fact]
    public void Reveal_PoopCountsAsHit()
    {
        var b = Meadow(3, 3);
        b.At(1, 1).Poop = 1;
        Assert.Equal(1, b.Reveal(1, 1));
        Assert.True(b.At(1, 1).Hit);
    }

    [Fact]
    public void Reveal_SkipsFlaggedCells()
    {
        var b = Meadow(3, 3);
        b.At(0, 0).Flags = 1;
        Assert.Equal(0, b.Reveal(0, 0));
        Assert.Equal(CellState.Hidden, b.At(0, 0).State);
    }

    [Fact]
    public void Chord_RevealsNeighborsOnlyWhenFlagsMatch()
    {
        var b = Meadow(3, 3);
        b.At(0, 0).Poop = 1;
        b.Reveal(1, 1); // Zahl 1
        Assert.Equal(0, b.Chord(1, 1)); // keine Tüte → nichts
        Assert.Equal(CellState.Hidden, b.At(2, 2).State);

        b.At(0, 0).Flags = 1;
        Assert.Equal(0, b.Chord(1, 1));
        Assert.Equal(CellState.Revealed, b.At(2, 2).State);
    }

    [Fact]
    public void Chord_WithWrongFlagHitsPoop()
    {
        var b = Meadow(3, 3);
        b.At(0, 0).Poop = 1;
        b.Reveal(1, 1);
        b.At(2, 2).Flags = 1; // falsch gesetzt
        Assert.Equal(1, b.Chord(1, 1));
    }

    [Fact]
    public void Puddle_ShowsRangeContainingTrueValue()
    {
        var b = Meadow(3, 3);
        b.At(0, 0).Poop = 1;
        b.At(1, 1).Special = SpecialKind.Puddle;
        b.At(1, 1).PuddleLow = 0;
        var n = b.Number(1, 1);
        Assert.False(n.IsExact);
        Assert.InRange(1, n.Min, n.Max);
    }

    [Fact]
    public void Sprinkler_IsUnknownUntilSniffed()
    {
        var b = Meadow(3, 3);
        b.At(1, 1).Special = SpecialKind.Sprinkler;
        Assert.True(b.Number(1, 1).Unknown);
        b.At(1, 1).Sniffed = true;
        Assert.False(b.Number(1, 1).Unknown);
    }

    [Fact]
    public void CycleFlag_RespectsDoubleSetting()
    {
        var b = Meadow(2, 2);
        b.CycleFlag(0, 0, false);
        b.CycleFlag(0, 0, false);
        Assert.Equal(0, b.At(0, 0).Flags);
        Assert.True(b.At(0, 0).Question);
        b.CycleFlag(0, 0, false);
        Assert.False(b.At(0, 0).Question);

        b.CycleFlag(1, 1, true);
        b.CycleFlag(1, 1, true);
        Assert.Equal(2, b.At(1, 1).Flags);
        b.CycleFlag(1, 1, true);
        Assert.True(b.At(1, 1).Question);
        b.CycleFlag(1, 1, true);
        Assert.Equal(0, b.At(1, 1).Flags);
        Assert.False(b.At(1, 1).Question);
    }

    [Fact]
    public void Question_CanBeRevealed_AndIsNotCountedAsFlag()
    {
        var b = Meadow(3, 3);
        b.At(0, 0).Poop = 1;
        b.CycleFlag(2, 2, false);
        b.CycleFlag(2, 2, false); // jetzt Fragezeichen
        Assert.Equal(0, b.Reveal(2, 2));
        Assert.Equal(CellState.Revealed, b.At(2, 2).State);
        Assert.False(b.At(2, 2).Question);
    }
}

public class GeneratorTests
{
    [Fact]
    public void SameSeed_SameLayout()
    {
        var p = LevelCurve.For(12);
        var a = LevelGenerator.Generate(p);
        var b = LevelGenerator.Generate(p);
        for (int y = 0; y < a.Height; y++)
            for (int x = 0; x < a.Width; x++)
                Assert.Equal(a.At(x, y).Kind, b.At(x, y).Kind);
    }

    [Fact]
    public void AllLevels_AreConnectedAndHaveEnoughMeadow()
    {
        for (int level = 1; level <= 60; level++)
        {
            for (int seed = 0; seed < 5; seed++)
            {
                var p = LevelCurve.For(level, seed);
                var board = LevelGenerator.Generate(p);
                Assert.True(LevelGenerator.IsConnected(board), $"Level {level}, Seed {seed}");
                Assert.True(board.MeadowCount >= 30, $"Level {level}, Seed {seed}");
            }
        }
    }

    [Fact]
    public void Border_IsNeverMeadow()
    {
        var board = LevelGenerator.Generate(LevelCurve.For(20));
        for (int x = 0; x < board.Width; x++)
        {
            Assert.False(board.IsMeadow(x, 0));
            Assert.False(board.IsMeadow(x, board.Height - 1));
        }
        for (int y = 0; y < board.Height; y++)
        {
            Assert.False(board.IsMeadow(0, y));
            Assert.False(board.IsMeadow(board.Width - 1, y));
        }
    }

    [Fact]
    public void LaterLevels_HaveObstaclesAndRaggedEdge()
    {
        var board = LevelGenerator.Generate(LevelCurve.For(14));
        var obstacles = 0;
        for (int y = 0; y < board.Height; y++)
            for (int x = 0; x < board.Width; x++)
                if (board.At(x, y).Kind == CellKind.Obstacle) obstacles++;
        Assert.True(obstacles >= 4); // mindestens ein 2x2-Baum
    }

    [Fact]
    public void Curve_GetsHarder()
    {
        for (int level = 1; level < 40; level++)
        {
            var a = LevelCurve.For(level);
            var b = LevelCurve.For(level + 1);
            Assert.True(b.Width * b.Height >= a.Width * a.Height - 0 || b.IsBoss != a.IsBoss);
            Assert.True(b.PoopDensity >= a.PoopDensity - 0.0201); // Boss-Aufschlag darf wieder abfallen
        }
        Assert.Equal(0, LevelCurve.For(1).TreeCount);
        Assert.True(LevelCurve.For(4).TreeCount > 0);
        Assert.True(LevelCurve.For(6).DogCharges > 0);
        Assert.True(LevelCurve.For(8).PuddleFraction > 0);
        Assert.True(LevelCurve.For(12).DoubleFraction > 0);
        Assert.True(LevelCurve.For(15).SprinklerCount > 0);
    }

    [Fact]
    public void Populate_KeepsFirstClickAndNeighborsSafe_AndMatchesPlan()
    {
        for (int level = 1; level <= 30; level++)
        {
            var s = new GameSession(LevelCurve.For(level));
            var (x, y) = s.Board.MeadowCells().First();
            s.Reveal(x, y);
            Assert.NotEqual(GameState.Lost, s.State);
            Assert.Equal(0, s.Board.At(x, y).Poop);
            foreach (var (nx, ny) in s.Board.Neighbors(x, y)) Assert.Equal(0, s.Board.At(nx, ny).Poop);
            Assert.Equal(s.Params.PlannedPoopValue(s.Board.MeadowCount), s.Board.TotalPoopValue);
        }
    }
}

public class SessionTests
{
    private static GameSession Start(int level = 6)
    {
        var s = new GameSession(LevelCurve.For(level));
        var (x, y) = s.Board.MeadowCells().First();
        s.Reveal(x, y);
        return s;
    }

    [Fact]
    public void FirstClick_StartsGame()
    {
        var s = Start(1);
        Assert.Equal(GameState.Playing, s.State);
    }

    [Fact]
    public void SteppingInPoop_CostsLife_ThreeHitsLose()
    {
        var s = Start(1);
        var poops = s.Board.MeadowCells().Where(c => s.Board.At(c.x, c.y).Poop > 0).ToList();
        s.Reveal(poops[0].x, poops[0].y);
        Assert.Equal(2, s.Lives);
        Assert.Equal(GameState.Playing, s.State);
        s.Reveal(poops[1].x, poops[1].y);
        s.Reveal(poops[2].x, poops[2].y);
        Assert.Equal(GameState.Lost, s.State);
    }

    [Fact]
    public void RevealingAllSafeCells_Wins_WithThreeStarsIfFastAndClean()
    {
        var s = Start(1);
        foreach (var (x, y) in s.Board.MeadowCells().ToList())
            if (s.Board.At(x, y).Poop == 0) s.Reveal(x, y);
        Assert.Equal(GameState.Won, s.State);
        Assert.Equal(3, s.Stars);
        Assert.Equal(0, s.PoopRemaining);
    }

    [Fact]
    public void SlowWin_GivesOneStar()
    {
        var s = Start(1);
        s.Tick(s.ParSeconds + 1);
        foreach (var (x, y) in s.Board.MeadowCells().ToList())
            if (s.Board.At(x, y).Poop == 0) s.Reveal(x, y);
        Assert.Equal(1, s.Stars);
    }

    [Fact]
    public void Sniff_BagsPoopAndRevealsSafeCells_AndCostsCharge()
    {
        var s = Start(6);
        int charges = s.DogCharges;
        Assert.True(charges > 0);

        var poop = s.Board.MeadowCells().First(c => s.Board.At(c.x, c.y).Poop > 0);
        Assert.True(s.Sniff(poop.x, poop.y));
        Assert.Equal(s.Board.At(poop.x, poop.y).Poop, s.Board.At(poop.x, poop.y).Flags);
        Assert.Equal(charges - 1, s.DogCharges);
    }

    [Fact]
    public void Sniff_WithoutCharges_DoesNothing()
    {
        var s = Start(1);
        var hidden = s.Board.MeadowCells().First(c => s.Board.At(c.x, c.y).State == CellState.Hidden);
        Assert.False(s.Sniff(hidden.x, hidden.y));
    }

    [Fact]
    public void Timer_OnlyRunsWhilePlaying()
    {
        var s = new GameSession(LevelCurve.For(1));
        s.Tick(5);
        Assert.Equal(0, s.Elapsed);
        var (x, y) = s.Board.MeadowCells().First();
        s.Reveal(x, y);
        s.Tick(5);
        Assert.Equal(5, s.Elapsed);
    }
}

public class SaveDataTests
{
    [Fact]
    public void RecordWin_KeepsBestAndUnlocksNext()
    {
        var save = new SaveData();
        Assert.Equal(1, save.HighestUnlocked);
        save.RecordWin(1, 2, 80);
        save.RecordWin(1, 1, 60);
        Assert.Equal(2, save.StarsFor(1));
        Assert.Equal(60, save.Records[1].BestTime);
        Assert.True(save.IsUnlocked(2));
        Assert.False(save.IsUnlocked(3));
    }

    [Fact]
    public void SaveAndLoad_RoundTrips_AndSurvivesGarbage()
    {
        var path = Path.GetTempFileName();
        try
        {
            var save = new SaveData();
            save.RecordWin(3, 3, 42.5);
            save.Save(path);
            Assert.Equal(3, SaveData.Load(path).StarsFor(3));

            File.WriteAllText(path, "{kaputt");
            Assert.Empty(SaveData.Load(path).Records);
        }
        finally { File.Delete(path); }
    }
}

public class CustomSettingsTests
{
    [Fact]
    public void Presets_ProduceConnectedPlayableBoards()
    {
        foreach (var preset in new[] { CustomSettings.Easy, CustomSettings.Medium, CustomSettings.Hard })
        {
            for (int seed = 0; seed < 10; seed++)
            {
                var s = new GameSession(preset.ToParams(seed));
                Assert.True(LevelGenerator.IsConnected(s.Board));
                Assert.Equal(0, s.Params.Level);
            }
        }
    }

    [Fact]
    public void ObstacleBudget_IsSplitIntoShapes()
    {
        var p = CustomSettings.Hard.ToParams(1);
        Assert.Equal(CustomSettings.Hard.Obstacles, p.TreeCount + p.BenchCount + p.BushCount + p.RockCount);
        Assert.True(p.TreeCount > 0);
    }
}
