using System.Windows;
using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>
/// Estado de la pantalla de acceso de XD Launcher. Valida las credenciales contra el
/// sistema real de cuentas (Firebase Authentication) y expone el resultado al shell.
/// </summary>
public sealed class LoginViewModel : ObservableObject
{
    private readonly XdAuthService _auth;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _displayName = string.Empty;
    private bool _isRegisterMode;
    private bool _isBusy;
    private bool _showPassword;
    private bool _isServerReachable = true;
    private string _errorMessage = string.Empty;
    private string _infoMessage = string.Empty;

    public LoginViewModel(XdAuthService auth)
    {
        _auth = auth;

        SignInCommand = new AsyncRelayCommand(SignInAsync, () => CanSubmit, OnError);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => CanSubmit, OnError);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => CanSubmit, OnError);
        TogglePasswordCommand = new RelayCommand(() => ShowPassword = !ShowPassword);
        SwitchModeCommand = new RelayCommand(() => IsRegisterMode = !IsRegisterMode);
        OpenWebsiteCommand = new RelayCommand(() => FileOps.OpenUrl(AppInfo.XdWebsite));
        OpenFirebaseConsoleCommand = new RelayCommand(() => FileOps.OpenUrl(XdAuthService.FirebaseConsoleUrl));
        ContinueWithGoogleCommand = new AsyncRelayCommand(SignInWithGoogleAsync, () => !IsBusy, OnError);
    }

    public event Action<XdAccount>? Authenticated;

    public string Email
    {
        get => _email;
        set => SetProperty(ref _email, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set => SetProperty(ref _confirmPassword, value);
    }

    public string DisplayName
    {
        get => _displayName;
        set => SetProperty(ref _displayName, value);
    }

    public bool IsRegisterMode
    {
        get => _isRegisterMode;
        set
        {
            if (!SetProperty(ref _isRegisterMode, value))
                return;

            ErrorMessage = string.Empty;
            InfoMessage = string.Empty;
            OnPropertyChanged(nameof(PrimaryActionLabel));
            OnPropertyChanged(nameof(PrimaryActionGlyph));
            OnPropertyChanged(nameof(SecondaryActionLabel));
            OnPropertyChanged(nameof(ModeTitle));
            OnPropertyChanged(nameof(ModeSubtitle));
            OnPropertyChanged(nameof(ShowRegisterFields));
            RaiseCommandStates();
        }
    }

    public bool ShowPassword
    {
        get => _showPassword;
        set
        {
            if (!SetProperty(ref _showPassword, value))
                return;

            OnPropertyChanged(nameof(PasswordVisibilityGlyph));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!SetProperty(ref _isBusy, value))
                return;

            OnPropertyChanged(nameof(StatusLabel));
            OnPropertyChanged(nameof(IsBusy));
            RaiseCommandStates();
        }
    }

    public bool IsServerReachable
    {
        get => _isServerReachable;
        private set
        {
            if (!SetProperty(ref _isServerReachable, value))
                OnPropertyChanged(nameof(ServerStatusLabel));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (!SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
                NeedsEmailProvider = value.Contains("no tiene habilitado", StringComparison.OrdinalIgnoreCase) ||
                                    value.Contains("Correo/contraseña", StringComparison.OrdinalIgnoreCase) ||
                                    value.Contains("proveedor", StringComparison.OrdinalIgnoreCase);
                OnPropertyChanged(nameof(NeedsEmailProvider));
            }
        }
    }

    public string InfoMessage
    {
        get => _infoMessage;
        private set
        {
            if (!SetProperty(ref _infoMessage, value))
                OnPropertyChanged(nameof(HasInfo));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasInfo => !string.IsNullOrWhiteSpace(InfoMessage);
    public bool ShowRegisterFields => IsRegisterMode;
    public string PasswordVisibilityGlyph => ShowPassword ? "\uE890" : "\uE7B3";
    public string ServerStatusLabel => IsServerReachable ? "Servidor en línea" : "Servidor sin conexión";
    public string StatusLabel => IsBusy ? "Conectando…" : string.Empty;
    public string PrimaryActionLabel => IsRegisterMode ? "CREAR CUENTA" : "INICIAR SESIÓN";
    public string PrimaryActionGlyph => IsRegisterMode ? "\uE710" : "\uE890";
    public string SecondaryActionLabel => IsRegisterMode ? "Ya tengo cuenta" : "CREAR CUENTA";
    public string ModeTitle => IsRegisterMode ? "Crea tu cuenta" : "Inicia sesión";
    public string ModeSubtitle => IsRegisterMode
        ? "Registra tu cuenta de XD Launcher para desbloquear el launcher"
        : "Accede con tu cuenta de XD Launcher para continuar";

    private bool CanSubmit => !IsBusy && !string.IsNullOrWhiteSpace(Email) && !string.IsNullOrWhiteSpace(Password);

    public AsyncRelayCommand SignInCommand { get; }
    public AsyncRelayCommand RegisterCommand { get; }
    public AsyncRelayCommand RecoverCommand { get; }
    public RelayCommand TogglePasswordCommand { get; }
    public RelayCommand SwitchModeCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }
    public RelayCommand OpenFirebaseConsoleCommand { get; }
    public AsyncRelayCommand ContinueWithGoogleCommand { get; }

    /// <summary>True cuando el servidor exige habilitar el proveedor de correo.</summary>
    public bool NeedsEmailProvider { get; private set; }

    /// <summary>Comprueba la conexión con el servidor de XD Launcher.</summary>
    public async Task CheckServerAsync()
    {
        IsServerReachable = await _auth.PingAsync();
        if (!IsServerReachable)
            ErrorMessage = "No se puede contactar con el servidor de XD Launcher. Revisa tu conexión.";
    }

    private async Task SignInAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (!ValidateCredentials())
            return;

        try
        {
            var account = await _auth.SignInAsync(Email, Password);
            await CompleteAsync(account);
        }
        catch (XdAuthException ex)
        {
            ErrorMessage = ex.Message;
            Password = string.Empty;
        }
    }

    private async Task RegisterAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (!ValidateCredentials())
            return;

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            ErrorMessage = "Escribe el nombre que quieres mostrar en el launcher.";
            return;
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "Las contraseñas no coinciden.";
            return;
        }

        try
        {
            var account = await _auth.RegisterAsync(Email, Password, DisplayName);
            await CompleteAsync(account);
        }
        catch (XdAuthException ex)
        {
            // Si Firebase no tiene habilitado el proveedor de correo y contraseña,
            // el alta se hace con Google, que crea la cuenta en el mismo sistema.
            if (ex.Code is "OPERATION_NOT_ALLOWED" or "PASSWORD_LOGIN_DISABLED")
            {
                LogService.Info("El proveedor de correo está deshabilitado: se ofrece el alta con Google.");
                ErrorMessage = "El servidor no permite crear cuentas con contraseña. "
                               + "Vamos a crearla con Google, que usa el mismo sistema de cuentas.";
                Password = string.Empty;
                ConfirmPassword = string.Empty;
                await SignInWithGoogleAsync();
                return;
            }

            ErrorMessage = ex.Message;
            Password = string.Empty;
            ConfirmPassword = string.Empty;
        }
    }

    private async Task SignInWithGoogleAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        try
        {
            InfoMessage = "Completa el inicio de sesión con Google en el navegador. Volveremos aquí automáticamente.";
            var account = await _auth.SignInWithGoogleAsync();
            await CompleteAsync(account);
        }
        catch (XdAuthException ex)
        {
            LogService.Warn("Acceso con Google rechazado: " + ex.Message);
            ErrorMessage = ex.Message;
        }
        catch (OperationCanceledException)
        {
            LogService.Info("El usuario canceló el acceso con Google.");
            ErrorMessage = "Se canceló el acceso con Google.";
        }
        catch (Exception ex)
        {
            OnError(ex);
        }
    }

    private async Task RecoverAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(Email))
        {
            ErrorMessage = "Escribe tu correo para enviarte el enlace de recuperación.";
            return;
        }

        try
        {
            await _auth.SendPasswordResetAsync(Email);
            InfoMessage = "Te enviamos un correo con el enlace para crear una contraseña nueva.";
        }
        catch (XdAuthException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private bool ValidateCredentials()
    {
        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Escribe un correo válido.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Escribe tu contraseña.";
            return false;
        }

        return true;
    }

    private async Task CompleteAsync(XdAccount account)
    {
        XdSessionStore.Save(account);
        await _auth.SyncProfileAsync(account);
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        Authenticated?.Invoke(account);
    }

    private void OnError(Exception ex)
    {
        LogService.Error(ex, "Error en el inicio de sesión de XD Launcher.");
        ErrorMessage = ex is XdAuthException auth ? auth.Message : "No se pudo iniciar sesión. Inténtalo de nuevo.";
    }

    private void RaiseCommandStates()
    {
        SignInCommand.RaiseCanExecuteChanged();
        RegisterCommand.RaiseCanExecuteChanged();
        RecoverCommand.RaiseCanExecuteChanged();
    }
}
