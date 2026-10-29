namespace Poopsweeper.Core;

/// <summary>Einstellungen für "Schnelles Spiel": drei Voreinstellungen plus freie Wahl.</summary>
public sealed record CustomSettings(
    int Width, int Height, double PoopDensity, int Obstacles, double Roughness,
    int DogCharges, double PuddleFraction, double DoubleFraction, int Sprinklers, int Lives, bool NoGuess = true)
{
    public static readonly CustomSettings Easy = new(10, 9, 0.12, 0, 0.2, 2, 0, 0, 0, 3);
    public static readonly CustomSettings Medium = new(16, 12, 0.16, 4, 0.45, 1, 0.04, 0, 0, 3);
    public static readonly CustomSettings Hard = new(26, 16, 0.21, 8, 0.7, 1, 0.06, 0.20, 3, 3, NoGuess: false);

    public LevelParams ToParams(int seed)
    {
        int trees = (Obstacles + 1) / 2;
        int benches = Obstacles / 4;
        int rest = Math.Max(0, Obstacles - trees - benches);
        return new LevelParams
        {
            Level = 0,
            Seed = seed,
            Width = Width,
            Height = Height,
            PoopDensity = PoopDensity,
            Roughness = Roughness,
            TreeCount = trees,
            BenchCount = benches,
            BushCount = (rest + 1) / 2,
            RockCount = rest / 2,
            DogCharges = DogCharges,
            PuddleFraction = PuddleFraction,
            DoubleFraction = DoubleFraction,
            SprinklerCount = Sprinklers,
            Lives = Lives,
            NoGuess = NoGuess,
        };
    }
}
