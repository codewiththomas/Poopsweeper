using Godot;

namespace Poopsweeper;

/// <summary>Hauptmenü: Kampagne, Schnelles Spiel, Kompendium.</summary>
public partial class HomeScreen : Control
{
    private Main _main = null!;

    public void Init(Main main) => _main = main;

    public override void _Ready()
    {
        var bg = new ColorRect { Color = UiTheme.Background };
        AddChild(bg);
        bg.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var center = new CenterContainer();
        AddChild(center);
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);

        var box = new VBoxContainer();
        box.AddThemeConstantOverride("separation", 8);
        center.AddChild(box);

        box.AddChild(new LogoRow { CustomMinimumSize = new Vector2(0, 100) });

        var title = new Label { Text = "Poopsweeper", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 60);
        title.AddThemeColorOverride("font_color", Icons.PoopLight);
        box.AddChild(title);

        var sub = new Label { Text = Loc.T("home.sub"), HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.8f));
        box.AddChild(sub);
        box.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        AddButton(box, Loc.T("home.campaign"), _main.OpenCampaign);
        AddButton(box, Loc.T("home.quick"), _main.ShowQuickSetup);
        AddButton(box, Loc.T("home.compendium"), _main.ShowCompendium);
        AddButton(box, Loc.T("home.settings"), _main.ShowSettings);
        if (OS.GetName() is "Windows" or "macOS" or "Linux")
            AddButton(box, Loc.T("home.quit"), () => GetTree().Quit());
    }

    private static void AddButton(Container parent, string text, System.Action action)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(420, 58), FocusMode = FocusModeEnum.None };
        b.AddThemeFontSizeOverride("font_size", 32);
        b.Pressed += () =>
        {
            Main.Instance.Audio.Play("click");
            action();
        };
        parent.AddChild(b);
    }
}

/// <summary>Kleines Logo: Haufen zwischen Tüte und Pfote.</summary>
public partial class LogoRow : Control
{
    public override void _Draw()
    {
        var c = Size / 2f;
        Icons.Bag(this, c + new Vector2(-110, 6), 74);
        Icons.Poop(this, c, 106);
        Icons.Paw(this, c + new Vector2(110, 6), 74, new Color(1f, 0.75f, 0.3f));
    }
}
