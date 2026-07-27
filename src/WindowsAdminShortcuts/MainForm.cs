using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsAdminShortcuts;

internal sealed class MainForm : Form
{
    private readonly CheckedListBox _shortcutList = new();
    private readonly Label _statusLabel = new();
    private readonly IReadOnlyList<ShortcutDefinition> _shortcuts;

    public MainForm()
    {
        Text = "Windows Admin Shortcuts";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(650, 440);
        Size = new Size(720, 500);
        Font = new Font("Segoe UI", 10F);
        BackColor = Color.FromArgb(245, 247, 250);

        string systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string system32 = Environment.SystemDirectory;
        string explorer = Path.Combine(systemRoot, "explorer.exe");
        string mmc = Path.Combine(system32, "mmc.exe");

        _shortcuts = new List<ShortcutDefinition>
        {
            new(
                "Администрирование",
                "Администрирование.lnk",
                explorer,
                "shell:::{D20EA4E1-3957-11D2-A40B-0C5020524153}",
                "Средства администрирования Windows",
                Path.Combine(system32, "imageres.dll"),
                109),
            new(
                "Панель управления",
                "Панель управления.lnk",
                Path.Combine(system32, "control.exe"),
                string.Empty,
                "Классическая панель управления Windows",
                Path.Combine(system32, "control.exe")),
            new(
                "Планировщик заданий",
                "Планировщик заданий.lnk",
                mmc,
                $"\"{Path.Combine(system32, "taskschd.msc")}\"",
                "Планировщик заданий Windows",
                Path.Combine(system32, "taskschd.msc")),
            new(
                "Диспетчер устройств",
                "Диспетчер устройств.lnk",
                mmc,
                $"\"{Path.Combine(system32, "devmgmt.msc")}\"",
                "Управление устройствами и драйверами",
                Path.Combine(system32, "devmgmt.msc"))
        };

        BuildInterface();
        LoadShortcutState();
    }

    private void BuildInterface()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(24),
            ColumnCount = 1,
            RowCount = 5
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "Системные ярлыки Windows",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 19F),
            ForeColor = Color.FromArgb(32, 39, 52),
            Margin = new Padding(0, 0, 0, 6)
        };

        var subtitle = new Label
        {
            Text = "Выбери нужные элементы и создай их на рабочем столе текущего пользователя.",
            AutoSize = true,
            ForeColor = Color.FromArgb(90, 98, 112),
            Margin = new Padding(0, 0, 0, 18)
        };

        _shortcutList.Dock = DockStyle.Fill;
        _shortcutList.CheckOnClick = true;
        _shortcutList.BorderStyle = BorderStyle.FixedSingle;
        _shortcutList.BackColor = Color.White;
        _shortcutList.IntegralHeight = false;
        _shortcutList.ItemHeight = 34;
        _shortcutList.Padding = new Padding(10);

        foreach (ShortcutDefinition shortcut in _shortcuts)
        {
            _shortcutList.Items.Add(shortcut.DisplayName, true);
        }

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            Margin = new Padding(0, 16, 0, 10)
        };

        buttons.Controls.Add(CreateButton("Создать выбранные", (_, _) => CreateSelected(), true));
        buttons.Controls.Add(CreateButton("Удалить выбранные", (_, _) => RemoveSelected(), false));
        buttons.Controls.Add(CreateButton("Выбрать все", (_, _) => SetAllChecked(true), false));
        buttons.Controls.Add(CreateButton("Открыть рабочий стол", (_, _) => OpenDesktop(), false));

        _statusLabel.AutoSize = true;
        _statusLabel.Text = "Готово к работе.";
        _statusLabel.ForeColor = Color.FromArgb(70, 78, 91);
        _statusLabel.Margin = new Padding(0, 4, 0, 0);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(subtitle, 0, 1);
        root.Controls.Add(_shortcutList, 0, 2);
        root.Controls.Add(buttons, 0, 3);
        root.Controls.Add(_statusLabel, 0, 4);
        Controls.Add(root);
    }

    private static Button CreateButton(string text, EventHandler onClick, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            Height = 38,
            Padding = new Padding(14, 3, 14, 3),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 8),
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
            button.FlatAppearance.BorderColor = Color.FromArgb(190, 197, 207);
        }

        button.Click += onClick;
        return button;
    }

    private void CreateSelected()
    {
        if (!OperatingSystem.IsWindows())
        {
            ShowError("Приложение работает только в Windows.");
            return;
        }

        var selected = GetSelectedShortcuts();
        if (selected.Count == 0)
        {
            SetStatus("Ничего не выбрано.", isError: true);
            return;
        }

        string desktop = GetDesktopPath();
        string workingDirectory = Environment.SystemDirectory;
        int created = 0;
        var errors = new List<string>();

        foreach (ShortcutDefinition shortcut in selected)
        {
            try
            {
                ShellLink.Create(
                    Path.Combine(desktop, shortcut.FileName),
                    shortcut.TargetPath,
                    shortcut.Arguments,
                    shortcut.Description,
                    workingDirectory,
                    shortcut.IconPath,
                    shortcut.IconIndex);
                created++;
            }
            catch (Exception ex)
            {
                errors.Add($"{shortcut.DisplayName}: {ex.Message}");
            }
        }

        RefreshDesktop();
        LoadShortcutState();

        if (errors.Count == 0)
        {
            SetStatus($"Создано ярлыков: {created}.");
        }
        else
        {
            ShowError($"Создано: {created}. Ошибки:\n\n{string.Join("\n", errors)}");
        }
    }

    private void RemoveSelected()
    {
        var selected = GetSelectedShortcuts();
        if (selected.Count == 0)
        {
            SetStatus("Ничего не выбрано.", isError: true);
            return;
        }

        string desktop = GetDesktopPath();
        int removed = 0;
        var errors = new List<string>();

        foreach (ShortcutDefinition shortcut in selected)
        {
            try
            {
                string path = Path.Combine(desktop, shortcut.FileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    removed++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{shortcut.DisplayName}: {ex.Message}");
            }
        }

        RefreshDesktop();
        LoadShortcutState();

        if (errors.Count == 0)
        {
            SetStatus($"Удалено ярлыков: {removed}.");
        }
        else
        {
            ShowError($"Удалено: {removed}. Ошибки:\n\n{string.Join("\n", errors)}");
        }
    }

    private List<ShortcutDefinition> GetSelectedShortcuts()
    {
        var selected = new List<ShortcutDefinition>();
        for (int index = 0; index < _shortcutList.Items.Count; index++)
        {
            if (_shortcutList.GetItemChecked(index))
            {
                selected.Add(_shortcuts[index]);
            }
        }

        return selected;
    }

    private void SetAllChecked(bool value)
    {
        for (int index = 0; index < _shortcutList.Items.Count; index++)
        {
            _shortcutList.SetItemChecked(index, value);
        }
    }

    private void LoadShortcutState()
    {
        string desktop = GetDesktopPath();
        int existing = _shortcuts.Count(shortcut => File.Exists(Path.Combine(desktop, shortcut.FileName)));
        SetStatus($"На рабочем столе найдено ярлыков: {existing} из {_shortcuts.Count}.");
    }

    private static string GetDesktopPath()
    {
        string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrWhiteSpace(desktop))
        {
            throw new InvalidOperationException("Windows не вернула путь к рабочему столу.");
        }

        return desktop;
    }

    private void OpenDesktop()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = GetDesktopPath(),
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
    }

    private void SetStatus(string text, bool isError = false)
    {
        _statusLabel.Text = text;
        _statusLabel.ForeColor = isError
            ? Color.FromArgb(178, 38, 38)
            : Color.FromArgb(56, 105, 62);
    }

    private void ShowError(string message)
    {
        SetStatus("Операция завершилась с ошибкой.", isError: true);
        MessageBox.Show(this, message, "Windows Admin Shortcuts", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    private static void RefreshDesktop()
    {
        if (OperatingSystem.IsWindows())
        {
            NativeMethods.SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll")]
        internal static extern void SHChangeNotify(uint eventId, uint flags, IntPtr item1, IntPtr item2);
    }
}
