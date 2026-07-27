namespace WindowsAdminShortcuts;

internal static class AdminShortcutCatalog
{
    internal static IReadOnlyList<ShortcutDefinition> Create()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string system32 = Environment.SystemDirectory;
        string explorer = Path.Combine(windows, "explorer.exe");
        string control = Path.Combine(system32, "control.exe");
        string mmc = Path.Combine(system32, "mmc.exe");
        var shortcuts = new List<ShortcutDefinition>();

        Add(shortcuts, "Основное", "Управление компьютером", "Управление компьютером.lnk",
            mmc, Quote(Path.Combine(system32, "compmgmt.msc")),
            "Управление дисками, пользователями, устройствами и службами",
            Path.Combine(system32, "compmgmt.msc"), Path.Combine(system32, "compmgmt.msc"),
            iconBadge: "УК");
        Add(shortcuts, "Основное", "Панель управления", "Панель управления.lnk",
            control, string.Empty, "Классическая панель управления Windows", control,
            runAsAdministrator: false);
        Add(shortcuts, "Основное", "Параметры Windows", "Параметры Windows.lnk",
            explorer, "ms-settings:", "Современные параметры Windows",
            Path.Combine(windows, "ImmersiveControlPanel", "SystemSettings.exe"),
            Path.Combine(windows, "ImmersiveControlPanel", "SystemSettings.exe"),
            runAsAdministrator: false);
        Add(shortcuts, "Основное", "Инструменты Windows", "Инструменты Windows.lnk",
            explorer, "shell:::{D20EA4E1-3957-11D2-A40B-0C5020524153}",
            "Полный набор встроенных инструментов Windows", control,
            runAsAdministrator: false, iconBadge: "ИW");
        Add(shortcuts, "Основное", "Диспетчер задач", "Диспетчер задач.lnk",
            Path.Combine(system32, "taskmgr.exe"), string.Empty,
            "Процессы, производительность и автозагрузка",
            Path.Combine(system32, "taskmgr.exe"));

        AddMmc(shortcuts, "Система", "Просмотр событий", "Просмотр событий.lnk",
            "eventvwr.msc", "Системные журналы Windows", system32, mmc);
        AddMmc(shortcuts, "Система", "Службы", "Службы.lnk",
            "services.msc", "Управление службами Windows", system32, mmc);
        AddMmc(shortcuts, "Система", "Планировщик заданий", "Планировщик заданий.lnk",
            "taskschd.msc", "Управление плановыми заданиями", system32, mmc);
        AddMmc(shortcuts, "Система", "Монитор производительности", "Монитор производительности.lnk",
            "perfmon.msc", "Счётчики производительности и наборы сборщиков данных", system32, mmc);
        Add(shortcuts, "Система", "Монитор ресурсов", "Монитор ресурсов.lnk",
            Path.Combine(system32, "resmon.exe"), string.Empty,
            "Нагрузка CPU, памяти, дисков и сети", Path.Combine(system32, "resmon.exe"));
        Add(shortcuts, "Система", "Монитор стабильности", "Монитор стабильности.lnk",
            Path.Combine(system32, "perfmon.exe"), "/rel",
            "История стабильности и сбоев Windows", Path.Combine(system32, "perfmon.exe"));
        Add(shortcuts, "Система", "Конфигурация системы", "Конфигурация системы.lnk",
            Path.Combine(system32, "msconfig.exe"), string.Empty,
            "Параметры загрузки и диагностики Windows", Path.Combine(system32, "msconfig.exe"));
        Add(shortcuts, "Система", "Сведения о системе", "Сведения о системе.lnk",
            Path.Combine(system32, "msinfo32.exe"), string.Empty,
            "Аппаратная и программная конфигурация компьютера", Path.Combine(system32, "msinfo32.exe"),
            runAsAdministrator: false);
        Add(shortcuts, "Система", "Свойства системы", "Свойства системы.lnk",
            Path.Combine(system32, "SystemPropertiesAdvanced.exe"), string.Empty,
            "Дополнительные параметры системы и переменные среды",
            Path.Combine(system32, "SystemPropertiesAdvanced.exe"));
        Add(shortcuts, "Система", "Диагностика памяти Windows", "Диагностика памяти Windows.lnk",
            Path.Combine(system32, "MdSched.exe"), string.Empty,
            "Проверка оперативной памяти после перезагрузки", Path.Combine(system32, "MdSched.exe"));
        AddMmc(shortcuts, "Система", "Службы компонентов", "Службы компонентов.lnk",
            "comexp.msc", "COM+, DCOM и службы компонентов", system32, mmc,
            iconBadge: "КС");
        AddMmc(shortcuts, "Система", "Управление WMI", "Управление WMI.lnk",
            "wmimgmt.msc", "Параметры и безопасность Windows Management Instrumentation", system32, mmc,
            iconBadge: "WMI");
        Add(shortcuts, "Система", "Восстановление системы", "Восстановление системы.lnk",
            Path.Combine(system32, "rstrui.exe"), string.Empty,
            "Откат системных файлов и параметров к точке восстановления",
            Path.Combine(system32, "rstrui.exe"));
        Add(shortcuts, "Система", "Электропитание", "Электропитание.lnk",
            control, Quote(Path.Combine(system32, "powercfg.cpl")),
            "Схемы питания и параметры энергосбережения",
            Path.Combine(system32, "powercfg.cpl"), Path.Combine(system32, "powercfg.cpl"),
            runAsAdministrator: false, iconBadge: "ЭП");
        Add(shortcuts, "Система", "Дата и время", "Дата и время.lnk",
            control, Quote(Path.Combine(system32, "timedate.cpl")),
            "Часовой пояс, дата, время и синхронизация",
            Path.Combine(system32, "timedate.cpl"), Path.Combine(system32, "timedate.cpl"),
            runAsAdministrator: false, iconBadge: "ДВ");

        AddMmc(shortcuts, "Оборудование и диски", "Диспетчер устройств", "Диспетчер устройств.lnk",
            "devmgmt.msc", "Устройства и драйверы Windows", system32, mmc);
        AddMmc(shortcuts, "Оборудование и диски", "Управление дисками", "Управление дисками.lnk",
            "diskmgmt.msc", "Разделы, тома и буквы дисков", system32, mmc);
        Add(shortcuts, "Оборудование и диски", "Оптимизация дисков", "Оптимизация дисков.lnk",
            Path.Combine(system32, "dfrgui.exe"), string.Empty,
            "Оптимизация и дефрагментация накопителей", Path.Combine(system32, "dfrgui.exe"));
        Add(shortcuts, "Оборудование и диски", "Очистка диска", "Очистка диска.lnk",
            Path.Combine(system32, "cleanmgr.exe"), string.Empty,
            "Очистка временных и системных файлов", Path.Combine(system32, "cleanmgr.exe"));
        Add(shortcuts, "Оборудование и диски", "Компоненты Windows", "Компоненты Windows.lnk",
            Path.Combine(system32, "OptionalFeatures.exe"), string.Empty,
            "Включение и отключение компонентов Windows", Path.Combine(system32, "OptionalFeatures.exe"));
        Add(shortcuts, "Оборудование и диски", "DiskPart", "DiskPart.lnk",
            Path.Combine(system32, "diskpart.exe"), string.Empty,
            "Командное управление дисками, разделами и томами", Path.Combine(system32, "diskpart.exe"));
        AddMmc(shortcuts, "Оборудование и диски", "Управление печатью", "Управление печатью.lnk",
            "printmanagement.msc", "Принтеры, драйверы и очереди печати", system32, mmc);

        Add(shortcuts, "Безопасность и пользователи", "Редактор реестра", "Редактор реестра.lnk",
            Path.Combine(windows, "regedit.exe"), string.Empty,
            "Системный реестр Windows", Path.Combine(windows, "regedit.exe"));
        AddMmc(shortcuts, "Безопасность и пользователи", "Локальные пользователи и группы",
            "Локальные пользователи и группы.lnk", "lusrmgr.msc",
            "Локальные учётные записи и группы", system32, mmc,
            iconBadge: "ЛГ");
        AddMmc(shortcuts, "Безопасность и пользователи", "Локальная политика безопасности",
            "Локальная политика безопасности.lnk", "secpol.msc",
            "Локальные политики безопасности", system32, mmc,
            iconBadge: "ПБ");
        AddMmc(shortcuts, "Безопасность и пользователи", "Редактор групповой политики",
            "Редактор групповой политики.lnk", "gpedit.msc",
            "Локальная групповая политика Windows", system32, mmc);
        AddMmc(shortcuts, "Безопасность и пользователи", "Результирующая политика",
            "Результирующая политика.lnk", "rsop.msc",
            "Фактически применённые параметры групповой политики", system32, mmc,
            iconBadge: "РП");
        AddMmc(shortcuts, "Безопасность и пользователи", "Брандмауэр в расширенном режиме",
            "Брандмауэр в расширенном режиме.lnk", "wf.msc",
            "Правила входящего и исходящего трафика", system32, mmc);
        AddMmc(shortcuts, "Безопасность и пользователи", "Общие папки",
            "Общие папки.lnk", "fsmgmt.msc",
            "Сетевые ресурсы, сеансы и открытые файлы", system32, mmc,
            iconBadge: "ОП");
        AddMmc(shortcuts, "Безопасность и пользователи", "Диспетчер авторизации",
            "Диспетчер авторизации.lnk", "azman.msc",
            "Хранилища политик авторизации Windows", system32, mmc,
            iconBadge: "ДА");
        AddMmc(shortcuts, "Безопасность и пользователи", "Сертификаты текущего пользователя",
            "Сертификаты текущего пользователя.lnk", "certmgr.msc",
            "Хранилища сертификатов текущего пользователя", system32, mmc,
            runAsAdministrator: false);
        AddMmc(shortcuts, "Безопасность и пользователи", "Сертификаты локального компьютера",
            "Сертификаты локального компьютера.lnk", "certlm.msc",
            "Хранилища сертификатов компьютера", system32, mmc);
        Add(shortcuts, "Безопасность и пользователи", "Диспетчер учётных данных",
            "Диспетчер учётных данных.lnk", control, "/name Microsoft.CredentialManager",
            "Сохранённые учётные данные Windows и веб-сайтов", control,
            runAsAdministrator: false, iconBadge: "УД");
        Add(shortcuts, "Безопасность и пользователи", "Учётные записи пользователей",
            "Учётные записи пользователей.lnk", Path.Combine(system32, "netplwiz.exe"), string.Empty,
            "Локальные пользователи, группы и параметры входа",
            Path.Combine(system32, "netplwiz.exe"));
        Add(shortcuts, "Безопасность и пользователи", "Безопасность Windows", "Безопасность Windows.lnk",
            explorer, "windowsdefender:", "Антивирус, защита учётной записи и устройства",
            Path.Combine(system32, "SecurityHealthSystray.exe"),
            Path.Combine(system32, "SecurityHealthSystray.exe"),
            runAsAdministrator: false);

        Add(shortcuts, "Сеть", "Сетевые подключения", "Сетевые подключения.lnk",
            control, "ncpa.cpl", "Сетевые адаптеры и подключения",
            Path.Combine(system32, "ncpa.cpl"), Path.Combine(system32, "ncpa.cpl"),
            iconBadge: "СП");
        Add(shortcuts, "Сеть", "Подключение к удалённому рабочему столу",
            "Удалённый рабочий стол.lnk", Path.Combine(system32, "mstsc.exe"), string.Empty,
            "Клиент удалённого рабочего стола", Path.Combine(system32, "mstsc.exe"),
            runAsAdministrator: false);
        Add(shortcuts, "Сеть", "Источники данных ODBC (64-разрядные)",
            "Источники данных ODBC 64.lnk", Path.Combine(system32, "odbcad32.exe"), string.Empty,
            "Системные и пользовательские источники данных ODBC", Path.Combine(system32, "odbcad32.exe"));
        string odbc32 = Path.Combine(windows, "SysWOW64", "odbcad32.exe");
        Add(shortcuts, "Сеть", "Источники данных ODBC (32-разрядные)",
            "Источники данных ODBC 32.lnk", odbc32, string.Empty,
            "32-разрядные системные и пользовательские источники данных ODBC", odbc32);
        Add(shortcuts, "Сеть", "Инициатор iSCSI", "Инициатор iSCSI.lnk",
            Path.Combine(system32, "iscsicpl.exe"), string.Empty,
            "Подключение к хранилищам iSCSI", Path.Combine(system32, "iscsicpl.exe"));

        Add(shortcuts, "Консоли", "PowerShell (администратор)", "PowerShell Администратор.lnk",
            Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe"), "-NoExit",
            "Windows PowerShell с повышенными правами",
            Path.Combine(system32, "WindowsPowerShell", "v1.0", "powershell.exe"));
        Add(shortcuts, "Консоли", "Командная строка (администратор)", "Командная строка Администратор.lnk",
            Path.Combine(system32, "cmd.exe"), "/k",
            "Командная строка с повышенными правами", Path.Combine(system32, "cmd.exe"));

        AddMmc(shortcuts, "Серверные роли", "Диспетчер Hyper-V", "Диспетчер Hyper-V.lnk",
            "virtmgmt.msc", "Управление виртуальными машинами Hyper-V", system32, mmc,
            iconBadge: "HV");
        AddMmc(shortcuts, "Серверные роли", "DNS Manager", "DNS Manager.lnk",
            "dnsmgmt.msc", "Управление DNS-сервером", system32, mmc,
            iconBadge: "DNS");
        AddMmc(shortcuts, "Серверные роли", "DHCP Manager", "DHCP Manager.lnk",
            "dhcpmgmt.msc", "Управление DHCP-сервером", system32, mmc,
            iconBadge: "DH");
        AddMmc(shortcuts, "Серверные роли", "Пользователи и компьютеры Active Directory",
            "Active Directory Users and Computers.lnk", "dsa.msc",
            "Управление объектами Active Directory", system32, mmc,
            iconBadge: "AD");
        AddMmc(shortcuts, "Серверные роли", "Управление групповой политикой",
            "Управление групповой политикой.lnk", "gpmc.msc",
            "Управление политиками домена", system32, mmc,
            iconBadge: "GPO");
        AddMmc(shortcuts, "Серверные роли", "Сайты и службы Active Directory",
            "Active Directory Sites and Services.lnk", "dssite.msc",
            "Топология репликации и сайты Active Directory", system32, mmc,
            iconBadge: "DS");
        AddMmc(shortcuts, "Серверные роли", "Домены и доверительные отношения Active Directory",
            "Active Directory Domains and Trusts.lnk", "domain.msc",
            "Домены, леса и доверительные отношения Active Directory", system32, mmc,
            iconBadge: "DT");
        AddMmc(shortcuts, "Серверные роли", "Редактор ADSI",
            "ADSI Edit.lnk", "adsiedit.msc",
            "Низкоуровневое редактирование объектов Active Directory", system32, mmc,
            iconBadge: "AE");
        AddMmc(shortcuts, "Серверные роли", "Управление DFS",
            "DFS Management.lnk", "dfsmgmt.msc",
            "Пространства имён и репликация DFS", system32, mmc,
            iconBadge: "DFS");
        AddMmc(shortcuts, "Серверные роли", "Сервер политики сети",
            "Network Policy Server.lnk", "nps.msc",
            "RADIUS, политики подключений и сетевого доступа", system32, mmc,
            iconBadge: "NPS");
        AddMmc(shortcuts, "Серверные роли", "Маршрутизация и удалённый доступ",
            "Routing and Remote Access.lnk", "rrasmgmt.msc",
            "VPN, маршрутизация и удалённый доступ Windows Server", system32, mmc,
            iconBadge: "RR");
        AddMmc(shortcuts, "Серверные роли", "Диспетчер отказоустойчивости кластеров",
            "Диспетчер кластеров.lnk", "CluAdmin.msc",
            "Управление отказоустойчивыми кластерами", system32, mmc,
            iconBadge: "CL");
        Add(shortcuts, "Серверные роли", "Диспетчер IIS", "Диспетчер IIS.lnk",
            Path.Combine(system32, "inetsrv", "InetMgr.exe"), string.Empty,
            "Управление Internet Information Services",
            Path.Combine(system32, "inetsrv", "InetMgr.exe"));
        Add(shortcuts, "Серверные роли", "Диспетчер серверов", "Диспетчер серверов.lnk",
            Path.Combine(system32, "ServerManager.exe"), string.Empty,
            "Роли и компоненты Windows Server", Path.Combine(system32, "ServerManager.exe"));

        return shortcuts;
    }

    private static void AddMmc(
        ICollection<ShortcutDefinition> shortcuts,
        string category,
        string displayName,
        string fileName,
        string snapInFileName,
        string description,
        string system32,
        string mmc,
        bool runAsAdministrator = true,
        string? iconBadge = null)
    {
        string snapInPath = Path.Combine(system32, snapInFileName);
        Add(shortcuts, category, displayName, fileName, mmc, Quote(snapInPath),
            description, snapInPath, snapInPath, runAsAdministrator, iconBadge ?? BuildBadge(displayName));
    }

    private static void Add(
        ICollection<ShortcutDefinition> shortcuts,
        string category,
        string displayName,
        string fileName,
        string targetPath,
        string arguments,
        string description,
        string iconSourcePath,
        string? requiredPath = null,
        bool runAsAdministrator = true,
        string? iconBadge = null)
    {
        if (!File.Exists(targetPath) ||
            !File.Exists(iconSourcePath) ||
            (requiredPath is not null && !File.Exists(requiredPath)))
        {
            return;
        }

        shortcuts.Add(new ShortcutDefinition(
            category,
            displayName,
            fileName,
            targetPath,
            arguments,
            description,
            iconSourcePath,
            iconBadge,
            runAsAdministrator));
    }

    private static string Quote(string value) => $"\"{value}\"";

    private static string BuildBadge(string displayName)
    {
        string[] words = displayName
            .Split(new[] { ' ', '(', ')', '-', '—' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => word.Length > 1 && word is not "для" and not "или")
            .ToArray();
        if (words.Length == 0)
        {
            return "AD";
        }

        if (words.Length == 1)
        {
            return words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant();
        }

        return string.Concat(words.Take(2).Select(word => char.ToUpperInvariant(word[0])));
    }
}
