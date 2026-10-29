using Godot;

namespace Poopsweeper;

public static class UiTheme
{
    public static readonly Color Background = new(0.09f, 0.20f, 0.11f);
    public static readonly Color PanelColor = new(0.14f, 0.30f, 0.17f);
    public static readonly Color Accent = new(0.95f, 0.72f, 0.20f);

    public static Theme Create()
    {
        var theme = new Theme { DefaultFontSize = 26 };
        theme.SetStylebox("normal", "Button", Box(new Color(0.22f, 0.45f, 0.25f)));
        theme.SetStylebox("hover", "Button", Box(new Color(0.28f, 0.54f, 0.31f)));
        theme.SetStylebox("pressed", "Button", Box(new Color(0.95f, 0.72f, 0.20f), new Color(0.4f, 0.3f, 0.05f)));
        theme.SetStylebox("hover_pressed", "Button", Box(new Color(1.0f, 0.78f, 0.28f), new Color(0.4f, 0.3f, 0.05f)));
        theme.SetStylebox("disabled", "Button", Box(new Color(0.16f, 0.26f, 0.18f)));
        theme.SetStylebox("focus", "Button", new StyleBoxEmpty());
        theme.SetColor("font_color", "Button", Colors.White);
        theme.SetColor("font_hover_color", "Button", Colors.White);
        theme.SetColor("font_pressed_color", "Button", new Color(0.15f, 0.10f, 0.0f));
        theme.SetColor("font_hover_pressed_color", "Button", new Color(0.15f, 0.10f, 0.0f));
        theme.SetColor("font_disabled_color", "Button", new Color(0.6f, 0.7f, 0.6f));
        theme.SetStylebox("panel", "PanelContainer", Box(PanelColor, new Color(0.05f, 0.12f, 0.06f), 3));
        return theme;
    }

    public static StyleBoxFlat Box(Color fill, Color? border = null, int borderWidth = 0)
    {
        var box = new StyleBoxFlat { BgColor = fill };
        box.SetCornerRadiusAll(14);
        box.ContentMarginLeft = box.ContentMarginRight = 22;
        box.ContentMarginTop = box.ContentMarginBottom = 12;
        if (border is Color c)
        {
            box.BorderColor = c;
            box.SetBorderWidthAll(borderWidth == 0 ? 3 : borderWidth);
        }
        return box;
    }
}
