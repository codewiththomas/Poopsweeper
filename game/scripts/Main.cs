using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

/// <summary>Wurzel der Anwendung: hält den Spielstand und wechselt zwischen Levelauswahl und Spiel.</summary>
public partial class Main : Control
{
    private string _savePath = "";
    private Control? _current;

    public SaveData Save { get; private set; } = new();

    public override void _Ready()
    {
        Theme = UiTheme.Create();
        _savePath = ProjectSettings.GlobalizePath("user://save.json");
        Save = SaveData.Load(_savePath);
        // Dev-Hilfe: "godot --path game -- --level=12" startet direkt ein Level.
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--level=") && int.TryParse(arg["--level=".Length..], out int level))
            {
                StartLevel(level);
                return;
            }
        }
        ShowMenu();
    }

    public void ShowMenu()
    {
        var screen = new LevelSelect();
        screen.Init(this);
        Swap(screen);
    }

    public void StartLevel(int level)
    {
        var screen = new GameScreen();
        screen.Init(this, level);
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
