using System;
using Godot;

namespace Poopsweeper;

/// <summary>Kompendium: erklärt Steuerung und alle Mechaniken.</summary>
public partial class Compendium : Control
{
    private sealed record Entry(string Key, Action<Control, Vector2, float> Icon);

    private static readonly Entry[] Entries =
    {
        new("goal", (c, p, s) => Icons.Poop(c, p, s)),
        new("numbers", (c, p, s) => Text(c, "2", p, s * 0.8f, new Color(0.08f, 0.48f, 0.08f))),
        new("controls", (c, p, s) =>
        {
            Icons.Ellipse(c, p, s * 0.26f, s * 0.38f, new Color(0.85f, 0.85f, 0.88f));
            c.DrawLine(p + new Vector2(0, -s * 0.38f), p + new Vector2(0, -s * 0.05f), new Color(0.3f, 0.3f, 0.35f), 2f);
            c.DrawLine(p + new Vector2(-s * 0.26f, -s * 0.05f), p + new Vector2(s * 0.26f, -s * 0.05f), new Color(0.3f, 0.3f, 0.35f), 2f);
            c.DrawRect(new Rect2(p + new Vector2(-s * 0.26f, -s * 0.36f), new Vector2(s * 0.26f, s * 0.31f)), new Color(0.35f, 0.65f, 0.95f, 0.8f));
        }),
        new("bag", (c, p, s) => Icons.Bag(c, p, s)),
        new("question", (c, p, s) => Text(c, "?", p, s * 0.9f, new Color(1f, 0.85f, 0.2f))),
        new("lives", (c, p, s) => Icons.Heart(c, p, s)),
        new("fair", (c, p, s) =>
        {
            var green = new Color(0.12f, 0.55f, 0.15f);
            c.DrawLine(p + new Vector2(-s * 0.28f, 0), p + new Vector2(-s * 0.08f, s * 0.22f), green, Mathf.Max(3f, s * 0.1f));
            c.DrawLine(p + new Vector2(-s * 0.08f, s * 0.22f), p + new Vector2(s * 0.30f, -s * 0.22f), green, Mathf.Max(3f, s * 0.1f));
        }),
        new("obstacles", (c, p, s) => Icons.Tree(c, new Rect2(p - new Vector2(s, s) * 0.5f, new Vector2(s, s)))),
        new("dog", (c, p, s) => Icons.Paw(c, p, s, new Color(1f, 0.75f, 0.3f))),
        new("hint", (c, p, s) =>
        {
            var r = new Rect2(p - new Vector2(s, s) * 0.4f, new Vector2(s, s) * 0.8f);
            c.DrawRect(r, new Color(0.2f, 0.95f, 0.3f, 0.3f));
            c.DrawRect(r, new Color(0.2f, 0.95f, 0.3f), false, 3f);
            Icons.Paw(c, p, s * 0.6f, new Color(1f, 1f, 1f, 0.9f));
        }),
        new("puddle", (c, p, s) => Icons.Ellipse(c, p, s * 0.42f, s * 0.30f, new Color(0.35f, 0.62f, 0.90f, 0.9f))),
        new("double", (c, p, s) =>
        {
            Icons.Poop(c, p, s);
            Text(c, "x2", p + new Vector2(s * 0.34f, -s * 0.34f), s * 0.45f, Colors.White);
        }),
        new("sprinkler", (c, p, s) =>
        {
            Text(c, "?", p, s * 0.9f, new Color(0.15f, 0.35f, 0.75f));
            for (int i = 0; i < 3; i++)
                c.DrawCircle(p + new Vector2(s * (0.1f + i * 0.14f), s * (0.20f - i * 0.1f)), s * 0.04f, new Color(0.2f, 0.5f, 0.95f));
        }),
        new("stars", (c, p, s) => Icons.Star(c, p, s * 0.5f, true)),
    };

    private Main _main = null!;

    public void Init(Main main) => _main = main;

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

        var title = new Label { Text = Loc.T("comp.title"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 52);
        title.AddThemeColorOverride("font_color", Icons.PoopLight);
        box.AddChild(title);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 12);
        scroll.AddChild(list);
        foreach (var e in Entries) list.AddChild(BuildCard(e));

        var back = new Button { Text = Loc.T("common.back"), FocusMode = FocusModeEnum.None };
        back.Pressed += _main.ShowHome;
        box.AddChild(back);
    }

    private static Control BuildCard(Entry e)
    {
        var panel = new PanelContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var margin = new MarginContainer();
        foreach (var side in new[] { "left", "right", "top", "bottom" })
            margin.AddThemeConstantOverride($"margin_{side}", 12);
        panel.AddChild(margin);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        margin.AddChild(row);

        row.AddChild(new IconBox { Draw = e.Icon, CustomMinimumSize = new Vector2(104, 104), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter });

        var text = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(text);

        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 14);
        text.AddChild(head);
        var title = new Label { Text = Loc.T($"comp.{e.Key}.t") };
        title.AddThemeFontSizeOverride("font_size", 32);
        title.AddThemeColorOverride("font_color", UiTheme.Accent);
        head.AddChild(title);
        string tagKey = $"comp.{e.Key}.tag";
        if (Loc.Has(tagKey))
        {
            var tag = new Label { Text = Loc.T(tagKey), VerticalAlignment = VerticalAlignment.Bottom };
            tag.AddThemeFontSizeOverride("font_size", 20);
            tag.AddThemeColorOverride("font_color", new Color(0.7f, 0.85f, 0.7f));
            head.AddChild(tag);
        }

        var body = new Label { Text = Loc.T($"comp.{e.Key}.b"), AutowrapMode = TextServer.AutowrapMode.WordSmart, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeFontSizeOverride("font_size", 22);
        text.AddChild(body);
        return panel;
    }

    private static void Text(Control c, string text, Vector2 center, float size, Color color)
    {
        var font = ThemeDB.FallbackFont;
        int fs = Mathf.RoundToInt(size);
        float baseline = center.Y - font.GetHeight(fs) / 2f + font.GetAscent(fs);
        c.DrawString(font, new Vector2(center.X - 100, baseline), text, HorizontalAlignment.Center, 200, fs, color);
    }
}

/// <summary>Zeichnet ein Symbol auf einem Wiesenfeld als Kachel.</summary>
public partial class IconBox : Control
{
    public Action<Control, Vector2, float>? Draw { get; set; }

    public override void _Draw()
    {
        var rect = new Rect2(Vector2.Zero, Size);
        DrawRect(rect, new Color(0.83f, 0.76f, 0.56f));
        DrawRect(rect, new Color(0, 0, 0, 0.25f), false, 2f);
        Draw?.Invoke(this, rect.GetCenter(), Mathf.Min(Size.X, Size.Y) * 0.8f);
    }
}
