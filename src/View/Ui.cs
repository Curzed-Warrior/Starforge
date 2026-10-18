using Godot;

namespace Starforge.View;

/// <summary>Shared look for every HUD element: thin, quiet, translucent glass over the void.</summary>
public static class Ui
{
    public static readonly Color Text = new(0.86f, 0.9f, 0.95f);
    public static readonly Color Dim = new(0.56f, 0.64f, 0.72f);
    public static readonly Color Accent = new(0.56f, 0.82f, 1f);
    public static readonly Color Warm = new(1f, 0.72f, 0.42f);
    public static readonly Color Danger = new(1f, 0.45f, 0.38f);
    public static readonly Color God = new(0.78f, 0.66f, 1f);
    public static readonly Color Omen = new(0.75f, 0.62f, 0.95f);
    static Font _font, _fontBold;

    public static Font Font => _font ??= new SystemFont { FontNames = new[] { "Bahnschrift", "Segoe UI", "Arial" }, Antialiasing = TextServer.FontAntialiasing.Lcd, SubpixelPositioning = TextServer.SubpixelPositioning.Auto };
    public static Font Bold => _fontBold ??= new SystemFont { FontNames = new[] { "Bahnschrift SemiBold", "Bahnschrift", "Segoe UI Semibold" }, FontWeight = 600, Antialiasing = TextServer.FontAntialiasing.Lcd };

    public static Theme Theme()
    {
        var t = new Theme { DefaultFont = Font, DefaultFontSize = 15 };
        var btn = Box(new Color(0.07f, 0.1f, 0.14f, 0.7f), new Color(0.55f, 0.72f, 0.9f, 0.25f));
        var hov = Box(new Color(0.12f, 0.18f, 0.25f, 0.85f), new Color(0.6f, 0.85f, 1f, 0.6f));
        var prs = Box(new Color(0.2f, 0.3f, 0.42f, 0.9f), new Color(0.7f, 0.9f, 1f, 0.8f));
        var dis = Box(new Color(0.05f, 0.06f, 0.08f, 0.5f), new Color(0.4f, 0.45f, 0.5f, 0.12f));
        foreach (var (n, b) in new[] { ("normal", btn), ("hover", hov), ("pressed", prs), ("disabled", dis), ("focus", hov) })
            t.SetStylebox(n, "Button", b);
        t.SetColor("font_color", "Button", Text);
        t.SetColor("font_hover_color", "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color(0.6f, 0.65f, 0.7f));
        t.SetColor("font_color", "Label", Text);
        t.SetFontSize("font_size", "Button", 14);
        t.SetColor("default_color", "RichTextLabel", Text);
        t.SetFont("bold_font", "RichTextLabel", Bold);
        t.SetFontSize("normal_font_size", "RichTextLabel", 15);
        t.SetFontSize("bold_font_size", "RichTextLabel", 15);
        var tip = Box(new Color(0.04f, 0.06f, 0.09f, 0.95f), new Color(0.55f, 0.75f, 0.95f, 0.4f));
        t.SetStylebox("panel", "TooltipPanel", tip);
        t.SetColor("font_color", "TooltipLabel", Text);
        return t;
    }

    public static StyleBoxFlat Box(Color bg, Color border, int radius = 3, int pad = 8)
    {
        var s = new StyleBoxFlat { BgColor = bg, BorderColor = border, AntiAliasing = true };
        s.SetBorderWidthAll(1);
        s.SetCornerRadiusAll(radius);
        s.ContentMarginLeft = s.ContentMarginRight = pad + 2;
        s.ContentMarginTop = s.ContentMarginBottom = pad;
        return s;
    }

    public static PanelContainer Panel(float alpha = 0.55f)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", Box(new Color(0.03f, 0.05f, 0.08f, alpha), new Color(0.55f, 0.72f, 0.9f, 0.16f), 4, 10));
        return p;
    }

    public static Label Label(string text, int size = 15, Color? color = null, bool bold = false)
    {
        var l = new Label { Text = text };
        l.AddThemeFontSizeOverride("font_size", size);
        if (bold) l.AddThemeFontOverride("font", Bold);
        l.AddThemeColorOverride("font_color", color ?? Text);
        return l;
    }

    public static Button Button(string text, string tip = null)
    {
        var b = new Button { Text = text, TooltipText = tip ?? "", FocusMode = Control.FocusModeEnum.None };
        return b;
    }

    /// <summary>Pin a control to an anchor point of the screen; it grows away from the edge it is pinned to.</summary>
    public static void Pin(Control c, float ax, float ay, Vector2 offset)
    {
        c.AnchorLeft = c.AnchorRight = ax;
        c.AnchorTop = c.AnchorBottom = ay;
        c.OffsetLeft = c.OffsetRight = offset.X;
        c.OffsetTop = c.OffsetBottom = offset.Y;
        c.GrowHorizontal = ax > 0.75f ? Control.GrowDirection.Begin : ax > 0.25f ? Control.GrowDirection.Both : Control.GrowDirection.End;
        c.GrowVertical = ay > 0.75f ? Control.GrowDirection.Begin : ay > 0.25f ? Control.GrowDirection.Both : Control.GrowDirection.End;
    }

    public static Color KindColor(string kind) => kind switch
    {
        "era" => Accent, "break" => new Color(0.6f, 1f, 0.8f), "loss" => Danger, "god" => God, "omen" => Omen, "council" => Dim, _ => Text
    };

    public static string Hex(Color c) => c.ToHtml(false);
}
