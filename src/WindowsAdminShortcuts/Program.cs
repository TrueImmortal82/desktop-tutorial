namespace WindowsAdminShortcuts;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        try
        {
            AppSettingsService.Initialize();
            if (!LicenseAgreementService.EnsureAccepted())
            {
                return;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"{UiLocalization.Text(
                    "Не удалось загрузить настройки или проверить лицензионное соглашение",
                    "Could not load settings or verify the license agreement",
                    "Sozlamalarni yuklash yoki litsenziya kelishuvini tekshirish imkoni bo‘lmadi")}:\n\n{ex.Message}",
                "Windows Admin Center",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        Application.Run(new MainForm());
    }
}
