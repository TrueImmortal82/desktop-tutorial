using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace WindowsAdminShortcuts;

internal static class LicenseAgreementService
{
    internal const string ResourceName = "WindowsAdminShortcuts.LICENSE.txt";
    internal const string AcceptanceFileName = "license-acceptance.txt";

    private static readonly Lazy<string> LicenseTextSource = new(LoadLicenseText);
    private static readonly Lazy<string> LicenseHashSource = new(
        () => Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(LicenseText))));

    internal static string LicenseText => LicenseTextSource.Value;

    internal static string LicenseHash => LicenseHashSource.Value;

    internal static string DefaultAcceptanceDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsAdminShortcuts");

    internal static bool EnsureAccepted()
    {
        if (IsAccepted(DefaultAcceptanceDirectory))
        {
            return true;
        }

        using var agreement = new LicenseAgreementForm(LicenseText);
        if (agreement.ShowDialog() != DialogResult.OK)
        {
            return false;
        }

        RecordAcceptance(DefaultAcceptanceDirectory);
        return IsAccepted(DefaultAcceptanceDirectory);
    }

    internal static bool IsAccepted(string directory)
    {
        string path = Path.Combine(directory, AcceptanceFileName);
        if (!File.Exists(path))
        {
            return false;
        }

        string storedHash = File.ReadAllText(path, Encoding.UTF8).Trim();
        return string.Equals(storedHash, LicenseHash, StringComparison.Ordinal);
    }

    internal static void RecordAcceptance(string directory)
    {
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, AcceptanceFileName);
        string temporaryPath = Path.Combine(
            directory,
            $".{AcceptanceFileName}.{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(
                temporaryPath,
                $"{LicenseHash}\r\n",
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            File.Move(temporaryPath, path, overwrite: true);
            if (!IsAccepted(directory))
            {
                throw new InvalidDataException(
                    "Запись о принятии лицензионного соглашения не прошла проверку.");
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static string LoadLicenseText()
    {
        using Stream stream = Assembly
            .GetExecutingAssembly()
            .GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Встроенный ресурс лицензии не найден: {ResourceName}");
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        string text = reader.ReadToEnd();
        if (!text.Contains("AS IS", StringComparison.Ordinal) ||
            !text.Contains("WITHOUT WARRANTY", StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Лицензионное соглашение не содержит обязательного условия AS IS.");
        }

        return text;
    }
}
