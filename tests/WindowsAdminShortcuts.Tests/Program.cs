using System.Diagnostics;
using System.Text;
using WindowsAdminShortcuts;

var tests = new (string Name, Action Run)[]
{
    ("Wallpaper layout mappings", TestWallpaperLayouts),
    ("Administrative shortcut catalog", TestAdminShortcutCatalog),
    ("Full administrative icon and link catalog", TestFullAdminShortcutCatalog),
    ("Shortcut filename normalization", TestShortcutFileNames),
    ("Win+P script creation and backup", TestWinPLauncher),
    ("Native Shell link creation", TestShellLink),
    ("Launcher BAT command parsing", TestLauncherBat),
    ("Responsive main window layout", TestMainWindow),
    ("Repository invariants", TestRepositoryInvariants)
};

var failures = new List<string>();
foreach ((string name, Action run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception ex)
    {
        failures.Add($"{name}: {ex.Message}");
        Console.Error.WriteLine($"FAIL: {name}: {ex}");
    }
}

if (failures.Count > 0)
{
    Console.Error.WriteLine(
        $"Failed tests: {failures.Count}{Environment.NewLine}{string.Join(Environment.NewLine, failures)}");
    return 1;
}

Console.WriteLine($"All tests passed: {tests.Length}.");
return 0;

static void TestWallpaperLayouts()
{
    AssertEqual(new WallpaperRegistryValues("10", "0"), WallpaperLayout.Fill.GetRegistryValues());
    AssertEqual(new WallpaperRegistryValues("6", "0"), WallpaperLayout.Fit.GetRegistryValues());
    AssertEqual(new WallpaperRegistryValues("2", "0"), WallpaperLayout.Stretch.GetRegistryValues());
    AssertEqual(new WallpaperRegistryValues("0", "0"), WallpaperLayout.Center.GetRegistryValues());
    AssertEqual(new WallpaperRegistryValues("0", "1"), WallpaperLayout.Tile.GetRegistryValues());
    AssertEqual(new WallpaperRegistryValues("22", "0"), WallpaperLayout.Span.GetRegistryValues());
    Assert(WallpaperService.IsSupportedImageExtension("wallpaper.JPG"), "JPG must be supported.");
    Assert(!WallpaperService.IsSupportedImageExtension("wallpaper.exe"), "EXE must not be accepted as an image.");
}

static void TestAdminShortcutCatalog()
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    IReadOnlyList<ShortcutDefinition> catalog = AdminShortcutCatalog.Create();
    Assert(catalog.Count >= 40, $"Administrative catalog is unexpectedly small: {catalog.Count}.");
    Assert(catalog.Any(item => item.DisplayName == "Управление компьютером"), "Computer Management is missing.");
    Assert(catalog.Any(item => item.DisplayName == "Просмотр событий"), "Event Viewer is missing.");
    Assert(catalog.Any(item => item.DisplayName == "Службы"), "Services are missing.");
    Assert(catalog.Any(item => item.DisplayName == "Редактор реестра"), "Registry Editor is missing.");
    Assert(catalog.Any(item => item.DisplayName == "Результирующая политика"), "Resultant Set of Policy is missing.");
    Assert(catalog.Any(item => item.DisplayName == "Управление WMI"), "WMI Control is missing.");
    Assert(catalog.Any(item => item.DisplayName == "DiskPart"), "DiskPart is missing.");
    Assert(catalog.Any(item => item.DisplayName == "PowerShell (администратор)"), "Admin PowerShell is missing.");

    string[] duplicateNames = catalog
        .GroupBy(item => item.FileName, StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToArray();
    Assert(duplicateNames.Length == 0, $"Duplicate shortcut filenames: {string.Join(", ", duplicateNames)}");
    string[] duplicateBadges = catalog
        .Where(item => !string.IsNullOrWhiteSpace(item.IconBadge))
        .GroupBy(
            item => $"{item.Category}|{item.IconBadge}",
            StringComparer.OrdinalIgnoreCase)
        .Where(group => group.Count() > 1)
        .Select(group => group.Key)
        .ToArray();
    Assert(duplicateBadges.Length == 0, $"Duplicate category badges: {string.Join(", ", duplicateBadges)}");

    foreach (ShortcutDefinition definition in catalog)
    {
        Assert(File.Exists(definition.TargetPath), $"Target is missing: {definition.TargetPath}");
        Assert(File.Exists(definition.IconSourcePath), $"Icon source is missing: {definition.IconSourcePath}");
        Assert(!string.IsNullOrWhiteSpace(definition.Category), $"Category is missing for {definition.DisplayName}.");
        if (Path.GetFileName(definition.TargetPath).Equals("mmc.exe", StringComparison.OrdinalIgnoreCase))
        {
            Assert(!string.IsNullOrWhiteSpace(definition.IconBadge), $"MMC badge is missing for {definition.DisplayName}.");
        }
    }
}

static void TestFullAdminShortcutCatalog()
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    IReadOnlyList<ShortcutDefinition> catalog = AdminShortcutCatalog.Create();
    string directory = Path.Combine(Path.GetTempPath(), $"WindowsAdminShortcuts-catalog-test-{Guid.NewGuid():N}");
    string? iconSheetPath = Environment.GetEnvironmentVariable("WINDOWS_ADMIN_ICON_SHEET");
    string iconDirectory = string.IsNullOrWhiteSpace(iconSheetPath)
        ? Path.Combine(directory, "icons")
        : Path.Combine(
            Path.GetDirectoryName(iconSheetPath)
                ?? throw new InvalidOperationException("Icon sheet directory is missing."),
            "admin-shortcut-icons");
    string linkDirectory = Path.Combine(directory, "links");
    Directory.CreateDirectory(iconDirectory);
    Directory.CreateDirectory(linkDirectory);
    var renderedIcons = new List<(ShortcutDefinition Definition, string IconPath)>();

    try
    {
        foreach (ShortcutDefinition definition in catalog)
        {
            string iconPath = ShortcutIconService.CreateIconInDirectory(
                definition.IconSourcePath,
                definition.FileName,
                iconDirectory,
                definition.IconBadge,
                definition.Category);
            ShortcutIconService.VerifyIcon(iconPath);

            string linkPath = Path.Combine(linkDirectory, definition.FileName);
            ShellLink.Create(
                linkPath,
                definition.TargetPath,
                definition.Arguments,
                definition.Description,
                Path.GetDirectoryName(definition.TargetPath) ?? Environment.SystemDirectory,
                iconPath,
                definition.RunAsAdministrator);

            ShellLink.Inspection inspection = ShellLink.Inspect(linkPath);
            AssertEqual(Path.GetFullPath(iconPath), Path.GetFullPath(inspection.IconPath));
            AssertEqual(0, inspection.IconIndex);
            bool hasRunAsFlag = (inspection.Flags & ShellLink.RunAsUserFlag) != 0;
            AssertEqual(definition.RunAsAdministrator, hasRunAsFlag);
            renderedIcons.Add((definition, iconPath));
        }

        if (!string.IsNullOrWhiteSpace(iconSheetPath))
        {
            RenderIconSheet(renderedIcons, iconSheetPath);
        }
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void RenderIconSheet(
    IReadOnlyList<(ShortcutDefinition Definition, string IconPath)> items,
    string outputPath)
{
    const int columns = 4;
    const int cellWidth = 235;
    const int cellHeight = 78;
    int rows = (int)Math.Ceiling(items.Count / (double)columns);
    string outputDirectory = Path.GetDirectoryName(outputPath)
        ?? throw new InvalidOperationException("Icon sheet directory is missing.");
    Directory.CreateDirectory(outputDirectory);
    if (File.Exists(outputPath))
    {
        File.Delete(outputPath);
    }

    using var bitmap = new Bitmap(columns * cellWidth, rows * cellHeight);
    using Graphics graphics = Graphics.FromImage(bitmap);
    graphics.Clear(Color.White);
    using var titleFont = new Font("Segoe UI Semibold", 9F);
    using var categoryFont = new Font("Segoe UI", 8F);
    using var textBrush = new SolidBrush(Color.FromArgb(31, 41, 55));
    using var categoryBrush = new SolidBrush(Color.FromArgb(100, 116, 139));
    using var borderPen = new Pen(Color.FromArgb(226, 232, 240));

    for (int index = 0; index < items.Count; index++)
    {
        int column = index % columns;
        int row = index / columns;
        int x = column * cellWidth;
        int y = row * cellHeight;
        graphics.DrawRectangle(borderPen, x, y, cellWidth - 1, cellHeight - 1);

        using var icon = new Icon(items[index].IconPath);
        using Bitmap iconBitmap = icon.ToBitmap();
        graphics.DrawImage(iconBitmap, new Rectangle(x + 10, y + 18, 40, 40));
        graphics.DrawString(
            items[index].Definition.DisplayName,
            titleFont,
            textBrush,
            new RectangleF(x + 60, y + 14, cellWidth - 68, 36));
        graphics.DrawString(
            items[index].Definition.Category,
            categoryFont,
            categoryBrush,
            new RectangleF(x + 60, y + 50, cellWidth - 68, 20));
    }

    bitmap.Save(outputPath);
}

static void TestShortcutFileNames()
{
    AssertEqual("Панель управления.lnk", ShortcutService.NormalizeShortcutFileName("Панель управления"));
    AssertEqual("Панель управления.lnk", ShortcutService.NormalizeShortcutFileName(" Панель управления.lnk "));
    AssertEqual("bad_name.lnk", ShortcutService.NormalizeShortcutFileName("bad:name"));
    AssertThrows<ArgumentException>(() => ShortcutService.NormalizeShortcutFileName("   "));
    AssertThrows<FileNotFoundException>(
        () => ShortcutService.CreateCustom(
            "Missing",
            Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe"),
            string.Empty,
            runAsAdministrator: true,
            scope: DesktopScope.CurrentUser));
}

static void TestWinPLauncher()
{
    string directory = Path.Combine(Path.GetTempPath(), $"WindowsAdminShortcuts-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        LauncherCreateResult first = WinPLauncherService.CreateInDirectory(directory, overwrite: false);
        Assert(File.Exists(first.Path), "Launcher was not created.");
        Assert(first.BackupPath is null, "A new launcher must not create a backup.");
        WinPLauncherService.VerifyScript(first.Path);
        string script = File.ReadAllText(first.Path, Encoding.UTF8);
        Assert(
            script.Contains("DisplaySwitch.exe\" /clone", StringComparison.Ordinal),
            "Win+P launcher must select duplicate mode.");
        Assert(
            !script.Contains("/extend", StringComparison.OrdinalIgnoreCase) &&
            !script.Contains("/external", StringComparison.OrdinalIgnoreCase) &&
            !script.Contains("/internal", StringComparison.OrdinalIgnoreCase),
            "Win+P launcher contains a conflicting projection mode.");
        AssertThrows<IOException>(
            () => WinPLauncherService.CreateInDirectory(directory, overwrite: false));

        File.WriteAllText(first.Path, "@echo off\r\necho old\r\n", Encoding.ASCII);
        LauncherCreateResult second = WinPLauncherService.CreateInDirectory(directory, overwrite: true);
        Assert(second.BackupPath is not null && File.Exists(second.BackupPath), "Existing launcher was not backed up.");
        AssertEqual("@echo off\r\necho old\r\n", File.ReadAllText(second.BackupPath!, Encoding.ASCII));
        WinPLauncherService.VerifyScript(second.Path);
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestShellLink()
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    string directory = Path.Combine(Path.GetTempPath(), $"WindowsAdminShortcuts-link-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        string target = Path.Combine(Environment.SystemDirectory, "notepad.exe");
        string icon = ShortcutIconService.CreateIconInDirectory(target, "Notepad", directory);
        ShortcutIconService.VerifyIcon(icon);
        string link = Path.Combine(directory, "Notepad.lnk");
        ShellLink.Create(
            link,
            target,
            string.Empty,
            "Test link",
            Environment.SystemDirectory,
            icon,
            runAsAdministrator: true);
        Assert(File.Exists(link), "Shell link file was not created.");
        Assert(new FileInfo(link).Length > 0, "Shell link file is empty.");

        ShellLink.Inspection inspection = ShellLink.Inspect(link);
        AssertEqual(Path.GetFullPath(icon), Path.GetFullPath(inspection.IconPath));
        AssertEqual(0, inspection.IconIndex);
        Assert(
            (inspection.Flags & ShellLink.RunAsUserFlag) != 0,
            "Administrative shortcut does not contain the RunAsUser flag.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestLauncherBat()
{
    if (!OperatingSystem.IsWindows())
    {
        return;
    }

    string root = FindRepositoryRoot();
    string directory = Path.Combine(Path.GetTempPath(), $"WindowsAdminShortcuts-bat-test-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        string launcher = Path.Combine(directory, "Start-WindowsAdminShortcuts.bat");
        File.Copy(Path.Combine(root, "Start-WindowsAdminShortcuts.bat"), launcher);

        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
            Arguments = $"/d /s /c \"\"{launcher}\" < nul\""
        };

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("cmd.exe did not start.");
        string output = process.StandardOutput.ReadToEnd();
        string error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        string combined = $"{output}\n{error}";

        AssertEqual(1, process.ExitCode);
        Assert(
            combined.Contains("Ошибка: файл WindowsAdminShortcuts.exe не найден.", StringComparison.Ordinal),
            $"Expected launcher error was not printed. Output: {combined}");
        Assert(
            !combined.Contains("not recognized as an internal or external command", StringComparison.OrdinalIgnoreCase),
            $"cmd.exe parsed launcher lines as commands. Output: {combined}");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void TestMainWindow()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            using var form = new MainForm();
            form.ShowInTaskbar = false;
            form.Location = new Point(-32000, -32000);
            form.Show();
            Application.DoEvents();
            form.PerformLayout();

            TabControl[] tabControls = FindControls<TabControl>(form).ToArray();
            TabControl? tabs = tabControls.SingleOrDefault(control => control.TabPages.Count == 3);
            TabControl? shortcutSections = tabControls.SingleOrDefault(control => control.TabPages.Count == 2);
            CheckedListBox? shortcuts = FindControl<CheckedListBox>(form);
            Assert(tabs is not null, "Main window does not contain a tab control.");
            Assert(shortcutSections is not null, "Shortcut sections are not separated into pages.");
            Assert(shortcuts is not null, "Main window does not contain the shortcut catalog.");
            AssertEqual(3, tabs!.TabPages.Count);
            AssertEqual(AdminShortcutCatalog.Create().Count, shortcuts!.Items.Count);
            Assert(shortcuts.Height >= 250, $"Shortcut catalog is too short: {shortcuts.Height}px.");
            Assert(shortcuts.ScrollAlwaysVisible, "Shortcut catalog scrollbar must remain visible.");
            shortcuts.TopIndex = shortcuts.Items.Count - 1;
            Application.DoEvents();
            Assert(shortcuts.TopIndex > 0, "Shortcut catalog cannot scroll to its final items.");
            shortcuts.TopIndex = 0;

            TextBox? search = FindControlByName<TextBox>(form, "ShortcutSearch");
            ComboBox? category = FindControlByName<ComboBox>(form, "ShortcutCategory");
            Assert(search is not null, "Shortcut search is missing.");
            Assert(category is not null, "Shortcut category filter is missing.");
            search!.Text = "WMI";
            Application.DoEvents();
            AssertEqual(1, shortcuts.Items.Count);
            Assert(
                shortcuts.Items[0]?.ToString()?.Contains("WMI", StringComparison.OrdinalIgnoreCase) == true,
                "Shortcut search returned an unrelated item.");
            search.Clear();
            category!.SelectedItem = "Сеть";
            Application.DoEvents();
            Assert(
                shortcuts.Items.Cast<object>().All(item =>
                    item.ToString()?.Contains("[Сеть]", StringComparison.Ordinal) == true),
                "Category filter returned an unrelated item.");
            category.SelectedIndex = 0;
            Application.DoEvents();
            AssertEqual(AdminShortcutCatalog.Create().Count, shortcuts.Items.Count);

            Assert(form.ClientSize.Width >= 900, "Main window is too narrow.");
            Assert(form.ClientSize.Height >= 640, "Main window is too short.");
            AssertNoInteractiveControlOverlaps(form);

            shortcutSections!.SelectedIndex = 1;
            Application.DoEvents();
            TextBox? customName = FindControlByName<TextBox>(form, "CustomShortcutName");
            TextBox? customArguments = FindControlByName<TextBox>(form, "CustomShortcutArguments");
            Assert(customName is not null && customName.Width >= 500, "Custom shortcut name field is too narrow.");
            Assert(
                customArguments is not null && customArguments.Width >= 500,
                "Custom shortcut arguments field is too narrow.");
            AssertNoInteractiveControlOverlaps(form);
            shortcutSections.SelectedIndex = 0;
            Application.DoEvents();

            Size defaultSize = form.Size;
            form.Size = form.MinimumSize;
            Application.DoEvents();
            form.PerformLayout();
            AssertNoInteractiveControlOverlaps(form);
            form.Size = defaultSize;
            Application.DoEvents();

            string? screenshotPath = Environment.GetEnvironmentVariable("WINDOWS_ADMIN_SCREENSHOT");
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                string screenshotDirectory = Path.GetDirectoryName(screenshotPath)
                    ?? throw new InvalidOperationException("Screenshot directory is missing.");
                Directory.CreateDirectory(screenshotDirectory);
                for (int tabIndex = 0; tabIndex < tabs.TabPages.Count; tabIndex++)
                {
                    tabs.SelectedIndex = tabIndex;
                    Application.DoEvents();
                    string tabScreenshotPath = tabIndex == 0
                        ? screenshotPath
                        : Path.Combine(
                            screenshotDirectory,
                            $"{Path.GetFileNameWithoutExtension(screenshotPath)}-{tabIndex}{Path.GetExtension(screenshotPath)}");
                    SaveWindowScreenshot(form, tabScreenshotPath);
                }

                tabs.SelectedIndex = 0;
                shortcutSections!.SelectedIndex = 1;
                Application.DoEvents();
                SaveWindowScreenshot(
                    form,
                    Path.Combine(
                        screenshotDirectory,
                        $"{Path.GetFileNameWithoutExtension(screenshotPath)}-custom{Path.GetExtension(screenshotPath)}"));

                shortcutSections.SelectedIndex = 0;
                form.Size = form.MinimumSize;
                Application.DoEvents();
                SaveWindowScreenshot(
                    form,
                    Path.Combine(
                        screenshotDirectory,
                        $"{Path.GetFileNameWithoutExtension(screenshotPath)}-compact{Path.GetExtension(screenshotPath)}"));
                form.Size = defaultSize;
            }

            form.Hide();
            AssertScaledMainWindowLayout(1.25F);
            AssertScaledMainWindowLayout(1.50F);
        }
        catch (Exception ex)
        {
            failure = ex;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
    {
        throw new InvalidOperationException("Main window smoke test failed.", failure);
    }
}

static void AssertScaledMainWindowLayout(float scale)
{
    using var form = new MainForm
    {
        ShowInTaskbar = false,
        Location = new Point(-32000, -32000)
    };
    form.Show();
    form.Scale(new SizeF(scale, scale));
    form.PerformLayout();
    Application.DoEvents();

    TabControl[] tabControls = FindControls<TabControl>(form).ToArray();
    TabControl outerTabs = tabControls.Single(control => control.TabPages.Count == 3);
    TabControl shortcutSections = tabControls.Single(control => control.TabPages.Count == 2);
    for (int tabIndex = 0; tabIndex < outerTabs.TabPages.Count; tabIndex++)
    {
        outerTabs.SelectedIndex = tabIndex;
        Application.DoEvents();
        AssertNoInteractiveControlOverlaps(form);
    }

    outerTabs.SelectedIndex = 0;
    shortcutSections.SelectedIndex = 1;
    Application.DoEvents();
    AssertNoInteractiveControlOverlaps(form);
    form.Hide();
}

static void AssertNoInteractiveControlOverlaps(Control root)
{
    Control[] interactive = FindControls<Control>(root)
        .Where(control =>
            control.Visible &&
            control is Button or CheckBox or CheckedListBox or ComboBox or TextBox)
        .ToArray();
    for (int firstIndex = 0; firstIndex < interactive.Length; firstIndex++)
    {
        Rectangle first = interactive[firstIndex].RectangleToScreen(interactive[firstIndex].ClientRectangle);
        for (int secondIndex = firstIndex + 1; secondIndex < interactive.Length; secondIndex++)
        {
            Rectangle second = interactive[secondIndex].RectangleToScreen(interactive[secondIndex].ClientRectangle);
            if (first.IntersectsWith(second))
            {
                throw new InvalidOperationException(
                    $"Interactive controls overlap: '{interactive[firstIndex].Text}' and " +
                    $"'{interactive[secondIndex].Text}'.");
            }
        }
    }
}

static void SaveWindowScreenshot(Form form, string path)
{
    if (File.Exists(path))
    {
        File.Delete(path);
    }

    using var bitmap = new Bitmap(form.Width, form.Height);
    form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
    AssertRenderedClientArea(bitmap);
    bitmap.Save(path);
}

static void AssertRenderedClientArea(Bitmap bitmap)
{
    var colors = new HashSet<int>();
    for (int y = 40; y < bitmap.Height; y += 10)
    {
        for (int x = 10; x < bitmap.Width - 10; x += 10)
        {
            colors.Add(bitmap.GetPixel(x, y).ToArgb());
        }
    }

    Assert(colors.Count >= 10, "Rendered window client area is blank.");
}

static void TestRepositoryInvariants()
{
    string root = FindRepositoryRoot();
    string attributes = File.ReadAllText(Path.Combine(root, ".gitattributes"));
    Assert(attributes.Contains("*.bat text eol=crlf", StringComparison.Ordinal), "BAT CRLF rule is missing.");
    Assert(attributes.Contains("*.cmd text eol=crlf", StringComparison.Ordinal), "CMD CRLF rule is missing.");

    AssertHasNoLoneLf(Path.Combine(root, "Start-WindowsAdminShortcuts.bat"));

    string manifest = File.ReadAllText(
        Path.Combine(root, "src", "WindowsAdminShortcuts", "app.manifest"));
    Assert(
        manifest.Contains("requestedExecutionLevel level=\"requireAdministrator\"", StringComparison.Ordinal),
        "All-user operations must require administrator rights.");

    string project = File.ReadAllText(
        Path.Combine(root, "src", "WindowsAdminShortcuts", "WindowsAdminShortcuts.csproj"));
    Assert(
        project.Contains(
            "<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>",
            StringComparison.Ordinal),
        "Single-file publish must embed native runtime libraries.");

    string sourceDirectory = Path.Combine(root, "src", "WindowsAdminShortcuts");
    int applicationRunCount = Directory
        .EnumerateFiles(sourceDirectory, "*.cs", SearchOption.TopDirectoryOnly)
        .Sum(path => CountOccurrences(File.ReadAllText(path), "Application.Run("));
    AssertEqual(1, applicationRunCount);

    string catalogSource = File.ReadAllText(Path.Combine(sourceDirectory, "AdminShortcutCatalog.cs"));
    Assert(
        !catalogSource.Contains("imageres.dll", StringComparison.OrdinalIgnoreCase),
        "Hard-coded imageres icon indices are not allowed.");
    Assert(
        File.Exists(Path.Combine(sourceDirectory, "ShortcutIconService.cs")),
        "Canonical Shell icon extraction service is missing.");

    string winPSource = File.ReadAllText(Path.Combine(sourceDirectory, "WinPLauncherService.cs"));
    Assert(
        winPSource.Contains("DisplaySwitch.exe", StringComparison.Ordinal),
        "Win+P launcher must use DisplaySwitch.exe.");
    Assert(
        winPSource.Contains("/clone", StringComparison.Ordinal),
        "Win+P launcher must select duplicate mode.");
    Assert(
        !winPSource.Contains("SendKeys", StringComparison.OrdinalIgnoreCase),
        "Keyboard emulation is not an allowed Win+P path.");
}

static T? FindControl<T>(Control root)
    where T : Control
{
    foreach (Control child in root.Controls)
    {
        if (child is T match)
        {
            return match;
        }

        T? nested = FindControl<T>(child);
        if (nested is not null)
        {
            return nested;
        }
    }

    return null;
}

static T? FindControlByName<T>(Control root, string name)
    where T : Control
{
    return FindControls<T>(root).FirstOrDefault(
        control => control.Name.Equals(name, StringComparison.Ordinal));
}

static IEnumerable<T> FindControls<T>(Control root)
    where T : Control
{
    foreach (Control child in root.Controls)
    {
        if (child is T match)
        {
            yield return match;
        }

        foreach (T nested in FindControls<T>(child))
        {
            yield return nested;
        }
    }
}

static string FindRepositoryRoot()
{
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, ".gitattributes")))
        {
            return current.FullName;
        }

        current = current.Parent;
    }

    throw new DirectoryNotFoundException("Repository root was not found.");
}

static void AssertHasNoLoneLf(string path)
{
    byte[] bytes = File.ReadAllBytes(path);
    for (int index = 0; index < bytes.Length; index++)
    {
        if (bytes[index] == (byte)'\n' && (index == 0 || bytes[index - 1] != (byte)'\r'))
        {
            throw new InvalidDataException($"{path} contains LF without CRLF.");
        }
    }
}

static int CountOccurrences(string text, string value)
{
    int count = 0;
    int offset = 0;
    while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
    {
        count++;
        offset += value.Length;
    }

    return count;
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected '{expected}', got '{actual}'.");
    }
}

static void AssertThrows<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected exception {typeof(TException).Name}.");
}
