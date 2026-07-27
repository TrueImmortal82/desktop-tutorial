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
    ("Application icon and AS IS license", TestApplicationBrandingAndLicense),
    ("Settings, localization and themes", TestSettingsLocalizationAndThemes),
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

static void TestApplicationBrandingAndLicense()
{
    string root = FindRepositoryRoot();
    string licensePath = Path.Combine(root, "LICENSE.txt");
    string iconPath = Path.Combine(
        root,
        "src",
        "WindowsAdminShortcuts",
        "Assets",
        "WindowsAdminShortcuts.ico");

    Assert(File.Exists(licensePath), "External license file is missing.");
    Assert(File.Exists(iconPath), "Application icon file is missing.");
    string externalLicense = File.ReadAllText(licensePath, Encoding.UTF8);
    AssertEqual(externalLicense, LicenseAgreementService.LicenseText);
    Assert(
        externalLicense.Contains("AS IS", StringComparison.Ordinal) &&
        externalLicense.Contains("WITHOUT WARRANTY", StringComparison.Ordinal),
        "License does not contain the required AS IS warranty disclaimer.");
    Assert(
        externalLicense.Contains("ENGLISH", StringComparison.Ordinal) &&
        externalLicense.Contains("O‘ZBEKCHA", StringComparison.Ordinal),
        "License is not available in all three interface languages.");
    Assert(
        !externalLicense.Contains(string.Concat("Vlad", "islav"), StringComparison.OrdinalIgnoreCase) &&
        !externalLicense.Contains(string.Concat("Влади", "слав"), StringComparison.OrdinalIgnoreCase),
        "License contains a real personal name.");
    AssertEqual(64, LicenseAgreementService.LicenseHash.Length);

    string acceptanceDirectory = Path.Combine(
        Path.GetTempPath(),
        $"WindowsAdminShortcuts-license-test-{Guid.NewGuid():N}");
    try
    {
        Assert(
            !LicenseAgreementService.IsAccepted(acceptanceDirectory),
            "Missing acceptance must not be treated as accepted.");
        Directory.CreateDirectory(acceptanceDirectory);
        File.WriteAllText(
            Path.Combine(acceptanceDirectory, LicenseAgreementService.AcceptanceFileName),
            "INVALID",
            Encoding.UTF8);
        Assert(
            !LicenseAgreementService.IsAccepted(acceptanceDirectory),
            "An unrelated license hash must not be accepted.");
        LicenseAgreementService.RecordAcceptance(acceptanceDirectory);
        Assert(
            LicenseAgreementService.IsAccepted(acceptanceDirectory),
            "Recorded license acceptance was not recognized.");
    }
    finally
    {
        Directory.Delete(acceptanceDirectory, recursive: true);
    }

    using (var icon = AppIcon.Load())
    {
        Assert(icon.Width > 0 && icon.Height > 0, "Embedded application icon is invalid.");
    }

    using FileStream iconStream = File.OpenRead(iconPath);
    using var reader = new BinaryReader(iconStream);
    AssertEqual((ushort)0, reader.ReadUInt16());
    AssertEqual((ushort)1, reader.ReadUInt16());
    ushort imageCount = reader.ReadUInt16();
    AssertEqual((ushort)9, imageCount);
    var sizes = new List<int>();
    for (int index = 0; index < imageCount; index++)
    {
        byte width = reader.ReadByte();
        byte height = reader.ReadByte();
        sizes.Add(width == 0 ? 256 : width);
        AssertEqual(width, height);
        reader.ReadByte();
        reader.ReadByte();
        AssertEqual((ushort)1, reader.ReadUInt16());
        AssertEqual((ushort)32, reader.ReadUInt16());
        Assert(reader.ReadUInt32() > 0, "ICO image payload is empty.");
        Assert(reader.ReadUInt32() >= 6 + (16 * imageCount), "ICO image offset is invalid.");
    }

    AssertEqual(
        "16,20,24,32,40,48,64,128,256",
        string.Join(",", sizes));
}

static void TestSettingsLocalizationAndThemes()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"WindowsAdminShortcuts-settings-test-{Guid.NewGuid():N}");
    try
    {
        var expected = new AppSettings(AppLanguage.Uzbek, AppTheme.Dark);
        AppSettingsService.Save(directory, expected);
        AssertEqual(expected, AppSettingsService.Load(directory));

        File.WriteAllText(
            Path.Combine(directory, AppSettingsService.SettingsFileName),
            """{"Language":"Unknown","Theme":"Dark"}""",
            Encoding.UTF8);
        AssertThrows<InvalidDataException>(() => AppSettingsService.Load(directory));

        IReadOnlyList<ShortcutDefinition> catalog = AdminShortcutCatalog.Create();
        foreach (ShortcutDefinition shortcut in catalog)
        {
            Assert(
                UiLocalization.HasCatalogTranslation(shortcut.Category),
                $"Category has no localization: {shortcut.Category}");
            Assert(
                UiLocalization.HasCatalogTranslation(shortcut.DisplayName),
                $"Shortcut has no localization: {shortcut.DisplayName}");
        }

        foreach (AppLanguage language in Enum.GetValues<AppLanguage>())
        {
            AppSettingsService.UseTransient(new AppSettings(language, AppTheme.Light));
            Assert(
                !string.IsNullOrWhiteSpace(
                    UiLocalization.Text("Русский", "English", "O‘zbekcha")),
                $"Interface text is missing for {language}.");
            Assert(
                !string.IsNullOrWhiteSpace(
                    UiLocalization.CatalogText("Управление компьютером")),
                $"Catalog text is missing for {language}.");
        }

        Assert(
            ThemePalette.Light.Background != ThemePalette.Dark.Background &&
            ThemePalette.Light.Text != ThemePalette.Dark.Text &&
            ThemePalette.Light.Accent != ThemePalette.Dark.Accent,
            "Light and dark palettes are not distinct.");
    }
    finally
    {
        AppSettingsService.UseTransient(
            new AppSettings(AppLanguage.Russian, AppTheme.Light));
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}

static void TestMainWindow()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        try
        {
            AppSettingsService.UseTransient(
                new AppSettings(AppLanguage.Russian, AppTheme.Light));
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
            LanguageSelectorControl? languageSelector =
                FindControl<LanguageSelectorControl>(form);
            ThemeToggleButton? themeToggle = FindControl<ThemeToggleButton>(form);
            Assert(
                languageSelector is not null &&
                FindControls<ModernButton>(languageSelector).Count() == 3,
                "RU/EN/UZ language buttons are missing.");
            Assert(themeToggle is not null, "Sun/moon theme button is missing.");

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
            string? screenshotDirectory = null;
            if (!string.IsNullOrWhiteSpace(screenshotPath))
            {
                screenshotDirectory = Path.GetDirectoryName(screenshotPath)
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

                tabs.SelectedIndex = 0;
                shortcutSections.SelectedIndex = 0;
                AppSettingsService.UseTransient(
                    new AppSettings(AppLanguage.English, AppTheme.Dark));
                Application.DoEvents();
                AssertNoInteractiveControlOverlaps(form);
                Assert(
                    shortcuts.Items.Cast<object>().Any(item =>
                        item.ToString()?.Contains("[System]", StringComparison.Ordinal) == true),
                    "English catalog localization was not applied.");
                SaveWindowScreenshot(
                    form,
                    GetPreviewVariantPath(screenshotPath, "dark"));

                AppSettingsService.UseTransient(
                    new AppSettings(AppLanguage.Uzbek, AppTheme.Light));
                Application.DoEvents();
                AssertNoInteractiveControlOverlaps(form);
                Assert(
                    shortcuts.Items.Cast<object>().Any(item =>
                        item.ToString()?.Contains("[Tizim]", StringComparison.Ordinal) == true),
                    "Uzbek catalog localization was not applied.");
                SaveWindowScreenshot(
                    form,
                    GetPreviewVariantPath(screenshotPath, "uzbek"));

                AppSettingsService.UseTransient(
                    new AppSettings(AppLanguage.Russian, AppTheme.Light));
                Application.DoEvents();
            }

            form.Hide();
            AssertScaledMainWindowLayout(
                1.25F,
                new AppSettings(AppLanguage.Russian, AppTheme.Light));
            AssertScaledMainWindowLayout(
                1.50F,
                new AppSettings(AppLanguage.Russian, AppTheme.Light));
            AssertScaledMainWindowLayout(
                1.25F,
                new AppSettings(AppLanguage.English, AppTheme.Dark));
            AssertScaledMainWindowLayout(
                1.25F,
                new AppSettings(AppLanguage.Uzbek, AppTheme.Light));

            AppSettingsService.UseTransient(
                new AppSettings(AppLanguage.Russian, AppTheme.Light));
            using var agreement = new LicenseAgreementForm(LicenseAgreementService.LicenseText)
            {
                ShowInTaskbar = false,
                Location = new Point(-32000, -32000)
            };
            agreement.Show();
            Application.DoEvents();
            agreement.PerformLayout();
            TextBox? licenseText = FindControlByName<TextBox>(agreement, "LicenseText");
            CheckBox? acceptance = FindControlByName<CheckBox>(agreement, "LicenseAcceptance");
            Assert(licenseText is not null && licenseText.Multiline, "License text box is missing.");
            Assert(acceptance is not null, "License acceptance checkbox is missing.");
            AssertNoInteractiveControlOverlaps(agreement);
            if (!string.IsNullOrWhiteSpace(screenshotPath) &&
                screenshotDirectory is not null)
            {
                SaveWindowScreenshot(
                    agreement,
                    Path.Combine(
                        screenshotDirectory,
                        $"{Path.GetFileNameWithoutExtension(screenshotPath)}-license" +
                        $"{Path.GetExtension(screenshotPath)}"));
            }
            agreement.Hide();
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

static void AssertScaledMainWindowLayout(float scale, AppSettings settings)
{
    AppSettingsService.UseTransient(settings);
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
            IsEffectivelyVisible(control) &&
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
                    $"Interactive controls overlap: {DescribeControl(interactive[firstIndex], first)} and " +
                    $"{DescribeControl(interactive[secondIndex], second)}.");
            }
        }
    }
}

static bool IsEffectivelyVisible(Control control)
{
    for (Control? current = control; current is not null; current = current.Parent)
    {
        if (!current.Visible)
        {
            return false;
        }

        if (current is TabPage page &&
            page.Parent is TabControl tabs &&
            tabs.SelectedTab != page)
        {
            return false;
        }
    }

    return true;
}

static string DescribeControl(Control control, Rectangle bounds)
{
    string name = string.IsNullOrWhiteSpace(control.Name) ? "<unnamed>" : control.Name;
    return $"{control.GetType().Name} '{name}'/'{control.Text}' at {bounds}";
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

static string GetPreviewVariantPath(string lightPath, string variant)
{
    string directory = Path.GetDirectoryName(lightPath)
        ?? throw new InvalidOperationException("Screenshot directory is missing.");
    string stem = Path.GetFileNameWithoutExtension(lightPath);
    if (stem.EndsWith("-light", StringComparison.OrdinalIgnoreCase))
    {
        stem = stem[..^"-light".Length];
    }

    return Path.Combine(
        directory,
        $"{stem}-{variant}{Path.GetExtension(lightPath)}");
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
    Assert(
        project.Contains("<ApplicationIcon>Assets\\WindowsAdminShortcuts.ico</ApplicationIcon>", StringComparison.Ordinal),
        "Application icon is not configured as the executable icon.");
    Assert(
        project.Contains("WindowsAdminShortcuts.LICENSE.txt", StringComparison.Ordinal),
        "License is not embedded in the executable.");
    Assert(
        project.Contains("<Authors>True Immortal</Authors>", StringComparison.Ordinal),
        "Executable author metadata must use only the public name.");
    Assert(
        project.Contains(
            "<IncludeSourceRevisionInInformationalVersion>false</IncludeSourceRevisionInInformationalVersion>",
            StringComparison.Ordinal),
        "Published ProductVersion must not expose a stale source revision.");

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

    string programSource = File.ReadAllText(Path.Combine(sourceDirectory, "Program.cs"));
    Assert(
        programSource.Contains("LicenseAgreementService.EnsureAccepted()", StringComparison.Ordinal),
        "Application startup does not enforce license acceptance.");
    Assert(
        programSource.Contains("AppSettingsService.Initialize()", StringComparison.Ordinal),
        "Application startup does not load the canonical interface settings.");

    string readme = File.ReadAllText(Path.Combine(root, "README.md"));
    Assert(
        readme.Contains("## Русский", StringComparison.Ordinal) &&
        readme.Contains("## English", StringComparison.Ordinal) &&
        readme.Contains("## O‘zbekcha", StringComparison.Ordinal),
        "README does not contain instructions in all three languages.");
    Assert(
        readme.Contains("docs/screenshots/windows-admin-center-light.png", StringComparison.Ordinal) &&
        readme.Contains("docs/screenshots/windows-admin-center-dark.png", StringComparison.Ordinal),
        "README does not reference both real UI previews.");
    foreach (string preview in new[]
    {
        "windows-admin-center-light.png",
        "windows-admin-center-dark.png",
        "windows-admin-center-uzbek.png",
        "admin-shortcut-icons.png"
    })
    {
        Assert(
            File.Exists(Path.Combine(root, "docs", "screenshots", preview)),
            $"README preview is missing: {preview}");
    }

    string workflow = File.ReadAllText(
        Path.Combine(root, ".github", "workflows", "build.yml"));
    Assert(
        workflow.Contains("dist/LICENSE.txt", StringComparison.Ordinal),
        "GitHub build artifact does not include LICENSE.txt.");

    string[] publicSources = Directory
        .EnumerateFiles(root, "*", SearchOption.AllDirectories)
        .Where(path =>
            !path.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            !path.Contains($"{Path.DirectorySeparatorChar}artifacts{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
            Path.GetExtension(path) is ".cs" or ".md" or ".txt" or ".csproj")
        .ToArray();
    foreach (string path in publicSources)
    {
        string contents = File.ReadAllText(path);
        Assert(
            !contents.Contains(
                string.Concat("Vlad", "islav ", "Nare", "chev"),
                StringComparison.OrdinalIgnoreCase) &&
            !contents.Contains(
                string.Concat("Влади", "слав ", "Наре", "чев"),
                StringComparison.OrdinalIgnoreCase),
            $"Real personal name remains in {path}.");
    }
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
