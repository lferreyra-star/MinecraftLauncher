using System.Windows;
using System.Windows.Input;
using XdLauncher.Services;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

/// <summary>
/// Pantalla de acceso premium de XD Launcher. Bloquea la aplicación hasta que
/// el servidor valida las credenciales y devuelve una sesión válida.
/// </summary>
public partial class LoginWindow : Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _viewModel.Authenticated += OnAuthenticated;
        Loaded += async (_, _) => await _viewModel.CheckServerAsync();
        Closing += (_, _) =>
        {
            LogService.Info("La pantalla de acceso se está cerrando.");
            _viewModel.Authenticated -= OnAuthenticated;
        };
    }

    /// <summary>Indica si el usuario completò el acceso correctamente.</summary>
    public bool IsAuthenticated { get; private set; }

    /// <summary>
    /// La ventana NO se cierra aquí: el cierre lo controla la aplicación después de
    /// registrar la cuenta, para evitar una carrera en la que el evento de cierre
    /// resolviera la espera con "sin sesión" y cerrara el launcher.
    /// </summary>
    private void OnAuthenticated(XdLauncher.Services.XdAccount account)
    {
        IsAuthenticated = true;
        DialogResult = true;
    }

    private void TogglePassword_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.TogglePasswordCommand.CanExecute(null))
            _viewModel.TogglePasswordCommand.Execute(null);

        var show = _viewModel.ShowPassword;
        SecretBox.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
        PlainBox.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (show)
        {
            PlainBox.Text = _viewModel.Password;
            PlainBox.CaretIndex = PlainBox.Text.Length;
            PlainBox.Focus();
        }
        else
        {
            _viewModel.Password = PlainBox.Text;
            SecretBox.Password = _viewModel.Password;
            SecretBox.Focus();
        }
    }

    private void SecretBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (!_viewModel.ShowPassword)
            _viewModel.Password = SecretBox.Password;
    }

    private void ConfirmBox_PasswordChanged(object sender, RoutedEventArgs e)
        => _viewModel.ConfirmPassword = ConfirmBox.Password;

    private void ContinueWithGoogle_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ContinueWithGoogleCommand.CanExecute(null))
            _viewModel.ContinueWithGoogleCommand.Execute(null);
    }

    private void OpenWebsiteHandoff_Click(object sender, RoutedEventArgs e)
        => FileOps.OpenUrl(XdLauncher.Services.XdAuthService.DesktopHandoffUrl);

    private void OpenFirebaseConsole_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.OpenFirebaseConsoleCommand.CanExecute(null))
            _viewModel.OpenFirebaseConsoleCommand.Execute(null);
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Primary_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsRegisterMode)
        {
            if (_viewModel.RegisterCommand.CanExecute(null))
                _viewModel.RegisterCommand.Execute(null);
        }
        else if (_viewModel.SignInCommand.CanExecute(null))
        {
            _viewModel.SignInCommand.Execute(null);
        }
    }

    private void Recover_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.RecoverCommand.CanExecute(null))
            _viewModel.RecoverCommand.Execute(null);
    }

    private void SwitchMode_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SwitchModeCommand.CanExecute(null))
            _viewModel.SwitchModeCommand.Execute(null);
    }

    private void OpenWebsite_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.OpenWebsiteCommand.CanExecute(null))
            _viewModel.OpenWebsiteCommand.Execute(null);
    }

    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
            return;

        e.Handled = true;
        Primary_Click(sender, e);
    }
}
