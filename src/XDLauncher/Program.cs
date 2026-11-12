namespace XdLauncher;

/// <summary>
/// Explicit WPF entry point. Keeping it in source avoids relying on the markup
/// compiler's generated Main and makes the project build reliably from clean.
/// </summary>
public static class Program
{
    [STAThread]
    public static void Main()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
