namespace WindowsAdminShortcuts;

internal sealed record LocalizedText(string Russian, string English, string Uzbek)
{
    internal string Current => AppSettingsService.Current.Language switch
    {
        AppLanguage.Russian => Russian,
        AppLanguage.English => English,
        AppLanguage.Uzbek => Uzbek,
        _ => throw new InvalidOperationException("Unsupported interface language.")
    };
}

internal static class UiLocalization
{
    private static readonly IReadOnlyDictionary<string, LocalizedText> Catalog =
        new Dictionary<string, LocalizedText>(StringComparer.Ordinal)
        {
            ["Основное"] = new("Основное", "Essentials", "Asosiy"),
            ["Система"] = new("Система", "System", "Tizim"),
            ["Оборудование и диски"] = new("Оборудование и диски", "Hardware & disks", "Qurilmalar va disklar"),
            ["Безопасность и пользователи"] = new("Безопасность и пользователи", "Security & users", "Xavfsizlik va foydalanuvchilar"),
            ["Сеть"] = new("Сеть", "Network", "Tarmoq"),
            ["Консоли"] = new("Консоли", "Consoles", "Konsollar"),
            ["Серверные роли"] = new("Серверные роли", "Server roles", "Server rollari"),

            ["Управление компьютером"] = new("Управление компьютером", "Computer Management", "Kompyuterni boshqarish"),
            ["Панель управления"] = new("Панель управления", "Control Panel", "Boshqaruv paneli"),
            ["Параметры Windows"] = new("Параметры Windows", "Windows Settings", "Windows sozlamalari"),
            ["Инструменты Windows"] = new("Инструменты Windows", "Windows Tools", "Windows vositalari"),
            ["Диспетчер задач"] = new("Диспетчер задач", "Task Manager", "Vazifalar dispetcheri"),
            ["Просмотр событий"] = new("Просмотр событий", "Event Viewer", "Voqealarni ko‘rish"),
            ["Службы"] = new("Службы", "Services", "Xizmatlar"),
            ["Планировщик заданий"] = new("Планировщик заданий", "Task Scheduler", "Vazifalar rejalashtiruvchisi"),
            ["Монитор производительности"] = new("Монитор производительности", "Performance Monitor", "Unumdorlik monitori"),
            ["Монитор ресурсов"] = new("Монитор ресурсов", "Resource Monitor", "Resurslar monitori"),
            ["Монитор стабильности"] = new("Монитор стабильности", "Reliability Monitor", "Barqarorlik monitori"),
            ["Конфигурация системы"] = new("Конфигурация системы", "System Configuration", "Tizim konfiguratsiyasi"),
            ["Сведения о системе"] = new("Сведения о системе", "System Information", "Tizim haqida ma’lumot"),
            ["Свойства системы"] = new("Свойства системы", "System Properties", "Tizim xususiyatlari"),
            ["Диагностика памяти Windows"] = new("Диагностика памяти Windows", "Windows Memory Diagnostic", "Windows xotira diagnostikasi"),
            ["Службы компонентов"] = new("Службы компонентов", "Component Services", "Komponent xizmatlari"),
            ["Управление WMI"] = new("Управление WMI", "WMI Control", "WMI boshqaruvi"),
            ["Восстановление системы"] = new("Восстановление системы", "System Restore", "Tizimni tiklash"),
            ["Электропитание"] = new("Электропитание", "Power Options", "Quvvat parametrlari"),
            ["Дата и время"] = new("Дата и время", "Date and Time", "Sana va vaqt"),
            ["Диспетчер устройств"] = new("Диспетчер устройств", "Device Manager", "Qurilmalar dispetcheri"),
            ["Управление дисками"] = new("Управление дисками", "Disk Management", "Disklarni boshqarish"),
            ["Оптимизация дисков"] = new("Оптимизация дисков", "Optimize Drives", "Disklarni optimallashtirish"),
            ["Очистка диска"] = new("Очистка диска", "Disk Cleanup", "Diskni tozalash"),
            ["Компоненты Windows"] = new("Компоненты Windows", "Windows Features", "Windows komponentlari"),
            ["DiskPart"] = new("DiskPart", "DiskPart", "DiskPart"),
            ["Управление печатью"] = new("Управление печатью", "Print Management", "Chop etishni boshqarish"),
            ["Редактор реестра"] = new("Редактор реестра", "Registry Editor", "Reyestr muharriri"),
            ["Локальные пользователи и группы"] = new("Локальные пользователи и группы", "Local Users and Groups", "Mahalliy foydalanuvchilar va guruhlar"),
            ["Локальная политика безопасности"] = new("Локальная политика безопасности", "Local Security Policy", "Mahalliy xavfsizlik siyosati"),
            ["Редактор групповой политики"] = new("Редактор групповой политики", "Group Policy Editor", "Guruh siyosati muharriri"),
            ["Результирующая политика"] = new("Результирующая политика", "Resultant Set of Policy", "Natijaviy siyosat"),
            ["Брандмауэр в расширенном режиме"] = new("Брандмауэр в расширенном режиме", "Windows Firewall with Advanced Security", "Kengaytirilgan xavfsizlik devori"),
            ["Общие папки"] = new("Общие папки", "Shared Folders", "Umumiy papkalar"),
            ["Диспетчер авторизации"] = new("Диспетчер авторизации", "Authorization Manager", "Avtorizatsiya boshqaruvi"),
            ["Сертификаты текущего пользователя"] = new("Сертификаты текущего пользователя", "Current User Certificates", "Joriy foydalanuvchi sertifikatlari"),
            ["Сертификаты локального компьютера"] = new("Сертификаты локального компьютера", "Local Computer Certificates", "Mahalliy kompyuter sertifikatlari"),
            ["Диспетчер учётных данных"] = new("Диспетчер учётных данных", "Credential Manager", "Hisob ma’lumotlari dispetcheri"),
            ["Учётные записи пользователей"] = new("Учётные записи пользователей", "User Accounts", "Foydalanuvchi hisoblari"),
            ["Безопасность Windows"] = new("Безопасность Windows", "Windows Security", "Windows xavfsizligi"),
            ["Сетевые подключения"] = new("Сетевые подключения", "Network Connections", "Tarmoq ulanishlari"),
            ["Подключение к удалённому рабочему столу"] = new("Подключение к удалённому рабочему столу", "Remote Desktop Connection", "Masofaviy ish stoliga ulanish"),
            ["Источники данных ODBC (64-разрядные)"] = new("Источники данных ODBC (64-разрядные)", "ODBC Data Sources (64-bit)", "ODBC ma’lumot manbalari (64-bit)"),
            ["Источники данных ODBC (32-разрядные)"] = new("Источники данных ODBC (32-разрядные)", "ODBC Data Sources (32-bit)", "ODBC ma’lumot manbalari (32-bit)"),
            ["Инициатор iSCSI"] = new("Инициатор iSCSI", "iSCSI Initiator", "iSCSI tashabbuskori"),
            ["PowerShell (администратор)"] = new("PowerShell (администратор)", "PowerShell (Administrator)", "PowerShell (administrator)"),
            ["Командная строка (администратор)"] = new("Командная строка (администратор)", "Command Prompt (Administrator)", "Buyruqlar satri (administrator)"),
            ["Диспетчер Hyper-V"] = new("Диспетчер Hyper-V", "Hyper-V Manager", "Hyper-V dispetcheri"),
            ["DNS Manager"] = new("DNS Manager", "DNS Manager", "DNS boshqaruvi"),
            ["DHCP Manager"] = new("DHCP Manager", "DHCP Manager", "DHCP boshqaruvi"),
            ["Пользователи и компьютеры Active Directory"] = new("Пользователи и компьютеры Active Directory", "Active Directory Users and Computers", "Active Directory foydalanuvchilari va kompyuterlari"),
            ["Управление групповой политикой"] = new("Управление групповой политикой", "Group Policy Management", "Guruh siyosatini boshqarish"),
            ["Сайты и службы Active Directory"] = new("Сайты и службы Active Directory", "Active Directory Sites and Services", "Active Directory saytlari va xizmatlari"),
            ["Домены и доверительные отношения Active Directory"] = new("Домены и доверительные отношения Active Directory", "Active Directory Domains and Trusts", "Active Directory domenlari va ishonchlari"),
            ["Редактор ADSI"] = new("Редактор ADSI", "ADSI Edit", "ADSI muharriri"),
            ["Управление DFS"] = new("Управление DFS", "DFS Management", "DFS boshqaruvi"),
            ["Сервер политики сети"] = new("Сервер политики сети", "Network Policy Server", "Tarmoq siyosati serveri"),
            ["Маршрутизация и удалённый доступ"] = new("Маршрутизация и удалённый доступ", "Routing and Remote Access", "Marshrutlash va masofaviy kirish"),
            ["Диспетчер отказоустойчивости кластеров"] = new("Диспетчер отказоустойчивости кластеров", "Failover Cluster Manager", "Klaster uzluksizligi dispetcheri"),
            ["Диспетчер IIS"] = new("Диспетчер IIS", "IIS Manager", "IIS dispetcheri"),
            ["Диспетчер серверов"] = new("Диспетчер серверов", "Server Manager", "Server dispetcheri")
        };

    internal static string Text(string russian, string english, string uzbek) =>
        new LocalizedText(russian, english, uzbek).Current;

    internal static T Attach<T>(T control, string russian, string english, string uzbek)
        where T : Control
    {
        control.Tag = new LocalizedText(russian, english, uzbek);
        control.Text = ((LocalizedText)control.Tag).Current;
        return control;
    }

    internal static void Apply(Control root)
    {
        if (root.Tag is LocalizedText text)
        {
            root.Text = text.Current;
        }

        foreach (Control child in root.Controls)
        {
            Apply(child);
        }
    }

    internal static string CatalogText(string source)
    {
        return Catalog.TryGetValue(source, out LocalizedText? text)
            ? text.Current
            : source;
    }

    internal static bool HasCatalogTranslation(string source) => Catalog.ContainsKey(source);
}
