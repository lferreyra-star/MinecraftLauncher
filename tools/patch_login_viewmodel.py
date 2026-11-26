import io

def read(p):
    return io.open(p, 'r', encoding='utf-8-sig').read()

def write(p, s):
    io.open(p, 'w', encoding='utf-8-sig', newline='\r\n').write(s)

p = 'src/XDLauncher/Services/SettingsService.cs'
s = read(p)
old = '    public bool AutoCheckUpdates { get; set; } = true;\n'
new = old + '    public bool RememberMe { get; set; } = true;\n'
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/ViewModels/LoginViewModel.cs'
s = read(p)
s = s.replace('    private readonly XdAuthService _auth;\n',
              '    private readonly XdAuthService _auth;\n    private readonly SettingsService? _settings;\n    private bool _attempted;\n', 1)
s = s.replace('    public LoginViewModel(XdAuthService auth)\n    {\n        _auth = auth;\n',
              '    public LoginViewModel(XdAuthService auth, SettingsService? settings = null)\n    {\n        _auth = auth;\n        _settings = settings;\n', 1)
s = s.replace('        SwitchModeCommand = new RelayCommand(() => IsRegisterMode = !IsRegisterMode);\n',
              '        SwitchModeCommand = new RelayCommand(() => IsRegisterMode = !IsRegisterMode);\n        SetModeCommand = new RelayCommand(parameter => IsRegisterMode = parameter as bool? ?? parameter as string == "register");\n', 1)

old = '''    public string Email
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
'''
new = '''    public string Email
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
'''
assert old in s
s = s.replace(old, new, 1)

anchor = '    public RelayCommand SwitchModeCommand { get; }\n'
s = s.replace(anchor, anchor + '    public RelayCommand SetModeCommand { get; }\n', 1)

old = '    private bool ValidateCredentials()\n    {\n'
new = '    private bool ValidateCredentials()\n    {\n        _attempted = true;\n        OnPropertyChanged(nameof(PasswordError));\n        OnPropertyChanged(nameof(ConfirmError));\n'
assert old in s
s = s.replace(old, new, 1)

old = '    private async Task RegisterAsync()\n    {\n        ErrorMessage = string.Empty;\n'
if old not in s:
    old = '    private async Task RegisterAsync()\n    {\n'
    new = '    private async Task RegisterAsync()\n    {\n        _attempted = true;\n        OnPropertyChanged(nameof(PasswordError));\n        OnPropertyChanged(nameof(ConfirmError));\n'
else:
    new = '    private async Task RegisterAsync()\n    {\n        _attempted = true;\n        OnPropertyChanged(nameof(PasswordError));\n        OnPropertyChanged(nameof(ConfirmError));\n        ErrorMessage = string.Empty;\n'
assert old in s
s = s.replace(old, new, 1)

old = '        _completionStarted = true;\n        XdSessionStore.Save(account);\n'
new = '        _completionStarted = true;\n        if (RememberMe)\n            XdSessionStore.Save(account);\n        else\n            XdSessionStore.Delete();\n'
assert old in s
s = s.replace(old, new, 1)
write(p, s)

p = 'src/XDLauncher/App.xaml.cs'
s = read(p)
s = s.replace('account = await ShowLoginAsync(auth);', 'account = await ShowLoginAsync(auth, settingsService);', 1)
s = s.replace('var account = await ShowLoginAsync(new XdAuthService());', 'var account = await ShowLoginAsync(new XdAuthService(), settingsService);', 1)
s = s.replace('private async Task<XdAccount?> ShowLoginAsync(XdAuthService auth)',
              'private async Task<XdAccount?> ShowLoginAsync(XdAuthService auth, SettingsService settingsService)', 1)
s = s.replace('var loginViewModel = new ViewModels.LoginViewModel(auth);',
              'var loginViewModel = new ViewModels.LoginViewModel(auth, settingsService);', 1)
write(p, s)
