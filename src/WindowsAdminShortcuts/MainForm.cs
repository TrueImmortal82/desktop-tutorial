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
    private readonly ComboBox _winPScope = CreateScopeCombo(defaultToAllUsers: true);
    private readonly TextBox _customName = new();
    private readonly TextBox _customTarget = new();
    private readonly TextBox _customArguments = new();
    private readonly CheckBox _customRunAsAdministrator = new()
    {
        Text = "Запускать от администратора",
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
    private readonly HashSet<string> _checkedShortcutFiles = new(StringComparer.OrdinalIgnoreCase);
    private bool _updatingShortcutList;

    public MainForm()
    {
        Text = "Windows Admin Center";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(940, 700);
        Size = new Size(1120, 820);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(244, 247, 251);
        AutoScaleMode = AutoScaleMode.Dpi;

        _shortcuts = AdminShortcutCatalog.Create();
        foreach (ShortcutDefinition shortcut in _shortcuts)
        {
            _checkedShortcutFiles.Add(shortcut.FileName);
        }
        _applyWallpaperButton = CreateButton("Применить для всех пользователей", ApplyWallpaperAsync, primary: true);

        BuildInterface();
        RefreshShortcutState();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _wallpaperPreview.Image?.Dispose();
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

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            Padding = new Point(18, 7),
            Margin = new Padding(0, 14, 0, 12)
        };
        tabs.TabPages.Add(BuildShortcutsTab());
        tabs.TabPages.Add(BuildWallpaperTab());
        tabs.TabPages.Add(BuildWinPTab());
        root.Controls.Add(tabs, 0, 1);

        var statusPanel = new Panel
        {
            Dock = DockStyle.Fill,
            Height = 36,
            BackColor = Color.White,
            Padding = new Padding(12, 8, 12, 8)
        };
        _statusLabel.Dock = DockStyle.Fill;
        _statusLabel.Text = "Готово к работе.";
        _statusLabel.ForeColor = Color.FromArgb(62, 72, 88);
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

        var title = new Label
        {
            Text = "Windows Admin Center",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 20F),
            ForeColor = Color.FromArgb(28, 37, 51),
            Margin = Padding.Empty
        };
        header.Controls.Add(title, 0, 0);

        var adminBadge = new Label
        {
            Text = ElevationService.IsAdministrator() ? "Администратор" : "Без прав администратора",
            AutoSize = true,
            BackColor = ElevationService.IsAdministrator()
                ? Color.FromArgb(225, 244, 231)
                : Color.FromArgb(253, 235, 235),
            ForeColor = ElevationService.IsAdministrator()
                ? Color.FromArgb(29, 107, 55)
                : Color.FromArgb(161, 42, 42),
            Padding = new Padding(12, 6, 12, 6),
            Margin = new Padding(12, 3, 0, 0)
        };
        header.Controls.Add(adminBadge, 1, 0);

        var subtitle = new Label
        {
            Text = "Ярлыки, общие обои и Win+P — в одном проверяемом окне.",
            AutoSize = true,
            ForeColor = Color.FromArgb(91, 102, 119),
            Margin = new Padding(0, 5, 0, 0)
        };
        header.Controls.Add(subtitle, 0, 1);
        header.SetColumnSpan(subtitle, 2);
        return header;
    }

    private TabPage BuildShortcutsTab()
    {
        var tab = CreateTab("Ярлыки");
        var sections = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(16, 6),
            Font = new Font("Segoe UI Semibold", 9.5F)
        };

        var catalogPage = CreateTab($"Готовые ярлыки ({_shortcuts.Count})");
        catalogPage.Controls.Add(BuildStandardShortcutsPanel());
        sections.TabPages.Add(catalogPage);

        var customPage = CreateTab("Свой ярлык");
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
            ColumnCount = 4,
            Margin = Padding.Empty
        };
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
        filters.Controls.Add(CreateFieldLabel("Поиск:"), 0, 0);
        _shortcutSearch.Name = "ShortcutSearch";
        _shortcutSearch.Dock = DockStyle.Fill;
        _shortcutSearch.PlaceholderText = "Название инструмента";
        _shortcutSearch.Margin = new Padding(6, 3, 18, 5);
        _shortcutSearch.TextChanged += (_, _) => ApplyShortcutFilter();
        filters.Controls.Add(_shortcutSearch, 1, 0);
        filters.Controls.Add(CreateFieldLabel("Категория:"), 2, 0);
        _shortcutCategory.Name = "ShortcutCategory";
        _shortcutCategory.Dock = DockStyle.Fill;
        _shortcutCategory.DropDownStyle = ComboBoxStyle.DropDownList;
        _shortcutCategory.Margin = new Padding(6, 3, 0, 5);
        _shortcutCategory.Items.Add("Все категории");
        _shortcutCategory.Items.AddRange(
            _shortcuts
                .Select(shortcut => shortcut.Category)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Cast<object>()
                .ToArray());
        _shortcutCategory.SelectedIndexChanged += (_, _) => ApplyShortcutFilter();
        _shortcutCategory.SelectedIndex = 0;
        filters.Controls.Add(_shortcutCategory, 3, 0);
        layout.Controls.Add(filters, 0, 0);

        _shortcutList.Name = "ShortcutCatalogList";
        _shortcutList.Dock = DockStyle.Fill;
        _shortcutList.CheckOnClick = true;
        _shortcutList.IntegralHeight = false;
        _shortcutList.HorizontalScrollbar = true;
        _shortcutList.ScrollAlwaysVisible = true;
        _shortcutList.BorderStyle = BorderStyle.FixedSingle;
        _shortcutList.BackColor = Color.White;
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
        scopePanel.Controls.Add(CreateFieldLabel("Рабочий стол:"), 0, 0);
        _presetScope.Dock = DockStyle.Fill;
        _presetScope.Margin = new Padding(8, 3, 0, 3);
        _presetScope.SelectedIndexChanged += (_, _) => RefreshShortcutState();
        scopePanel.Controls.Add(_presetScope, 1, 0);
        layout.Controls.Add(scopePanel, 0, 2);

        var buttons = CreateActionGrid(5);
        buttons.Controls.Add(CreateGridButton("Создать выбранные", CreateSelectedShortcuts, primary: true), 0, 0);
        buttons.Controls.Add(CreateGridButton("Удалить выбранные", RemoveSelectedShortcuts), 1, 0);
        buttons.Controls.Add(CreateGridButton("Выбрать показанные", (_, _) => SetAllChecked(true)), 2, 0);
        buttons.Controls.Add(CreateGridButton("Снять показанные", (_, _) => SetAllChecked(false)), 3, 0);
        buttons.Controls.Add(
            CreateGridButton("Открыть рабочий стол", (_, _) => OpenDesktop(GetScope(_presetScope))),
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
        var group = CreateGroup("Добавить свой ярлык");
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
        layout.Controls.Add(BuildLabeledControl("Название:", _customName), 0, 0);

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
        targetPanel.Controls.Add(CreateButton("Обзор…", BrowseShortcutTarget), 1, 0);
        layout.Controls.Add(BuildLabeledControl("Цель:", targetPanel), 0, 1);
        _customArguments.Name = "CustomShortcutArguments";
        _customArguments.Dock = DockStyle.Fill;
        layout.Controls.Add(BuildLabeledControl("Аргументы:", _customArguments), 0, 2);
        layout.Controls.Add(BuildLabeledControl("Рабочий стол:", _customScope), 0, 3);
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
        action.Controls.Add(CreateButton("Создать ярлык", CreateCustomShortcut, primary: true), 1, 0);
        layout.Controls.Add(action, 0, 5);

        group.Controls.Add(layout);
        host.Controls.Add(group);
        return host;
    }

    private TabPage BuildWallpaperTab()
    {
        var tab = CreateTab("Обои для всех");
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

        controls.Controls.Add(CreateSectionTitle("Общие обои Windows"), 0, 0);
        controls.Controls.Add(new Label
        {
            Text = "Изображение копируется в ProgramData, затем настройки применяются ко всем существующим профилям и профилю новых пользователей. Перед изменением создаётся backup реестровых значений.",
            AutoSize = true,
            MaximumSize = new Size(500, 0),
            ForeColor = Color.FromArgb(83, 94, 111),
            Margin = new Padding(0, 6, 0, 18)
        }, 0, 1);

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
        filePanel.Controls.Add(CreateButton("Выбрать изображение…", BrowseWallpaper), 1, 0);
        controls.Controls.Add(BuildLabeledControl("Файл обоев:", filePanel), 0, 2);

        _wallpaperLayout.DropDownStyle = ComboBoxStyle.DropDownList;
        _wallpaperLayout.Items.AddRange(new object[]
        {
            "Заполнение",
            "По размеру",
            "Растянуть",
            "По центру",
            "Замостить",
            "Панорама"
        });
        _wallpaperLayout.SelectedIndex = 0;
        _wallpaperLayout.Width = 220;
        controls.Controls.Add(BuildLabeledControl("Расположение:", _wallpaperLayout), 0, 3);

        _applyWallpaperButton.Margin = new Padding(0, 18, 0, 10);
        controls.Controls.Add(_applyWallpaperButton, 0, 4);

        _wallpaperResult.AutoSize = true;
        _wallpaperResult.MaximumSize = new Size(520, 0);
        _wallpaperResult.ForeColor = Color.FromArgb(70, 82, 99);
        controls.Controls.Add(_wallpaperResult, 0, 5);

        _wallpaperPreview.Dock = DockStyle.Fill;
        _wallpaperPreview.SizeMode = PictureBoxSizeMode.Zoom;
        _wallpaperPreview.BackColor = Color.FromArgb(226, 232, 240);
        _wallpaperPreview.BorderStyle = BorderStyle.FixedSingle;
        layout.Controls.Add(controls, 0, 0);
        layout.Controls.Add(_wallpaperPreview, 1, 0);
        tab.Controls.Add(layout);
        return tab;
    }

    private TabPage BuildWinPTab()
    {
        var tab = CreateTab("Win+P");
        var card = new TableLayoutPanel
        {
            AutoSize = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
            Padding = new Padding(26),
            ColumnCount = 1,
            RowCount = 5,
            BackColor = Color.White
        };
        card.Controls.Add(CreateSectionTitle("Дублирование экранов"), 0, 0);
        card.Controls.Add(new Label
        {
            Text = "Создаёт Win+P.cmd, который сразу включает режим «Дублировать» штатной командой DisplaySwitch.exe /clone. Панель выбора режима не открывается. Скрипт записывается с CRLF и проверяется после сохранения.",
            AutoSize = true,
            MaximumSize = new Size(720, 0),
            ForeColor = Color.FromArgb(83, 94, 111),
            Margin = new Padding(0, 8, 0, 20)
        }, 0, 1);
        card.Controls.Add(BuildLabeledControl("Рабочий стол:", _winPScope), 0, 2);

        var actions = CreateButtonRow();
        actions.Margin = new Padding(0, 18, 0, 0);
        actions.Controls.Add(CreateButton("Создать «Дублировать»", CreateWinPLauncher, primary: true));
        actions.Controls.Add(CreateButton("Открыть рабочий стол", (_, _) => OpenDesktop(GetScope(_winPScope))));
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
            SetStatus("Ничего не выбрано.", isError: true);
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
                errors.Add($"{shortcut.DisplayName}: {ex.Message}");
            }
        }

        RefreshDesktop();
        RefreshShortcutState();
        ShowOperationResult("Создано ярлыков", created, errors, backups);
    }

    private void RemoveSelectedShortcuts(object? sender, EventArgs e)
    {
        List<ShortcutDefinition> selected = GetSelectedShortcuts();
        if (selected.Count == 0)
        {
            SetStatus("Ничего не выбрано.", isError: true);
            return;
        }

        if (MessageBox.Show(
                this,
                $"Удалить выбранные ярлыки ({selected.Count})?",
                "Подтверждение удаления",
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
                errors.Add($"{shortcut.DisplayName}: {ex.Message}");
            }
        }

        RefreshDesktop();
        RefreshShortcutState();
        ShowOperationResult("Удалено ярлыков", removed, errors);
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
                : $" Backup исходного файла: {result.BackupPath}";
            SetStatus($"Ярлык создан: {result.Path}.{backup}");
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
            ShowError("Сначала выберите файл обоев.");
            return;
        }

        if (MessageBox.Show(
                this,
                "Настройки обоев будут изменены для всех существующих и новых пользователей. Перед изменением будет создан backup. Продолжить?",
                "Применить общие обои",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes)
        {
            return;
        }

        _applyWallpaperButton.Enabled = false;
        SetStatus("Настройки профилей применяются…");
        try
        {
            WallpaperLayout layout = GetWallpaperLayout();
            WallpaperApplyResult result = await Task.Run(
                () => WallpaperService.ApplyToAllUsers(path, layout));

            string skipped = result.SkippedProfiles.Count == 0
                ? string.Empty
                : $"\nПропущено записей без NTUSER.DAT: {result.SkippedProfiles.Count}.";
            _wallpaperResult.Text =
                $"Настроено профилей: {result.ProfileCount}.\n" +
                $"Файл: {result.ManagedWallpaperPath}\n" +
                $"Backup: {result.BackupPath}{skipped}";
            SetStatus($"Обои применены. Настроено профилей: {result.ProfileCount}.");
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
            string backup = result.BackupPath is null ? string.Empty : $" Backup: {result.BackupPath}";
            SetStatus($"Скрипт дублирования экранов создан: {result.Path}.{backup}");
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
            Title = "Выберите программу или файл",
            CheckFileExists = true,
            Filter = "Программы и файлы|*.exe;*.com;*.bat;*.cmd;*.msc;*.cpl;*.ps1;*.url|Все файлы|*.*"
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
            Title = "Выберите изображение",
            CheckFileExists = true,
            Filter = "Изображения|*.jpg;*.jpeg;*.png;*.bmp|Все файлы|*.*"
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
            SetStatus("Изображение выбрано. Общесистемные настройки ещё не изменены.");
        }
        catch (Exception ex)
        {
            ShowError($"Не удалось открыть изображение: {ex.Message}");
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
            SetStatus($"На выбранном рабочем столе найдено системных ярлыков: {existing} из {_shortcuts.Count}.");
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
            : _shortcutCategory.SelectedItem?.ToString();

        _updatingShortcutList = true;
        try
        {
            _shortcutList.BeginUpdate();
            _shortcutList.Items.Clear();
            foreach (ShortcutDefinition shortcut in _shortcuts)
            {
                bool categoryMatches = category is null ||
                    shortcut.Category.Equals(category, StringComparison.OrdinalIgnoreCase);
                bool searchMatches = search.Length == 0 ||
                    shortcut.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
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
            string backups = backupCount == 0 ? string.Empty : $" Backup-файлов: {backupCount}.";
            SetStatus($"{label}: {count}.{backups}");
            return;
        }

        ShowError($"{label}: {count}. Ошибки:\n\n{string.Join("\n", errors)}");
    }

    private bool ConfirmOverwrite(int count)
    {
        return MessageBox.Show(
            this,
            count == 1
                ? "Файл уже существует. Заменить его?"
                : $"Уже существует файлов: {count}. Заменить их?",
            "Подтверждение замены",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2) == DialogResult.Yes;
    }

    private void SetStatus(string text, bool isError = false)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError
            ? Color.FromArgb(178, 38, 38)
            : Color.FromArgb(45, 105, 63);
    }

    private void ShowError(string message)
    {
        SetStatus("Операция завершилась с ошибкой.", isError: true);
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

    private static ComboBox CreateScopeCombo(bool defaultToAllUsers = false)
    {
        var combo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 300
        };
        combo.Items.AddRange(new object[]
        {
            "Текущий пользователь",
            "Все пользователи (общий рабочий стол)"
        });
        combo.SelectedIndex = defaultToAllUsers ? 1 : 0;
        return combo;
    }

    private static TabPage CreateTab(string text)
    {
        return new TabPage
        {
            Text = text,
            BackColor = Color.FromArgb(244, 247, 251),
            UseVisualStyleBackColor = false
        };
    }

    private static GroupBox CreateGroup(string text)
    {
        return new GroupBox
        {
            Text = text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 10F),
            ForeColor = Color.FromArgb(38, 48, 63),
            Padding = new Padding(8),
            Margin = new Padding(0, 0, 0, 10)
        };
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

    private static Button CreateGridButton(string text, EventHandler onClick, bool primary = false)
    {
        Button button = CreateButton(text, onClick, primary);
        button.Dock = DockStyle.Fill;
        button.AutoSize = false;
        button.Margin = new Padding(4, 2, 4, 2);
        button.MinimumSize = new Size(0, 42);
        return button;
    }

    private static Button CreateButton(string text, EventHandler onClick, bool primary = false)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(0, 38),
            Padding = new Padding(14, 3, 14, 3),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 6),
            UseVisualStyleBackColor = false
        };

        if (primary)
        {
            button.BackColor = Color.FromArgb(35, 102, 209);
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = button.BackColor;
        }
        else
        {
            button.BackColor = Color.White;
            button.ForeColor = Color.FromArgb(42, 49, 61);
            button.FlatAppearance.BorderColor = Color.FromArgb(181, 190, 203);
        }

        button.Click += onClick;
        return button;
    }

    private static Control BuildLabeledControl(string label, Control control)
    {
        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Top,
            ColumnCount = 1,
            RowCount = 2,
            Margin = new Padding(0, 0, 0, 8)
        };
        layout.Controls.Add(new Label
        {
            Text = label,
            AutoSize = true,
            ForeColor = Color.FromArgb(67, 78, 95),
            Margin = new Padding(0, 0, 0, 4)
        }, 0, 0);
        control.Margin = Padding.Empty;
        layout.Controls.Add(control, 0, 1);
        return layout;
    }

    private static Label CreateFieldLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            ForeColor = Color.FromArgb(67, 78, 95),
            Margin = new Padding(4, 8, 4, 4)
        };
    }

    private static Label CreateSectionTitle(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 16F),
            ForeColor = Color.FromArgb(31, 41, 56),
            Margin = Padding.Empty
        };
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
        public override string ToString() => $"[{Definition.Category}]  {Definition.DisplayName}";
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll")]
        internal static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
