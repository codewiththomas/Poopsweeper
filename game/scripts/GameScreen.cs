using System.Collections.Generic;
using System.Linq;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

public partial class GameScreen : Control
{
    private static readonly Dictionary<int, string> Intros = new()
    {
        [1] = "Willkommen auf der Hundewiese!\n\nKlick = graben. Rechtsklick (am Handy: lange drücken) = Tüte auf einen vermuteten Haufen.\n" +
              "Die Zahl sagt, wie viele Haufen in den 8 Nachbarfeldern liegen. Klick auf eine Zahl mit genug Tüten räumt die Nachbarn.\n" +
              "Tritt nicht rein – du hast 3 Leben!\nMausrad = Zoom, Ziehen = Verschieben.",
        [4] = "Hindernisse!\n\nBäume, Bänke, Büsche und Steine sind gesperrt. Sie zählen nie als Haufen, und der Waldrand ist nicht mehr gerade.",
        [6] = "Der Schnüffelhund hilft dir!\n\nWähle \"Hund\" und tippe ein verdecktes Feld: Liegt dort ein Haufen, kommt eine Tüte drauf – sonst wird das Feld aufgedeckt. Nur wenige Einsätze pro Level.",
        [8] = "Pfützen!\n\nAuf Pfützen steht eine Spanne wie \"1-2\": Die echte Zahl ist einer der beiden Werte.",
        [12] = "Doppelhaufen!\n\nEin Doppelhaufen zählt 2 in den Nachbarzahlen. Tüte mehrfach tippen setzt zwei Tüten.",
        [15] = "Sprinkler!\n\nEin aufgedeckter Sprinkler zeigt nur \"?\" – bis der Hund daran schnüffelt.",
    };

    private Main _main = null!;
    private int _level;
    private GameSession _session = null!;
    private BoardView _board = null!;
    private HudBar _hud = null!;
    private Button _digButton = null!, _flagButton = null!, _dogButton = null!, _nextButton = null!;
    private Control? _overlay;
    private bool _resultShown;

    public void Init(Main main, int level)
    {
        _main = main;
        _level = level;
    }

    public override void _Ready()
    {
        var bg = new ColorRect { Color = UiTheme.Background };
        AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 8);
        AddChild(margin);
        margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        margin.AddChild(box);

        _hud = new HudBar { CustomMinimumSize = new Vector2(0, 56) };
        box.AddChild(_hud);

        _board = new BoardView { SizeFlagsVertical = SizeFlags.ExpandFill, SizeFlagsHorizontal = SizeFlags.ExpandFill };
        box.AddChild(_board);

        box.AddChild(BuildToolbar());

        StartSession();
        if (OS.GetCmdlineUserArgs().Contains("--demo")) DevDemo();
        else MaybeShowIntro();
    }

    /// <summary>Dev-Hilfe für Screenshots: spielt ein paar Züge, damit alle Zelltypen sichtbar sind.</summary>
    private void DevDemo()
    {
        var b = _session.Board;
        var (sx, sy) = b.MeadowCells().First();
        _session.Reveal(sx, sy);
        var poops = b.MeadowCells().Where(c => b.At(c.x, c.y).Poop > 0).ToList();
        _session.ToggleFlag(poops[0].x, poops[0].y);
        _session.ToggleFlag(poops[1].x, poops[1].y);
        if (_session.DogCharges > 0) _session.Sniff(poops[2].x, poops[2].y);
        _session.Reveal(poops[3].x, poops[3].y);
        foreach (var (x, y) in b.MeadowCells().Where(c => b.At(c.x, c.y).Poop == 0).Take(400).ToList())
            if (b.At(x, y).Special != SpecialKind.None) _session.Reveal(x, y);
    }

    private Control BuildToolbar()
    {
        var bar = new HBoxContainer { CustomMinimumSize = new Vector2(0, 64) };
        bar.AddThemeConstantOverride("separation", 8);

        var group = new ButtonGroup();
        _digButton = ToolButton("Graben", group, Tool.Dig);
        _flagButton = ToolButton("Tüte", group, Tool.Flag);
        _dogButton = ToolButton("Hund", group, Tool.Dog);
        _digButton.ButtonPressed = true;
        bar.AddChild(_digButton);
        bar.AddChild(_flagButton);
        bar.AddChild(_dogButton);

        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _nextButton = new Button { Text = "Weiter", Visible = false };
        _nextButton.Pressed += () => _main.StartLevel(_level + 1);
        bar.AddChild(_nextButton);

        var restart = new Button { Text = "Neu" };
        restart.Pressed += Restart;
        bar.AddChild(restart);

        var menu = new Button { Text = "Menü" };
        menu.Pressed += _main.ShowMenu;
        bar.AddChild(menu);
        return bar;
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

    // ---------------------------------------------------------------- Ablauf

    private void StartSession()
    {
        _session = new GameSession(LevelCurve.For(_level));
        _session.Changed += OnChanged;
        _board.SetSession(_session);
        _hud.Session = _session;
        _dogButton.Visible = _session.Params.DogCharges > 0;
        _resultShown = false;
        _nextButton.Visible = false;
        _digButton.ButtonPressed = true;
        UpdateUi();
    }

    private void Restart()
    {
        _overlay?.QueueFree();
        _overlay = null;
        _session.Changed -= OnChanged;
        StartSession();
    }

    private void OnChanged()
    {
        UpdateUi();
        if (_resultShown || _session.State is not (GameState.Won or GameState.Lost)) return;
        _resultShown = true;
        if (_session.State == GameState.Won)
        {
            _main.RecordWin(_level, _session.Stars, _session.Elapsed);
            _nextButton.Visible = true;
        }
        ShowResult();
    }

    private void UpdateUi()
    {
        _dogButton.Text = $"Hund ({_session.DogCharges})";
        _dogButton.Disabled = _session.DogCharges == 0;
        if (_session.DogCharges == 0 && _dogButton.ButtonPressed) _digButton.ButtonPressed = true;
        _hud.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _session.Tick(delta);
        if (_session.State == GameState.Playing) _hud.QueueRedraw();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.R: Restart(); break;
            case Key.Escape: _main.ShowMenu(); break;
            case Key.Key1: _digButton.ButtonPressed = true; break;
            case Key.Key2: _flagButton.ButtonPressed = true; break;
            case Key.Key3 when !_dogButton.Disabled && _dogButton.Visible: _dogButton.ButtonPressed = true; break;
        }
    }

    // ---------------------------------------------------------------- Overlays

    private void MaybeShowIntro()
    {
        if (!Intros.TryGetValue(_level, out var text) || _main.Save.Records.ContainsKey(_level)) return;
        ShowPanel($"Level {_level}", text, stars: -1, new[] { ("Los geht's", (System.Action)CloseOverlay) });
    }

    private void ShowResult()
    {
        bool won = _session.State == GameState.Won;
        int secs = (int)_session.Elapsed;
        string body = won
            ? $"Zeit {secs / 60}:{secs % 60:00} (Par {(int)_session.ParSeconds / 60}:{(int)_session.ParSeconds % 60:00})"
            : "Alle Leben verbraucht. Der Hund guckt dich vorwurfsvoll an.";

        var buttons = new List<(string, System.Action)>();
        if (won) buttons.Add(("Nächstes Level", () => _main.StartLevel(_level + 1)));
        buttons.Add(("Nochmal", Restart));
        buttons.Add(("Feld ansehen", CloseOverlay));
        buttons.Add(("Menü", _main.ShowMenu));
        ShowPanel(won ? "Wiese geräumt!" : "Reingetreten!", body, won ? _session.Stars : -1, buttons);
    }

    private void CloseOverlay()
    {
        _overlay?.QueueFree();
        _overlay = null;
    }

    private void ShowPanel(string title, string body, int stars, IEnumerable<(string text, System.Action action)> buttons)
    {
        CloseOverlay();
        var overlay = new Control();
        AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = MouseFilterEnum.Stop };
        overlay.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        overlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        center.AddChild(panel);

        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 18);
        panel.AddChild(margin);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 14);
        margin.AddChild(box);

        var titleLabel = new Label { Text = title, HorizontalAlignment = HorizontalAlignment.Center };
        titleLabel.AddThemeFontSizeOverride("font_size", 42);
        titleLabel.AddThemeColorOverride("font_color", UiTheme.Accent);
        box.AddChild(titleLabel);

        if (stars >= 0) box.AddChild(new StarRow { Filled = stars, CustomMinimumSize = new Vector2(0, 80) });

        var bodyLabel = new Label
        {
            Text = body,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(440, 0),
        };
        bodyLabel.AddThemeFontSizeOverride("font_size", 22);
        box.AddChild(bodyLabel);

        foreach (var (text, action) in buttons)
        {
            var b = new Button { Text = text };
            b.Pressed += action;
            box.AddChild(b);
        }
        _overlay = overlay;
    }
}

/// <summary>Kopfzeile: Level, Leben, Tüten, Hund, Zeit.</summary>
public partial class HudBar : Control
{
    public GameSession? Session { get; set; }

    public override void _Draw()
    {
        if (Session == null) return;
        var s = Session;
        var font = ThemeDB.FallbackFont;
        const int fs = 28;
        float cy = Size.Y / 2f;
        float baseline = cy + (font.GetAscent(fs) - font.GetDescent(fs)) / 2f;
        float x = 8;

        string levelText = s.Params.IsBoss ? $"Level {s.Params.Level} (Boss)" : $"Level {s.Params.Level}";
        DrawString(font, new Vector2(x, baseline), levelText, HorizontalAlignment.Left, -1, fs, Colors.White);
        x += font.GetStringSize(levelText, HorizontalAlignment.Left, -1, fs).X + 28;

        for (int i = 0; i < s.Params.Lives; i++)
        {
            Icons.Heart(this, new Vector2(x + 18, cy), 38, i < s.Lives);
            x += 38;
        }
        x += 24;

        Icons.Bag(this, new Vector2(x + 18, cy), 40);
        string bags = s.PoopRemaining.ToString();
        DrawString(font, new Vector2(x + 42, baseline), bags, HorizontalAlignment.Left, -1, fs, Colors.White);
        x += 42 + font.GetStringSize(bags, HorizontalAlignment.Left, -1, fs).X + 24;

        if (s.Params.DogCharges > 0)
        {
            Icons.Paw(this, new Vector2(x + 18, cy), 38, new Color(1f, 0.75f, 0.3f));
            string dog = s.DogCharges.ToString();
            DrawString(font, new Vector2(x + 42, baseline), dog, HorizontalAlignment.Left, -1, fs, Colors.White);
            x += 42 + font.GetStringSize(dog, HorizontalAlignment.Left, -1, fs).X + 24;
        }

        int secs = (int)s.Elapsed;
        int par = (int)s.ParSeconds;
        string time = $"{secs / 60}:{secs % 60:00}";
        string parText = $"Par {par / 60}:{par % 60:00}";
        var timeColor = s.Elapsed <= s.ParSeconds ? Colors.White : new Color(1f, 0.6f, 0.5f);

        float parW = font.GetStringSize(parText, HorizontalAlignment.Left, -1, 20).X;
        DrawString(font, new Vector2(Size.X - parW - 8, baseline), parText, HorizontalAlignment.Left, -1, 20, new Color(1, 1, 1, 0.6f));
        float timeW = font.GetStringSize(time, HorizontalAlignment.Left, -1, fs).X;
        float tx = Size.X - parW - 24 - timeW;
        DrawString(font, new Vector2(tx, baseline), time, HorizontalAlignment.Left, -1, fs, timeColor);
        Icons.Clock(this, new Vector2(tx - 26, cy), 38, timeColor);
    }
}

/// <summary>Drei Sterne, die ersten <see cref="Filled"/> leuchten.</summary>
public partial class StarRow : Control
{
    public int Filled { get; set; }

    public override void _Draw()
    {
        float r = Mathf.Min(Size.Y / 2f, 38f);
        for (int i = 0; i < 3; i++)
            Icons.Star(this, new Vector2(Size.X / 2f + (i - 1) * r * 2.4f, Size.Y / 2f), r, i < Filled);
    }
}
