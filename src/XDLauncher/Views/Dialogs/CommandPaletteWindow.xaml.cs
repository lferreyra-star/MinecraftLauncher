using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using XdLauncher.ViewModels;

namespace XdLauncher.Views.Dialogs;

public partial class CommandPaletteWindow : Window
{
    private sealed record PaletteItem(string Key, string Title, string Subtitle, string Glyph, string Group);

    private readonly ShellViewModel _shell;
    private readonly List<PaletteItem> _items;

    public CommandPaletteWindow(ShellViewModel shell)
    {
        InitializeComponent();
        _shell = shell;
        _items = shell.Navigation
            .Select(item => new PaletteItem(item.Key, item.Title, item.Subtitle, item.Glyph, item.Group))
            .ToList();
        SearchBox.TextChanged += (_, _) => Filter();
        Filter();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void Filter()
    {
        var query = SearchBox.Text.Trim();
        var results = _items
            .Where(item => string.IsNullOrWhiteSpace(query) ||
                           item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           item.Subtitle.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                           item.Group.Contains(query, StringComparison.OrdinalIgnoreCase))
            .Take(12)
            .ToList();
        Results.ItemsSource = results;
        if (results.Count > 0)
            Results.SelectedIndex = 0;
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => Filter();

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ExecuteSelected();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            DialogResult = false;
            e.Handled = true;
        }
        else if (e.Key == Key.Down && Results.Items.Count > 0)
        {
            Results.SelectedIndex = Math.Min(Results.SelectedIndex + 1, Results.Items.Count - 1);
            e.Handled = true;
        }
        else if (e.Key == Key.Up && Results.Items.Count > 0)
        {
            Results.SelectedIndex = Math.Max(Results.SelectedIndex - 1, 0);
            e.Handled = true;
        }
    }

    private void Results_MouseDoubleClick(object sender, MouseButtonEventArgs e) => ExecuteSelected();

    private void ExecuteSelected()
    {
        if (Results.SelectedItem is not PaletteItem item)
            return;
        _shell.Navigate(item.Key);
        DialogResult = true;
    }
}
