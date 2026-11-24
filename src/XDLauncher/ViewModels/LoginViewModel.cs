using System.Net.Http;
using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.ViewModels;

/// <summary>
/// Estado de la pantalla de acceso de XD Launcher. Valida las credenciales contra el
/// sistema real de cuentas (Firebase Authentication) y expone el resultado al shell.
/// </summary>
public sealed class LoginViewModel : ObservableObject
{
    private const int GoogleTimeoutMinutes = 4;
    private static readonly TimeSpan GoogleTimeout = TimeSpan.FromMinutes(GoogleTimeoutMinutes);
    private readonly XdAuthService _auth;
    private readonly SettingsService? _settings;
    private bool _attempted;
    private string _email = string.Empty;
    private string _password = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _displayName = string.Empty;
    private bool _isRegisterMode;
    private bool _isBusy;
    private bool _showPassword;
    private bool _isServerReachable = true;
    private bool _needsEmailProvider;
    private bool _googleFailed;
    private bool _isGoogleAuth;
    private string _googleStatusMessage = string.Empty;
    private string _errorMessage = string.Empty;
    private string _infoMessage = string.Empty;
    private string _busyLabel = "Conectando…";
    private int _busyDepth;
    private CancellationTokenSource? _googleCts;
    private bool _isSuccess;
    private string _successMessage = "Acceso concedido";
    private string _successDetail = "Preparando tu launcher…";
    private bool _completionStarted;

    public LoginViewModel(XdAuthService auth, SettingsService? settings = null)
    {
        _auth = auth;
        _settings = settings;

        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsBusy, OnError);
        RegisterCommand = new AsyncRelayCommand(RegisterAsync, () => !IsBusy, OnError);
        RecoverCommand = new AsyncRelayCommand(RecoverAsync, () => !IsBusy, OnError);
        TogglePasswordCommand = new RelayCommand(() => ShowPassword = !ShowPassword);
        SwitchModeCommand = new RelayCommand(() => IsRegisterMode = !IsRegisterMode);
        SetModeCommand = new RelayCommand(parameter => IsRegisterMode = parameter as bool? ?? parameter as string == "register");
        OpenWebsiteCommand = new RelayCommand(() => FileOps.OpenUrl(AppInfo.XdWebsite));
        OpenFirebaseConsoleCommand = new RelayCommand(() => FileOps.OpenUrl(XdAuthService.FirebaseConsoleUrl));
        ContinueWithGoogleCommand = new AsyncRelayCommand(() => SignInWithGoogleAsync(), () => !IsBusy, OnError);
        CancelGoogleCommand = new RelayCommand(CancelGoogle, () => IsGoogleAuth);
    }

    public event Action<XdAccount>? Authenticated;

    public string Email
    {
        get => _email;
        set
        {
            if (SetProperty(ref _email, value))
                OnPropertyChanged(nameof(EmailError));
        }
    }

    public string Password
    {
        get => _password;
        set
        {
            if (SetProperty(ref _password, value))
            {
                OnPropertyChanged(nameof(PasswordError));
                OnPropertyChanged(nameof(ConfirmError));
            }
        }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set
        {
            if (SetProperty(ref _confirmPassword, value))
                OnPropertyChanged(nameof(ConfirmError));
        }
    }

    public bool RememberMe
    {
        get => _settings?.Settings.RememberMe ?? true;
        set
        {
            if (_settings is null || _settings.Settings.RememberMe == value)
                return;
            _settings.Settings.RememberMe = value;
            _settings.Save();
            OnPropertyChanged(nameof(RememberMe));
        }
    }

    public string EmailError => !string.IsNullOrWhiteSpace(_email) && !_email.Contains('@')
        ? "Correo no válido"
        : string.Empty;

    public string PasswordError => _attempted && string.IsNullOrWhiteSpace(_password)
        ? "Escribe tu contraseña."
        : string.Empty;

    public string ConfirmError => _attempted && IsRegisterMode && !string.Equals(Password, ConfirmPassword, StringComparison.Ordinal)
        ? "Las contraseñas no coinciden."
        : string.Empty;

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
            RaiseCommandStates();
        }
    }

    public bool IsServerReachable
    {
        get => _isServerReachable;
        private set
        {
            if (SetProperty(ref _isServerReachable, value))
                OnPropertyChanged(nameof(ServerStatusLabel));
        }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasError));
        }
    }

    public string InfoMessage
    {
        get => _infoMessage;
        private set
        {
            if (SetProperty(ref _infoMessage, value))
                OnPropertyChanged(nameof(HasInfo));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
    public bool HasInfo => !string.IsNullOrWhiteSpace(InfoMessage);
    public bool ShowRegisterFields => IsRegisterMode;
    public string PasswordVisibilityGlyph => ShowPassword ? "\uE890" : "\uE7B3";
    public string ServerStatusLabel => IsServerReachable ? "Servidor en línea" : "Servidor sin conexión";
    public string StatusLabel => IsBusy ? _busyLabel : string.Empty;
    public string PrimaryActionLabel => IsRegisterMode ? "CREAR CUENTA" : "INICIAR SESIÓN";
    public string PrimaryActionGlyph => IsRegisterMode ? "\uE710" : "\uE890";
    public string SecondaryActionLabel => IsRegisterMode ? "Ya tengo cuenta" : "CREAR CUENTA";
    public string ModeTitle => IsRegisterMode ? "Crea tu cuenta" : "Inicia sesión";
    public string ModeSubtitle => IsRegisterMode
        ? "Registra tu cuenta de XD Launcher para desbloquear el launcher"
        : "Accede con tu cuenta de XD Launcher para continuar";

    /// <summary>True cuando Firebase exige habilitar el proveedor de correo.</summary>
    public bool NeedsEmailProvider
    {
        get => _needsEmailProvider;
        private set => SetProperty(ref _needsEmailProvider, value);
    }

    /// <summary>True cuando el último intento con Google falló y el botón invita a reintentar.</summary>
    public bool GoogleFailed
    {
        get => _googleFailed;
        private set
        {
            if (SetProperty(ref _googleFailed, value))
                OnPropertyChanged(nameof(GoogleActionLabel));
        }
    }

    public string GoogleActionLabel => IsGoogleAuth
        ? "CONECTANDO CON GOOGLE…"
        : GoogleFailed ? "REINTENTAR CON GOOGLE" : "CONTINUAR CON GOOGLE";

    /// <summary>True mientras el flujo de Google está activo (permite cancelarlo).</summary>
    public bool IsGoogleAuth
    {
        get => _isGoogleAuth;
        private set
        {
            if (!SetProperty(ref _isGoogleAuth, value))
                return;

            OnPropertyChanged(nameof(ShowGoogleProgress));
            OnPropertyChanged(nameof(GoogleActionLabel));
            OnPropertyChanged(nameof(CanCancelGoogle));
            RaiseCommandStates();
        }
    }

    /// <summary>Etapa actual del acceso con Google para mostrarla en la interfaz.</summary>
    public string GoogleStatusMessage
    {
        get => _googleStatusMessage;
        private set => SetProperty(ref _googleStatusMessage, value);
    }

    public bool ShowGoogleProgress => IsGoogleAuth;
    public bool CanCancelGoogle => IsGoogleAuth;

    /// <summary>True mientras se muestra la transición cinematográfica de acceso concedido.</summary>
    public bool IsSuccess
    {
        get => _isSuccess;
        private set => SetProperty(ref _isSuccess, value);
    }

    public string SuccessMessage
    {
        get => _successMessage;
        private set => SetProperty(ref _successMessage, value);
    }

    public string SuccessDetail
    {
        get => _successDetail;
        private set => SetProperty(ref _successDetail, value);
    }

    public AsyncRelayCommand SignInCommand { get; }
    public AsyncRelayCommand RegisterCommand { get; }
    public AsyncRelayCommand RecoverCommand { get; }
    public RelayCommand TogglePasswordCommand { get; }
    public RelayCommand SwitchModeCommand { get; }
    public RelayCommand SetModeCommand { get; }
    public RelayCommand OpenWebsiteCommand { get; }
    public RelayCommand OpenFirebaseConsoleCommand { get; }
    public AsyncRelayCommand ContinueWithGoogleCommand { get; }
    public RelayCommand CancelGoogleCommand { get; }

    /// <summary>Comprueba la conexión con el servidor de XD Launcher.</summary>
    public async Task CheckServerAsync()
    {
        IsServerReachable = await _auth.PingAsync();
        if (!IsServerReachable && !IsBusy && !HasError)
            ErrorMessage = "No se puede contactar con el servidor de XD Launcher. Revisa tu conexión.";
    }

    private async Task SignInAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        NeedsEmailProvider = false;

        if (!ValidateCredentials())
            return;

        BeginBusy("Conectando…");
        try
        {
            var account = await _auth.SignInAsync(Email, Password);
            await CompleteAsync(account);
        }
        catch (XdAuthException ex)
        {
            ErrorMessage = ex.Message;
            NeedsEmailProvider = ex.Code is "OPERATION_NOT_ALLOWED" or "PASSWORD_LOGIN_DISABLED";
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task RegisterAsync()
    {
        _attempted = true;
        OnPropertyChanged(nameof(PasswordError));
        OnPropertyChanged(nameof(ConfirmError));
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        NeedsEmailProvider = false;

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

        if (Password.Length < 6)
        {
            ErrorMessage = "La contraseña debe tener al menos 6 caracteres.";
            return;
        }

        BeginBusy("Creando cuenta…");
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
                NeedsEmailProvider = true;
                ErrorMessage = "El servidor no permite crear cuentas con contraseña. "
                               + "Vamos a crearla con Google, que usa el mismo sistema de cuentas.";
                await SignInWithGoogleAsync(providerFallback: true);
                return;
            }

            ErrorMessage = ex.Message;
        }
        finally
        {
            EndBusy();
        }
    }

    private async Task SignInWithGoogleAsync(bool providerFallback = false)
    {
        if (!providerFallback)
        {
            ErrorMessage = string.Empty;
            NeedsEmailProvider = false;
        }

        InfoMessage = string.Empty;
        GoogleFailed = false;
        _googleCts?.Dispose();
        _googleCts = new CancellationTokenSource();
        IsGoogleAuth = true;
        GoogleStatusMessage = "Conectando con Google…";
        BeginBusy("Conectando con Google…");
        InfoMessage = "Pulsa «Continuar con Google» en la página del navegador. Esta ventana se actualizará automáticamente.";
        try
        {
            var progress = new Progress<GoogleSignInProgress>(update =>
            {
                if (!IsGoogleAuth)
                    return;

                GoogleStatusMessage = update.Message;
                _busyLabel = update.Message;
                OnPropertyChanged(nameof(StatusLabel));
            });

            var account = await _auth.SignInWithGoogleAsync(
                GoogleTimeout, progress, _googleCts.Token);

            GoogleStatusMessage = "Acceso concedido. Preparando el launcher…";
            InfoMessage = string.Empty;
            await CompleteAsync(account, cinematic: true, syncProfile: false);
        }
        catch (XdAuthException ex)
        {
            LogService.Warn("Acceso con Google rechazado: " + ex.Message);
            InfoMessage = string.Empty;
            ErrorMessage = ex.Code switch
            {
                "GOOGLE_TIMEOUT" => $"Google no respondió en {GoogleTimeoutMinutes} minutos. El launcher dejó de esperar. "
                    + "Pulsa «Reintentar con Google» o usa correo y contraseña.",
                "GOOGLE_BROWSER" => "No se pudo abrir el navegador. Abre tu navegador manualmente "
                    + "o usa el acceso por correo y contraseña.",
                "GOOGLE_BRIDGE" => ex.Message,
                _ => ex.Message
            };
            NeedsEmailProvider = providerFallback;
            GoogleFailed = true;
        }
        catch (OperationCanceledException)
        {
            LogService.Info("El usuario canceló el acceso con Google.");
            InfoMessage = string.Empty;
            ErrorMessage = "Se canceló el acceso con Google. Pulsa «Reintentar con Google» si quieres volver a intentarlo.";
            GoogleFailed = true;
        }
        catch (Exception ex)
        {
            InfoMessage = string.Empty;
            OnError(ex);
            GoogleFailed = true;
        }
        finally
        {
            _googleCts?.Dispose();
            _googleCts = null;
            IsGoogleAuth = false;
            GoogleStatusMessage = string.Empty;
            EndBusy();
        }
    }

    /// <summary>Cancela la espera del acceso con Google sin bloquear la interfaz.</summary>
    private void CancelGoogle()
    {
        if (!IsGoogleAuth)
            return;

        GoogleStatusMessage = "Cancelando el acceso con Google…";
        try
        {
            _googleCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // El flujo ya había terminado.
        }
    }

    private async Task RecoverAsync()
    {
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        NeedsEmailProvider = false;

        if (string.IsNullOrWhiteSpace(Email) || !Email.Contains('@'))
        {
            ErrorMessage = "Escribe un correo válido para enviarte el enlace de recuperación.";
            return;
        }

        BeginBusy("Enviando correo…");
        try
        {
            await _auth.SendPasswordResetAsync(Email);
            InfoMessage = "Te enviamos un correo con el enlace para crear una contraseña nueva.";
        }
        catch (XdAuthException ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            EndBusy();
        }
    }

    private bool ValidateCredentials()
    {
        _attempted = true;
        OnPropertyChanged(nameof(PasswordError));
        OnPropertyChanged(nameof(ConfirmError));
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

    /// <summary>
    /// Completa un acceso ya validado (por ejemplo, el traspaso de sesión desde la web).
    /// La sesión debe haberse guardado antes de llamar a este método.
    /// </summary>
    public void CompleteExternalLogin(XdAccount account)
        => _ = CompleteExternalLoginAsync(account);

    private async Task CompleteExternalLoginAsync(XdAccount account)
    {
        if (!BeginCompletion(account, "Sesión recibida desde la web. Preparando tu launcher…"))
            return;

        try
        {
            _googleCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // El flujo ya había terminado.
        }

        await FinishCompletionAsync(account, cinematic: true, syncProfile: false);
    }

    private async Task CompleteAsync(XdAccount account, bool cinematic = false, bool syncProfile = true)
    {
        if (!BeginCompletion(account, "Preparando tu launcher…"))
            return;

        await FinishCompletionAsync(account, cinematic, syncProfile);
    }

    private bool BeginCompletion(XdAccount account, string detail)
    {
        if (_completionStarted)
            return false;

        _completionStarted = true;
        if (RememberMe)
            XdSessionStore.Save(account);
        else
            XdSessionStore.Delete();
        Password = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = string.Empty;
        InfoMessage = string.Empty;
        NeedsEmailProvider = false;
        SuccessMessage = "Acceso concedido";
        SuccessDetail = detail;
        IsSuccess = true;
        return true;
    }

    private async Task FinishCompletionAsync(XdAccount account, bool cinematic, bool syncProfile)
    {
        if (cinematic && !AnimationHelper.ReduceMotion)
            await Task.Delay(850);
        else
            await Task.Delay(120);

        Authenticated?.Invoke(account);

        if (syncProfile)
            await _auth.SyncProfileAsync(account);
    }

    /// <summary>Cancela el flujo de Google si la ventana de acceso se está cerrando.</summary>
    public void CancelPendingOperations()
    {
        try
        {
            _googleCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Nada pendiente.
        }
    }

    private void OnError(Exception ex)
    {
        LogService.Error(ex, "Error en el inicio de sesión de XD Launcher.");
        ErrorMessage = ex switch
        {
            XdAuthException auth => auth.Message,
            HttpRequestException => "No se pudo conectar con el servidor de XD Launcher. Revisa tu conexión a Internet.",
            TaskCanceledException => "El servidor tardó demasiado en responder. Inténtalo de nuevo.",
            _ => "No se pudo completar la operación. Inténtalo de nuevo."
        };
    }

    private void BeginBusy(string label)
    {
        _busyLabel = label;
        _busyDepth++;
        if (_busyDepth == 1)
            IsBusy = true;
        else
            OnPropertyChanged(nameof(StatusLabel));
    }

    private void EndBusy()
    {
        if (_busyDepth == 0)
            return;

        _busyDepth--;
        if (_busyDepth == 0)
            IsBusy = false;
    }

    private void RaiseCommandStates()
    {
        SignInCommand.RaiseCanExecuteChanged();
        RegisterCommand.RaiseCanExecuteChanged();
        RecoverCommand.RaiseCanExecuteChanged();
        ContinueWithGoogleCommand.RaiseCanExecuteChanged();
        CancelGoogleCommand.RaiseCanExecuteChanged();
    }
}
