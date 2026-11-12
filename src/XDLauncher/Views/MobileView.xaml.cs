using System.Windows;
using System.Windows.Controls;
using XdLauncher.ViewModels;

namespace XdLauncher.Views;

public partial class MobileView : UserControl
{
    private MobileConnectState? _state;

    public MobileView()
    {
        InitializeComponent();
        Loaded += MobileView_Loaded;
        Unloaded += MobileView_Unloaded;
    }

    private void MobileView_Loaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MobileConnectState state)
            return;

        _state = state;
        state.StreamWindowAvailable += OnStreamWindowAvailable;
        state.StreamEnded += OnStreamEnded;
        UpdatePlaceholder(state.IsStreaming);
    }

    private void MobileView_Unloaded(object sender, RoutedEventArgs e)
    {
        if (_state is null)
            return;

        _state.StreamWindowAvailable -= OnStreamWindowAvailable;
        _state.StreamEnded -= OnStreamEnded;
        _state = null;
    }

    private void OnStreamWindowAvailable(IntPtr handle)
    {
        if (handle == IntPtr.Zero)
            return;

        StreamHost.AttachWindow(handle);
        Placeholder.Visibility = Visibility.Collapsed;
    }

    private void OnStreamEnded()
    {
        StreamHost.DetachWindow();
        Placeholder.Visibility = Visibility.Visible;
    }

    private void UpdatePlaceholder(bool streaming)
    {
        Placeholder.Visibility = streaming ? Visibility.Collapsed : Visibility.Visible;
    }
}
