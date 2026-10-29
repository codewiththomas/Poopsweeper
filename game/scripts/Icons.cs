using Godot;

namespace Poopsweeper;

/// <summary>Alle Grafiken sind Vektorformen, damit keine Assets nötig sind. Aufruf nur innerhalb von _Draw().</summary>
public static class Icons
{
    public static readonly Color PoopBrown = new(0.45f, 0.27f, 0.12f);
    public static readonly Color PoopDark = new(0.32f, 0.18f, 0.07f);
    public static readonly Color PoopLight = new(0.62f, 0.40f, 0.20f);
    public static readonly Color BagBlue = new(0.25f, 0.55f, 0.95f);
    public static readonly Color StarGold = new(1.0f, 0.78f, 0.15f);
    public static readonly Color StarEmpty = new(0.25f, 0.30f, 0.25f, 0.6f);
    public static readonly Color HeartRed = new(0.92f, 0.22f, 0.28f);

    public static void Ellipse(CanvasItem ci, Vector2 c, float rx, float ry, Color color)
    {
        ci.DrawSetTransform(c, 0, new Vector2(rx, ry));
        ci.DrawCircle(Vector2.Zero, 1f, color);
        ci.DrawSetTransform(Vector2.Zero, 0, Vector2.One);
    }

    /// <summary>Hundehaufen: drei gestapelte Kugeln mit Augen. s = Kantenlänge der Zelle.</summary>
    public static void Poop(CanvasItem ci, Vector2 c, float s)
    {
        ci.DrawCircle(c + new Vector2(0, s * 0.18f), s * 0.34f, PoopBrown);
        ci.DrawCircle(c + new Vector2(0, -0.05f * s), s * 0.26f, PoopBrown);
        ci.DrawCircle(c + new Vector2(0, -0.24f * s), s * 0.15f, PoopBrown);
        ci.DrawColoredPolygon(new[]
        {
            c + new Vector2(-0.05f * s, -0.36f * s), c + new Vector2(0.05f * s, -0.36f * s), c + new Vector2(0.10f * s, -0.46f * s),
        }, PoopBrown);
        ci.DrawArc(c + new Vector2(0, s * 0.18f), s * 0.34f, 3.6f, 5.8f, 12, PoopLight, Mathf.Max(1f, s * 0.05f));
        ci.DrawArc(c + new Vector2(0, -0.05f * s), s * 0.26f, 3.6f, 5.8f, 12, PoopLight, Mathf.Max(1f, s * 0.05f));
        foreach (float dx in new[] { -0.09f, 0.09f })
        {
            ci.DrawCircle(c + new Vector2(dx * s, 0.12f * s), s * 0.075f, Colors.White);
            ci.DrawCircle(c + new Vector2(dx * s, 0.13f * s), s * 0.035f, Colors.Black);
        }
    }

    /// <summary>Kotbeutel-Tüte: blauer Beutel mit Knoten.</summary>
    public static void Bag(CanvasItem ci, Vector2 c, float s)
    {
        ci.DrawColoredPolygon(new[]
        {
            c + new Vector2(-0.18f * s, -0.18f * s), c + new Vector2(0.18f * s, -0.18f * s),
            c + new Vector2(0.30f * s, 0.34f * s), c + new Vector2(-0.30f * s, 0.34f * s),
        }, BagBlue);
        ci.DrawCircle(c + new Vector2(0, -0.26f * s), s * 0.09f, BagBlue);
        ci.DrawLine(c + new Vector2(-0.14f * s, -0.10f * s), c + new Vector2(-0.20f * s, 0.24f * s),
            new Color(1, 1, 1, 0.5f), Mathf.Max(1f, s * 0.05f));
        ci.DrawLine(c + new Vector2(-0.06f * s, -0.34f * s), c + new Vector2(-0.12f * s, -0.44f * s), BagBlue, Mathf.Max(1.5f, s * 0.06f));
        ci.DrawLine(c + new Vector2(0.06f * s, -0.34f * s), c + new Vector2(0.14f * s, -0.43f * s), BagBlue, Mathf.Max(1.5f, s * 0.06f));
    }

    public static void Heart(CanvasItem ci, Vector2 c, float s, bool filled = true)
    {
        var col = filled ? HeartRed : new Color(0.3f, 0.2f, 0.2f, 0.6f);
        ci.DrawCircle(c + new Vector2(-0.16f * s, -0.12f * s), s * 0.20f, col);
        ci.DrawCircle(c + new Vector2(0.16f * s, -0.12f * s), s * 0.20f, col);
        ci.DrawColoredPolygon(new[]
        {
            c + new Vector2(-0.34f * s, -0.04f * s), c + new Vector2(0.34f * s, -0.04f * s), c + new Vector2(0, 0.36f * s),
        }, col);
    }

    public static void Star(CanvasItem ci, Vector2 c, float r, bool filled)
    {
        var pts = new Vector2[10];
        for (int i = 0; i < 10; i++)
        {
            float a = -Mathf.Pi / 2 + i * Mathf.Pi / 5;
            float rad = i % 2 == 0 ? r : r * 0.45f;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
        }
        ci.DrawColoredPolygon(pts, filled ? StarGold : StarEmpty);
    }

    /// <summary>Pfotenabdruck (Schnüffelhund).</summary>
    public static void Paw(CanvasItem ci, Vector2 c, float s, Color color)
    {
        Ellipse(ci, c + new Vector2(0, 0.14f * s), s * 0.24f, s * 0.20f, color);
        ci.DrawCircle(c + new Vector2(-0.27f * s, -0.06f * s), s * 0.10f, color);
        ci.DrawCircle(c + new Vector2(-0.09f * s, -0.22f * s), s * 0.10f, color);
        ci.DrawCircle(c + new Vector2(0.09f * s, -0.22f * s), s * 0.10f, color);
        ci.DrawCircle(c + new Vector2(0.27f * s, -0.06f * s), s * 0.10f, color);
    }

    public static void Clock(CanvasItem ci, Vector2 c, float s, Color color)
    {
        ci.DrawArc(c, s * 0.34f, 0, Mathf.Tau, 24, color, Mathf.Max(1.5f, s * 0.07f), true);
        ci.DrawLine(c, c + new Vector2(0, -0.22f * s), color, Mathf.Max(1.5f, s * 0.07f));
        ci.DrawLine(c, c + new Vector2(0.16f * s, 0.06f * s), color, Mathf.Max(1.5f, s * 0.07f));
    }

    public static void Lock(CanvasItem ci, Vector2 c, float s, Color color)
    {
        ci.DrawArc(c + new Vector2(0, -0.08f * s), s * 0.16f, Mathf.Pi, Mathf.Tau, 12, color, Mathf.Max(2f, s * 0.07f), true);
        ci.DrawRect(new Rect2(c + new Vector2(-0.24f * s, -0.08f * s), new Vector2(0.48f * s, 0.38f * s)), color);
    }

    public static void Tree(CanvasItem ci, Rect2 r)
    {
        var c = r.GetCenter();
        float s = Mathf.Min(r.Size.X, r.Size.Y);
        ci.DrawRect(new Rect2(c + new Vector2(-0.07f * s, 0.12f * s), new Vector2(0.14f * s, 0.36f * s)), new Color(0.36f, 0.22f, 0.10f));
        ci.DrawCircle(c + new Vector2(0, 0.12f * s), s * 0.36f, new Color(0.10f, 0.38f, 0.14f));
        ci.DrawCircle(c + new Vector2(-0.18f * s, -0.04f * s), s * 0.32f, new Color(0.13f, 0.46f, 0.17f));
        ci.DrawCircle(c + new Vector2(0.18f * s, -0.06f * s), s * 0.32f, new Color(0.15f, 0.50f, 0.18f));
        ci.DrawCircle(c + new Vector2(0, -0.20f * s), s * 0.30f, new Color(0.20f, 0.58f, 0.22f));
        ci.DrawCircle(c + new Vector2(-0.08f * s, -0.26f * s), s * 0.10f, new Color(0.35f, 0.72f, 0.32f));
    }

    public static void Bush(CanvasItem ci, Rect2 r)
    {
        var c = r.GetCenter();
        float s = r.Size.X;
        ci.DrawCircle(c + new Vector2(-0.18f * s, 0.08f * s), s * 0.24f, new Color(0.14f, 0.44f, 0.16f));
        ci.DrawCircle(c + new Vector2(0.18f * s, 0.08f * s), s * 0.24f, new Color(0.14f, 0.44f, 0.16f));
        ci.DrawCircle(c + new Vector2(0, -0.06f * s), s * 0.28f, new Color(0.19f, 0.54f, 0.20f));
        foreach (var d in new[] { new Vector2(-0.1f, -0.1f), new Vector2(0.14f, 0.0f), new Vector2(-0.12f, 0.14f) })
            ci.DrawCircle(c + d * s, s * 0.04f, new Color(0.9f, 0.3f, 0.35f));
    }

    public static void Rock(CanvasItem ci, Rect2 r)
    {
        var c = r.GetCenter();
        float s = r.Size.X;
        ci.DrawColoredPolygon(new[]
        {
            c + new Vector2(-0.36f * s, 0.26f * s), c + new Vector2(-0.30f * s, -0.10f * s), c + new Vector2(-0.05f * s, -0.30f * s),
            c + new Vector2(0.26f * s, -0.18f * s), c + new Vector2(0.38f * s, 0.26f * s),
        }, new Color(0.50f, 0.52f, 0.54f));
        ci.DrawColoredPolygon(new[]
        {
            c + new Vector2(-0.30f * s, -0.10f * s), c + new Vector2(-0.05f * s, -0.30f * s), c + new Vector2(0.06f * s, -0.04f * s), c + new Vector2(-0.18f * s, 0.06f * s),
        }, new Color(0.68f, 0.70f, 0.72f));
    }

    public static void Bench(CanvasItem ci, Rect2 r)
    {
        bool horizontal = r.Size.X >= r.Size.Y;
        var wood = new Color(0.60f, 0.40f, 0.20f);
        var dark = new Color(0.40f, 0.26f, 0.12f);
        var inner = r.Grow(-Mathf.Min(r.Size.X, r.Size.Y) * 0.12f);
        ci.DrawRect(inner, dark);
        const int planks = 4;
        for (int i = 0; i < planks; i++)
        {
            Rect2 plank = horizontal
                ? new Rect2(inner.Position + new Vector2(0, inner.Size.Y * (i + 0.12f) / planks), new Vector2(inner.Size.X, inner.Size.Y * 0.76f / planks))
                : new Rect2(inner.Position + new Vector2(inner.Size.X * (i + 0.12f) / planks, 0), new Vector2(inner.Size.X * 0.76f / planks, inner.Size.Y));
            ci.DrawRect(plank, wood);
        }
    }

    public static void X(CanvasItem ci, Vector2 c, float s, Color color)
    {
        float w = Mathf.Max(2f, s * 0.08f);
        ci.DrawLine(c + new Vector2(-0.28f, -0.28f) * s, c + new Vector2(0.28f, 0.28f) * s, color, w);
        ci.DrawLine(c + new Vector2(-0.28f, 0.28f) * s, c + new Vector2(0.28f, -0.28f) * s, color, w);
    }
}
