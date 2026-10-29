using System;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

/// <summary>Wurzel der Anwendung: hält den Spielstand und wechselt zwischen den Bildschirmen.</summary>
public partial class Main : Control
{
    private string _savePath = "";
    private Control? _current;

    public static Main Instance { get; private set; } = null!;

    public AudioManager Audio { get; private set; } = null!;

    public SaveData Save { get; private set; } = new();

    /// <summary>Zuletzt gespielte Einstellungen im Schnellen Spiel.</summary>
    public CustomSettings LastQuick { get; set; } = CustomSettings.Easy;

    public GameSettings Settings => Save.Settings;

    public override void _Ready()
    {
        Instance = this;
        Theme = UiTheme.Create();
        _savePath = ProjectSettings.GlobalizePath("user://save.json");
        Save = SaveData.Load(_savePath);

        // Dev-Hilfe: "-- --lang=en" erzwingt eine Sprache (nur für diesen Lauf).
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--lang=")) Settings.Language = arg["--lang=".Length..];

        Audio = new AudioManager();
        AddChild(Audio);
        ApplySettings();

        // Dev-Hilfe: "godot --path game -- --level=12" startet direkt ein Level.
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--level=") && int.TryParse(arg["--level=".Length..], out int level))
            {
                StartLevel(level);
                return;
            }
        }
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg == "--quick") { ShowQuickSetup(); return; }
            if (arg == "--quickgame") { StartQuick(CustomSettings.Hard.ToParams(7)); return; }
            if (arg == "--compendium") { ShowCompendium(); return; }
            if (arg == "--campaign") { ShowCampaign(); return; }
            if (arg == "--settings") { ShowSettings(); return; }
            if (arg == "--tutorial") { ShowTutorial(); return; }
        }
        ShowHome();
    }

    /// <summary>Überträgt die Einstellungen auf Sprache, Ton, Vibration und Oberflächengröße.</summary>
    public void ApplySettings()
    {
        Loc.Init(Settings.Language);
        Audio.Volume = Settings.SfxVolume;
        Platform.VibrationEnabled = Settings.Vibration;
        GetWindow().ContentScaleFactor = (float)Math.Clamp(Settings.UiScale, 0.8, 1.6);
    }

    public void SaveAll() => Save.Save(_savePath);

    public void ShowSettings()
    {
        var screen = new SettingsScreen();
        screen.Init(this);
        Swap(screen);
    }

    public void ShowHome()
    {
        var screen = new HomeScreen();
        screen.Init(this);
        Swap(screen);
    }

    public void ShowTutorial()
    {
        var screen = new TutorialScreen();
        screen.Init(this);
        Swap(screen);
    }

    /// <summary>Kampagne; beim allerersten Mal startet vorher das Tutorial.</summary>
    public void OpenCampaign()
    {
        if (!Settings.TutorialDone && Save.Records.Count == 0) ShowTutorial();
        else ShowCampaign();
    }

    public void ShowCampaign()
    {
        var screen = new LevelSelect();
        screen.Init(this);
        Swap(screen);
    }

    public void ShowQuickSetup()
    {
        var screen = new QuickSetup();
        screen.Init(this);
        Swap(screen);
    }

    public void ShowCompendium()
    {
        var screen = new Compendium();
        screen.Init(this);
        Swap(screen);
    }

    public void StartLevel(int level)
    {
        var screen = new GameScreen();
        screen.Init(this, level);
        Swap(screen);
    }

    public void StartQuick(LevelParams p)
    {
        var screen = new GameScreen();
        screen.InitQuick(this, p);
        Swap(screen);
    }

    public void RecordWin(int level, int stars, double time)
    {
        Save.RecordWin(level, stars, time);
        Save.Save(_savePath);
    }

    private void Swap(Control screen)
    {
        _current?.QueueFree();
        _current = screen;
        AddChild(screen);
        screen.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
    }
}
