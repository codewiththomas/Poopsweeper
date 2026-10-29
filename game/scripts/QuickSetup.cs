using System;
using System.Collections.Generic;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

/// <summary>Schnelles Spiel: Voreinstellung wählen (einfach/mittel/schwer) oder alles selbst einstellen.</summary>
public partial class QuickSetup : Control
{
    private sealed record Row(HSlider Slider, Label Value, Func<CustomSettings, double> Get, Func<CustomSettings, double, CustomSettings> Set, Func<double, string> Format);

    private Main _main = null!;
    private CustomSettings _s = CustomSettings.Easy;
    private readonly List<Row> _rows = new();
    private readonly Dictionary<string, Button> _presetButtons = new();
    private bool _updating;
    private Label _summary = null!;
    private CheckButton _noGuess = null!;

    public void Init(Main main)
    {
        _main = main;
        _s = main.LastQuick;
    }

    public override void _Ready()
    {
        var bg = new ColorRect { Color = UiTheme.Background };
        AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 24);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 12);
        margin.AddChild(box);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        // Rechter Rand, damit Werte nicht am Scrollbalken kleben.
        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_right", 36);
        scroll.AddChild(pad);

        var page = new VBoxContainer();
        page.AddThemeConstantOverride("separation", 12);
        pad.AddChild(page);

        var title = new Label { Text = Loc.T("quick.title"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 52);
        title.AddThemeColorOverride("font_color", Icons.PoopLight);
        page.AddChild(title);

        var presets = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        presets.AddThemeConstantOverride("separation", 10);
        page.AddChild(presets);
        var group = new ButtonGroup();
        AddPreset(presets, group, Loc.T("quick.easy"), CustomSettings.Easy, "easy");
        AddPreset(presets, group, Loc.T("quick.medium"), CustomSettings.Medium, "medium");
        AddPreset(presets, group, Loc.T("quick.hard"), CustomSettings.Hard, "hard");
        var custom = new Button { Text = Loc.T("quick.custom"), ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None };
        presets.AddChild(custom);
        _presetButtons["custom"] = custom;

        var grid = new GridContainer { Columns = 3, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 10);
        page.AddChild(grid);

        AddSlider(grid, Loc.T("quick.width"), 8, LevelCurve.MaxWidth, 1, s => s.Width, (s, v) => s with { Width = (int)v }, v => $"{v:0}");
        AddSlider(grid, Loc.T("quick.height"), 8, LevelCurve.MaxHeight, 1, s => s.Height, (s, v) => s with { Height = (int)v }, v => $"{v:0}");
        AddSlider(grid, Loc.T("quick.density"), 5, 30, 1, s => s.PoopDensity * 100, (s, v) => s with { PoopDensity = v / 100 }, v => $"{v:0} %");
        AddSlider(grid, Loc.T("quick.obstacles"), 0, 14, 1, s => s.Obstacles, (s, v) => s with { Obstacles = (int)v }, v => $"{v:0}");
        AddSlider(grid, Loc.T("quick.roughness"), 0, 100, 5, s => s.Roughness * 100, (s, v) => s with { Roughness = v / 100 }, v => $"{v:0} %");
        AddSlider(grid, Loc.T("quick.dog"), 0, 6, 1, s => s.DogCharges, (s, v) => s with { DogCharges = (int)v }, v => $"{v:0}");
        AddSlider(grid, Loc.T("quick.puddles"), 0, 12, 1, s => s.PuddleFraction * 100, (s, v) => s with { PuddleFraction = v / 100 }, v => $"{v:0} %");
        AddSlider(grid, Loc.T("quick.doubles"), 0, 40, 5, s => s.DoubleFraction * 100, (s, v) => s with { DoubleFraction = v / 100 }, v => $"{v:0} %");
        AddSlider(grid, Loc.T("quick.sprinklers"), 0, 8, 1, s => s.Sprinklers, (s, v) => s with { Sprinklers = (int)v }, v => $"{v:0}");
        AddSlider(grid, Loc.T("quick.lives"), 1, 5, 1, s => s.Lives, (s, v) => s with { Lives = (int)v }, v => $"{v:0}");

        _noGuess = new CheckButton { Text = Loc.T("quick.noguess"), FocusMode = FocusModeEnum.None };
        _noGuess.Toggled += on =>
        {
            if (_updating) return;
            _s = _s with { NoGuess = on };
            _presetButtons["custom"].ButtonPressed = true;
        };
        page.AddChild(_noGuess);

        _summary = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _summary.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.8f));
        page.AddChild(_summary);

        var bar = new HBoxContainer();
        bar.AddThemeConstantOverride("separation", 12);
        box.AddChild(bar);

        var back = new Button { Text = Loc.T("common.back"), FocusMode = FocusModeEnum.None };
        back.Pressed += _main.ShowHome;
        bar.AddChild(back);
        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var start = new Button { Text = Loc.T("quick.start"), FocusMode = FocusModeEnum.None };
        start.AddThemeFontSizeOverride("font_size", 32);
        start.Pressed += () =>
        {
            _main.LastQuick = _s;
            _main.Audio.Play("click");
            _main.StartQuick(_s.ToParams(Random.Shared.Next()));
        };
        bar.AddChild(start);

        Refresh();
        SelectMatchingPreset();
    }

    private void AddPreset(Container parent, ButtonGroup group, string name, CustomSettings settings, string key)
    {
        var b = new Button { Text = name, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None };
        b.Pressed += () =>
        {
            _s = settings;
            Refresh();
        };
        parent.AddChild(b);
        _presetButtons[key] = b;
    }

    private void AddSlider(Container grid, string name, double min, double max, double step,
        Func<CustomSettings, double> get, Func<CustomSettings, double, CustomSettings> set, Func<double, string> format)
    {
        grid.AddChild(new Label { Text = name, CustomMinimumSize = new Vector2(330, 0) });
        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step,
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(260, 40),
            FocusMode = FocusModeEnum.None,
            Scrollable = false, // Mausrad scrollt die Seite statt den Regler zu verstellen
        };
        var value = new Label { CustomMinimumSize = new Vector2(90, 0), HorizontalAlignment = HorizontalAlignment.Right };
        value.AddThemeColorOverride("font_color", UiTheme.Accent);
        grid.AddChild(slider);
        grid.AddChild(value);

        var row = new Row(slider, value, get, set, format);
        _rows.Add(row);
        slider.ValueChanged += v =>
        {
            if (_updating) return;
            _s = set(_s, v);
            value.Text = format(v);
            _presetButtons["custom"].ButtonPressed = true;
            UpdateSummary();
        };
    }

    /// <summary>Schiebereglern die Werte aus _s geben, ohne "Individuell" auszulösen.</summary>
    private void Refresh()
    {
        _updating = true;
        foreach (var r in _rows)
        {
            double v = r.Get(_s);
            r.Slider.Value = v;
            r.Value.Text = r.Format(v);
        }
        _noGuess.SetPressedNoSignal(_s.NoGuess);
        _updating = false;
        UpdateSummary();
    }

    private void SelectMatchingPreset()
    {
        string name = _s == CustomSettings.Easy ? "easy"
            : _s == CustomSettings.Medium ? "medium"
            : _s == CustomSettings.Hard ? "hard" : "custom";
        _presetButtons[name].ButtonPressed = true;
    }

    private void UpdateSummary()
    {
        var p = _s.ToParams(1);
        var preview = LevelGenerator.Generate(p);
        int meadow = preview.MeadowCount;
        _summary.Text = Loc.T("quick.summary", meadow, p.PlannedPoopValue(meadow));
    }
}
