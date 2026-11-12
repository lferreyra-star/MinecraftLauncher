using System.Windows;
using XdLauncher.Core;
using XdLauncher.Models;
using XdLauncher.Services;
using XdLauncher.Views.Dialogs;

namespace XdLauncher.ViewModels;

/// <summary>Microsoft/Xbox Live profile and local profile state.</summary>
public sealed class ProfileState : ObservableObject
{
    private readonly AuthService _auth;
    private readonly SettingsState _settings;
    private readonly DialogService _dialogs;

    private PlayerProfile _profile;
    private bool _isBusy;
    private string _statusMessage = "Perfil local del launcher.";

    public ProfileState(AuthService auth, SettingsState settings, DialogService dialogs)
    {
        _auth = auth;
        _settings = settings;
        _dialogs = dialogs;
        _profile = BuildLocalProfile();

        SignInCommand = new AsyncRelayCommand(SignInAsync, () => !IsBusy, OnError);
        SignOutCommand = new RelayCommand(SignOut);
        UseLocalCommand = new RelayCommand(UseLocal);
        RenameLocalCommand = new RelayCommand(RenameLocal);
        PickAvatarCommand = new RelayCommand(PickAvatar);

        _settings.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(SettingsState.LocalProfileName) or nameof(SettingsState.IsMicrosoftConfigured))
            {
                if (Profile.IsLocal)
                    _profile = BuildLocalProfile();
                OnPropertyChanged(nameof(IsMicrosoftConfigured));
                SignInCommand.RaiseCanExecuteChanged();
                NotifyProfile();
            }
        };
    }

    public PlayerProfile Profile
    {
        get => _profile;
        private set
        {
            _profile = value;
            NotifyProfile();
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                SignInCommand.RaiseCanExecuteChanged();
                OnPropertyChanged(nameof(SignInLabel));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsMicrosoftAccount => Profile.IsMicrosoftAccount;
    public bool IsLocal => Profile.IsLocal;
    public bool IsMicrosoftConfigured => _settings.IsMicrosoftConfigured;
    public string DisplayName => Profile.Gamertag;
    public string AccountTypeLabel => Profile.AccountTypeLabel;
    public string Initial => Profile.Initial;
    public string? AvatarPath => Profile.AvatarPath;
    public string GamerscoreLabel => string.IsNullOrWhiteSpace(Profile.Gamerscore) ? "—" : Profile.Gamerscore;
    public string SignInLabel => IsBusy ? "Conectando…" : IsMicrosoftAccount ? "Cuenta conectada" : "Iniciar sesión con Microsoft";
    public string SignInGlyph => IsMicrosoftAccount ? "\uE73E" : "\uE77B";
    public string SwitchLabel => IsMicrosoftAccount ? "Usar perfil local" : "Volver a Microsoft";

    public AsyncRelayCommand SignInCommand { get; }
    public RelayCommand SignOutCommand { get; }
    public RelayCommand UseLocalCommand { get; }
    public RelayCommand RenameLocalCommand { get; }
    public RelayCommand PickAvatarCommand { get; }

    public event Action? ProfileChanged;

    /// <summary>
    /// Devuelve la sesión que usará Minecraft Java. Con cuenta Microsoft configurada se solicita
    /// un token oficial de Minecraft Services; si no está disponible se usa un perfil local (sin
    /// hacerse pasar por premium) y se informa del motivo.
    /// </summary>
    public async Task<JavaSession> GetJavaSessionAsync(CancellationToken cancellationToken = default)
    {
        var localName = string.IsNullOrWhiteSpace(_settings.Settings.Java.PlayerName)
            ? _settings.Settings.LocalProfileName
            : _settings.Settings.Java.PlayerName;

        if (IsMicrosoftAccount && _settings.IsMicrosoftConfigured)
        {
            var (refreshToken, _) = AccountStore.Load();
            if (!string.IsNullOrWhiteSpace(refreshToken))
            {
                try
                {
                    var auth = await _auth.GetMinecraftAuthAsync(
                        _settings.Settings.MicrosoftClientId, refreshToken, cancellationToken);
                    if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
                        AccountStore.Save(auth.RefreshToken, Profile);

                    return new JavaSession
                    {
                        IsPremium = true,
                        PlayerName = auth.Name,
                        Uuid = auth.Uuid,
                        AccessToken = auth.AccessToken
                    };
                }
                catch (Exception ex)
                {
                    LogService.Warn("No se pudo obtener la sesión de Minecraft: " + ex.Message);
                    return new JavaSession
                    {
                        IsPremium = false,
                        PlayerName = localName,
                        Uuid = JavaService.OfflineUuid(localName),
                        AccessToken = "0",
                        Notice = ex.Message
                    };
                }
            }
        }

        return new JavaSession
        {
            IsPremium = false,
            PlayerName = localName,
            Uuid = JavaService.OfflineUuid(localName),
            AccessToken = "0"
        };
    }

    /// <summary>
    /// Sesión exclusivamente premium para el workspace Premium. Nunca cae a un perfil local:
    /// si falta la cuenta o no se puede validar, devuelve null y el llamador no debe lanzar.
    /// </summary>
    public async Task<JavaSession?> GetPremiumJavaSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!IsMicrosoftAccount || !_settings.IsMicrosoftConfigured)
            return null;

        var (refreshToken, _) = AccountStore.Load();
        if (string.IsNullOrWhiteSpace(refreshToken))
            return null;

        try
        {
            var auth = await _auth.GetMinecraftAuthAsync(
                _settings.Settings.MicrosoftClientId, refreshToken, cancellationToken);
            if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
                AccountStore.Save(auth.RefreshToken, Profile);

            return new JavaSession
            {
                IsPremium = true,
                PlayerName = auth.Name,
                Uuid = auth.Uuid,
                AccessToken = auth.AccessToken
            };
        }
        catch (Exception ex)
        {
            LogService.Warn("No se pudo validar la sesión premium de Minecraft: " + ex.Message);
            return null;
        }
    }

    public async Task RestoreAsync()
    {
        var (refreshToken, stored) = AccountStore.Load();
        if (stored is not null && stored.Gamertag is { Length: > 0 })
        {
            _profile = new PlayerProfile
            {
                Gamertag = stored.Gamertag,
                AvatarUrl = stored.AvatarUrl,
                Gamerscore = stored.Gamerscore,
                Xuid = stored.Xuid,
                IsMicrosoftAccount = true,
                AvatarPath = ResolveAvatar(stored.Gamertag, null)
            };
            NotifyProfile();
        }

        if (refreshToken is null || !_settings.IsMicrosoftConfigured || !_settings.Settings.AutoRestoreAccount)
        {
            UseLocal(silent: true);
            return;
        }

        IsBusy = true;
        StatusMessage = "Restaurando sesión de Microsoft…";
        try
        {
            var result = await _auth.RestoreAsync(_settings.Settings.MicrosoftClientId, refreshToken);
            AccountStore.Save(result.RefreshToken, result.Profile);
            ApplyMicrosoft(result.Profile);
            _settings.Settings.UseLocalProfile = false;
            _settings.Save();
            StatusMessage = "Sesión de Microsoft restaurada.";
        }
        catch (AuthException ex)
        {
            LogService.Warn("No se pudo restaurar la sesión de Microsoft: " + ex.Message);
            if (ex.ErrorCode is "invalid_grant")
                AccountStore.Delete();
            UseLocal(silent: true);
            StatusMessage = "La sesión guardada no está disponible. Se usa el perfil local.";
        }
        catch (Exception ex)
        {
            LogService.Warn("Error restaurando la cuenta: " + ex.Message);
            UseLocal(silent: true);
            StatusMessage = "No se pudo conectar con Microsoft. Se usa el perfil local.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task SignInAsync()
    {
        if (!_settings.IsMicrosoftConfigured)
        {
            _dialogs.ShowMessage("Falta configurar Microsoft",
                "El inicio de sesión usa el flujo oficial de código de dispositivo de Microsoft. " +
                "Necesitas registrar una aplicación pública en Microsoft Entra y pegar su Client ID en Ajustes → Cuenta.\n\n" +
                "El launcher no incluye credenciales de terceros y nunca solicita tu contraseña.",
                DialogKind.Info);
            return;
        }

        if (IsBusy)
            return;

        IsBusy = true;
        StatusMessage = "Solicitando un código de inicio de sesión a Microsoft…";
        try
        {
            var deviceCode = await _auth.RequestDeviceCodeAsync(_settings.Settings.MicrosoftClientId);
            StatusMessage = "Esperando a que completes el inicio de sesión en el navegador…";

            var window = new DeviceCodeWindow(_auth, _settings.Settings.MicrosoftClientId, deviceCode);
            var owner = Application.Current?.MainWindow;
            if (owner is { IsLoaded: true })
                window.Owner = owner;

            var completed = window.ShowDialog() == true && window.Result is not null;
            if (!completed || window.Result is null)
            {
                StatusMessage = "Inicio de sesión cancelado.";
                return;
            }

            AccountStore.Save(window.Result.RefreshToken, window.Result.Profile);
            ApplyMicrosoft(window.Result.Profile);
            _settings.Settings.UseLocalProfile = false;
            _settings.Save();
            StatusMessage = "Sesión iniciada con Xbox Live.";
            ToastHub.Success($"Hola, {window.Result.Profile.Gamertag}.");
        }
        catch (AuthException ex)
        {
            StatusMessage = ex.Message;
            _dialogs.ShowMessage(
                ex.ConfigurationRequired ? "Configuración de Microsoft" : "No se pudo iniciar sesión",
                ex.Message, ex.ConfigurationRequired ? DialogKind.Info : DialogKind.Error);
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error iniciando sesión con Microsoft.");
            StatusMessage = "No se pudo conectar con Microsoft.";
            _dialogs.ShowError("No se pudo iniciar sesión", ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void SignOut()
    {
        if (!_dialogs.Confirm("Cerrar sesión",
                "Se eliminará el token cifrado guardado en este equipo. El juego y tus compras de Microsoft no se modifican.",
                "Cerrar sesión", danger: true))
            return;

        AccountStore.Delete();
        UseLocal();
        StatusMessage = "Sesión cerrada.";
        ToastHub.Info("Se cerró la sesión de Microsoft.");
    }

    private void UseLocal()
    {
        var wasMicrosoft = IsMicrosoftAccount;
        UseLocal(silent: false);
        if (wasMicrosoft)
            ToastHub.Info("Ahora se usa el perfil local del launcher.");
    }

    private void UseLocal(bool silent)
    {
        _settings.Settings.UseLocalProfile = true;
        _settings.Save();
        Profile = BuildLocalProfile();
        StatusMessage = "Perfil local del launcher.";
        if (!silent)
            ProfileChanged?.Invoke();
    }

    private void RenameLocal()
    {
        var name = _dialogs.Prompt("Nombre de jugador local",
            "Este nombre solo se muestra dentro del launcher. Minecraft usa tu gamertag de Xbox Live.",
            _settings.LocalProfileName, "Máximo 32 caracteres");
        if (name is null)
            return;

        _settings.LocalProfileName = name;
        if (Profile.IsLocal)
        {
            Profile = BuildLocalProfile();
            ProfileChanged?.Invoke();
        }
        StatusMessage = "Nombre local actualizado.";
    }

    private void PickAvatar()
    {
        var file = _dialogs.PickImage("Elige un avatar");
        if (file is null)
            return;

        try
        {
            Directory.CreateDirectory(Paths.AvatarsDirectory);
            var extension = Path.GetExtension(file);
            if (string.IsNullOrWhiteSpace(extension) || extension.Length > 6)
                extension = ".png";
            var destination = Path.Combine(Paths.AvatarsDirectory, $"avatar-{DateTime.Now:yyyyMMddHHmmss}{extension}");

            var oldPath = _settings.Settings.LocalAvatarPath;
            File.Copy(file, destination, overwrite: true);
            _settings.Settings.LocalAvatarPath = destination;
            _settings.Save();

            if (!string.IsNullOrWhiteSpace(oldPath) && File.Exists(oldPath) &&
                !oldPath.Equals(destination, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(oldPath); } catch { /* not critical */ }
            }

            if (Profile.IsLocal)
            {
                Profile = BuildLocalProfile();
                ProfileChanged?.Invoke();
            }
            ToastHub.Success("Avatar actualizado.");
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "No se pudo aplicar el avatar.");
            ToastHub.Error("No se pudo copiar la imagen del avatar.");
        }
    }

    private PlayerProfile BuildLocalProfile() => new()
    {
        Gamertag = _settings.LocalProfileName,
        IsLocal = true,
        IsMicrosoftAccount = false,
        AvatarPath = ResolveLocalAvatar()
    };

    private string? ResolveLocalAvatar()
    {
        if (!string.IsNullOrWhiteSpace(_settings.Settings.LocalAvatarPath) &&
            File.Exists(_settings.Settings.LocalAvatarPath))
            return _settings.Settings.LocalAvatarPath;
        return Paths.DefaultAvatarPath;
    }

    private string? ResolveAvatar(string? gamertag, string? downloadUrl)
    {
        if (!string.IsNullOrWhiteSpace(gamertag))
        {
            var cached = Paths.GetAvatarCachePath("xbox:" + gamertag);
            if (File.Exists(cached))
                return cached;
        }
        return ResolveLocalAvatar();
    }

    private void ApplyMicrosoft(PlayerProfile profile)
    {
        var avatar = ResolveLocalAvatar();
        var cached = Paths.GetAvatarCachePath("xbox:" + profile.Gamertag);
        if (File.Exists(cached))
            avatar = cached;

        profile.AvatarPath = avatar;
        Profile = profile;
        _settings.Settings.UseLocalProfile = false;
        ProfileChanged?.Invoke();
    }

    private void NotifyProfile()
    {
        OnPropertyChanged(nameof(Profile));
        OnPropertyChanged(nameof(IsMicrosoftAccount));
        OnPropertyChanged(nameof(IsLocal));
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(AccountTypeLabel));
        OnPropertyChanged(nameof(Initial));
        OnPropertyChanged(nameof(AvatarPath));
        OnPropertyChanged(nameof(GamerscoreLabel));
        OnPropertyChanged(nameof(SignInLabel));
        OnPropertyChanged(nameof(SignInGlyph));
        OnPropertyChanged(nameof(SwitchLabel));
    }

    private void OnError(Exception exception)
    {
        LogService.Error(exception, "Error en una acción de cuenta.");
        _dialogs.ShowError("Error de cuenta", exception.Message);
    }
}
