using System.Text.Json;

namespace Poopsweeper.Core;

public sealed class LevelRecord
{
    public int Stars { get; set; }
    public double BestTime { get; set; }
}

/// <summary>Spielfortschritt: freigeschaltete Level, Sterne, Bestzeiten.</summary>
public sealed class SaveData
{
    public Dictionary<int, LevelRecord> Records { get; set; } = new();

    public int HighestUnlocked => Records.Count == 0 ? 1 : Records.Keys.Max() + 1;

    public bool IsUnlocked(int level) => level <= HighestUnlocked;

    public int StarsFor(int level) => Records.TryGetValue(level, out var r) ? r.Stars : 0;

    /// <summary>Trägt ein gewonnenes Level ein und behält jeweils die besten Werte.</summary>
    public void RecordWin(int level, int stars, double time)
    {
        if (!Records.TryGetValue(level, out var r))
        {
            Records[level] = new LevelRecord { Stars = stars, BestTime = time };
            return;
        }
        r.Stars = Math.Max(r.Stars, stars);
        r.BestTime = Math.Min(r.BestTime, time);
    }

    public static SaveData Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path)) ?? new SaveData();
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            // Defekter Spielstand: lieber neu anfangen als abstürzen.
        }
        return new SaveData();
    }

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
}
