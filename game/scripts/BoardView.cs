using System;
using System.Collections.Generic;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

public enum Tool { Dig, Flag, Dog }

/// <summary>Was eine Eingabe auslösen würde (für das Tutorial, das nur bestimmte Aktionen erlaubt).</summary>
public enum GuardAction { Dig, Flag, Sniff }

/// <summary>Zeichnet die Wiese und übersetzt Maus-/Touch-Eingaben in Aktionen auf der GameSession.</summary>
public partial class BoardView : Control
{
    private const float MaxCell = 96f;
    private const float PanMargin = 40f;
    private const float DragThreshold = 8f;
    private const double LongPressSeconds = 0.45;

    private static readonly Color[] NumberColors =
    {
        Colors.Transparent, new(0.10f, 0.30f, 0.90f), new(0.08f, 0.48f, 0.08f), new(0.85f, 0.10f, 0.10f),
        new(0.45f, 0.10f, 0.65f), new(0.60f, 0.05f, 0.05f), new(0.00f, 0.50f, 0.50f), Colors.Black, new(0.35f, 0.35f, 0.35f),
    };

    private GameSession? _session;
    private float _cs = 40f;
    private float _minCs = 20f;
    private Vector2 _origin;
    private Vector2I _hover = new(-1, -1);
    private bool _userMoved;

    // Maus
    private bool _mouseDown, _mouseDragged, _middleDown;
    private Vector2 _mouseDownPos;

    // Touch
    private readonly Dictionary<int, Vector2> _touches = new();
    private Vector2 _touchStart;
    private bool _touchMoved, _longPressFired;
    private double _touchTime;
    private float _pinchDist;

    public Tool CurrentTool { get; set; } = Tool.Dig;

    /// <summary>Optionaler Filter: false = Aktion wird verworfen.</summary>
    public Func<GuardAction, Vector2I, bool>? Guard { get; set; }

    /// <summary>Hervorgehobene Felder (Tutorial).</summary>
    public List<Vector2I> Highlights { get; set; } = new();

    public override void _Ready()
    {
        ClipContents = true;
        Resized += () =>
        {
            if (_session == null) return;
            // Einpassen, solange der Spieler nicht selbst gezoomt/verschoben hat (Layout läuft oft in mehreren Durchgängen).
            Fit(keepZoom: _userMoved);
        };
    }

    public void SetSession(GameSession session)
    {
        if (_session != null) _session.Changed -= OnSessionChanged;
        _session = session;
        _session.Changed += OnSessionChanged;
        _touches.Clear();
        _revealAnims.Clear();
        _flagAnims.Clear();
        _particles.Clear();
        _snapshot = TakeSnapshot();
        _lastClick = new Vector2I(-1, -1);
        _userMoved = false;
        Fit(keepZoom: false);
        QueueRedraw();
    }

    // ---------------------------------------------------------------- Layout

    private void Fit(bool keepZoom)
    {
        if (_session == null) return;
        var b = _session.Board;
        float fit = Mathf.Min((Size.X - 2 * PanMargin) / b.Width, (Size.Y - 2 * PanMargin) / b.Height);
        _minCs = Mathf.Min(Mathf.Max(fit, 8f), 28f);
        if (!keepZoom) _cs = Mathf.Clamp(fit, 28f, MaxCell);
        _cs = Mathf.Clamp(_cs, _minCs, MaxCell);
        if (!keepZoom) _origin = (Size - new Vector2(b.Width, b.Height) * _cs) / 2f;
        ClampOrigin();
        QueueRedraw();
    }

    private void ClampOrigin()
    {
        if (_session == null) return;
        var b = _session.Board;
        _origin.X = ClampAxis(_origin.X, b.Width * _cs, Size.X);
        _origin.Y = ClampAxis(_origin.Y, b.Height * _cs, Size.Y);
    }

    private static float ClampAxis(float origin, float boardPx, float viewPx)
    {
        if (boardPx + 2 * PanMargin <= viewPx) return (viewPx - boardPx) / 2f;
        return Mathf.Clamp(origin, viewPx - boardPx - PanMargin, PanMargin);
    }

    private void ZoomAt(float factor, Vector2 anchor)
    {
        float newCs = Mathf.Clamp(_cs * factor, _minCs, MaxCell);
        _userMoved = true;
        _origin = anchor - (anchor - _origin) * (newCs / _cs);
        _cs = newCs;
        ClampOrigin();
        QueueRedraw();
    }

    private void Pan(Vector2 delta)
    {
        _userMoved = true;
        _origin += delta;
        ClampOrigin();
        QueueRedraw();
    }

    private Vector2I CellAt(Vector2 p)
    {
        var v = (p - _origin) / _cs;
        return new Vector2I(Mathf.FloorToInt(v.X), Mathf.FloorToInt(v.Y));
    }

    // ---------------------------------------------------------------- Effekte ("Juice")

    private const double RevealAnimTime = 0.28;
    private const double FlagAnimTime = 0.30;
    private const double ShakeTime = 0.35;

    private struct Particle
    {
        public Vector2 Pos, Vel;
        public double Born, Life;
        public Color Color;
        public float Size;
    }

    private readonly Dictionary<int, double> _revealAnims = new();
    private readonly Dictionary<int, double> _flagAnims = new();
    private readonly List<Particle> _particles = new();
    private int[] _snapshot = Array.Empty<int>();
    private Vector2I _lastClick = new(-1, -1);
    private double _shakeStart = -10;
    private Vector2 _shake;
    private readonly Random _fx = new(7);

    private static double Now => Time.GetTicksMsec() / 1000.0;

    private static int Encode(in Cell c) =>
        (c.State == CellState.Revealed ? 1 : 0) | (c.Hit ? 2 : 0) | (c.Flags << 2) | (c.Question ? 16 : 0);

    private int[] TakeSnapshot()
    {
        var b = _session!.Board;
        var snap = new int[b.Width * b.Height];
        for (int y = 0; y < b.Height; y++)
            for (int x = 0; x < b.Width; x++)
                snap[y * b.Width + x] = Encode(b.At(x, y));
        return snap;
    }

    /// <summary>Vergleicht mit dem letzten Stand und löst Animationen, Töne und Vibration aus.</summary>
    private void OnSessionChanged()
    {
        if (_session == null) return;
        var b = _session.Board;
        var now = _snapshot.Length == b.Width * b.Height ? _snapshot : TakeSnapshot();
        bool quiet = _session.State is GameState.Won or GameState.Lost;
        var audio = Main.Instance.Audio;

        int revealed = 0, flagsAdded = 0, flagsRemoved = 0;
        var hitCells = new List<(int x, int y)>();
        double t0 = Now;

        for (int y = 0; y < b.Height; y++)
        {
            for (int x = 0; x < b.Width; x++)
            {
                int id = y * b.Width + x;
                int before = now[id];
                int after = Encode(b.At(x, y));
                if (before == after) continue;

                if ((after & 1) != 0 && (before & 1) == 0)
                {
                    if ((after & 2) != 0) hitCells.Add((x, y));
                    else
                    {
                        revealed++;
                        double dist = _lastClick.X < 0 ? 0 : Math.Sqrt((x - _lastClick.X) * (x - _lastClick.X) + (y - _lastClick.Y) * (y - _lastClick.Y));
                        _revealAnims[id] = t0 + Math.Min(0.45, dist * 0.03);
                    }
                }
                if (((after >> 2) & 3) > ((before >> 2) & 3) && !quiet) { flagsAdded++; _flagAnims[id] = t0; }
                else if (((after >> 2) & 3) < ((before >> 2) & 3) && (after & 16) == 0) flagsRemoved++;
            }
        }
        _snapshot = TakeSnapshot();

        foreach (var (x, y) in hitCells) SpawnSplat(x, y);
        if (hitCells.Count > 0)
        {
            _shakeStart = t0;
            if (!quiet || _session.State == GameState.Lost) audio.Play("splat");
            Platform.Vibrate(120);
        }
        else if (revealed > 0 && !quiet) audio.Play("dig", 0.9f + (float)_fx.NextDouble() * 0.25f);
        if (flagsAdded > 0) audio.Play("flag");
        else if (flagsRemoved > 0 && revealed == 0 && hitCells.Count == 0) audio.Play("unflag");
        if (flagsAdded > 0) Platform.Vibrate(20);

        QueueRedraw();
    }

    private void SpawnSplat(int x, int y)
    {
        var c = CellPos(x, y) + new Vector2(_cs, _cs) * 0.5f;
        double now = Now;
        for (int i = 0; i < 18; i++)
        {
            double a = _fx.NextDouble() * Math.Tau;
            float speed = 80f + (float)_fx.NextDouble() * 260f;
            _particles.Add(new Particle
            {
                Pos = c,
                Vel = new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * speed,
                Born = now,
                Life = 0.5 + _fx.NextDouble() * 0.4,
                Color = _fx.Next(3) == 0 ? Icons.PoopLight : Icons.PoopBrown,
                Size = _cs * (0.05f + (float)_fx.NextDouble() * 0.08f),
            });
        }
    }

    private void UpdateEffects(double delta)
    {
        double now = Now;
        List<int>? done = null;
        foreach (var kv in _revealAnims)
            if (now - kv.Value > RevealAnimTime) (done ??= new()).Add(kv.Key);
        if (done != null) foreach (int id in done) _revealAnims.Remove(id);

        done = null;
        foreach (var kv in _flagAnims)
            if (now - kv.Value > FlagAnimTime) (done ??= new()).Add(kv.Key);
        if (done != null) foreach (int id in done) _flagAnims.Remove(id);

        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            if (now - p.Born > p.Life) { _particles.RemoveAt(i); continue; }
            p.Vel += new Vector2(0, 700f) * (float)delta;
            p.Pos += p.Vel * (float)delta;
            _particles[i] = p;
        }

        double st = now - _shakeStart;
        if (st < ShakeTime)
        {
            float amp = (float)(1 - st / ShakeTime) * Mathf.Min(10f, _cs * 0.22f);
            _shake = new Vector2((float)(_fx.NextDouble() - 0.5), (float)(_fx.NextDouble() - 0.5)) * 2f * amp;
        }
        else _shake = Vector2.Zero;

        if (_revealAnims.Count > 0 || _flagAnims.Count > 0 || _particles.Count > 0 || st < ShakeTime + 0.05
            || _session?.CurrentHint != null || Highlights.Count > 0)
            QueueRedraw();
    }

    private static double EaseOutBack(double x)
    {
        x = Math.Clamp(x, 0, 1);
        const double c1 = 1.70158, c3 = c1 + 1;
        return 1 + c3 * Math.Pow(x - 1, 3) + c1 * Math.Pow(x - 1, 2);
    }

    private Color[] Palette() => Main.Instance.Settings.ColorBlind ? ColorBlindColors : NumberColors;

    private static readonly Color[] ColorBlindColors =
    {
        Colors.Transparent, new(0.00f, 0.35f, 0.70f), new(0.80f, 0.38f, 0.00f), new(0.70f, 0.00f, 0.45f),
        new(0.00f, 0.42f, 0.42f), new(0.30f, 0.10f, 0.10f), new(0.45f, 0.45f, 0.00f), Colors.Black, new(0.35f, 0.35f, 0.35f),
    };

    // ---------------------------------------------------------------- Eingabe

    public override void _Process(double delta)
    {
        UpdateEffects(delta);
        if (_touches.Count == 1 && !_touchMoved && !_longPressFired)
        {
            _touchTime += delta;
            if (_touchTime >= LongPressSeconds)
            {
                _longPressFired = true;
                Secondary(CellAt(_touchStart));
                Input.VibrateHandheld(30);
            }
        }
    }

    public override void _GuiInput(InputEvent e)
    {
        switch (e)
        {
            // Vom Touch erzeugte Mausereignisse ignorieren: Touch wird unten selbst behandelt.
            case InputEventMouse m when m.Device == InputEvent.DeviceIdEmulation:
                return;
            case InputEventMouseButton mb:
                OnMouseButton(mb);
                break;
            case InputEventMouseMotion mm:
                OnMouseMotion(mm);
                break;
            case InputEventScreenTouch st:
                OnTouch(st);
                break;
            case InputEventScreenDrag sd:
                OnTouchDrag(sd);
                break;
        }
    }

    private void OnMouseButton(InputEventMouseButton mb)
    {
        switch (mb.ButtonIndex)
        {
            case MouseButton.WheelUp when mb.Pressed:
                ZoomAt(1.12f, mb.Position);
                break;
            case MouseButton.WheelDown when mb.Pressed:
                ZoomAt(1f / 1.12f, mb.Position);
                break;
            case MouseButton.Left:
                if (mb.Pressed)
                {
                    _mouseDown = true;
                    _mouseDragged = false;
                    _mouseDownPos = mb.Position;
                }
                else
                {
                    if (_mouseDown && !_mouseDragged) Primary(CellAt(mb.Position));
                    _mouseDown = false;
                }
                break;
            case MouseButton.Right when mb.Pressed:
                Secondary(CellAt(mb.Position));
                break;
            case MouseButton.Middle:
                _middleDown = mb.Pressed;
                break;
        }
    }

    private void OnMouseMotion(InputEventMouseMotion mm)
    {
        var cell = CellAt(mm.Position);
        if (cell != _hover)
        {
            _hover = cell;
            QueueRedraw();
        }
        if (_mouseDown && !_mouseDragged && mm.Position.DistanceTo(_mouseDownPos) > DragThreshold) _mouseDragged = true;
        if (_mouseDragged || _middleDown) Pan(mm.Relative);
    }

    private void OnTouch(InputEventScreenTouch st)
    {
        if (st.Pressed)
        {
            _touches[st.Index] = st.Position;
            if (_touches.Count == 1)
            {
                _touchStart = st.Position;
                _touchMoved = false;
                _longPressFired = false;
                _touchTime = 0;
            }
            else
            {
                _touchMoved = true; // zweiter Finger: kein Tap mehr
                _pinchDist = PinchDistance();
            }
        }
        else
        {
            bool wasTap = _touches.Count == 1 && !_touchMoved && !_longPressFired;
            _touches.Remove(st.Index);
            if (wasTap) Primary(CellAt(st.Position));
        }
    }

    private void OnTouchDrag(InputEventScreenDrag sd)
    {
        if (!_touches.ContainsKey(sd.Index)) return;
        var before = Midpoint();
        _touches[sd.Index] = sd.Position;

        if (_touches.Count >= 2)
        {
            float dist = PinchDistance();
            var mid = Midpoint();
            if (_pinchDist > 1f) ZoomAt(dist / _pinchDist, mid);
            Pan(mid - before);
            _pinchDist = dist;
        }
        else
        {
            if (!_touchMoved && sd.Position.DistanceTo(_touchStart) > DragThreshold * 1.5f) _touchMoved = true;
            if (_touchMoved) Pan(sd.Relative);
        }
    }

    private float PinchDistance()
    {
        var pts = new List<Vector2>(_touches.Values);
        return pts.Count >= 2 ? pts[0].DistanceTo(pts[1]) : 0f;
    }

    private Vector2 Midpoint()
    {
        var sum = Vector2.Zero;
        foreach (var p in _touches.Values) sum += p;
        return _touches.Count == 0 ? Vector2.Zero : sum / _touches.Count;
    }

    private void Primary(Vector2I cell)
    {
        if (_session == null) return;
        _lastClick = cell;
        var action = CurrentTool switch { Tool.Flag => GuardAction.Flag, Tool.Dog => GuardAction.Sniff, _ => GuardAction.Dig };
        if (Guard != null && !Guard(action, cell)) return;
        switch (CurrentTool)
        {
            case Tool.Dig: _session.Reveal(cell.X, cell.Y); break;
            case Tool.Flag: _session.ToggleFlag(cell.X, cell.Y); break;
            case Tool.Dog: _session.Sniff(cell.X, cell.Y); break;
        }
    }

    private void Secondary(Vector2I cell)
    {
        if (Guard != null && !Guard(GuardAction.Flag, cell)) return;
        _session?.ToggleFlag(cell.X, cell.Y);
    }

    // ---------------------------------------------------------------- Zeichnen

    public override void _Draw()
    {
        if (_session == null) return;
        var board = _session.Board;
        var font = ThemeDB.FallbackFont;

        // Sichtbarer Zellbereich
        int x0 = Math.Max(0, Mathf.FloorToInt(-_origin.X / _cs));
        int y0 = Math.Max(0, Mathf.FloorToInt(-_origin.Y / _cs));
        int x1 = Math.Min(board.Width - 1, Mathf.FloorToInt((Size.X - _origin.X) / _cs));
        int y1 = Math.Min(board.Height - 1, Mathf.FloorToInt((Size.Y - _origin.Y) / _cs));

        // Ebene 1: Boden, Wald, Zäune, Zellinhalt
        double now = Now;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!_revealAnims.TryGetValue(y * board.Width + x, out double start))
                {
                    DrawCellBase(board, x, y, font);
                    continue;
                }
                double t = (now - start) / RevealAnimTime;
                if (t < 0)
                {
                    DrawCellBase(board, x, y, font, asHidden: true); // wartet noch auf seine Welle
                    continue;
                }
                float s = 0.55f + 0.45f * (float)EaseOutBack(t);
                var center = CellPos(x, y) + new Vector2(_cs, _cs) * 0.5f;
                DrawSetTransform(center * (1 - s), 0, new Vector2(s, s));
                DrawCellBase(board, x, y, font);
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            }
        }

        // Ebene 2: Hindernisse (über Zellgrenzen hinweg)
        for (int y = y0 - 1; y <= y1; y++)
        {
            for (int x = x0 - 1; x <= x1; x++)
            {
                if (!board.InBounds(x, y)) continue;
                ref var c = ref board.At(x, y);
                if (c.Kind != CellKind.Obstacle || c.ObsPx != 0 || c.ObsPy != 0) continue;
                var r = new Rect2(CellPos(x, y), new Vector2(c.ObsW, c.ObsH) * _cs);
                switch (c.Obstacle)
                {
                    case ObstacleType.Tree: Icons.Tree(this, r); break;
                    case ObstacleType.Bench: Icons.Bench(this, r); break;
                    case ObstacleType.Bush: Icons.Bush(this, r); break;
                    case ObstacleType.Rock: Icons.Rock(this, r); break;
                }
            }
        }

        // Hervorhebungen (Tutorial): goldener, pulsierender Rahmen
        foreach (var h in Highlights)
        {
            if (!board.InBounds(h.X, h.Y)) continue;
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)now * 5f);
            var gold = new Color(1f, 0.82f, 0.2f);
            var hr = new Rect2(CellPos(h.X, h.Y), new Vector2(_cs, _cs)).Grow(-1f);
            DrawRect(hr, new Color(gold, 0.12f + 0.16f * pulse));
            DrawRect(hr, gold, false, 3f + 2f * pulse);
        }

        // Tipp des Hundes: pulsierender Rahmen
        if (_session.CurrentHint is { } hint && board.InBounds(hint.X, hint.Y))
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)now * 6f);
            var hc = hint.Kind == HintKind.Safe ? new Color(0.2f, 0.95f, 0.3f) : new Color(1f, 0.25f, 0.25f);
            var hr = new Rect2(CellPos(hint.X, hint.Y), new Vector2(_cs, _cs)).Grow(-1f);
            DrawRect(hr, new Color(hc, 0.18f + 0.18f * pulse));
            DrawRect(hr, hc, false, 3f + 2f * pulse);
            Icons.Paw(this, hr.GetCenter(), _cs * 0.7f, new Color(1f, 1f, 1f, 0.9f));
        }

        // Hover-Rahmen
        if (_hover.X >= 0 && board.IsMeadow(_hover.X, _hover.Y) && _session.State is GameState.Ready or GameState.Playing)
        {
            var col = CurrentTool switch
            {
                Tool.Flag => Icons.BagBlue,
                Tool.Dog => new Color(1f, 0.65f, 0.1f),
                _ => Colors.White,
            };
            DrawRect(new Rect2(CellPos(_hover.X, _hover.Y), new Vector2(_cs, _cs)), col, false, 2.5f);
        }

        // Partikel (Haufen-Spritzer)
        foreach (var p in _particles)
        {
            float life = (float)Mathf.Clamp(1.0 - (now - p.Born) / p.Life, 0, 1);
            DrawCircle(p.Pos, p.Size * (0.4f + 0.6f * life), new Color(p.Color, life));
        }
    }

    private Vector2 CellPos(int x, int y) => _origin + _shake + new Vector2(x, y) * _cs;

    private void DrawCellBase(Board board, int x, int y, Font font, bool asHidden = false)
    {
        Cell cell = board.At(x, y);
        if (asHidden)
        {
            cell.State = CellState.Hidden;
            cell.Flags = 0;
            cell.Question = false;
        }
        ref var c = ref cell;
        var pos = CellPos(x, y);
        var rect = new Rect2(pos, new Vector2(_cs, _cs));
        bool alt = ((x + y) & 1) == 0;

        switch (c.Kind)
        {
            case CellKind.Void:
                DrawForest(board, x, y, rect);
                return;
            case CellKind.Obstacle:
                DrawRect(rect, alt ? new Color(0.30f, 0.58f, 0.25f) : new Color(0.28f, 0.55f, 0.23f));
                return;
        }

        if (c.State == CellState.Hidden)
        {
            DrawRect(rect, alt ? new Color(0.37f, 0.70f, 0.31f) : new Color(0.33f, 0.65f, 0.28f));
            float b = Mathf.Max(2f, _cs * 0.07f);
            DrawRect(new Rect2(pos, new Vector2(_cs, b)), new Color(1, 1, 1, 0.22f));
            DrawRect(new Rect2(pos, new Vector2(b, _cs)), new Color(1, 1, 1, 0.22f));
            DrawRect(new Rect2(pos + new Vector2(0, _cs - b), new Vector2(_cs, b)), new Color(0, 0, 0, 0.20f));
            DrawRect(new Rect2(pos + new Vector2(_cs - b, 0), new Vector2(b, _cs)), new Color(0, 0, 0, 0.20f));

            if (c.Flags > 0)
            {
                float bagScale = _flagAnims.TryGetValue(y * board.Width + x, out double fs0) ? (float)EaseOutBack((Now - fs0) / FlagAnimTime) : 1f;
                Icons.Bag(this, rect.GetCenter(), _cs * Mathf.Max(0.01f, bagScale));
                if (c.Flags > 1) DrawCentered(font, "2", rect, _cs * 0.42f, Colors.White, new Vector2(0, _cs * 0.12f));
            }
            else if (c.Question)
            {
                DrawCentered(font, "?", rect, _cs * 0.7f, new Color(1f, 0.85f, 0.2f), Vector2.Zero);
            }
            return;
        }

        // aufgedeckt
        DrawRect(rect, alt ? new Color(0.83f, 0.76f, 0.56f) : new Color(0.79f, 0.72f, 0.52f));
        DrawRect(rect, new Color(0, 0, 0, 0.08f), false, 1f);

        if (c.Poop > 0)
        {
            if (c.Hit) DrawRect(rect, new Color(0.9f, 0.15f, 0.15f, 0.45f));
            Icons.Poop(this, rect.GetCenter(), _cs);
            if (c.Poop > 1) DrawCentered(font, "x2", rect, _cs * 0.30f, Colors.White, new Vector2(_cs * 0.30f, -_cs * 0.32f));
            return;
        }

        var info = board.Number(x, y);
        if (c.Special == SpecialKind.Puddle)
        {
            Icons.Ellipse(this, rect.GetCenter() + new Vector2(0, _cs * 0.05f), _cs * 0.42f, _cs * 0.30f, new Color(0.35f, 0.62f, 0.90f, 0.85f));
            Icons.Ellipse(this, rect.GetCenter() + new Vector2(-_cs * 0.10f, -_cs * 0.02f), _cs * 0.12f, _cs * 0.06f, new Color(1, 1, 1, 0.45f));
        }
        else if (c.Special == SpecialKind.Sprinkler)
        {
            DrawRect(rect, new Color(0.55f, 0.75f, 0.90f, 0.35f));
            DrawCircle(rect.GetCenter() + new Vector2(_cs * 0.32f, _cs * 0.30f), _cs * 0.10f, new Color(0.45f, 0.48f, 0.52f));
            for (int i = 0; i < 3; i++)
                DrawCircle(rect.GetCenter() + new Vector2(_cs * (0.14f + i * 0.12f), _cs * (0.14f - i * 0.04f)), _cs * 0.035f, new Color(0.2f, 0.5f, 0.95f));
        }

        if (info.Unknown)
        {
            DrawCentered(font, "?", rect, _cs * 0.62f, new Color(0.15f, 0.35f, 0.75f), Vector2.Zero);
        }
        else if (!info.IsExact)
        {
            DrawCentered(font, $"{info.Min}-{info.Max}", rect, _cs * 0.38f, new Color(0.05f, 0.20f, 0.55f), Vector2.Zero);
        }
        else if (info.Min > 0)
        {
            var pal = Palette();
            var color = pal[Math.Min(info.Min, pal.Length - 1)];
            DrawCentered(font, info.Min.ToString(), rect, _cs * 0.66f, color, Vector2.Zero);
        }

        if (_session!.State == GameState.Lost && c.Flags > 0 && c.Poop == 0) Icons.X(this, rect.GetCenter(), _cs, Colors.Red);
    }

    private void DrawForest(Board board, int x, int y, Rect2 rect)
    {
        DrawRect(rect, new Color(0.09f, 0.27f, 0.12f));

        uint h = (uint)(x * 73856093 ^ y * 19349663);
        float s = _cs;
        var c1 = rect.GetCenter() + new Vector2(((h & 0xFF) / 255f - 0.5f) * 0.3f, (((h >> 8) & 0xFF) / 255f - 0.5f) * 0.3f) * s;
        DrawCircle(c1, s * 0.42f, new Color(0.11f, 0.35f, 0.15f));
        DrawCircle(c1 + new Vector2(-0.10f, -0.10f) * s, s * 0.26f, new Color(0.15f, 0.42f, 0.19f));
        if ((h >> 16 & 3) == 0) DrawCircle(c1 + new Vector2(0.28f, 0.22f) * s, s * 0.18f, new Color(0.17f, 0.45f, 0.21f));

        // Zaun zur Wiese hin
        var fence = new Color(0.62f, 0.45f, 0.25f);
        float t = Mathf.Max(2f, s * 0.08f);
        (int dx, int dy)[] dirs = { (0, -1), (0, 1), (-1, 0), (1, 0) };
        foreach (var (dx, dy) in dirs)
        {
            int nx = x + dx, ny = y + dy;
            if (!board.InBounds(nx, ny) || board.At(nx, ny).Kind == CellKind.Void) continue;
            Vector2 a, b;
            if (dy != 0)
            {
                float ly = dy < 0 ? rect.Position.Y + t : rect.End.Y - t;
                a = new Vector2(rect.Position.X, ly);
                b = new Vector2(rect.End.X, ly);
            }
            else
            {
                float lx = dx < 0 ? rect.Position.X + t : rect.End.X - t;
                a = new Vector2(lx, rect.Position.Y);
                b = new Vector2(lx, rect.End.Y);
            }
            DrawLine(a, b, fence, t);
            DrawCircle(a, t * 0.9f, new Color(0.45f, 0.30f, 0.15f));
            DrawCircle(b, t * 0.9f, new Color(0.45f, 0.30f, 0.15f));
        }
    }

    private void DrawCentered(Font font, string text, Rect2 rect, float size, Color color, Vector2 offset)
    {
        int fs = Mathf.Max(6, Mathf.RoundToInt(size));
        float baseline = rect.Position.Y + (rect.Size.Y - font.GetHeight(fs)) / 2f + font.GetAscent(fs);
        var shadow = new Color(0, 0, 0, 0.25f);
        DrawString(font, new Vector2(rect.Position.X + 1, baseline + 1) + offset, text, HorizontalAlignment.Center, rect.Size.X, fs, shadow);
        DrawString(font, new Vector2(rect.Position.X, baseline) + offset, text, HorizontalAlignment.Center, rect.Size.X, fs, color);
    }
}
