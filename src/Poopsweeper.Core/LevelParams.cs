namespace Poopsweeper.Core;

/// <summary>Alles, was ein Level beschreibt. Width/Height schließen den Waldrand ein.</summary>
public sealed record LevelParams
{
    public int Level { get; init; } = 1;
    public int Seed { get; init; } = 1;
    public int Width { get; init; } = 10;
    public int Height { get; init; } = 10;

    /// <summary>Anteil der Wiesenzellen, die einen Haufen enthalten (0..1).</summary>
    public double PoopDensity { get; init; } = 0.12;

    /// <summary>Anteil der Haufen-Zellen, die Doppelhaufen sind.</summary>
    public double DoubleFraction { get; init; }

    /// <summary>Wie ausgefranst der Waldrand ist (0..1).</summary>
    public double Roughness { get; init; } = 0.2;

    public int TreeCount { get; init; }
    public int BenchCount { get; init; }
    public int BushCount { get; init; }
    public int RockCount { get; init; }

    public double PuddleFraction { get; init; }
    public int SprinklerCount { get; init; }

    public int Lives { get; init; } = 3;
    public int DogCharges { get; init; }
    public double ParSecondsPerCell { get; init; } = 0.7;
    public bool IsBoss { get; init; }

    /// <summary>Anzahl Haufen-Zellen für eine Wiese dieser Größe (lässt Platz für die sichere Startöffnung).</summary>
    public int PlannedPoopCells(int meadowCount) =>
        Math.Clamp((int)Math.Round(meadowCount * PoopDensity), 1, Math.Max(1, meadowCount - 10));

    public int PlannedDoubles(int meadowCount) =>
        (int)Math.Floor(PlannedPoopCells(meadowCount) * DoubleFraction);

    /// <summary>Gesamtwert aller Haufen (Doppelhaufen zählen 2) = Tüten, die man braucht.</summary>
    public int PlannedPoopValue(int meadowCount) =>
        PlannedPoopCells(meadowCount) + PlannedDoubles(meadowCount);
}
