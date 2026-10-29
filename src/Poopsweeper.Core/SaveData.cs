using System.Text.Json;

namespace Poopsweeper.Core;

public sealed class LevelRecord
{
    public int Stars { get; set; }
    public double BestTime { get; set; }
}

/// <summary>Spielfortschritt und Einstellungen: freigeschaltete Level, Sterne, Bestzeiten.</summary>
public sealed class SaveData
{
    /// <summary>Format-Version des Spielstands (für spätere Migrationen).</summary>
    public int Version { get; set; } = 2;

    public Dictionary<int, LevelRecord> Records { get; set; } = new();

    public GameSettings Settings { get; set; } = new();

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

    /// <summary>Setzt Level-Fortschritt zurück, Einstellungen bleiben erhalten.</summary>
    public void ResetProgress() => Records.Clear();

    public static SaveData Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var data = JsonSerializer.Deserialize<SaveData>(File.ReadAllText(path)) ?? new SaveData();
                data.Settings ??= new GameSettings();
                data.Records ??= new Dictionary<int, LevelRecord>();
                // Spielstände vor Version 2 hatten keine Einstellungen: Defaults sind bereits gesetzt.
                data.Version = 2;
                return data;
            }
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
