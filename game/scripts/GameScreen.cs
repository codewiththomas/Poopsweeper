using System.Collections.Generic;
using System.Linq;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

public partial class GameScreen : Control
{
    private Main _main = null!;
    private int _level;
    private LevelParams? _custom; // gesetzt im Schnellen Spiel
    private bool IsQuick => _custom != null;
    private GameSession _session = null!;
    private BoardView _board = null!;
    private HudBar _hud = null!;
    private Button _digButton = null!, _flagButton = null!, _dogButton = null!, _hintButton = null!, _nextButton = null!;
    private Control? _overlay;
    private Control _toastLayer = null!;
    private PanelContainer _toast = null!;
    private Label _toastLabel = null!;
    private Tween? _toastTween;
    private bool _resultShown;
    private bool _focusLost;

    public void Init(Main main, int level)
    {
        _main = main;
        _level = level;
    }

    public void InitQuick(Main main, LevelParams p)
    {
        _main = main;
        _custom = p;
    }

    /// <summary>Zurück dorthin, woher das Spiel gestartet wurde.</summary>
    private void GoBack()
    {
        if (IsQuick) _main.ShowQuickSetup();
        else _main.ShowCampaign();
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
        BuildToast();

        StartSession();
        if (OS.GetCmdlineUserArgs().Contains("--demo")) DevDemo();
        else MaybeShowIntro();
    }

    /// <summary>Spiel pausiert, solange das Fenster/die App nicht im Vordergrund ist.</summary>
    public override void _Notification(int what)
    {
        switch ((long)what)
        {
            case NotificationApplicationFocusOut:
            case NotificationApplicationPaused:
                _focusLost = true;
                break;
            case NotificationApplicationFocusIn:
            case NotificationApplicationResumed:
                _focusLost = false;
                break;
        }
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
        _digButton = ToolButton(Loc.T("game.dig"), group, Tool.Dig);
        _flagButton = ToolButton(Loc.T("game.flag"), group, Tool.Flag);
        _dogButton = ToolButton(Loc.T("game.dog", 0), group, Tool.Dog);
        _digButton.ButtonPressed = true;
        bar.AddChild(_digButton);
        bar.AddChild(_flagButton);
        bar.AddChild(_dogButton);

        _hintButton = new Button { Text = Loc.T("game.hint"), FocusMode = FocusModeEnum.None };
        _hintButton.Pressed += UseHint;
        bar.AddChild(_hintButton);

        bar.AddChild(new Control { SizeFlagsHorizontal = SizeFlags.ExpandFill });

        _nextButton = new Button { Text = Loc.T("game.next"), Visible = false, FocusMode = FocusModeEnum.None };
        _nextButton.Pressed += () => _main.StartLevel(_level + 1);
        bar.AddChild(_nextButton);

        var restart = new Button { Text = Loc.T("game.restart"), FocusMode = FocusModeEnum.None };
        restart.Pressed += Restart;
        bar.AddChild(restart);

        var menu = new Button { Text = Loc.T("game.menu"), FocusMode = FocusModeEnum.None };
        menu.Pressed += GoBack;
        bar.AddChild(menu);
        return bar;
    }

    private Button ToolButton(string text, ButtonGroup group, Tool tool)
    {
        var b = new Button { Text = text, ToggleMode = true, ButtonGroup = group, FocusMode = FocusModeEnum.None };
        b.Toggled += on =>
        {
            if (!on) return;
            _board.CurrentTool = tool;
            _main.Audio.Play("click");
        };
        return b;
    }

    private void UseHint()
    {
        if (_session.UseHint()) _main.Audio.Play("hint");
        else if (_session.DogCharges > 0) ShowToast(Loc.T("game.nohint"));
    }

    // ---------------------------------------------------------------- Meldung (Toast)

    private void BuildToast()
    {
        _toastLayer = new Control { MouseFilter = MouseFilterEnum.Ignore };
        AddChild(_toastLayer);
        _toastLayer.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var v = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        _toastLayer.AddChild(v);
        v.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        v.AddChild(new Control { SizeFlagsVertical = SizeFlags.ExpandFill, MouseFilter = MouseFilterEnum.Ignore });

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        v.AddChild(center);
        _toast = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        center.AddChild(_toast);
        _toastLabel = new Label();
        _toast.AddChild(_toastLabel);

        v.AddChild(new Control { CustomMinimumSize = new Vector2(0, 90), MouseFilter = MouseFilterEnum.Ignore });
    }

    private void ShowToast(string text)
    {
        _toastLabel.Text = text;
        _toastTween?.Kill();
        _toast.Modulate = Colors.White;
        _toastTween = CreateTween();
        _toastTween.TweenInterval(1.8);
        _toastTween.TweenProperty(_toast, "modulate:a", 0.0, 0.5);
    }

    // ---------------------------------------------------------------- Ablauf

    private void StartSession()
    {
        _session = new GameSession(_custom ?? LevelCurve.For(_level));
        _session.Changed += OnChanged;
        _board.SetSession(_session);
        _hud.Session = _session;
        _hud.Title = IsQuick ? Loc.T("hud.quick") : null;
        _dogButton.Visible = _session.Params.DogCharges > 0;
        _hintButton.Visible = _session.Params.DogCharges > 0;
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
        if (_custom != null) _custom = _custom with { Seed = System.Random.Shared.Next() }; // frisches Feld
        StartSession();
    }

    private void OnChanged()
    {
        UpdateUi();
        if (_resultShown || _session.State is not (GameState.Won or GameState.Lost)) return;
        _resultShown = true;
        if (_session.State == GameState.Won)
        {
            if (!IsQuick)
            {
                _main.RecordWin(_level, _session.Stars, _session.Elapsed);
                _nextButton.Visible = true;
            }
            _main.Audio.Play("win");
            Platform.Vibrate(60);
        }
        else
        {
            _main.Audio.Play("lose");
        }
        ShowResult();
    }

    private void UpdateUi()
    {
        _dogButton.Text = Loc.T("game.dog", _session.DogCharges);
        _dogButton.Disabled = _session.DogCharges == 0;
        _hintButton.Disabled = _session.DogCharges == 0;
        if (_session.DogCharges == 0 && _dogButton.ButtonPressed) _digButton.ButtonPressed = true;
        _hud.QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (!_focusLost) _session.Tick(delta);
        if (_session.State == GameState.Playing) _hud.QueueRedraw();
    }

    public override void _UnhandledKeyInput(InputEvent e)
    {
        if (e is not InputEventKey { Pressed: true, Echo: false } key) return;
        switch (key.Keycode)
        {
            case Key.R: Restart(); break;
            case Key.Escape: GoBack(); break;
            case Key.Key1: _digButton.ButtonPressed = true; break;
            case Key.Key2: _flagButton.ButtonPressed = true; break;
            case Key.Key3 when !_dogButton.Disabled && _dogButton.Visible: _dogButton.ButtonPressed = true; break;
            case Key.H when _hintButton.Visible && !_hintButton.Disabled: UseHint(); break;
        }
    }

    // ---------------------------------------------------------------- Overlays

    private void MaybeShowIntro()
    {
        string key = $"intro.{_level}";
        if (IsQuick || _level <= 1 || !Loc.Has(key) || _main.Save.Records.ContainsKey(_level)) return;
        ShowPanel(Loc.T("hud.level", _level), Loc.T(key), stars: -1, new[] { (Loc.T("game.go"), (System.Action)CloseOverlay) });
    }

    private static string Clock(double seconds)
    {
        int s = (int)seconds;
        return $"{s / 60}:{s % 60:00}";
    }

    private void ShowResult()
    {
        bool won = _session.State == GameState.Won;
        string body = won
            ? Loc.T("result.wonbody", Clock(_session.Elapsed), Clock(_session.ParSeconds))
            : Loc.T("result.lostbody");

        var buttons = new List<(string, System.Action)>();
        if (won && !IsQuick) buttons.Add((Loc.T("result.next"), () => _main.StartLevel(_level + 1)));
        buttons.Add((IsQuick ? Loc.T("result.newgame") : Loc.T("result.again"), Restart));
        buttons.Add((Loc.T("result.view"), CloseOverlay));
        buttons.Add((Loc.T("common.back"), GoBack));
        ShowPanel(won ? Loc.T("result.won") : Loc.T("result.lost"), body, won ? _session.Stars : -1, buttons);
    }

    private void CloseOverlay()
    {
        _overlay?.QueueFree();
        _overlay = null;
    }

    private void ShowPanel(string title, string body, int stars, IEnumerable<(string text, System.Action action)> buttons)
    {
        CloseOverlay();
        _overlay = Dialogs.Show(this, title, body, stars, buttons);
    }
}

/// <summary>Kopfzeile: Level, Leben, Tüten, Hund, Zeit.</summary>
public partial class HudBar : Control
{
    public GameSession? Session { get; set; }
    public string? Title { get; set; }

    public override void _Draw()
    {
        if (Session == null) return;
        var s = Session;
        var font = ThemeDB.FallbackFont;
        const int fs = 28;
        float cy = Size.Y / 2f;
        float baseline = cy + (font.GetAscent(fs) - font.GetDescent(fs)) / 2f;
        float x = 8;

        string levelText = Title ?? (s.Params.IsBoss ? Loc.T("hud.boss", s.Params.Level) : Loc.T("hud.level", s.Params.Level));
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
        string parText = Loc.T("hud.par", $"{par / 60}:{par % 60:00}");
        var timeColor = s.Elapsed <= s.ParSeconds ? Colors.White : new Color(1f, 0.6f, 0.5f);

        float parW = font.GetStringSize(parText, HorizontalAlignment.Left, -1, 20).X;
        DrawString(font, new Vector2(Size.X - parW - 8, baseline), parText, HorizontalAlignment.Left, -1, 20, new Color(1, 1, 1, 0.6f));
        float timeW = font.GetStringSize(time, HorizontalAlignment.Left, -1, fs).X;
        float tx = Size.X - parW - 24 - timeW;
        DrawString(font, new Vector2(tx, baseline), time, HorizontalAlignment.Left, -1, fs, timeColor);
        Icons.Clock(this, new Vector2(tx - 26, cy), 38, timeColor);
    }
}

/// <summary>Drei Sterne, die nacheinander einfliegen; die ersten <see cref="Filled"/> leuchten.</summary>
public partial class StarRow : Control
{
    private const double FirstDelay = 0.25;
    private const double Step = 0.35;
    private const double PopTime = 0.3;

    private double _t;
    private int _sounded;

    public int Filled { get; set; }

    public override void _Process(double delta)
    {
        _t += delta;
        if (_sounded < 3 && _t >= FirstDelay + _sounded * Step)
        {
            if (_sounded < Filled) Main.Instance.Audio.Play("star", 1f + _sounded * 0.12f);
            _sounded++;
        }
        if (_t < FirstDelay + 3 * Step + PopTime) QueueRedraw();
    }

    public override void _Draw()
    {
        float r = Mathf.Min(Size.Y / 2f, 38f);
        for (int i = 0; i < 3; i++)
        {
            double local = (_t - (FirstDelay + i * Step)) / PopTime;
            float scale = local <= 0 ? 0f : (float)EaseOutBack(Mathf.Min(1.0, local));
            Icons.Star(this, new Vector2(Size.X / 2f + (i - 1) * r * 2.4f, Size.Y / 2f), r * scale, i < Filled);
        }
    }

    private static double EaseOutBack(double x)
    {
        const double c1 = 1.70158, c3 = c1 + 1;
        return 1 + c3 * System.Math.Pow(x - 1, 3) + c1 * System.Math.Pow(x - 1, 2);
    }
}
