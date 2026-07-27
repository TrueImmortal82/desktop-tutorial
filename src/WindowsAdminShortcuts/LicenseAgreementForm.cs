namespace WindowsAdminShortcuts;

internal sealed class LicenseAgreementForm : Form
{
    private readonly ModernButton _acceptButton;
    private readonly ModernButton _exitButton;
    private readonly LanguageSelectorControl _languageSelector = new();
    private readonly ThemeToggleButton _themeToggle = new();
    private readonly ToolTip _toolTip = new();
    private bool _updatingSelectors;

    internal LicenseAgreementForm(string licenseText)
    {
        Text = "Windows Admin Center";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(760, 580);
        Size = new Size(900, 700);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = AppIcon.Load();

        _acceptButton = UiLocalization.Attach(new ModernButton
        {
            Name = "AcceptLicense",
            Kind = ModernButtonKind.Primary,
            MinimumSize = new Size(150, 44),
            Enabled = false,
            DialogResult = DialogResult.OK
        }, "Принимаю", "Accept", "Qabul qilaman");
        _exitButton = UiLocalization.Attach(new ModernButton
        {
            Name = "ExitLicense",
            Kind = ModernButtonKind.Secondary,
            MinimumSize = new Size(120, 44),
            DialogResult = DialogResult.Cancel,
            Margin = new Padding(0, 0, 10, 0)
        }, "Выход", "Exit", "Chiqish");

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 20),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(UiLocalization.Attach(new Label
        {
            Name = "Warning",
            AutoSize = true,
            MaximumSize = new Size(820, 0),
            Padding = new Padding(12, 9, 12, 9),
            Margin = new Padding(0, 10, 0, 12)
        },
            "Программа предоставляется «КАК ЕСТЬ» (AS IS). Перед продолжением прочитайте условия полностью.",
            "The software is provided AS IS. Read the complete terms before continuing.",
            "Dastur AS IS («QANDAY BO‘LSA, SHUNDAY») taqdim etiladi. Davom etishdan oldin barcha shartlarni o‘qing."), 0, 1);

        var licenseBox = new TextBox
        {
            Name = "LicenseText",
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            WordWrap = true,
            Text = NormalizeLineEndings(licenseText),
            Margin = Padding.Empty
        };
        root.Controls.Add(licenseBox, 0, 2);

        var acceptance = UiLocalization.Attach(new CheckBox
        {
            Name = "LicenseAcceptance",
            AutoSize = true,
            Margin = new Padding(0, 14, 0, 10)
        },
            "Соглашение прочитано; условия AS IS приняты.",
            "The agreement has been read and the AS IS terms are accepted.",
            "Kelishuv o‘qildi va AS IS shartlari qabul qilindi.");
        acceptance.CheckedChanged += (_, _) => _acceptButton.Enabled = acceptance.Checked;
        root.Controls.Add(acceptance, 0, 3);

        var actions = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            Margin = Padding.Empty
        };
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        actions.Controls.Add(_exitButton, 1, 0);
        _acceptButton.Margin = Padding.Empty;
        actions.Controls.Add(_acceptButton, 2, 0);
        root.Controls.Add(actions, 0, 4);

        AcceptButton = _acceptButton;
        CancelButton = _exitButton;
        Controls.Add(root);

        _languageSelector.SelectedLanguageChanged += ChangeLanguage;
        _themeToggle.Click += ToggleTheme;
        AppSettingsService.Changed += AppearanceChanged;
        ApplyLocalizationAndTheme();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppSettingsService.Changed -= AppearanceChanged;
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 1,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Controls.Add(UiLocalization.Attach(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F),
            Margin = Padding.Empty
        }, "Лицензионное соглашение", "License agreement", "Litsenziya kelishuvi"), 0, 0);

        var selectors = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = Padding.Empty
        };
        _languageSelector.Name = "LicenseLanguageSelector";
        _languageSelector.Margin = new Padding(0, 1, 8, 0);
        selectors.Controls.Add(_languageSelector);
        selectors.Controls.Add(_themeToggle);
        header.Controls.Add(selectors, 1, 0);
        return header;
    }

    private void ChangeLanguage(object? sender, EventArgs e)
    {
        if (_updatingSelectors)
        {
            return;
        }

        try
        {
            AppSettingsService.SetLanguage(_languageSelector.SelectedLanguage);
        }
        catch (Exception ex)
        {
            ShowSettingsError(
                UiLocalization.Text(
                    "Не удалось сохранить язык интерфейса",
                    "Could not save the interface language",
                    "Interfeys tilini saqlab bo‘lmadi"),
                ex);
            ApplyLocalizationAndTheme();
        }
    }

    private void ToggleTheme(object? sender, EventArgs e)
    {
        try
        {
            AppSettingsService.SetTheme(
                AppSettingsService.Current.Theme == AppTheme.Light
                    ? AppTheme.Dark
                    : AppTheme.Light);
        }
        catch (Exception ex)
        {
            ShowSettingsError(
                UiLocalization.Text(
                    "Не удалось сохранить тему",
                    "Could not save the theme",
                    "Mavzuni saqlab bo‘lmadi"),
                ex);
        }
    }

    private void AppearanceChanged(object? sender, EventArgs e)
    {
        if (!IsDisposed)
        {
            ApplyLocalizationAndTheme();
        }
    }

    private void ApplyLocalizationAndTheme()
    {
        _updatingSelectors = true;
        try
        {
            _languageSelector.SelectedLanguage = AppSettingsService.Current.Language;
            UiLocalization.Apply(this);
            string themeTip = AppSettingsService.Current.Theme == AppTheme.Light
                ? UiLocalization.Text(
                    "Включить тёмную тему",
                    "Switch to dark theme",
                    "Qorong‘i mavzuni yoqish")
                : UiLocalization.Text(
                    "Включить светлую тему",
                    "Switch to light theme",
                    "Yorug‘ mavzuni yoqish");
            _themeToggle.AccessibleName = themeTip;
            _toolTip.SetToolTip(_themeToggle, themeTip);
        }
        finally
        {
            _updatingSelectors = false;
        }

        ThemeManager.Apply(this);
        PerformLayout();
    }

    private void ShowSettingsError(string message, Exception exception)
    {
        MessageBox.Show(
            this,
            $"{message}: {exception.Message}",
            "Windows Admin Center",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
    }

    private static string NormalizeLineEndings(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Replace("\n", Environment.NewLine, StringComparison.Ordinal);
    }
}
