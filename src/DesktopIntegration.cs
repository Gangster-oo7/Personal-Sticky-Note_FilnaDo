using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal static class DesktopIntegration
    {
        public const string StartupArgument = "--startup";
        private const string StartupName = "PersonalStickyNote_FilnaDo.lnk";

        private static string StartupPath
        {
            get { return ShortcutPath(Environment.SpecialFolder.Startup, StartupName); }
        }

        public static bool StartsWithWindows { get { return File.Exists(StartupPath); } }

        public static void SetStartup(bool enabled)
        {
            if (enabled) WriteShortcut(StartupPath, Application.ExecutablePath, StartupArgument);
            else File.Delete(StartupPath);
        }

        public static void CreateDesktopShortcut()
        {
            WriteShortcut(ShortcutPath(Environment.SpecialFolder.DesktopDirectory, Program.Name + ".lnk"),
                Application.ExecutablePath, "");
        }

        private static string ShortcutPath(Environment.SpecialFolder folder, string file)
        {
            var directory = Environment.GetFolderPath(folder, Environment.SpecialFolderOption.DoNotVerify);
            if (string.IsNullOrEmpty(directory)) throw new IOException("无法找到 Windows 快捷方式目录。");
            return Path.Combine(directory, file);
        }

        internal static void WriteShortcut(string path, string executable, string arguments)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell", true));
            try
            {
                dynamic link = shell.CreateShortcut(path);
                try
                {
                    if (File.Exists(path) && !string.Equals(Path.GetFileName((string)link.TargetPath),
                        Path.GetFileName(executable), StringComparison.OrdinalIgnoreCase))
                        throw new IOException("已有同名快捷方式。请先将它改名，再创建个人便签的快捷方式。");
                    link.TargetPath = executable;
                    link.WorkingDirectory = Path.GetDirectoryName(executable);
                    link.Arguments = arguments;
                    link.Description = Program.Name;
                    link.IconLocation = executable + ",0";
                    link.Save();
                }
                finally { Marshal.FinalReleaseComObject(link); }
            }
            finally { Marshal.FinalReleaseComObject(shell); }
        }
    }

    internal sealed class ListActivationWindow : NativeWindow, IDisposable
    {
        private const string WindowName = "PersonalStickyNote_FilnaDo.ListActivation";
        private const int ShowListMessage = 0x8001; // WM_APP + 1
        private static readonly IntPtr MessageOnly = new IntPtr(-3); // HWND_MESSAGE
        private readonly Action showList;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr parent, IntPtr childAfter, string className, string title);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint processId);
        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(uint processId);
        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr handle, int message, IntPtr wParam, IntPtr lParam);

        public ListActivationWindow(Action showList)
        {
            this.showList = showList;
            CreateHandle(new CreateParams { Caption = WindowName, Parent = MessageOnly });
        }

        public static bool RequestList()
        {
            var window = FindWindowEx(MessageOnly, IntPtr.Zero, null, WindowName);
            if (window == IntPtr.Zero) return false;
            uint process;
            if (GetWindowThreadProcessId(window, out process) == 0) return false;
            AllowSetForegroundWindow(process);
            return PostMessage(window, ShowListMessage, IntPtr.Zero, IntPtr.Zero);
        }

        protected override void WndProc(ref Message message)
        {
            if (message.Msg == ShowListMessage) showList();
            else base.WndProc(ref message);
        }

        public void Dispose() { DestroyHandle(); }
    }
}
