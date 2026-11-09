using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: System.Reflection.AssemblyTitle("个人便签")]
[assembly: System.Reflection.AssemblyProduct("个人便签")]
[assembly: System.Reflection.AssemblyVersion("1.0.5.0")]

namespace PersonalStickyNote
{
    internal enum LaunchMode { Manual, WindowsStartup, AfterUpdate }

    internal static class Program
    {
        public const string ProductName = "个人便签";
        public static string Name { get { return L.T(ProductName); } }

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main(string[] args)
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "PersonalStickyNote");
            var store = new NoteStore(directory);
            if (args.Length == 3 && args[0] == "--apply-update")
            {
                try { L.Use(store.Load().Language); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (InvalidOperationException) { }
                AppUpdates.Apply(args[1], args[2]);
                return;
            }
            Guid receipt;
            bool updated = args.Length == 2 && args[0] == "--updated" && Guid.TryParseExact(args[1], "N", out receipt);
            var mode = updated ? LaunchMode.AfterUpdate : args.Contains(DesktopIntegration.StartupArgument)
                ? LaunchMode.WindowsStartup : LaunchMode.Manual;
            using (var instance = new Mutex(false, @"Local\PersonalStickyNote_FilnaDo"))
            {
                if (!AcquireInstance(instance, mode != LaunchMode.Manual))
                {
                    return;
                }
                try
                {
                    using (var session = new NoteSession(store, mode))
                    using (var activation = new ListActivationWindow(() => session.ShowList()))
                    {
                        if (updated)
                        {
                            AppUpdates.ConfirmLaunch(args[1]);
                            session.Notify(L.T("已更新到版本 {0}。", AppUpdates.DisplayVersion));
                        }
                        Application.Run(session);
                    }
                }
                catch (Exception error)
                {
                    if (!updated) MessageBox.Show(L.T("无法打开个人便签。\n\n") + error.Message,
                        Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally { instance.ReleaseMutex(); }
            }
        }

        private static bool AcquireInstance(Mutex instance, bool silent)
        {
            for (int attempt = 0; attempt < 20; attempt++)
            {
                try { if (instance.WaitOne(0)) return true; }
                catch (AbandonedMutexException) { return true; }
                if (silent || ListActivationWindow.RequestList()) return false;
                Thread.Sleep(100);
            }
            MessageBox.Show(L.T("个人便签已在运行。请从任务栏切换。如果正在运行旧版，请先退出旧版。"),
                Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            return false;
        }
    }

    internal sealed class NoteSession : ApplicationContext
    {
        private readonly NoteStore store;
        private readonly Dictionary<Guid, NoteWindow> windows = new Dictionary<Guid, NoteWindow>();
        private readonly Timer saveTimer = new Timer { Interval = 450 };
        private readonly ListWindow list;
        private readonly TrayHost tray;
        private bool dirty;
        private bool closing;
        public Notebook Book { get; private set; }
        public bool Dark { get; private set; }
        public AppUpdates Updates { get; private set; }
        public ShortcutMap Shortcuts { get; private set; }

        public NoteSession(NoteStore store, LaunchMode mode = LaunchMode.Manual)
        {
            this.store = store;
            Book = store.Load();
            Book.Language = L.Normalize(Book.Language);
            L.Use(Book.Language);
            Shortcuts = new ShortcutMap(Book);
            Dark = IsDark();
            list = new ListWindow(this);
            Updates = new AppUpdates(this);
            tray = new TrayHost(this, list);
            saveTimer.Tick += (sender, args) => Save(false);
            SystemEvents.UserPreferenceChanged += WindowsThemeChanged;

            bool windowsStartup = mode == LaunchMode.WindowsStartup;
            var restore = Book.Notes.Where(note => windowsStartup ? note.Pinned : note.Open).ToArray();
            if (windowsStartup || (mode == LaunchMode.AfterUpdate && (Book.ListVisible || store.Recovered)))
                ShowList(false);
            foreach (var note in restore) Open(note, false);
            if (mode == LaunchMode.Manual) ShowList();
            if (store.Recovered)
                MessageBox.Show(list, L.T("已从上次保存的备份恢复便签。"), Program.Name);
        }

        public void NewNote(Guid groupId = default(Guid))
        {
            var note = new Note
            {
                GroupId = groupId, Color = Book.SharedPaper ? Book.ListPaper : Book.NewNotePaper,
                CustomBackground = Book.SharedPaper ? Book.ListCustomColor : Book.NewNoteCustomColor
            };
            Book.Notes.Insert(0, note);
            Open(note);
            Changed();
            list.RefreshNotes();
        }

        public IEnumerable<Note> OrderedNotes()
        {
            return Book.ManualNoteOrder ? Book.Notes : (IEnumerable<Note>)Book.Notes.OrderByDescending(note => note.Updated);
        }

        public void Move(Note note, Guid groupId, Note anchor = null, bool after = false)
        {
            if (note == anchor || (anchor == null && note.GroupId == groupId)) return;
            if (!Book.ManualNoteOrder)
            {
                Book.Notes = OrderedNotes().ToList();
                Book.ManualNoteOrder = true;
            }
            note.GroupId = groupId;
            Book.Notes.Remove(note);
            int index = anchor == null ? -1 : Book.Notes.IndexOf(anchor);
            if (index < 0) Book.Notes.Add(note);
            else Book.Notes.Insert(index + (after ? 1 : 0), note);
            Changed();
            list.RefreshNotes();
        }

        public void RemoveGroup(NoteGroup group)
        {
            Book.Groups.Remove(group);
            foreach (var note in Book.Notes.Where(note => note.GroupId == group.Id))
                note.GroupId = Guid.Empty;
            Changed();
            list.RefreshNotes();
        }

        public void Open(Note note, bool activate = true)
        {
            NoteWindow window;
            if (!windows.TryGetValue(note.Id, out window))
            {
                window = new NoteWindow(this, note);
                windows.Add(note.Id, window);
                if (!note.Open)
                {
                    note.Open = true;
                    Changed();
                }
                window.Show();
                if (note.Position.Width == 0) NoteStyle.Remember(window, note.Position, this);
            }
            if (activate)
            {
                if (window.WindowState == FormWindowState.Minimized)
                    window.RestoreNormal();
                window.Activate();
            }
        }

        public void ShowList(bool activate = true, bool openNotebook = true)
        {
            if (closing) return;
            if (!Book.ListVisible)
            {
                Book.ListVisible = true;
                Changed();
            }
            if (openNotebook) list.ShowNotebook();
            list.RefreshNotes();
            list.Show();
            if (Book.ListPosition.Width == 0) NoteStyle.Remember(list, Book.ListPosition, this);
            if (list.WindowState == FormWindowState.Minimized)
                list.RestoreNormal();
            if (activate) list.Activate();
        }

        public void Cycle(Form current, bool backwards)
        {
            var all = new List<Form> { list };
            all.AddRange(windows.Values);
            int index = (all.IndexOf(current) + (backwards ? all.Count - 1 : 1)) % all.Count;
            var note = all[index] as NoteWindow;
            if (note == null) ShowList();
            else Open(note.Note);
        }

        public void SearchNotes()
        {
            ShowList();
            list.FocusSearch();
        }

        public IEnumerable<Rectangle> OtherWindowBounds(CaptionWindow except)
        {
            return windows.Values.Cast<CaptionWindow>().Concat(new[] { list }).Where(window => window != except && window.Visible &&
                window.WindowState == FormWindowState.Normal).Select(window => window.Bounds);
        }

        public void Notify(string message) { tray.Notify(message); }

        public void ConfigurePalette()
        {
            ShowList(openNotebook: false);
            using (var dialog = new PaletteDialog(list, Book, Dark))
            {
                if (dialog.ShowDialog(list) != DialogResult.OK) return;
                dialog.Save();
                list.ApplyTheme();
                Changed();
            }
        }

        public void CheckForUpdates() { Updates.Check(list); }

        public void SetLanguage(string preference)
        {
            Book.Language = L.Normalize(preference);
            L.Use(Book.Language);
            list.ApplyLanguage();
            foreach (var window in windows.Values) window.ApplyLanguage();
            tray.ApplyLanguage();
            Changed();
        }

        public void ShortcutsChanged()
        {
            list.RefreshShortcutLabels();
            foreach (var window in windows.Values) window.RefreshShortcutLabels();
            Changed();
        }

        public void CloseList(FormClosingEventArgs args)
        {
            if (closing) return;
            if (args.CloseReason == CloseReason.UserClosing)
            {
                args.Cancel = true;
                list.Hide();
                Book.ListVisible = false;
                Changed();
                return;
            }
            args.Cancel = !Quit();
        }

        public void CloseNote(NoteWindow window, FormClosingEventArgs args)
        {
            if (closing) return;
            if (args.CloseReason != CloseReason.UserClosing)
            {
                args.Cancel = !Quit();
                return;
            }
            window.CaptureContent();
            windows.Remove(window.Note.Id);
            window.Note.Open = false;
            Changed();
            Save(false);
        }

        public void Rename(Note note, Form owner)
        {
            var title = TextPrompt.Ask(owner, L.T("便笺名称"), L.T("名称（留空时显示正文预览）"), note.Title, 64, null);
            if (title == null || title == note.Title) return;
            note.Title = title;
            note.Updated = DateTime.UtcNow;
            NoteWindow window;
            if (windows.TryGetValue(note.Id, out window)) window.RefreshTitle();
            Changed();
            list.RefreshNotes();
        }

        public void Delete(Note note, Form owner)
        {
            if (Book.ConfirmDelete && MessageBox.Show(owner, L.T("要永久删除这条便签吗？"),
                Program.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) != DialogResult.Yes) return;

            Book.Notes.Remove(note);
            NoteWindow window;
            if (windows.TryGetValue(note.Id, out window))
                window.Close();
            else
            {
                Changed();
                Save(false);
            }
        }

        public void Changed()
        {
            if (closing) return;
            dirty = true;
            saveTimer.Stop();
            saveTimer.Start();
        }

        public bool Quit()
        {
            if (closing) return true;
            if (!Save(true)) return false;
            ExitSaved();
            return true;
        }

        public bool InstallUpdate(Action startHelper)
        {
            if (closing || !Save(true)) return false;
            startHelper();
            ExitSaved();
            return true;
        }

        private void ExitSaved()
        {
            closing = true;
            ExitThread();
        }

        public void ApplySettings()
        {
            bool dark = IsDark();
            if (Dark != dark)
            {
                Dark = dark;
                list.ApplyTheme();
                foreach (var window in windows.Values) window.ApplyTheme();
            }
            Changed();
        }

        private bool Save(bool interactive)
        {
            saveTimer.Stop();
            if (!interactive && windows.Values.Any(window => window.IsImeComposing))
            {
                saveTimer.Start();
                return false;
            }
            foreach (var window in windows.Values) window.CaptureContent();
            list.RefreshNotes();
            if (!dirty) return true;
            try
            {
                store.Save(Book);
                dirty = false;
                list.SetSaveError(false);
                return true;
            }
            catch (Exception error)
            {
                list.SetSaveError(true);
                if (interactive)
                    MessageBox.Show(Form.ActiveForm, L.T("保存失败，请检查本地存储后重试。\n\n") +
                        error.Message, Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        private bool IsDark()
        {
            if (Book.Theme != "system") return Book.Theme == "dark";
            return Equals(0, Registry.GetValue(
                @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
                "AppsUseLightTheme", 1));
        }

        private void WindowsThemeChanged(object sender, UserPreferenceChangedEventArgs args)
        {
            if (!closing && list.IsHandleCreated)
                list.BeginInvoke(new Action(() =>
                {
                    if (Book.Theme == "system") ApplySettings();
                }));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SystemEvents.UserPreferenceChanged -= WindowsThemeChanged;
                saveTimer.Dispose();
                foreach (var window in windows.Values) window.Dispose();
                tray.Dispose();
                list.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
