using System.Windows;
using XdLauncher.Core;
using XdLauncher.Services;

namespace XdLauncher.Services;

public sealed class UpdateService
{
    public event Action<XdLauncher.Models.UpdateInfo>? UpdateChecked;
    public event Action<XdLauncher.Models.UpdateInfo, bool>? UpdateAvailable;

    public UpdateService() { }

    public void CheckForUpdatesCore() { }
}
