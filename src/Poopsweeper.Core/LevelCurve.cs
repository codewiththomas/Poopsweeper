namespace Poopsweeper.Core;

/// <summary>Schwierigkeitskurve: Level-Nummer → LevelParams. Jedes 5. Level ist ein Bosslevel.</summary>
public static class LevelCurve
{
    /// <summary>Bis zu diesem Level garantiert der Generator Felder ohne Raten (danach klassisch, mit Hund-Tipp).</summary>
    public const int NoGuessUntilLevel = 12;

    public const int MaxWidth = 34;
    public const int MaxHeight = 22;

    public static bool IsBoss(int level) => level >= 5 && level % 5 == 0;

    public static LevelParams For(int level, int? seed = null)
    {
        level = Math.Max(1, level);
        bool boss = IsBoss(level);
        int step = level - 1;

        int width = Math.Min(MaxWidth, 10 + (int)Math.Round(step * 1.2) + (boss ? 4 : 0));
        int height = Math.Min(MaxHeight, 10 + (int)Math.Round(step * 0.8) + (boss ? 3 : 0));
        double density = Math.Min(0.22, 0.12 + 0.0035 * step) + (boss ? 0.02 : 0);

        int obstacleBudget = level < 4 ? 0 : Math.Min(width * height / 35, 1 + (level - 4) / 2);
        int trees = (obstacleBudget + 1) / 2;
        int benches = level >= 7 ? obstacleBudget / 4 : 0;
        int rest = Math.Max(0, obstacleBudget - trees - benches);

        return new LevelParams
        {
            Level = level,
            Seed = seed ?? 1000 + level * 7919,
            Width = width,
            Height = height,
            PoopDensity = density,
            Roughness = Math.Min(1.0, 0.2 + 0.03 * step),
            TreeCount = trees,
            BenchCount = benches,
            BushCount = (rest + 1) / 2,
            RockCount = rest / 2,
            DogCharges = level < 6 ? 0 : Math.Min(4, 1 + (level - 6) / 6),
            PuddleFraction = level < 8 ? 0 : Math.Min(0.10, 0.04 + 0.004 * (level - 8)),
            DoubleFraction = level < 12 ? 0 : Math.Min(0.30, 0.10 + 0.01 * (level - 12)),
            SprinklerCount = level < 15 ? 0 : Math.Min(6, 1 + (level - 15) / 3),
            Lives = 3,
            IsBoss = boss,
            NoGuess = level <= NoGuessUntilLevel,
        };
    }
}
