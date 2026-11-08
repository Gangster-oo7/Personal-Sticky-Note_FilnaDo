using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class AppUpdates
    {
        private const string Repository = "Gangster-oo7/Personal-Sticky-Note_FilnaDo";
        private const string Api = "https://api.github.com/repos/" + Repository + "/";
        private const string StagePrefix = ".personal-sticky-update-";
        private const int MaximumExe = 8 * 1024 * 1024;
        private const int MaximumJson = 12 * 1024 * 1024;
        private static readonly Version Current = typeof(AppUpdates).Assembly.GetName().Version;
        private readonly NoteSession session;
        private readonly string tokenFile;
        private bool checking;

        public static string DisplayVersion { get { return Current.ToString(3); } }

        public AppUpdates(NoteSession session, string directory)
        {
            this.session = session;
            tokenFile = Path.Combine(directory, "update-access.dat");
            CleanSuccessfulStages(Application.ExecutablePath);
        }

        public async void Check(Form owner)
        {
            if (checking) { session.Notify("正在检查更新。"); return; }
            checking = true;
            try
            {
                string token = ReadToken();
                if (token.Length == 0)
                {
                    session.ShowList();
                    if (!ConfigureAccess(owner)) return;
                    token = ReadToken();
                    if (token.Length == 0) return;
                }
                var manifest = await Task.Run(() => LoadManifest(token));
                if (owner.IsDisposed) return;
                if (manifest.ReleaseVersion <= Current)
                {
                    session.Notify("当前已是最新版本 " + DisplayVersion + "。");
                    return;
                }
                if (MessageBox.Show(owner, "发现版本 " + manifest.ReleaseVersion.ToString(3) +
                    "。更新会先保存便笺，再重启程序。现在更新吗？", Program.Name,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;

                var binary = await Task.Run(() => Download(token, manifest));
                if (owner.IsDisposed) return;
                session.InstallUpdate(() => StartHelper(manifest, binary));
            }
            catch (Exception error)
            {
                if (!owner.IsDisposed)
                    MessageBox.Show(owner, "更新未完成。\n\n" + error.Message, Program.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally { checking = false; }
        }

        public bool ConfigureAccess(Form owner)
        {
            using (var dialog = new Form { Text = "GitHub 更新访问", Font = owner.Font,
                ClientSize = new Size(440, 190), StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog, MaximizeBox = false, MinimizeBox = false,
                ShowInTaskbar = false, AutoScaleDimensions = new SizeF(96, 96), AutoScaleMode = AutoScaleMode.Dpi })
            {
                var help = new Label { Text = "检查私有仓库需要 GitHub 细粒度令牌。\n仅授权 " +
                    "Personal-Sticky-Note_FilnaDo 的 Contents 读取权限。\n凭据加密保存在当前 Windows 账户。",
                    Location = new Point(14, 12), Size = new Size(410, 70) };
                var input = new TextBox { UseSystemPasswordChar = true, Width = 410, MaxLength = 256,
                    Location = new Point(14, 88), AccessibleName = "GitHub 只读令牌" };
                var clear = new Button { Text = "清除凭据", Location = new Point(14, 139), Size = new Size(99, 30),
                    Enabled = File.Exists(tokenFile) };
                var save = new Button { Text = "保存", Location = new Point(262, 139), Size = new Size(76, 30) };
                var cancel = new Button { Text = "取消", Location = new Point(347, 139), Size = new Size(76, 30),
                    DialogResult = DialogResult.Cancel };
                clear.Click += (sender, args) => SaveAccess(dialog, "");
                save.Click += (sender, args) =>
                {
                    string token = input.Text.Trim();
                    if (!token.StartsWith("github_pat_", StringComparison.Ordinal) || !IsToken(token))
                    {
                        MessageBox.Show(dialog, "请输入 GitHub 细粒度令牌（github_pat_ 开头）。", dialog.Text);
                        return;
                    }
                    SaveAccess(dialog, token);
                };
                dialog.Controls.AddRange(new Control[] { help, input, clear, save, cancel });
                dialog.AcceptButton = save;
                dialog.CancelButton = cancel;
                dialog.ActiveControl = input;
                return dialog.ShowDialog(owner) == DialogResult.OK;
            }
        }

        private static bool IsToken(string token)
        {
            foreach (char c in token)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '_')
                    return false;
            return token.Length > "github_pat_".Length;
        }

        private void SaveAccess(Form dialog, string token)
        {
            try
            {
                if (token.Length == 0) File.Delete(tokenFile);
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(tokenFile));
                    byte[] encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), null, DataProtectionScope.CurrentUser);
                    string temporary = tokenFile + ".tmp";
                    File.WriteAllBytes(temporary, encrypted);
                    if (File.Exists(tokenFile)) File.Replace(temporary, tokenFile, null);
                    else File.Move(temporary, tokenFile);
                }
                dialog.DialogResult = DialogResult.OK;
            }
            catch (Exception error) { MessageBox.Show(dialog, "无法保存更新凭据。\n\n" + error.Message, dialog.Text); }
        }

        private string ReadToken()
        {
            if (!File.Exists(tokenFile)) return "";
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(tokenFile), null,
                DataProtectionScope.CurrentUser));
        }

        private static Manifest LoadManifest(string token)
        {
            var entry = Request<GitBlob>("contents/updates/version.json?ref=main", token);
            return ParseManifest(Encoding.UTF8.GetString(DecodeBlob(entry)));
        }

        internal static Manifest ParseManifest(string json)
        {
            var manifest = new JavaScriptSerializer().Deserialize<Manifest>(json);
            Version version;
            if (manifest == null || !Version.TryParse(manifest.version, out version) ||
                version.Revision < 0 || !IsHex(manifest.sha256, 64) || !IsHex(manifest.blob, 40) ||
                manifest.size <= 0 || manifest.size > MaximumExe)
                throw new InvalidDataException("仓库版本清单无效。");
            manifest.ReleaseVersion = version;
            return manifest;
        }

        private static byte[] Download(string token, Manifest manifest)
        {
            var binary = DecodeBlob(Request<GitBlob>("git/blobs/" + manifest.blob, token));
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(binary)).Replace("-", "");
                if (binary.Length != manifest.size || !string.Equals(actual, manifest.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("更新文件校验失败。");
            }
            return binary;
        }

        private static T Request<T>(string relative, string token)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = WebRequest.CreateHttp(Api + relative);
            request.AllowAutoRedirect = false;
            request.Accept = "application/vnd.github+json";
            request.UserAgent = "PersonalStickyNote/" + DisplayVersion;
            request.Headers[HttpRequestHeader.Authorization] = "Bearer " + token;
            request.Timeout = request.ReadWriteTimeout = 15000;
            try
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                using (var input = response.GetResponseStream())
                using (var output = new MemoryStream())
                {
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (output.Length + read > MaximumJson) throw new InvalidDataException("更新响应超出大小限制。");
                        output.Write(buffer, 0, read);
                    }
                    return new JavaScriptSerializer { MaxJsonLength = MaximumJson }
                        .Deserialize<T>(Encoding.UTF8.GetString(output.ToArray()));
                }
            }
            catch (WebException error)
            {
                using (var response = error.Response as HttpWebResponse)
                {
                    if (response != null && (response.StatusCode == HttpStatusCode.Unauthorized ||
                        response.StatusCode == HttpStatusCode.Forbidden || response.StatusCode == HttpStatusCode.NotFound))
                        throw new IOException("无法读取私有仓库中的更新。请检查「GitHub 更新访问」中的令牌权限或有效期。");
                }
                throw new IOException("连接 GitHub 失败，请稍后重试。");
            }
        }

        private static byte[] DecodeBlob(GitBlob blob)
        {
            if (blob == null || blob.encoding != "base64" || string.IsNullOrEmpty(blob.content))
                throw new InvalidDataException("GitHub 未返回完整的更新内容。");
            return Convert.FromBase64String(blob.content);
        }

        private static bool IsHex(string text, int length)
        {
            if (text == null || text.Length != length) return false;
            foreach (char c in text) if (!Uri.IsHexDigit(c)) return false;
            return true;
        }

        private static void StartHelper(Manifest manifest, byte[] binary)
        {
            string target = Application.ExecutablePath;
            string id = Guid.NewGuid().ToString("N");
            string stage = Path.Combine(Path.GetDirectoryName(target), StagePrefix + id);
            Directory.CreateDirectory(stage);
            string staged = Path.Combine(stage, "new.exe");
            string helper = Path.Combine(stage, "helper.exe");
            try
            {
                File.WriteAllBytes(staged, binary);
                VerifyExecutable(staged, manifest.ReleaseVersion);
                File.Copy(target, helper);
                using (var process = Start(helper, "--apply-update " + Process.GetCurrentProcess().Id + " \"" +
                    Path.GetFileName(target) + "\""))
                {
                    if (process == null) throw new IOException("无法启动更新程序。");
                }
            }
            catch
            {
                RemoveStage(stage);
                throw;
            }
        }

        internal static void VerifyExecutable(string path, Version expected)
        {
            var assembly = AssemblyName.GetAssemblyName(path);
            if (assembly.Name != "PersonalStickyNote" || assembly.Version != expected)
                throw new InvalidDataException("更新程序的版本与仓库清单不符。");
        }

        internal static void SwapFiles(string target, string staged, string backup)
        {
            File.Replace(staged, target, backup);
        }

        public static void Apply(string previousProcess, string targetFile)
        {
            int previousId;
            string stage = Path.GetDirectoryName(Application.ExecutablePath);
            string name = Path.GetFileName(stage);
            Guid id;
            if (!int.TryParse(previousProcess, out previousId) || previousId <= 0 ||
                !name.StartsWith(StagePrefix, StringComparison.Ordinal) ||
                !Guid.TryParseExact(name.Substring(StagePrefix.Length), "N", out id) ||
                targetFile != Path.GetFileName(targetFile) ||
                !string.Equals(Path.GetExtension(targetFile), ".exe", StringComparison.OrdinalIgnoreCase)) return;

            string target = Path.Combine(Path.GetDirectoryName(stage), targetFile);
            string staged = Path.Combine(stage, "new.exe");
            string backup = Path.Combine(stage, "previous.exe");
            bool swapped = false;
            bool canRestore = true;
            try
            {
                WaitForPrevious(previousId);
                var current = AssemblyName.GetAssemblyName(target).Version;
                var newer = AssemblyName.GetAssemblyName(staged).Version;
                VerifyExecutable(staged, newer);
                if (newer <= current) throw new IOException("更新版本没有高于当前版本。");
                using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(id.ToString("N"))))
                {
                    SwapFiles(target, staged, backup);
                    swapped = true;
                    using (var child = Start(target, "--updated " + id.ToString("N")))
                    {
                        if (child == null) throw new IOException("无法启动新版。");
                        for (int attempt = 0; attempt < 150; attempt++)
                        {
                            if (ready.WaitOne(100))
                            {
                                canRestore = false;
                                File.WriteAllText(Path.Combine(stage, "complete"), "");
                                return;
                            }
                            if (child.HasExited) throw new IOException("新版未能启动。");
                        }
                        canRestore = false;
                        throw new IOException("新版仍在启动，旧版 EXE 已留在更新目录中。");
                    }
                }
            }
            catch (Exception error)
            {
                string result = error.Message;
                if (swapped && canRestore)
                {
                    try
                    {
                        SwapFiles(target, backup, null);
                        result += "\n已恢复旧版本。";
                    }
                    catch (Exception rollback) { result += "\n旧版程序保留在 " + backup + "\n" + rollback.Message; }
                }
                MessageBox.Show("更新未完成。\n\n" + result, Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                if (canRestore && File.Exists(target))
                {
                    try { using (var old = Start(target, DesktopIntegration.StartupArgument)) { } }
                    catch (System.ComponentModel.Win32Exception) { }
                }
            }
        }

        private static void WaitForPrevious(int processId)
        {
            Process previous;
            try { previous = Process.GetProcessById(processId); }
            catch (ArgumentException) { return; }
            using (previous)
                if (!previous.WaitForExit(20000)) throw new IOException("旧版窗口尚未退出，请关闭后重试。");
        }

        private static Process Start(string file, string arguments)
        {
            return Process.Start(new ProcessStartInfo(file, arguments)
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Path.GetDirectoryName(file)
            });
        }

        private static string EventName(string id) { return @"Local\PersonalStickyNote_Update_" + id; }

        public static void ConfirmLaunch(string id)
        {
            EventWaitHandle ready;
            if (EventWaitHandle.TryOpenExisting(EventName(id), out ready))
            {
                using (ready) ready.Set();
            }
        }

        private static void CleanSuccessfulStages(string executable)
        {
            try
            {
                foreach (string stage in Directory.EnumerateDirectories(Path.GetDirectoryName(executable), StagePrefix + "*"))
                {
                    Guid id;
                    if (Guid.TryParseExact(Path.GetFileName(stage).Substring(StagePrefix.Length), "N", out id) &&
                        File.Exists(Path.Combine(stage, "complete")) &&
                        (File.GetAttributes(stage) & FileAttributes.ReparsePoint) == 0)
                        RemoveStage(stage);
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        private static void RemoveStage(string stage)
        {
            foreach (var file in new[] { "helper.exe", "new.exe", "previous.exe", "complete" })
            {
                try { File.Delete(Path.Combine(stage, file)); }
                catch (IOException) { return; }
                catch (UnauthorizedAccessException) { return; }
            }
            try { Directory.Delete(stage); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        public sealed class Manifest
        {
            public string version { get; set; }
            public string sha256 { get; set; }
            public string blob { get; set; }
            public int size { get; set; }
            [ScriptIgnore] public Version ReleaseVersion { get; set; }
        }

        public sealed class GitBlob
        {
            public string encoding { get; set; }
            public string content { get; set; }
        }
    }
}
