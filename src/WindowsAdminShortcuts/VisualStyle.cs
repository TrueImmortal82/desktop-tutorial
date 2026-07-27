using System.Drawing.Drawing2D;

namespace WindowsAdminShortcuts;

internal sealed record ThemePalette(
    Color Background,
    Color Surface,
    Color SurfaceRaised,
    Color Text,
    Color Muted,
    Color Border,
    Color Accent,
    Color AccentHover,
    Color Success,
    Color Danger,
    Color WarningSurface,
    Color WarningText)
{
    internal static ThemePalette Current => AppSettingsService.Current.Theme == AppTheme.Dark
        ? Dark
        : Light;

    internal static ThemePalette Light { get; } = new(
        Color.FromArgb(245, 247, 251),
        Color.White,
        Color.FromArgb(249, 250, 252),
        Color.FromArgb(15, 23, 42),
        Color.FromArgb(92, 105, 124),
        Color.FromArgb(216, 224, 234),
        Color.FromArgb(37, 99, 235),
        Color.FromArgb(29, 78, 216),
        Color.FromArgb(21, 128, 61),
        Color.FromArgb(220, 38, 38),
        Color.FromArgb(255, 247, 237),
        Color.FromArgb(154, 52, 18));

    internal static ThemePalette Dark { get; } = new(
        Color.FromArgb(10, 16, 28),
        Color.FromArgb(17, 24, 39),
        Color.FromArgb(24, 34, 53),
        Color.FromArgb(241, 245, 249),
        Color.FromArgb(148, 163, 184),
        Color.FromArgb(43, 58, 80),
        Color.FromArgb(59, 130, 246),
        Color.FromArgb(96, 165, 250),
        Color.FromArgb(74, 222, 128),
        Color.FromArgb(248, 113, 113),
        Color.FromArgb(67, 43, 25),
        Color.FromArgb(253, 186, 116));
}

internal enum ModernButtonKind
{
    Primary,
    Secondary
}

internal class ModernButton : Button
{
    private bool _hovered;

    internal ModernButtonKind Kind { get; set; }

    internal ModernButton()
    {
        AutoSize = true;
        MinimumSize = new Size(0, 42);
        Padding = new Padding(16, 4, 16, 4);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        UseVisualStyleBackColor = false;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hovered = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hovered = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ThemePalette palette = ThemePalette.Current;
        Color fill = Kind == ModernButtonKind.Primary
            ? (_hovered ? palette.AccentHover : palette.Accent)
            : (_hovered ? palette.SurfaceRaised : palette.Surface);
        Color text = Kind == ModernButtonKind.Primary ? Color.White : palette.Text;

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? palette.Background);
        Rectangle bounds = ClientRectangle;
        bounds.Width--;
        bounds.Height--;
        using GraphicsPath path = RoundedRectangle(bounds, 9);
        using var brush = new SolidBrush(Enabled ? fill : palette.SurfaceRaised);
        using var pen = new Pen(Kind == ModernButtonKind.Primary ? fill : palette.Border);
        e.Graphics.FillPath(brush, path);
        e.Graphics.DrawPath(pen, path);

        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            bounds,
            Enabled ? text : palette.Muted,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);

        if (Focused && ShowFocusCues)
        {
            Rectangle focus = Rectangle.Inflate(bounds, -5, -5);
            ControlPaint.DrawFocusRectangle(e.Graphics, focus, text, fill);
        }
    }

    internal static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}

internal sealed class LanguageSelectorControl : FlowLayoutPanel
{
    private readonly Dictionary<AppLanguage, ModernButton> _buttons = new();
    private AppLanguage _selectedLanguage;

    internal event EventHandler? SelectedLanguageChanged;

    internal AppLanguage SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }

            if (_selectedLanguage == value)
            {
                UpdateSelection();
                return;
            }

            _selectedLanguage = value;
            UpdateSelection();
            SelectedLanguageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    internal LanguageSelectorControl()
    {
        Name = "LanguageSelector";
        AutoSize = true;
        FlowDirection = FlowDirection.LeftToRight;
        WrapContents = false;
        Margin = Padding.Empty;
        Padding = Padding.Empty;

        AddButton(AppLanguage.Russian, "RU");
        AddButton(AppLanguage.English, "EN");
        AddButton(AppLanguage.Uzbek, "UZ");
        _selectedLanguage = AppSettingsService.Current.Language;
        UpdateSelection();
    }

    private void AddButton(AppLanguage language, string text)
    {
        var button = new ModernButton
        {
            Name = $"Language{language}",
            Text = text,
            AccessibleName = text,
            AutoSize = false,
            Size = new Size(42, 40),
            MinimumSize = new Size(42, 40),
            MaximumSize = new Size(42, 40),
            Padding = Padding.Empty,
            Margin = new Padding(0, 0, 5, 0)
        };
        button.Click += (_, _) => SelectedLanguage = language;
        _buttons.Add(language, button);
        Controls.Add(button);
    }

    private void UpdateSelection()
    {
        foreach ((AppLanguage language, ModernButton button) in _buttons)
        {
            button.Kind = language == _selectedLanguage
                ? ModernButtonKind.Primary
                : ModernButtonKind.Secondary;
            button.Invalidate();
        }
    }
}

internal sealed class ThemeToggleButton : Button
{
    internal ThemeToggleButton()
    {
        Name = "ThemeToggle";
        AccessibleName = "Theme";
        Size = new Size(46, 40);
        MinimumSize = Size;
        MaximumSize = Size;
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        Cursor = Cursors.Hand;
        TabStop = true;
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ThemePalette palette = ThemePalette.Current;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.Clear(Parent?.BackColor ?? palette.Background);
        Rectangle bounds = Rectangle.Inflate(ClientRectangle, -1, -1);
        bounds.Width--;
        bounds.Height--;
        using GraphicsPath path = ModernButton.RoundedRectangle(bounds, 10);
        using var fill = new SolidBrush(palette.Surface);
        using var border = new Pen(palette.Border);
        e.Graphics.FillPath(fill, path);
        e.Graphics.DrawPath(border, path);

        Color glyphColor = palette.Text;
        using var glyphPen = new Pen(glyphColor, 2F)
        {
            StartCap = LineCap.Round,
            EndCap = LineCap.Round
        };
        if (AppSettingsService.Current.Theme == AppTheme.Dark)
        {
            DrawSun(e.Graphics, glyphPen);
        }
        else
        {
            DrawMoon(e.Graphics, glyphColor, palette.Surface);
        }
    }

    private void DrawSun(Graphics graphics, Pen pen)
    {
        var center = new Point(Width / 2, Height / 2);
        graphics.DrawEllipse(pen, center.X - 5, center.Y - 5, 10, 10);
        for (int angle = 0; angle < 360; angle += 45)
        {
            double radians = Math.PI * angle / 180D;
            Point start = new(
                center.X + (int)Math.Round(Math.Cos(radians) * 9),
                center.Y + (int)Math.Round(Math.Sin(radians) * 9));
            Point end = new(
                center.X + (int)Math.Round(Math.Cos(radians) * 12),
                center.Y + (int)Math.Round(Math.Sin(radians) * 12));
            graphics.DrawLine(pen, start, end);
        }
    }

    private void DrawMoon(Graphics graphics, Color glyph, Color cutout)
    {
        using var moonBrush = new SolidBrush(glyph);
        graphics.FillEllipse(moonBrush, Width / 2 - 8, Height / 2 - 9, 18, 18);
        using var cutoutBrush = new SolidBrush(cutout);
        graphics.FillEllipse(cutoutBrush, Width / 2 - 2, Height / 2 - 12, 18, 18);
    }
}

internal sealed class PremiumTabControl : TabControl
{
    internal PremiumTabControl()
    {
        DrawMode = TabDrawMode.OwnerDrawFixed;
        SizeMode = TabSizeMode.Normal;
        ItemSize = new Size(150, 34);
        Padding = new Point(18, 7);
        SetStyle(
            ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw,
            true);
    }

    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        DrawTab(e.Graphics, e.Index);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        ThemePalette palette = ThemePalette.Current;
        e.Graphics.Clear(palette.Background);
        using var border = new Pen(palette.Border);
        Rectangle pageBounds = DisplayRectangle;
        pageBounds.Width--;
        pageBounds.Height--;
        e.Graphics.DrawRectangle(border, pageBounds);
        for (int index = 0; index < TabPages.Count; index++)
        {
            DrawTab(e.Graphics, index);
        }
    }

    private void DrawTab(Graphics graphics, int index)
    {
        ThemePalette palette = ThemePalette.Current;
        bool selected = index == SelectedIndex;
        Rectangle bounds = GetTabRect(index);
        using var background = new SolidBrush(selected ? palette.Surface : palette.Background);
        graphics.FillRectangle(background, bounds);
        TextRenderer.DrawText(
            graphics,
            TabPages[index].Text,
            Font,
            bounds,
            selected ? palette.Accent : palette.Muted,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.EndEllipsis);
        if (selected)
        {
            using var accent = new SolidBrush(palette.Accent);
            graphics.FillRectangle(accent, bounds.Left + 10, bounds.Bottom - 3, bounds.Width - 20, 3);
        }
    }
}

internal static class ThemeManager
{
    internal static void Apply(Control root)
    {
        ThemePalette palette = ThemePalette.Current;
        ApplyRecursive(root, palette);
        root.Invalidate(invalidateChildren: true);
    }

    private static void ApplyRecursive(Control control, ThemePalette palette)
    {
        control.ForeColor = palette.Text;
        control.BackColor = control switch
        {
            Form => palette.Background,
            TabPage => palette.Background,
            PremiumTabControl => palette.Background,
            TextBox => palette.Surface,
            ComboBox => palette.Surface,
            CheckedListBox => palette.Surface,
            PictureBox => palette.SurfaceRaised,
            _ when control.Name == "SurfaceCard" => palette.Surface,
            _ when control.Name == "StatusBar" => palette.Surface,
            _ => control.Parent?.BackColor ?? palette.Background
        };

        if (control is Label label)
        {
            label.ForeColor = label.Name switch
            {
                "Muted" => palette.Muted,
                "Success" => palette.Success,
                "Warning" => palette.WarningText,
                "Error" => palette.Danger,
                _ => palette.Text
            };
            if (label.Name == "Warning")
            {
                label.BackColor = palette.WarningSurface;
            }
        }
        else if (control is GroupBox group)
        {
            group.ForeColor = palette.Text;
        }
        else if (control is ModernButton or ThemeToggleButton or PremiumTabControl)
        {
            control.Invalidate();
        }

        foreach (Control child in control.Controls)
        {
            ApplyRecursive(child, palette);
        }
    }
}
