using WinFormsAdministrativo.Config;
using WinFormsAdministrativo.Services;
using WinFormsAdministrativo.UI;

namespace WinFormsAdministrativo;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        AppConfig config;
        try
        {
            config = AppConfig.Cargar();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Configuración incompleta", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        using var api = new ApiClient(config);

        // Login → ventana principal; al cerrar sesión se vuelve al login.
        while (true)
        {
            using (var login = new FormLogin(api))
            {
                if (login.ShowDialog() != DialogResult.OK) return;
            }

            using var principal = new FormAdministracion(api);
            Application.Run(principal);
            if (!principal.CerroSesion) return;
        }
    }
}
