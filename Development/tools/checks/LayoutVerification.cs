using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;

internal static class LayoutVerification
{
    public static void Run(string project, Action<bool, string> assert)
    {
        string program = Path.Combine(project, "Program");
        assert(Directory.Exists(program) && Directory.Exists(Path.Combine(project, "Development"))
            && Directory.Exists(Path.Combine(project, "Archive")) && !Directory.Exists(Path.Combine(project, "release"))
            && !Directory.Exists(Path.Combine(project, "src")) && !Directory.Exists(Path.Combine(project, "tools")),
            "The project uses Program, Development and Archive without obsolete root source/output folders");
        string executable = Path.Combine(program, "FKPServerMonitor.exe");
        assert(File.Exists(executable) && File.Exists(executable + ".config")
            && File.Exists(Path.Combine(program, "FKPServerMonitor-Setup.exe")) && File.Exists(Path.Combine(program, "USER_GUIDE_TH.md")),
            "Program contains the runnable app, required runtime config, installer and Thai user guide");
        string shortcut = Path.Combine(project, "FKP Server Monitor.lnk");
        object instance = new DesktopShortcut.ShellLink();
        try
        {
            ((IPersistFile)instance).Load(shortcut, 0);
            var link = (DesktopShortcut.IShellLinkW)instance;
            var target = new StringBuilder(32768);
            var working = new StringBuilder(32768);
            var icon = new StringBuilder(32768);
            int iconIndex;
            link.GetPath(target, target.Capacity, IntPtr.Zero, 0);
            link.GetWorkingDirectory(working, working.Capacity);
            link.GetIconLocation(icon, icon.Capacity, out iconIndex);
            assert(String.Equals(target.ToString(), executable, StringComparison.OrdinalIgnoreCase)
                && String.Equals(working.ToString(), program, StringComparison.OrdinalIgnoreCase)
                && String.Equals(icon.ToString(), executable, StringComparison.OrdinalIgnoreCase) && iconIndex == 0,
                "The root Windows shortcut targets Program with the correct working directory and robot icon");
        }
        finally { Marshal.FinalReleaseComObject(instance); }
    }
}
