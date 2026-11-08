using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
using Timer = System.Windows.Forms.Timer;

[assembly: System.Reflection.AssemblyTitle("个人便签")]
[assembly: System.Reflection.AssemblyProduct("个人便签")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]

namespace PersonalStickyNote
{
    internal static class Program
    {
        public const string Name = "个人便签";

        [DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();

        [STAThread]
        private static void Main()
        {
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool acquired;
            using (var instance = new Mutex(true, @"Local\PersonalStickyNote_FilnaDo", out acquired))
            {
                if (!acquired)
                {
                    MessageBox.Show("个人便签已在运行，请从任务栏切换。", Name);
                    return;
                }
                try
                {
                    string directory = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "PersonalStickyNote");
                    using (var session = new NoteSession(new NoteStore(directory)))
                        Application.Run(session);
                }
                catch (Exception error)
                {
                    MessageBox.Show("无法打开个人便签。\n\n" + error.Message,
                        Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }

    internal sealed class NoteSession : ApplicationContext
    {
        private readonly NoteStore store;
        private readonly Dictionary<Guid, NoteWindow> windows = new Dictionary<Guid, NoteWindow>();
        private readonly Timer saveTimer = new Timer { Interval = 450 };
        private readonly ListWindow list;
        private bool dirty;
        private bool closing;
        public Notebook Book { get; private set; }
        public bool Dark { get; private set; }

        public NoteSession(NoteStore store)
        {
            this.store = store;
            Book = store.Load();
            Dark = IsDark();
            list = new ListWindow(this);
            saveTimer.Tick += (sender, args) => Save(false);
            SystemEvents.UserPreferenceChanged += WindowsThemeChanged;

            var restore = Book.Notes.Where(note => note.Open).ToArray();
            if (Book.ListVisible || restore.Length == 0 || store.Recovered) ShowList(false);
            foreach (var note in restore) Open(note, false);
            if (store.Recovered)
                MessageBox.Show(list, "已从上次保存的备份恢复便签。", Program.Name);
        }

        public void NewNote()
        {
            var note = new Note();
            Book.Notes.Add(note);
            Open(note);
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
                    window.WindowState = FormWindowState.Normal;
                window.Activate();
            }
        }

        public void ShowList(bool activate = true)
        {
            if (!Book.ListVisible)
            {
                Book.ListVisible = true;
                Changed();
            }
            list.RefreshNotes();
            list.Show();
            if (Book.ListPosition.Width == 0) NoteStyle.Remember(list, Book.ListPosition, this);
            if (list.WindowState == FormWindowState.Minimized)
                list.WindowState = FormWindowState.Normal;
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

        public void CloseList(FormClosingEventArgs args)
        {
            if (closing) return;
            if (args.CloseReason == CloseReason.UserClosing && windows.Count > 0)
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
            if (windows.Count == 0 && !list.Visible) ShowList();
            Changed();
            Save(false);
        }

        public void Delete(Note note, Form owner)
        {
            if (Book.ConfirmDelete && MessageBox.Show(owner, "要永久删除这条便签吗？",
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
            closing = true;
            ExitThread();
            return true;
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
            foreach (var window in windows.Values) window.CaptureContent();
            list.RefreshNotes();
            if (!dirty) return true;
            try
            {
                store.Save(Book);
                dirty = false;
                list.SetSaveError("");
                return true;
            }
            catch (Exception error)
            {
                list.SetSaveError("保存失败，编辑后会重试");
                if (interactive)
                    MessageBox.Show(Form.ActiveForm, "保存失败，请检查本地存储后重试。\n\n" +
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
                list.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
