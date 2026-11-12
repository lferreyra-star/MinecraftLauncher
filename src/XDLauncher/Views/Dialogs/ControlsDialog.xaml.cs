using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XdLauncher.Services;

namespace XdLauncher.Views.Dialogs;

/// <summary>Live row of a bindable action inside the controls dialog.</summary>
public sealed class BindableAction : INotifyPropertyChanged
{
    private int _virtualKey;

    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }
    public required string Glyph { get; init; }

    public int VirtualKey
    {
        get => _virtualKey;
        set
        {
            if (_virtualKey == value)
                return;
            _virtualKey = value;
            OnPropertyChanged(nameof(VirtualKey));
            OnPropertyChanged(nameof(KeyLabel));
        }
    }

    public string KeyLabel => MobileInputCatalog.KeyLabel(VirtualKey);

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Lets the user rebind the PC keys used to control the phone.</summary>
public partial class ControlsDialog : Window
{
    private readonly Dictionary<string, int> _bindings = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<BindableAction> _actions = new();
    private string? _waitingFor;

    public ControlsDialog(Dictionary<string, int> bindings)
    {
        InitializeComponent();

        foreach (var action in MobileInputCatalog.Actions)
        {
            var key = bindings.TryGetValue(action.Id, out var value) ? value : action.DefaultVirtualKey;
            _bindings[action.Id] = key;
            _actions.Add(new BindableAction
            {
                Id = action.Id,
                Label = action.Label,
                Description = action.Description,
                Glyph = action.Glyph,
                VirtualKey = key
            });
        }

        ActionsList.ItemsSource = _actions;
    }

    /// <summary>Shows the dialog and returns the new bindings, or null when cancelled.</summary>
    public static Dictionary<string, int>? Show(Dictionary<string, int> bindings)
    {
        var dialog = new ControlsDialog(bindings);
        var owner = Application.Current?.MainWindow;
        if (owner is { IsLoaded: true })
            dialog.Owner = owner;

        return dialog.ShowDialog() == true ? dialog._bindings : null;
    }

    private void Assign_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string actionId)
            return;

        _waitingFor = actionId;
        button.Content = "Pulsa una tecla…";
        button.IsEnabled = false;
        Focus();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (_waitingFor is null)
        {
            base.OnPreviewKeyDown(e);
            return;
        }

        if (e.Key == Key.Escape)
        {
            _waitingFor = null;
            RestoreButtons();
            e.Handled = true;
            return;
        }

        var virtualKey = KeyInterop.VirtualKeyFromKey(e.Key);
        if (virtualKey != 0)
        {
            var action = _actions.FirstOrDefault(a => a.Id == _waitingFor);
            if (action is not null)
            {
                action.VirtualKey = virtualKey;
                _bindings[action.Id] = virtualKey;
            }

            _waitingFor = null;
            RestoreButtons();
        }

        e.Handled = true;
    }

    private void RestoreButtons()
    {
        foreach (var action in _actions)
        {
            // The row template rebuilds the button, so only the label needs to be restored.
            _ = action;
        }

        ActionsList.Items.Refresh();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        foreach (var action in MobileInputCatalog.Actions)
        {
            var row = _actions.FirstOrDefault(a => a.Id == action.Id);
            if (row is null)
                continue;
            row.VirtualKey = action.DefaultVirtualKey;
            _bindings[action.Id] = action.DefaultVirtualKey;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        foreach (var action in _actions)
            _bindings[action.Id] = action.VirtualKey;

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
