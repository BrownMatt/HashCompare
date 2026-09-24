using System.ComponentModel;
using System.Diagnostics;
using HashCompare.Core;

namespace HashCompare.App.Services;

/// <summary>Starts the external diff tool chosen by <see cref="DiffToolResolver"/>.</summary>
public static class DiffToolLauncher
{
    /// <summary>
    /// Opens <paramref name="left"/> and <paramref name="right"/> in the configured or auto-detected
    /// diff tool. Returns false if no tool was found or it could not be started.
    /// </summary>
    public static bool TryLaunch(AppConfig config, string left, string right)
    {
        var command = DiffToolResolver.Resolve(config, left, right);
        if (command is null)
            return false;

        try
        {
            Process.Start(new ProcessStartInfo(command.FileName, command.Arguments) { UseShellExecute = false });
            return true;
        }
        catch (Win32Exception)
        {
            // The file exists but isn't runnable (e.g. not executable, wrong architecture).
            return false;
        }
    }
}
