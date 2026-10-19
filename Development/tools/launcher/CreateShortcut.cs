using System;
using System.IO;

internal static class CreateShortcut
{
    [STAThread]
    private static int Main(string[] arguments)
    {
        if (arguments.Length != 3) return 1;
        string shortcut = Path.GetFullPath(arguments[0]);
        string executable = Path.GetFullPath(arguments[1]);
        if (!File.Exists(executable)) return 1;
        DesktopShortcut.Create(shortcut, executable, Path.GetDirectoryName(executable), arguments[2]);
        return 0;
    }
}
