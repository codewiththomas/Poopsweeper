using System;
using System.Collections.Generic;
using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

public enum Tool { Dig, Flag, Dog }

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
    private bool _needsFit;

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

    public override void _Ready()
    {
        ClipContents = true;
        Resized += () =>
        {
            if (_session == null) return;
            // Beim ersten echten Layout einpassen; danach nur noch Zoom/Pan beibehalten.
            Fit(keepZoom: !_needsFit);
            _needsFit = false;
        };
    }

    public void SetSession(GameSession session)
    {
        if (_session != null) _session.Changed -= QueueRedraw;
        _session = session;
        _session.Changed += QueueRedraw;
        _touches.Clear();
        _needsFit = Size.X < 1f || Size.Y < 1f; // Layout noch nicht gelaufen → beim ersten Resized einpassen
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
        _origin = anchor - (anchor - _origin) * (newCs / _cs);
        _cs = newCs;
        ClampOrigin();
        QueueRedraw();
    }

    private void Pan(Vector2 delta)
    {
        _origin += delta;
        ClampOrigin();
        QueueRedraw();
    }

    private Vector2I CellAt(Vector2 p)
    {
        var v = (p - _origin) / _cs;
        return new Vector2I(Mathf.FloorToInt(v.X), Mathf.FloorToInt(v.Y));
    }

    // ---------------------------------------------------------------- Eingabe

    public override void _Process(double delta)
    {
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
        switch (CurrentTool)
        {
            case Tool.Dig: _session.Reveal(cell.X, cell.Y); break;
            case Tool.Flag: _session.ToggleFlag(cell.X, cell.Y); break;
            case Tool.Dog: _session.Sniff(cell.X, cell.Y); break;
        }
    }

    private void Secondary(Vector2I cell) => _session?.ToggleFlag(cell.X, cell.Y);

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
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                DrawCellBase(board, x, y, font);

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
    }

    private Vector2 CellPos(int x, int y) => _origin + new Vector2(x, y) * _cs;

    private void DrawCellBase(Board board, int x, int y, Font font)
    {
        ref var c = ref board.At(x, y);
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
                Icons.Bag(this, rect.GetCenter(), _cs);
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
            var color = NumberColors[Math.Min(info.Min, NumberColors.Length - 1)];
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
