using System;
using Godot;

namespace Poopsweeper;

/// <summary>Einstellungen: Sprache, Ton, Vibration, Farbenblind-Modus, Oberflächengröße, Zurücksetzen.</summary>
public partial class SettingsScreen : Control
{
    private Main _main = null!;
    private VBoxContainer _page = null!;
    private Control? _confirm;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var s = _main.Settings;

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

        var pad = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        pad.AddThemeConstantOverride("margin_right", 36);
        scroll.AddChild(pad);

        _page = new VBoxContainer();
        _page.AddThemeConstantOverride("separation", 18);
        pad.AddChild(_page);

        var title = new Label { Text = Loc.T("set.title"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 52);
        title.AddThemeColorOverride("font_color", Icons.PoopLight);
        _page.AddChild(title);

        // Sprache
        _page.AddChild(Heading(Loc.T("set.language")));
        var langRow = new HBoxContainer();
        langRow.AddThemeConstantOverride("separation", 10);
        _page.AddChild(langRow);
        var group = new ButtonGroup();
        foreach (var (code, label) in new[] { ("auto", Loc.T("set.lang.auto")), ("de", "Deutsch"), ("en", "English") })
        {
            var b = new Button { Text = label, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None, ButtonPressed = s.Language == code };
            string captured = code;
            b.Pressed += () =>
            {
                if (s.Language == captured) return;
                s.Language = captured;
                _main.ApplySettings();
                _main.SaveAll();
                _main.ShowSettings(); // Bildschirm in der neuen Sprache neu aufbauen
            };
            langRow.AddChild(b);
        }

        AddSlider(Loc.T("set.sfx"), 0, 1, 0.05, s.SfxVolume, v => $"{v * 100:0} %", v =>
        {
            s.SfxVolume = v;
            _main.Audio.Volume = v;
        }, onRelease: () => _main.Audio.Play("dig"));

        AddToggle(Loc.T("set.vibration"), s.Vibration, on =>
        {
            s.Vibration = on;
            Platform.VibrationEnabled = on;
            if (on) Platform.Vibrate(40);
        });
        AddToggle(Loc.T("set.colorblind"), s.ColorBlind, on => s.ColorBlind = on);

        AddSlider(Loc.T("set.uiscale"), 0.8, 1.6, 0.1, s.UiScale, v => $"{v * 100:0} %", v => s.UiScale = v,
            onRelease: () => _main.ApplySettings());

        var tutorial = new Button { Text = Loc.T("set.tutorial"), FocusMode = FocusModeEnum.None };
        tutorial.Pressed += _main.ShowTutorial;
        _page.AddChild(tutorial);

        var reset = new Button { Text = Loc.T("set.reset"), FocusMode = FocusModeEnum.None };
        reset.Pressed += ShowResetConfirm;
        _page.AddChild(reset);

        var feedback = new Button { Text = Loc.T("set.feedback"), FocusMode = FocusModeEnum.None };
        feedback.Pressed += () => Platform.OpenUrl("https://github.com/codewiththomas/Poopsweeper/issues");
        _page.AddChild(feedback);

        var about = new Label { Text = Loc.T("set.about"), HorizontalAlignment = HorizontalAlignment.Center };
        about.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.8f));
        _page.AddChild(about);

        var back = new Button { Text = Loc.T("common.back"), FocusMode = FocusModeEnum.None };
        back.Pressed += () =>
        {
            _main.SaveAll();
            _main.ShowHome();
        };
        box.AddChild(back);
    }

    private static Label Heading(string text)
    {
        var l = new Label { Text = text };
        l.AddThemeColorOverride("font_color", UiTheme.Accent);
        return l;
    }

    private void AddToggle(string text, bool on, Action<bool> set)
    {
        var cb = new CheckButton { Text = text, ButtonPressed = on, FocusMode = FocusModeEnum.None };
        cb.Toggled += v => set(v);
        _page.AddChild(cb);
    }

    private void AddSlider(string text, double min, double max, double step, double value,
        Func<double, string> format, Action<double> set, Action? onRelease = null)
    {
        var row = new VBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        _page.AddChild(row);

        var head = new HBoxContainer();
        row.AddChild(head);
        head.AddChild(new Label { Text = text, SizeFlagsHorizontal = SizeFlags.ExpandFill });
        var valueLabel = new Label { Text = format(value) };
        valueLabel.AddThemeColorOverride("font_color", UiTheme.Accent);
        head.AddChild(valueLabel);

        var slider = new HSlider
        {
            MinValue = min, MaxValue = max, Step = step, Value = value,
            CustomMinimumSize = new Vector2(0, 40),
            Scrollable = false,
            FocusMode = FocusModeEnum.None,
        };
        slider.ValueChanged += v =>
        {
            set(v);
            valueLabel.Text = format(v);
        };
        if (onRelease != null) slider.DragEnded += changed => { if (changed) onRelease(); };
        row.AddChild(slider);
    }

    private void ShowResetConfirm()
    {
        _confirm?.QueueFree();
        var overlay = new Control();
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _confirm = overlay;

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f), MouseFilter = MouseFilterEnum.Stop };
        overlay.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        overlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        center.AddChild(panel);
        var m = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            m.AddThemeConstantOverride($"margin_{side}", 18);
        panel.AddChild(m);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 14);
        m.AddChild(v);

        v.AddChild(new Label
        {
            Text = Loc.T("set.reset.confirm"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(440, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
        });

        var yes = new Button { Text = Loc.T("set.reset.yes"), FocusMode = FocusModeEnum.None };
        yes.Pressed += () =>
        {
            _main.Save.ResetProgress();
            _main.SaveAll();
            overlay.QueueFree();
            _confirm = null;
        };
        v.AddChild(yes);
        var no = new Button { Text = Loc.T("set.reset.no"), FocusMode = FocusModeEnum.None };
        no.Pressed += () =>
        {
            overlay.QueueFree();
            _confirm = null;
        };
        v.AddChild(no);
    }
}
