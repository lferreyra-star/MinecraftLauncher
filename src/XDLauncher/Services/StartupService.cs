using Microsoft.Win32;

namespace XdLauncher.Services;

public static class StartupService
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "XDLauncher";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            var value = key?.GetValue(ValueName) as string;
            return !string.IsNullOrWhiteSpace(value);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo consultar el inicio automático.");
            return false;
        }
    }

    public static bool SetEnabled(bool enabled, out string message)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
            if (key is null)
            {
                message = "No se pudo abrir la clave de inicio de Windows.";
                return false;
            }

            if (enabled)
            {
                var executable = Environment.ProcessPath;
                if (string.IsNullOrWhiteSpace(executable) || !File.Exists(executable))
                {
                    message = "No se pudo determinar la ruta del launcher.";
                    return false;
                }

                key.SetValue(ValueName, $"\"{executable}\" --startup", RegistryValueKind.String);
                message = "XD Launcher se iniciará con Windows.";
            }
            else
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
                message = "Inicio con Windows desactivado.";
            }

            return true;
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo cambiar el inicio automático.");
            message = "Windows bloqueó el cambio en el registro: " + ex.Message;
            return false;
        }
    }
}
