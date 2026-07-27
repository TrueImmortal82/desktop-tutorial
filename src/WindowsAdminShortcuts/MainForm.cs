using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.VisualBasic.FileIO;

namespace WindowsAdminShortcuts;

internal sealed class MainForm : Form
{
    private readonly CheckedListBox _shortcutList = new();
    private readonly TextBox _shortcutSearch = new();
    private readonly ComboBox _shortcutCategory = new();
    private readonly ComboBox _presetScope = CreateScopeCombo();
    private readonly ComboBox _customScope = CreateScopeCombo();
    private readonly ComboBox _winPScope = CreateScopeCombo();
    private readonly LanguageSelectorControl _languageSelector = new();
    private readonly ThemeToggleButton _themeToggle = new();
    private readonly ToolTip _toolTip = new();
    private readonly TextBox _customName = new();
    private readonly TextBox _customTarget = new();
    private readonly TextBox _customArguments = new();
    private readonly CheckBox _customRunAsAdministrator = new()
    {
        AutoSize = true,
        Checked = true
    };
    private readonly TextBox _wallpaperPath = new();
    private readonly ComboBox _wallpaperLayout = new();
    private readonly PictureBox _wallpaperPreview = new();
    private readonly Label _wallpaperResult = new();
    private readonly Label _statusLabel = new();
    private readonly Button _applyWallpaperButton;
    private readonly IReadOnlyList<ShortcutDefinition> _shortcuts;
    private readonly IReadOnlyList<string> _shortcutCategories;
    private readonly HashSet<string> _checkedShortcutFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _updatingShortcutList;
    private bool _updatingAppearanceSelectors;
    private bool _statusIsError;

    public MainForm()
    {
        Text = "Windows Admin Center";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 700);
        Size = new Size(1120, 820);
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;
        Icon = AppIcon.Load();

        _shortcuts = AdminShortcutCatalog.Create();
        _shortcutCategories = _shortcuts
            .Select(shortcut => shortcut.Category)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (ShortcutDefinition shortcut in _shortcuts)
        {
            _checkedShortcutFiles.Add(shortcut.FileName);
        }
        _applyWallpaperButton = CreateButton(
            "Применить для всех пользователей",
            "Apply for all users",
            "Barcha foydalanuvchilarga qo‘llash",
            ApplyWallpaperAsync,
            primary: true);

        InitializeSelectors();
        BuildInterface();
        ApplyLocalizationAndTheme();
        AppSettingsService.Changed += AppearanceChanged;
        RefreshShortcutState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            AppSettingsService.Changed -= AppearanceChanged;
            _wallpaperPreview.Image?.Dispose();
            _toolTip.Dispose();
        }

        base.Dispose(disposing);
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24, 20, 24, 18),
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        root.Controls.Add(BuildHeader(), 0, 0);

        var tabs = new PremiumTabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            Margin = new Padding(0, 14, 0, 12)
        };
        tabs.TabPages.Add(BuildShortcutsTab());
        tabs.TabPages.Add(BuildWallpaperTab());
        tabs.TabPages.Add(BuildWinPTab());
        root.Controls.Add(tabs, 0, 1);

        var statusPanel = new Panel
        {
            Name = "StatusBar",
            Dock = DockStyle.Fill,
            Height = 36,
            Padding = new Padding(12, 8, 12, 8)
        };
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Name = "Success";
        UiLocalization.Attach(_statusLabel, "Готово к работе.", "Ready.", "Ishga tayyor.");
        statusPanel.Controls.Add(_statusLabel);
        root.Controls.Add(statusPanel, 0, 2);

        Controls.Add(root);
    }

    private Control BuildHeader()
    {
        var header = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            RowCount = 2,
            Margin = Padding.Empty
        };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var title = UiLocalization.Attach(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F),
            Margin = Padding.Empty
        }, "Windows Admin Center", "Windows Admin Center", "Windows Admin Center");
        header.Controls.Add(title, 0, 0);

        var headerActions = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Margin = Padding.Empty
        };
        _languageSelector.Margin = new Padding(0, 1, 3, 0);
        _languageSelector.SelectedLanguageChanged += ChangeLanguage;
        headerActions.Controls.Add(_languageSelector);
        _themeToggle.Margin = new Padding(0, 0, 10, 0);
        _themeToggle.Click += ToggleTheme;
        headerActions.Controls.Add(_themeToggle);

        var adminBadge = UiLocalization.Attach(new Label
        {
            Name = ElevationService.IsAdministrator() ? "Success" : "Warning",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6),
            Margin = Padding.Empty
        },
            ElevationService.IsAdministrator() ? "Администратор" : "Без прав администратора",
            ElevationService.IsAdministrator() ? "Administrator" : "Not elevated",
            ElevationService.IsAdministrator() ? "Administrator" : "Administrator huquqisiz");
        headerActions.Controls.Add(adminBadge);
        header.Controls.Add(headerActions, 1, 0);

        var subtitle = UiLocalization.Attach(new Label
        {
            Name = "Muted",
            AutoSize = true,
            Margin = new Padding(0, 5, 0, 0)
        },
            "Ярлыки, общие обои и Win+P — в одном проверяемом окне.",
            "Admin shortcuts, shared wallpaper and Win+P in one reliable window.",
            "Administrator yorliqlari, umumiy fon va Win+P — bitta ishonchli oynada.");
        header.Controls.Add(subtitle, 0, 1);
        header.SetColumnSpan(subtitle, 2);
        return header;
    }

    private TabPage BuildShortcutsTab()
    {
        var tab = CreateTab("Ярлыки", "Shortcuts", "Yorliqlar");
        var sections = new PremiumTabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9.5F)
        };

        var catalogPage = CreateTab(
            $"Готовые ярлыки ({_shortcuts.Count})",
            $"Admin catalog ({_shortcuts.Count})",
            $"Administrator katalogi ({_shortcuts.Count})");
        catalogPage.Controls.Add(BuildStandardShortcutsPanel());
        sections.TabPages.Add(catalogPage);

        var customPage = CreateTab("Свой ярлык", "Custom shortcut", "Shaxsiy yorliq");
        customPage.Controls.Add(BuildCustomShortcutPanel());
        sections.TabPages.Add(customPage);

        tab.Controls.Add(sections);
        return tab;
    }

    private Control BuildStandardShortcutsPanel()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16, 14, 16, 14)
        };
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var filters = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = Padding.Empty
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44));

        var searchFilter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 12, 0)
        };
        searchFilter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        searchFilter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        searchFilter.Controls.Add(
            CreateFieldLabel("Поиск:", "Search:", "Qidirish:"),
            0,
            0);
        _shortcutSearch.Name = "ShortcutSearch";
        _shortcutSearch.Dock = DockStyle.Fill;
        _shortcutSearch.Margin = new Padding(6, 3, 0, 5);
        _shortcutSearch.TextChanged += (_, _) => ApplyShortcutFilter();
        searchFilter.Controls.Add(_shortcutSearch, 1, 0);
        filters.Controls.Add(searchFilter, 0, 0);

        var categoryFilter = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = Padding.Empty
        };
        categoryFilter.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        categoryFilter.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        categoryFilter.Controls.Add(
            CreateFieldLabel("Категория:", "Category:", "Toifa:"),
            0,
            0);
        _shortcutCategory.Name = "ShortcutCategory";
        _shortcutCategory.Dock = DockStyle.Fill;
        _shortcutCategory.AutoSize = false;
        _shortcutCategory.DropDownStyle = ComboBoxStyle.DropDownList;
        _shortcutCategory.Margin = new Padding(6, 3, 0, 5);
        _shortcutCategory.SelectedIndexChanged += (_, _) => ApplyShortcutFilter();
        categoryFilter.Controls.Add(_shortcutCategory, 1, 0);
        filters.Controls.Add(categoryFilter, 1, 0);
        layout.Controls.Add(filters, 0, 0);

        _shortcutList.Name = "ShortcutCatalogList";
        _shortcutList.Dock = DockStyle.Fill;
        _shortcutList.CheckOnClick = true;
        _shortcutList.IntegralHeight = false;
        _shortcutList.HorizontalScrollbar = true;
        _shortcutList.ScrollAlwaysVisible = true;
        _shortcutList.BorderStyle = BorderStyle.FixedSingle;
        _shortcutList.Margin = new Padding(0, 8, 0, 10);
        _shortcutList.ItemCheck += ShortcutListItemCheck;
        layout.Controls.Add(_shortcutList, 0, 1);
        ApplyShortcutFilter();

        var scopePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        scopePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        scopePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        scopePanel.Controls.Add(CreateFieldLabel("Рабочий стол:", "Desktop:", "Ish stoli:"), 0, 0);
        _presetScope.Dock = DockStyle.Fill;
        _presetScope.Margin = new Padding(8, 3, 0, 3);
        _presetScope.SelectedIndexChanged += (_, _) => RefreshShortcutState();
        scopePanel.Controls.Add(_presetScope, 1, 0);
        layout.Controls.Add(scopePanel, 0, 2);

        var buttons = CreateActionGrid(5);
        buttons.Controls.Add(CreateGridButton(
            "Создать выбранные", "Create selected", "Tanlanganlarni yaratish",
            CreateSelectedShortcuts, primary: true), 0, 0);
        buttons.Controls.Add(CreateGridButton(
            "Удалить выбранные", "Remove selected", "Tanlanganlarni o‘chirish",
            RemoveSelectedShortcuts), 1, 0);
        buttons.Controls.Add(CreateGridButton(
            "Выбрать показанные", "Select visible", "Ko‘rinadiganlarni tanlash",
            (_, _) => SetAllChecked(true)), 2, 0);
        buttons.Controls.Add(CreateGridButton(
            "Снять показанные", "Clear visible", "Ko‘rinadiganlarni bekor qilish",
            (_, _) => SetAllChecked(false)), 3, 0);
        buttons.Controls.Add(
            CreateGridButton(
                "Открыть рабочий стол", "Open desktop", "Ish stolini ochish",
                (_, _) => OpenDesktop(GetScope(_presetScope))),
            4,
            0);
        layout.Controls.Add(buttons, 0, 3);

        host.Controls.Add(layout);
        return host;
    }

    private Control BuildCustomShortcutPanel()
    {
        var host = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(20)
        };
        var group = CreateGroup(
            "Добавить свой ярлык",
            "Create a custom shortcut",
            "Shaxsiy yorliq yaratish");
        group.Dock = DockStyle.Top;
        group.Height = 430;
        group.Margin = Padding.Empty;
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            RowCount = 6
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _customName.Name = "CustomShortcutName";
        _customName.Dock = DockStyle.Fill;
        layout.Controls.Add(BuildLabeledControl(
            "Название:", "Name:", "Nomi:", _customName), 0, 0);

        var targetPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(4)
        };
        targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        targetPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _customTarget.Dock = DockStyle.Fill;
        targetPanel.Controls.Add(_customTarget, 0, 0);
        targetPanel.Controls.Add(CreateButton(
            "Обзор…", "Browse…", "Tanlash…", BrowseShortcutTarget), 1, 0);
        layout.Controls.Add(BuildLabeledControl(
            "Цель:", "Target:", "Maqsad:", targetPanel), 0, 1);
        _customArguments.Name = "CustomShortcutArguments";
        _customArguments.Dock = DockStyle.Fill;
        layout.Controls.Add(BuildLabeledControl(
            "Аргументы:", "Arguments:", "Argumentlar:", _customArguments), 0, 2);
        layout.Controls.Add(BuildLabeledControl(
            "Рабочий стол:", "Desktop:", "Ish stoli:", _customScope), 0, 3);
        _customRunAsAdministrator.Margin = new Padding(0, 8, 0, 8);
        layout.Controls.Add(_customRunAsAdministrator, 0, 4);

        var action = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 8, 0, 0)
        };
        action.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        action.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        action.Controls.Add(new Panel { Dock = DockStyle.Fill }, 0, 0);
        action.Controls.Add(CreateButton(
            "Создать ярлык", "Create shortcut", "Yorliq yaratish",
            CreateCustomShortcut, primary: true), 1, 0);
        layout.Controls.Add(action, 0, 5);

        group.Controls.Add(layout);
        host.Controls.Add(group);
        return host;
    }

    private TabPage BuildWallpaperTab()
    {
        var tab = CreateTab(
            "Обои для всех",
            "Shared wallpaper",
            "Umumiy fon rasmi");
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(20),
            ColumnCount = 2,
            RowCount = 1
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        var controls = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 7,
            Padding = new Padding(0, 0, 18, 0)
        };
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        controls.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        controls.Controls.Add(CreateSectionTitle(
            "Общие обои Windows",
            "Windows wallpaper for every user",
            "Barcha foydalanuvchilar uchun Windows fon rasmi"), 0, 0);
        controls.Controls.Add(UiLocalization.Attach(new Label
        {
            Name = "Muted",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            Margin = new Padding(0, 6, 0, 18)
        },
            "Изображение копируется в ProgramData, затем настройки применяются ко всем существующим профилям и профилю новых пользователей. Перед изменением создаётся backup реестровых значений.",
            "The image is copied to ProgramData and applied to existing profiles and the default profile. Registry values are backed up before any change.",
            "Rasm ProgramData ichiga nusxalanadi va mavjud hamda yangi foydalanuvchi profillariga qo‘llanadi. O‘zgarishdan oldin reyestr qiymatlari zaxiralanadi."), 0, 1);

        var filePanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 12)
        };
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        filePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _wallpaperPath.Dock = DockStyle.Fill;
        filePanel.Controls.Add(_wallpaperPath, 0, 0);
        filePanel.Controls.Add(CreateButton(
            "Выбрать изображение…", "Choose image…", "Rasmni tanlash…",
            BrowseWallpaper), 1, 0);
        controls.Controls.Add(BuildLabeledControl(
            "Файл обоев:", "Wallpaper file:", "Fon rasmi fayli:", filePanel), 0, 2);

        _wallpaperLayout.DropDownStyle = ComboBoxStyle.DropDownList;
        _wallpaperLayout.Width = 220;
        controls.Controls.Add(BuildLabeledControl(
            "Расположение:", "Layout:", "Joylashuvi:", _wallpaperLayout), 0, 3);

        _applyWallpaperButton.Margin = new Padding(0, 18, 0, 10);
        controls.Controls.Add(_applyWallpaperButton, 0, 4);

        _wallpaperResult.AutoSize = true;
        _wallpaperResult.MaximumSize = new Size(520, 0);
        _wallpaperResult.Name = "Muted";
        controls.Controls.Add(_wallpaperResult, 0, 5);

        _wallpaperPreview.Dock = DockStyle.Fill;
        _wallpaperPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _wallpaperPreview.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(controls, 0, 0);
        layout.Controls.Add(_wallpaperPreview, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildWinPTab()
    {
        var tab = CreateTab("Win+P", "Win+P", "Win+P");
        var card = new TableLayoutPanel
        {
            Name = "SurfaceCard",
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Padding = new Padding(26),
            ColumnCount = 1,
            RowCount = 5
        };
        card.Controls.Add(CreateSectionTitle(
            "Дублирование экранов",
            "Duplicate displays",
            "Ekranlarni takrorlash"), 0, 0);
        card.Controls.Add(UiLocalization.Attach(new Label
        {
            Name = "Muted",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            Margin = new Padding(0, 8, 0, 20)
        },
            "Создаёт Win+P.cmd, который сразу включает режим «Дублировать» штатной командой DisplaySwitch.exe /clone. Панель выбора режима не открывается. Скрипт записывается с CRLF и проверяется после сохранения.",
            "Creates Win+P.cmd that immediately selects Duplicate with DisplaySwitch.exe /clone. No projection menu is opened; the CRLF script is verified after writing.",
            "DisplaySwitch.exe /clone orqali darhol Takrorlash rejimini yoqadigan Win+P.cmd yaratiladi. Proyeksiya menyusi ochilmaydi; CRLF skripti yozilgach tekshiriladi."), 0, 1);
        card.Controls.Add(BuildLabeledControl(
            "Рабочий стол:", "Desktop:", "Ish stoli:", _winPScope), 0, 2);

        var actions = CreateButtonRow();
        actions.Margin = new Padding(0, 18, 0, 0);
        actions.Controls.Add(CreateButton(
            "Создать «Дублировать»", "Create Duplicate launcher", "Takrorlash yorlig‘ini yaratish",
            CreateWinPLauncher, primary: true));
        actions.Controls.Add(CreateButton(
            "Открыть рабочий стол", "Open desktop", "Ish stolini ochish",
            (_, _) => OpenDesktop(GetScope(_winPScope))));
        card.Controls.Add(actions, 0, 3);

        var host = new Panel { Dock = DockStyle.Fill, Padding = new Padding(24), AutoScroll = true };
        card.Dock = DockStyle.Top;
        host.Controls.Add(card);
        tab.Controls.Add(host);
        return tab;
    }

    private void CreateSelectedShortcuts(object? sender, EventArgs e)
    {
        List<ShortcutDefinition> selected = GetSelectedShortcuts();
        if (selected.Count == 0)
        {
            SetStatus(T("Ничего не выбрано.", "Nothing is selected.", "Hech narsa tanlanmagan."), isError: true);
            return;
        }

        DesktopScope scope = GetScope(_presetScope);
        List<string> existing = selected
            .Select(shortcut => Path.Combine(
                DesktopPathProvider.GetPath(scope),
                ShortcutService.NormalizeShortcutFileName(shortcut.FileName)))
            .Where(File.Exists)
            .ToList();
        if (existing.Count > 0 && !ConfirmOverwrite(existing.Count))
        {
            return;
        }

        int created = 0;
        int backups = 0;
        var errors = new List<string>();
        foreach (ShortcutDefinition shortcut in selected)
        {
            try
            {
                ShortcutCreateResult result = ShortcutService.Create(shortcut, scope);
                if (result.BackupPath is not null)
                {
                    backups++;
                }
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"{UiLocalization.CatalogText(shortcut.DisplayName)}: {ex.Message}");
            }
        }

        RefreshDesktop();
        RefreshShortcutState();
        ShowOperationResult(
            T("Создано ярлыков", "Shortcuts created", "Yaratilgan yorliqlar"),
            created,
            errors,
            backups);
    }

    private void RemoveSelectedShortcuts(object? sender, EventArgs e)
    {
        List<ShortcutDefinition> selected = GetSelectedShortcuts();
        if (selected.Count == 0)
        {
            SetStatus(T("Ничего не выбрано.", "Nothing is selected.", "Hech narsa tanlanmagan."), isError: true);
            return;
        }

        if (MessageBox.Show(
                this,
                T(
                    $"Удалить выбранные ярлыки ({selected.Count})?",
                    $"Remove the selected shortcuts ({selected.Count})?",
                    $"Tanlangan yorliqlar o‘chirilsinmi ({selected.Count})?"),
                T("Подтверждение удаления", "Confirm removal", "O‘chirishni tasdiqlash"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        DesktopScope scope = GetScope(_presetScope);
        int removed = 0;
        var errors = new List<string>();
        foreach (ShortcutDefinition shortcut in selected)
        {
            try
            {
                string path = Path.Combine(
                    DesktopPathProvider.GetPath(scope),
                    ShortcutService.NormalizeShortcutFileName(shortcut.FileName));
                if (File.Exists(path))
                {
                    FileSystem.DeleteFile(
                        path,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                    removed++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{UiLocalization.CatalogText(shortcut.DisplayName)}: {ex.Message}");
            }
        }

        RefreshDesktop();
        RefreshShortcutState();
        ShowOperationResult(
            T("Удалено ярлыков", "Shortcuts removed", "O‘chirilgan yorliqlar"),
            removed,
            errors);
    }

    private void CreateCustomShortcut(object? sender, EventArgs e)
    {
        try
        {
            DesktopScope scope = GetScope(_customScope);
            string destination = ShortcutService.GetDestinationPath(_customName.Text, scope);
            if (File.Exists(destination) && !ConfirmOverwrite(1))
            {
                return;
            }

            ShortcutCreateResult result = ShortcutService.CreateCustom(
                _customName.Text,
                _customTarget.Text,
                _customArguments.Text,
                _customRunAsAdministrator.Checked,
                scope);
            RefreshDesktop();
            string backup = result.BackupPath is null
                ? string.Empty
                : $" {T("Backup исходного файла", "Original file backup", "Asl fayl zaxirasi")}: {result.BackupPath}";
            SetStatus(
                $"{T("Ярлык создан", "Shortcut created", "Yorliq yaratildi")}: {result.Path}.{backup}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private async void ApplyWallpaperAsync(object? sender, EventArgs e)
    {
        string path = _wallpaperPath.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            ShowError(T(
                "Сначала выберите файл обоев.",
                "Choose a wallpaper image first.",
                "Avval fon rasmi faylini tanlang."));
            return;
        }

        if (MessageBox.Show(
                this,
                T(
                    "Настройки обоев будут изменены для всех существующих и новых пользователей. Перед изменением будет создан backup. Продолжить?",
                    "Wallpaper settings will change for all existing and new users. A backup will be created first. Continue?",
                    "Fon rasmi sozlamalari barcha mavjud va yangi foydalanuvchilar uchun o‘zgaradi. Avval zaxira yaratiladi. Davom etilsinmi?"),
                T("Применить общие обои", "Apply shared wallpaper", "Umumiy fon rasmini qo‘llash"),
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        _applyWallpaperButton.Enabled = false;
        SetStatus(T(
            "Настройки профилей применяются…",
            "Applying profile settings…",
            "Profil sozlamalari qo‘llanmoqda…"));
        try
        {
            WallpaperLayout layout = GetWallpaperLayout();
            WallpaperApplyResult result = await Task.Run(
                () => WallpaperService.ApplyToAllUsers(path, layout));

            string skipped = result.SkippedProfiles.Count == 0
                ? string.Empty
                : $"\n{T("Пропущено записей без NTUSER.DAT", "Profiles without NTUSER.DAT skipped", "NTUSER.DAT yo‘q profillar o‘tkazib yuborildi")}: {result.SkippedProfiles.Count}.";
            _wallpaperResult.Text =
                $"{T("Настроено профилей", "Profiles configured", "Sozlangan profillar")}: {result.ProfileCount}.\n" +
                $"{T("Файл", "File", "Fayl")}: {result.ManagedWallpaperPath}\n" +
                $"{T("Backup", "Backup", "Zaxira")}: {result.BackupPath}{skipped}";
            SetStatus(
                $"{T("Обои применены. Настроено профилей", "Wallpaper applied. Profiles configured", "Fon rasmi qo‘llandi. Sozlangan profillar")}: {result.ProfileCount}.");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            _applyWallpaperButton.Enabled = true;
        }
    }

    private void CreateWinPLauncher(object? sender, EventArgs e)
    {
        try
        {
            DesktopScope scope = GetScope(_winPScope);
            string destination = Path.Combine(DesktopPathProvider.GetPath(scope), WinPLauncherService.FileName);
            bool overwrite = false;
            if (File.Exists(destination))
            {
                if (!ConfirmOverwrite(1))
                {
                    return;
                }

                overwrite = true;
            }

            LauncherCreateResult result = WinPLauncherService.Create(scope, overwrite);
            RefreshDesktop();
            string backup = result.BackupPath is null
                ? string.Empty
                : $" {T("Backup", "Backup", "Zaxira")}: {result.BackupPath}";
            SetStatus(
                $"{T("Скрипт дублирования экранов создан", "Duplicate-display script created", "Ekranni takrorlash skripti yaratildi")}: {result.Path}.{backup}");
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void BrowseShortcutTarget(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = T(
                "Выберите программу или файл",
                "Choose a program or file",
                "Dastur yoki faylni tanlang"),
            CheckFileExists = true,
            Filter = T(
                "Программы и файлы|*.exe;*.com;*.bat;*.cmd;*.msc;*.cpl;*.ps1;*.url|Все файлы|*.*",
                "Programs and files|*.exe;*.com;*.bat;*.cmd;*.msc;*.cpl;*.ps1;*.url|All files|*.*",
                "Dasturlar va fayllar|*.exe;*.com;*.bat;*.cmd;*.msc;*.cpl;*.ps1;*.url|Barcha fayllar|*.*")
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _customTarget.Text = dialog.FileName;
            if (string.IsNullOrWhiteSpace(_customName.Text))
            {
                _customName.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void BrowseWallpaper(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = T("Выберите изображение", "Choose an image", "Rasmni tanlang"),
            CheckFileExists = true,
            Filter = T(
                "Изображения|*.jpg;*.jpeg;*.png;*.bmp|Все файлы|*.*",
                "Images|*.jpg;*.jpeg;*.png;*.bmp|All files|*.*",
                "Rasmlar|*.jpg;*.jpeg;*.png;*.bmp|Barcha fayllar|*.*")
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            using Image source = Image.FromFile(dialog.FileName);
            var preview = new Bitmap(source);
            Image? previous = _wallpaperPreview.Image;
            _wallpaperPreview.Image = preview;
            previous?.Dispose();
            _wallpaperPath.Text = dialog.FileName;
            SetStatus(T(
                "Изображение выбрано. Общесистемные настройки ещё не изменены.",
                "Image selected. System-wide settings have not changed yet.",
                "Rasm tanlandi. Tizim sozlamalari hali o‘zgartirilmadi."));
        }
        catch (Exception ex)
        {
            ShowError(
                $"{T("Не удалось открыть изображение", "Could not open the image", "Rasmni ochib bo‘lmadi")}: {ex.Message}");
        }
    }

    private void RefreshShortcutState()
    {
        try
        {
            DesktopScope scope = GetScope(_presetScope);
            string desktop = DesktopPathProvider.GetPath(scope);
            int existing = _shortcuts.Count(
                shortcut => File.Exists(Path.Combine(desktop, ShortcutService.NormalizeShortcutFileName(shortcut.FileName))));
            SetStatus(
                $"{T("На выбранном рабочем столе найдено системных ярлыков", "Admin shortcuts found on the selected desktop", "Tanlangan ish stolida topilgan administrator yorliqlari")}: {existing} {T("из", "of", "ta")} {_shortcuts.Count}.");
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, isError: true);
        }
    }

    private void ApplyShortcutFilter()
    {
        if (_updatingShortcutList)
        {
            return;
        }

        CaptureVisibleShortcutChecks();
        string search = _shortcutSearch.Text.Trim();
        string? category = _shortcutCategory.SelectedIndex <= 0
            ? null
            : _shortcutCategories[_shortcutCategory.SelectedIndex - 1];

        _updatingShortcutList = true;
        try
        {
            _shortcutList.BeginUpdate();
            _shortcutList.Items.Clear();
            foreach (ShortcutDefinition shortcut in _shortcuts)
            {
                bool categoryMatches = category is null ||
                    shortcut.Category.Equals(category, StringComparison.OrdinalIgnoreCase);
                string localizedName = UiLocalization.CatalogText(shortcut.DisplayName);
                bool searchMatches = search.Length == 0 ||
                    shortcut.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    localizedName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                    shortcut.Description.Contains(search, StringComparison.CurrentCultureIgnoreCase);
                if (categoryMatches && searchMatches)
                {
                    _shortcutList.Items.Add(
                        new ShortcutListItem(shortcut),
                        _checkedShortcutFiles.Contains(shortcut.FileName));
                }
            }
        }
        finally
        {
            _shortcutList.EndUpdate();
            _updatingShortcutList = false;
        }
    }

    private void CaptureVisibleShortcutChecks()
    {
        for (int index = 0; index < _shortcutList.Items.Count; index++)
        {
            if (_shortcutList.Items[index] is not ShortcutListItem item)
            {
                continue;
            }

            if (_shortcutList.GetItemChecked(index))
            {
                _checkedShortcutFiles.Add(item.Definition.FileName);
            }
            else
            {
                _checkedShortcutFiles.Remove(item.Definition.FileName);
            }
        }
    }

    private void ShortcutListItemCheck(object? sender, ItemCheckEventArgs e)
    {
        if (_updatingShortcutList ||
            _shortcutList.Items[e.Index] is not ShortcutListItem item)
        {
            return;
        }

        if (e.NewValue == CheckState.Checked)
        {
            _checkedShortcutFiles.Add(item.Definition.FileName);
        }
        else
        {
            _checkedShortcutFiles.Remove(item.Definition.FileName);
        }
    }

    private List<ShortcutDefinition> GetSelectedShortcuts()
    {
        var selected = new List<ShortcutDefinition>();
        for (int index = 0; index < _shortcutList.Items.Count; index++)
        {
            if (_shortcutList.GetItemChecked(index) &&
                _shortcutList.Items[index] is ShortcutListItem item)
            {
                selected.Add(item.Definition);
            }
        }

        return selected;
    }

    private void SetAllChecked(bool value)
    {
        for (int index = 0; index < _shortcutList.Items.Count; index++)
        {
            if (_shortcutList.Items[index] is ShortcutListItem item)
            {
                if (value)
                {
                    _checkedShortcutFiles.Add(item.Definition.FileName);
                }
                else
                {
                    _checkedShortcutFiles.Remove(item.Definition.FileName);
                }
            }

            _shortcutList.SetItemChecked(index, value);
        }
    }

    private void OpenDesktop(DesktopScope scope)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DesktopPathProvider.GetPath(scope),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void ShowOperationResult(
        string label,
        int count,
        IReadOnlyList<string> errors,
        int backupCount = 0)
    {
        if (errors.Count == 0)
        {
            string backups = backupCount == 0
                ? string.Empty
                : $" {T("Backup-файлов", "Backup files", "Zaxira fayllari")}: {backupCount}.";
            SetStatus($"{label}: {count}.{backups}");
            return;
        }

        ShowError(
            $"{label}: {count}. {T("Ошибки", "Errors", "Xatolar")}:\n\n{string.Join("\n", errors)}");
    }

    private bool ConfirmOverwrite(int count)
    {
        return MessageBox.Show(
            this,
            count == 1
                ? T(
                    "Файл уже существует. Заменить его?",
                    "The file already exists. Replace it?",
                    "Fayl allaqachon mavjud. Almashtirilsinmi?")
                : T(
                    $"Уже существует файлов: {count}. Заменить их?",
                    $"{count} files already exist. Replace them?",
                    $"{count} ta fayl mavjud. Ular almashtirilsinmi?"),
            T("Подтверждение замены", "Confirm replacement", "Almashtirishni tasdiqlash"),
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void SetStatus(string text, bool isError = false)
    {
        _statusIsError = isError;
        _statusLabel.Text = text;
        _statusLabel.Name = isError ? "Error" : "Success";
        _statusLabel.ForeColor = isError
            ? ThemePalette.Current.Danger
            : ThemePalette.Current.Success;
    }

    private void ShowError(string message)
    {
        SetStatus(T(
            "Операция завершилась с ошибкой.",
            "The operation failed.",
            "Amal xato bilan yakunlandi."), isError: true);
        MessageBox.Show(this, message, "Windows Admin Center", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private WallpaperLayout GetWallpaperLayout()
    {
        return _wallpaperLayout.SelectedIndex switch
        {
            0 => WallpaperLayout.Fill,
            1 => WallpaperLayout.Fit,
            2 => WallpaperLayout.Stretch,
            3 => WallpaperLayout.Center,
            4 => WallpaperLayout.Tile,
            5 => WallpaperLayout.Span,
            _ => WallpaperLayout.Fill
        };
    }

    private static DesktopScope GetScope(ComboBox combo)
    {
        return combo.SelectedIndex == 1 ? DesktopScope.AllUsers : DesktopScope.CurrentUser;
    }

    private void InitializeSelectors()
    {
        UiLocalization.Attach(
            _customRunAsAdministrator,
            "Запускать от администратора",
            "Run as administrator",
            "Administrator sifatida ishga tushirish");
    }

    private void ChangeLanguage(object? sender, EventArgs e)
    {
        if (_updatingAppearanceSelectors)
        {
            return;
        }

        try
        {
            AppSettingsService.SetLanguage(_languageSelector.SelectedLanguage);
        }
        catch (Exception ex)
        {
            ShowError(
                $"{T("Не удалось сохранить язык интерфейса", "Could not save the interface language", "Interfeys tilini saqlab bo‘lmadi")}: {ex.Message}");
            ApplyLocalizationAndTheme();
        }
    }

    private void ToggleTheme(object? sender, EventArgs e)
    {
        try
        {
            AppTheme next = AppSettingsService.Current.Theme == AppTheme.Light
                ? AppTheme.Dark
                : AppTheme.Light;
            AppSettingsService.SetTheme(next);
        }
        catch (Exception ex)
        {
            ShowError(
                $"{T("Не удалось сохранить тему", "Could not save the theme", "Mavzuni saqlab bo‘lmadi")}: {ex.Message}");
        }
    }

    private void AppearanceChanged(object? sender, EventArgs e)
    {
        if (IsDisposed)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(ApplyLocalizationAndTheme);
            return;
        }

        ApplyLocalizationAndTheme();
    }

    private void ApplyLocalizationAndTheme()
    {
        _updatingAppearanceSelectors = true;
        try
        {
            _languageSelector.SelectedLanguage = AppSettingsService.Current.Language;
            UiLocalization.Apply(this);
            _shortcutSearch.PlaceholderText = T(
                "Название инструмента",
                "Tool name",
                "Vosita nomi");
            RefreshScopeCombo(_presetScope, defaultIndex: 0);
            RefreshScopeCombo(_customScope, defaultIndex: 0);
            RefreshScopeCombo(_winPScope, defaultIndex: 1);
            RefreshWallpaperLayouts();
            RefreshCategoryItems();

            string themeTip = AppSettingsService.Current.Theme == AppTheme.Light
                ? T("Включить тёмную тему", "Switch to dark theme", "Qorong‘i mavzuni yoqish")
                : T("Включить светлую тему", "Switch to light theme", "Yorug‘ mavzuni yoqish");
            _themeToggle.AccessibleName = themeTip;
            _toolTip.SetToolTip(_themeToggle, themeTip);
        }
        finally
        {
            _updatingAppearanceSelectors = false;
        }

        ThemeManager.Apply(this);
        _statusLabel.ForeColor = _statusIsError
            ? ThemePalette.Current.Danger
            : ThemePalette.Current.Success;
        ApplyShortcutFilter();
        PerformLayout();
    }

    private static void RefreshScopeCombo(ComboBox combo, int defaultIndex)
    {
        int selected = combo.SelectedIndex >= 0 ? combo.SelectedIndex : defaultIndex;
        combo.BeginUpdate();
        try
        {
            combo.Items.Clear();
            combo.Items.AddRange(new object[]
            {
                T("Текущий пользователь", "Current user", "Joriy foydalanuvchi"),
                T(
                    "Все пользователи (общий рабочий стол)",
                    "All users (public desktop)",
                    "Barcha foydalanuvchilar (umumiy ish stoli)")
            });
            combo.SelectedIndex = Math.Clamp(selected, 0, combo.Items.Count - 1);
        }
        finally
        {
            combo.EndUpdate();
        }
    }

    private void RefreshWallpaperLayouts()
    {
        int selected = _wallpaperLayout.SelectedIndex >= 0
            ? _wallpaperLayout.SelectedIndex
            : 0;
        _wallpaperLayout.BeginUpdate();
        try
        {
            _wallpaperLayout.Items.Clear();
            _wallpaperLayout.Items.AddRange(new object[]
            {
                T("Заполнение", "Fill", "To‘ldirish"),
                T("По размеру", "Fit", "Sig‘dirish"),
                T("Растянуть", "Stretch", "Cho‘zish"),
                T("По центру", "Center", "Markazda"),
                T("Замостить", "Tile", "Plitka"),
                T("Панорама", "Span", "Panorama")
            });
            _wallpaperLayout.SelectedIndex = Math.Clamp(
                selected,
                0,
                _wallpaperLayout.Items.Count - 1);
        }
        finally
        {
            _wallpaperLayout.EndUpdate();
        }
    }

    private void RefreshCategoryItems()
    {
        int selected = _shortcutCategory.SelectedIndex >= 0
            ? _shortcutCategory.SelectedIndex
            : 0;
        _shortcutCategory.BeginUpdate();
        try
        {
            _shortcutCategory.Items.Clear();
            _shortcutCategory.Items.Add(
                T("Все категории", "All categories", "Barcha toifalar"));
            _shortcutCategory.Items.AddRange(
                _shortcutCategories
                    .Select(UiLocalization.CatalogText)
                    .Cast<object>()
                    .ToArray());
            _shortcutCategory.SelectedIndex = Math.Clamp(
                selected,
                0,
                _shortcutCategory.Items.Count - 1);
        }
        finally
        {
            _shortcutCategory.EndUpdate();
        }
    }

    private static string T(string russian, string english, string uzbek) =>
        UiLocalization.Text(russian, english, uzbek);

    private static ComboBox CreateScopeCombo()
    {
        return new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300
        };
    }

    private static TabPage CreateTab(string russian, string english, string uzbek)
    {
        return UiLocalization.Attach(new TabPage
        {
            UseVisualStyleBackColor = false
        }, russian, english, uzbek);
    }

    private static GroupBox CreateGroup(
        string russian,
        string english,
        string uzbek)
    {
        return UiLocalization.Attach(new GroupBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 10)
        }, russian, english, uzbek);
    }

    private static FlowLayoutPanel CreateButtonRow()
    {
        return new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 8, 0, 0)
        };
    }

    private static TableLayoutPanel CreateActionGrid(int columns)
    {
        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 50,
            ColumnCount = columns,
            RowCount = 1,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
            Margin = Padding.Empty
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (int index = 0; index < columns; index++)
        {
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / columns));
        }

        return grid;
    }

    private static Button CreateGridButton(
        string russian,
        string english,
        string uzbek,
        EventHandler onClick,
        bool primary = false)
    {
        Button button = CreateButton(russian, english, uzbek, onClick, primary);
        button.Dock = DockStyle.Fill;
        button.AutoSize = false;
        button.Margin = new Padding(4, 2, 4, 2);
        button.MinimumSize = new Size(0, 42);
        return button;
    }

    private static Button CreateButton(
        string russian,
        string english,
        string uzbek,
        EventHandler onClick,
        bool primary = false)
    {
        var button = UiLocalization.Attach(new ModernButton
        {
            Kind = primary ? ModernButtonKind.Primary : ModernButtonKind.Secondary,
            Margin = new Padding(0, 0, 10, 6),
        }, russian, english, uzbek);

        button.Click += onClick;
        return button;
    }

    private static Control BuildLabeledControl(
        string russian,
        string english,
        string uzbek,
        Control control)
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(UiLocalization.Attach(new Label
        {
            Name = "Muted",
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4)
        }, russian, english, uzbek), 0, 0);
        control.Margin = Padding.Empty;
        layout.Controls.Add(control, 0, 1);
        return layout;
    }

    private static Label CreateFieldLabel(string russian, string english, string uzbek)
    {
        return UiLocalization.Attach(new Label
        {
            Name = "Muted",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(4, 8, 4, 4)
        }, russian, english, uzbek);
    }

    private static Label CreateSectionTitle(string russian, string english, string uzbek)
    {
        return UiLocalization.Attach(new Label
        {
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F),
            Margin = Padding.Empty
        }, russian, english, uzbek);
    }

    private static void RefreshDesktop()
    {
        if (OperatingSystem.IsWindows())
        {
            NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private sealed record ShortcutListItem(ShortcutDefinition Definition)
    {
        public override string ToString() =>
            $"[{UiLocalization.CatalogText(Definition.Category)}]  " +
            UiLocalization.CatalogText(Definition.DisplayName);
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll")]
        internal static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
