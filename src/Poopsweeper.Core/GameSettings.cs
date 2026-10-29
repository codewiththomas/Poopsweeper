namespace Poopsweeper.Core;

/// <summary>Einstellungen des Spielers (werden mit dem Spielstand gespeichert).</summary>
public sealed class GameSettings
{
    /// <summary>"auto" (Systemsprache), "de" oder "en".</summary>
    public string Language { get; set; } = "auto";

    public double SfxVolume { get; set; } = 0.8;
    public bool Vibration { get; set; } = true;
    public bool ColorBlind { get; set; }

    /// <summary>Skalierung der Oberfläche (0.8 .. 1.6).</summary>
    public double UiScale { get; set; } = 1.0;

    /// <summary>Wird einmalig gesetzt, sobald das Tutorial durchgespielt oder übersprungen wurde.</summary>
    public bool TutorialDone { get; set; }
}
