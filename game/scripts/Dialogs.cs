using System;
using System.Collections.Generic;
using Godot;

namespace Poopsweeper;

public static class Dialogs
{
    /// <summary>Zeigt ein Panel mit Titel, Text, optionalen Sternen (stars &gt;= 0) und Buttons über dem Bildschirm.</summary>
    public static Control Show(Control parent, string title, string body, int stars, IEnumerable<(string text, Action action)> buttons)
    {
        var overlay = new Control();
        parent.AddChild(overlay);
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.55f), MouseFilter = Control.MouseFilterEnum.Stop };
        overlay.AddChild(dim);
        dim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var center = new CenterContainer();
        overlay.AddChild(center);
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

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
            var b = new Button { Text = text, FocusMode = Control.FocusModeEnum.None };
            b.Pressed += () =>
            {
                Main.Instance.Audio.Play("click");
                action();
            };
            box.AddChild(b);
        }
        return overlay;
    }
}
