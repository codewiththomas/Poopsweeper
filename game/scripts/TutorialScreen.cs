using System.Collections.Generic;
using System.Linq;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

/// <summary>Geführtes Tutorial auf einem festen 1-2-1-Lehrfeld (siehe <see cref="Tutorial"/>).</summary>
public partial class TutorialScreen : Control
{
    private Main _main = null!;
    private GameSession _session = null!;
    private BoardView _board = null!;
    private Label _text = null!;
    private Button _next = null!, _digButton = null!, _bagButton = null!;
    private Tween? _pulse;
    private int _step;
    private bool _finished;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var bg = new ColorRect { Color = UiTheme.Background };
        AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 12);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var head = new HBoxContainer();
        box.AddChild(head);
        var title = new Label { Text = Loc.T("tut.title"), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 40);
        title.AddThemeColorOverride("font_color", Icons.PoopLight);
        head.AddChild(title);
        var skip = new Button { Text = Loc.T("tut.skip"), FocusMode = FocusModeEnum.None };
        skip.Pressed += () =>
        {
            _main.Audio.Play("click");
            Finish();
        };
        head.AddChild(skip);

        var panel = new PanelContainer();
        box.AddChild(panel);
        var pm = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            pm.AddThemeConstantOverride($"margin_{side}", 10);
        panel.AddChild(pm);
        var pv = new VBoxContainer();
        pv.AddThemeConstantOverride("separation", 8);
        pm.AddChild(pv);
        _text = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(0, 84) };
        _text.AddThemeFontSizeOverride("font_size", 24);
        pv.AddChild(_text);
        _next = new Button { Text = Loc.T("tut.next"), FocusMode = FocusModeEnum.None, SizeFlagsHorizontal = SizeFlags.ShrinkEnd };
        _next.Pressed += () =>
        {
            _main.Audio.Play("click");
            ShowStep(_step + 1);
        };
        pv.AddChild(_next);

        _board = new BoardView { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(_board);

        var bar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 64) };
        bar.AddThemeConstantOverride("separation", 8);
        box.AddChild(bar);
        var group = new ButtonGroup();
        _digButton = ToolButton(Loc.T("game.dig"), group, Tool.Dig);
        _bagButton = ToolButton(Loc.T("game.flag"), group, Tool.Flag);
        _digButton.ButtonPressed = true;
        bar.AddChild(_digButton);
        bar.AddChild(_bagButton);

        _session = Tutorial.CreateSession();
        _session.Changed += OnChanged;
        _board.SetSession(_session);
        _board.Guard = Guard;
        ShowStep(0);
    }

    private Button ToolButton(string text, ButtonGroup group, Tool tool)
    {
        var b = new Button { Text = text, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None };
        b.Toggled += on =>
        {
            if (on) _board.CurrentTool = tool;
        };
        return b;
    }

    private TutorialStep Current => Tutorial.Steps[_step];

    private void ShowStep(int index)
    {
        if (index >= Tutorial.Steps.Length) return;
        _step = index;
        var step = Current;
        _text.Text = Loc.T(step.TextKey);
        _next.Visible = step.Kind == TutorialKind.Info;
        _board.Highlights = step.Targets.Select(t => new Vector2I(t.x, t.y)).ToList();
        _board.QueueRedraw();

        // passendes Werkzeug vorwählen
        if (step.Kind == TutorialKind.Flag) _bagButton.ButtonPressed = true;
        else _digButton.ButtonPressed = true;
    }

    /// <summary>Erlaubt nur die Aktion, die der aktuelle Schritt verlangt.</summary>
    private bool Guard(GuardAction action, Vector2I cell)
    {
        var step = Current;
        bool onTarget = step.Targets.Any(t => t.x == cell.X && t.y == cell.Y);
        bool ok = step.Kind switch
        {
            TutorialKind.Dig or TutorialKind.Chord => action == GuardAction.Dig && onTarget,
            TutorialKind.Flag => action == GuardAction.Flag && onTarget,
            TutorialKind.Finish => action == GuardAction.Dig && _session.Board.IsMeadow(cell.X, cell.Y)
                                   && _session.Board.At(cell.X, cell.Y).Poop == 0,
            _ => false,
        };
        if (!ok) Nudge();
        return ok;
    }

    /// <summary>Lässt den Text kurz aufleuchten, wenn der Spieler etwas anderes antippt.</summary>
    private void Nudge()
    {
        _pulse?.Kill();
        _text.Modulate = new Color(1f, 0.55f, 0.45f);
        _pulse = CreateTween();
        _pulse.TweenProperty(_text, "modulate", Colors.White, 0.5);
    }

    private void OnChanged()
    {
        if (_finished) return;
        if (Tutorial.IsDone(Current, _session))
        {
            if (_step + 1 < Tutorial.Steps.Length) ShowStep(_step + 1);
            else Complete();
        }
        // Falls der letzte Schritt nicht über das Ziel-Feld, sondern über Gewinn endet
        else if (_session.State == GameState.Won) Complete();
    }

    private void Complete()
    {
        _finished = true;
        _board.Highlights = new List<Vector2I>();
        _main.Audio.Play("win");
        Dialogs.Show(this, Loc.T("tut.done.title"), Loc.T("tut.done.body"), 3, new (string, System.Action)[]
        {
            (Loc.T("tut.done.go"), Finish),
        });
    }

    private void Finish()
    {
        _finished = true;
        _main.Settings.TutorialDone = true;
        _main.SaveAll();
        _main.ShowCampaign();
    }
}
