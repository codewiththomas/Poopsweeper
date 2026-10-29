using Godot;
using Poopsweeper.Core;

namespace Poopsweeper;

public partial class LevelSelect : Control
{
    private Main _main = null!;
    private GridContainer _grid = null!;

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
        box.AddThemeConstantOverride("separation", 10);
        margin.AddChild(box);

        var title = new Label { Text = "Poopsweeper", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 64);
        title.AddThemeColorOverride("font_color", Icons.PoopLight);
        box.AddChild(title);

        int totalStars = 0;
        foreach (var r in _main.Save.Records.Values) totalStars += r.Stars;
        var sub = new Label
        {
            Text = $"Räume die Hundewiese – Sterne: {totalStars}",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        sub.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.8f));
        box.AddChild(sub);

        var scroll = new ScrollContainer
        {
            SizeFlagsVertical = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        box.AddChild(scroll);

        _grid = new GridContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        _grid.AddThemeConstantOverride("h_separation", 12);
        _grid.AddThemeConstantOverride("v_separation", 12);
        scroll.AddChild(_grid);

        int count = Mathf.Max(30, _main.Save.HighestUnlocked + 5);
        for (int level = 1; level <= count; level++)
        {
            var tile = new LevelTile
            {
                Level = level,
                Locked = !_main.Save.IsUnlocked(level),
                Stars = _main.Save.StarsFor(level),
                Boss = LevelCurve.IsBoss(level),
                CustomMinimumSize = new Vector2(110, 110),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            int captured = level;
            tile.Pressed += () => _main.StartLevel(captured);
            _grid.AddChild(tile);
        }

        Resized += UpdateColumns;
        UpdateColumns();
    }

    private void UpdateColumns() => _grid.Columns = Mathf.Max(2, (int)((Size.X - 48) / 122));
}

/// <summary>Kachel mit Levelnummer, Sternen, Schloss bzw. Boss-Markierung.</summary>
public partial class LevelTile : Button
{
    public int Level { get; set; }
    public bool Locked { get; set; }
    public int Stars { get; set; }
    public bool Boss { get; set; }

    public override void _Ready()
    {
        Flat = false;
        Disabled = Locked;
        FocusMode = FocusModeEnum.None;
        var normal = UiTheme.Box(Boss ? new Color(0.45f, 0.30f, 0.15f) : new Color(0.20f, 0.42f, 0.24f));
        var hover = UiTheme.Box(Boss ? new Color(0.55f, 0.38f, 0.20f) : new Color(0.28f, 0.54f, 0.31f));
        AddThemeStyleboxOverride("normal", normal);
        AddThemeStyleboxOverride("hover", hover);
        AddThemeStyleboxOverride("pressed", hover);
        AddThemeStyleboxOverride("disabled", UiTheme.Box(new Color(0.13f, 0.22f, 0.15f)));
    }

    public override void _Draw()
    {
        var font = ThemeDB.FallbackFont;
        var rect = new Rect2(Vector2.Zero, Size);
        var c = rect.GetCenter();

        if (Locked)
        {
            Icons.Lock(this, c, Size.X * 0.5f, new Color(0.45f, 0.55f, 0.45f));
            return;
        }

        const int fs = 40;
        string text = Level.ToString();
        DrawString(font, new Vector2(0, c.Y - 6), text, HorizontalAlignment.Center, Size.X, fs, Colors.White);

        for (int i = 0; i < 3; i++)
            Icons.Star(this, new Vector2(c.X + (i - 1) * Size.X * 0.26f, Size.Y - 24), Size.X * 0.10f, i < Stars);

        if (Boss) Icons.Poop(this, new Vector2(Size.X - 24, 24), 30);
    }
}
