using System.Windows;
using System.Windows.Input;
using XdLauncher.Services;

namespace XdLauncher.Views.Dialogs;

public partial class DeviceCodeWindow : Window
{
    private readonly AuthService _auth;
    private readonly string _clientId;
    private readonly DeviceCodeInfo _deviceCode;
    private CancellationTokenSource _cts = new();
    private bool _closing;

    public DeviceCodeWindow(AuthService auth, string clientId, DeviceCodeInfo deviceCode)
    {
        InitializeComponent();
        _auth = auth;
        _clientId = clientId;
        _deviceCode = deviceCode;

        UserCodeText.Text = deviceCode.UserCode;
        CodeMessageText.Text = deviceCode.Message;
        LinkText.Text = deviceCode.VerificationUri;
        Loaded += async (_, _) => await RunAsync();
        Closed += (_, _) =>
        {
            _closing = true;
            _cts.Cancel();
        };
    }

    public SignInResult? Result { get; private set; }

    private async Task RunAsync()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        ErrorPanel.Visibility = Visibility.Collapsed;
        ProgressPanel.Visibility = Visibility.Visible;
        StatusText.Text = "Esperando a que completes el inicio de sesión…";

        try
        {
            var result = await _auth.CompleteSignInAsync(_clientId, _deviceCode, _cts.Token);
            if (_closing)
                return;

            Result = result;
            if (_closing)
                return;
            _closing = true;
            DialogResult = true;
        }
        catch (OperationCanceledException)
        {
            // User closed the dialog.
        }
        catch (AuthException ex)
        {
            ShowError(ex.Message, retry: ex.ErrorCode is not ("expired_token" or "authorization_declined"));
        }
        catch (Exception ex)
        {
            LogService.Error(ex, "Error en el flujo de código de dispositivo.");
            ShowError("No se pudo completar el inicio de sesión: " + ex.Message, retry: true);
        }
    }

    private void ShowError(string message, bool retry)
    {
        if (_closing)
            return;
        ProgressPanel.Visibility = Visibility.Collapsed;
        ErrorPanel.Visibility = Visibility.Visible;
        ErrorText.Text = message;
        RetryButton.Visibility = retry ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OpenBrowser_Click(object sender, RoutedEventArgs e)
    {
        var url = _deviceCode.VerificationUriComplete ?? _deviceCode.VerificationUri;
        if (!FileOps.OpenUrl(url))
            FileOps.CopyText(url);
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
        => FileOps.CopyText(_deviceCode.UserCode);

    private async void Retry_Click(object sender, RoutedEventArgs e)
        => await RunAsync();

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        if (_closing)
            return;
        _closing = true;
        _cts.Cancel();
        DialogResult = false;
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }
}
