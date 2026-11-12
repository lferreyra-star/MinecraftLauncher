using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XdLauncher.Models;
using XdLauncher.Services;

namespace XdLauncher.Views.Dialogs;

public sealed class MobileConnectResult
{
    public required string Address { get; init; }
    public required string Message { get; init; }
}

/// <summary>Wi-Fi connection and pairing dialog. Pairing is performed by Android itself.</summary>
public partial class MobileConnectDialog : Window
{
    private readonly AdbService _adb;
    private readonly SettingsService _settings;

    public MobileConnectDialog(AdbService adb, SettingsService settings)
    {
        InitializeComponent();
        _adb = adb;
        _settings = settings;

        var last = settings.Settings.MobileConnect.LastWifiAddress;
        if (!string.IsNullOrWhiteSpace(last))
        {
            var parts = last.Split(':');
            AddressBox.Text = parts[0];
        }
    }

    public string? ConnectedAddress { get; private set; }
    public string ResultMessage { get; private set; } = string.Empty;

    /// <summary>Shows the dialog and returns the connection result, or null when cancelled.</summary>
    public static async Task<MobileConnectResult?> ShowAsync(AdbService adb, SettingsService settings)
    {
        var dialog = new MobileConnectDialog(adb, settings);
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true })
            dialog.Owner = owner;

        var accepted = dialog.ShowDialog() == true;
        if (!accepted || string.IsNullOrWhiteSpace(dialog.ConnectedAddress))
            return null;

        return new MobileConnectResult
        {
            Address = dialog.ConnectedAddress,
            Message = dialog.ResultMessage
        };
    }

    private async void Discover_Click(object sender, RoutedEventArgs e)
    {
        DiscoverButtonState(true);
        DiscoveryText.Text = "Buscando…";
        try
        {
            var services = await _adb.DiscoverMdnsAsync();
            ServicesList.ItemsSource = services;
            ServicesList.Visibility = services.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            DiscoveryText.Text = services.Count > 0
                ? $"{services.Count} servicio(s) encontrados"
                : "No se encontraron servicios. Comprueba que ambos equipos están en la misma red.";
        }
        catch (Exception ex)
        {
            DiscoveryText.Text = "Error: " + ex.Message;
        }
        finally
        {
            DiscoverButtonState(false);
        }
    }

    private void DiscoverButtonState(bool busy)
    {
        var button = (Button?)FindName("DiscoverButton");
        if (button is not null)
            button.IsEnabled = !busy;
    }

    private async void Connect_Click(object sender, RoutedEventArgs e)
    {
        var address = AddressBox.Text.Trim();
        if (!address.Contains(':'))
            address += ":5555";

        ConnectStatus.Text = "Conectando con " + address + "…";
        try
        {
            var (ok, message) = await _adb.ConnectAsync(address);
            if (ok)
            {
                ConnectedAddress = address;
                ResultMessage = message;
                ConnectStatus.Text = "Conectado: " + message;
                _settings.Settings.MobileConnect.LastWifiAddress = address;
                _settings.Save();
                DialogResult = true;
                return;
            }

            ConnectStatus.Text = "Falló: " + message;
        }
        catch (Exception ex)
        {
            ConnectStatus.Text = "Error: " + ex.Message;
        }
    }

    private async void Pair_Click(object sender, RoutedEventArgs e)
    {
        var address = PairAddressBox.Text.Trim();
        var code = PairCodeBox.Text.Trim();
        if (address.Length == 0 || code.Length == 0)
        {
            PairStatus.Text = "Escribe la dirección de emparejamiento y el código de 6 dígitos.";
            return;
        }

        PairStatus.Text = "Emparejando con " + address + "…";
        try
        {
            var (ok, message) = await _adb.PairAsync(address, code);
            PairStatus.Text = ok ? "Emparejado correctamente." : "Falló: " + message;
            if (ok)
            {
                var connectAddress = address.Replace("_adb-tls-pairing", string.Empty);
                var (connectOk, connectMessage) = await _adb.ConnectAsync(connectAddress);
                if (connectOk)
                {
                    ConnectedAddress = connectAddress;
                    ResultMessage = connectMessage;
                    _settings.Settings.MobileConnect.LastWifiAddress = connectAddress;
                    _settings.Save();
                    DialogResult = true;
                }
                else
                {
                    ConnectStatus.Text = "Emparejado, pero la conexión falló: " + connectMessage;
                }
            }
        }
        catch (Exception ex)
        {
            PairStatus.Text = "Error: " + ex.Message;
        }
    }

    private void Accept_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ConnectedAddress))
        {
            ConnectStatus.Text = "Conéctate a un teléfono antes de continuar.";
            return;
        }
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void HeaderBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
