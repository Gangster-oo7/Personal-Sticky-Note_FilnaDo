using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Cache;
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
        private const string RawUpdates = "https://raw.githubusercontent.com/" + Repository + "/main/updates/";
        private const string StagePrefix = ".personal-sticky-update-";
        private const int MaximumExe = 8 * 1024 * 1024;
        private const int MaximumManifest = 64 * 1024;
        private static readonly Version Current = typeof(AppUpdates).Assembly.GetName().Version;
        private readonly NoteSession session;
        private bool checking;

        public static string DisplayVersion { get { return Current.ToString(3); } }

        public AppUpdates(NoteSession session)
        {
            this.session = session;
            CleanSuccessfulStages(Application.ExecutablePath);
        }

        public async void Check(ListWindow owner)
        {
            if (checking) { owner.OpenSettings(focusUpdate: true); return; }
            checking = true;
            try
            {
                owner.SetUpdateStatus("正在检查更新。");
                owner.OpenSettings(focusUpdate: true);
                var manifest = await Task.Run(() => LoadManifest());
                if (owner.IsDisposed) return;
                if (manifest.ReleaseVersion <= Current)
                {
                    owner.SetUpdateStatus("当前已是最新版本 {0}。");
                    return;
                }
                owner.SetUpdateStatus(null);
                if (MessageBox.Show(owner, L.T("发现版本 {0}。更新会先保存便笺，再重启程序。现在更新吗？",
                    manifest.ReleaseVersion.ToString(3)), Program.Name,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                    return;

                var binary = await Task.Run(() => Download(manifest));
                if (owner.IsDisposed) return;
                session.InstallUpdate(() => StartHelper(manifest, binary));
            }
            catch (Exception error)
            {
                if (!owner.IsDisposed)
                {
                    owner.SetUpdateStatus("检查更新失败。");
                    MessageBox.Show(owner, L.T("更新未完成。\n\n") + error.Message, Program.Name,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            finally { checking = false; }
        }

        private static Manifest LoadManifest()
        {
            var bytes = Request("version.json?from=" + DisplayVersion, MaximumManifest);
            return ParseManifest(Encoding.UTF8.GetString(bytes));
        }

        internal static Manifest ParseManifest(string json)
        {
            var manifest = new JavaScriptSerializer().Deserialize<Manifest>(json);
            Version version;
            if (manifest == null || !Version.TryParse(manifest.version, out version) ||
                version.Revision < 0 || !IsHex(manifest.sha256, 64) ||
                manifest.size <= 0 || manifest.size > MaximumExe)
                throw new InvalidDataException(L.T("仓库版本清单无效。"));
            manifest.ReleaseVersion = version;
            return manifest;
        }

        private static byte[] Download(Manifest manifest)
        {
            var binary = Request("PersonalStickyNote.exe?v=" + manifest.ReleaseVersion, manifest.size);
            using (var sha = SHA256.Create())
            {
                var actual = BitConverter.ToString(sha.ComputeHash(binary)).Replace("-", "");
                if (binary.Length != manifest.size || !string.Equals(actual, manifest.sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException(L.T("更新文件校验失败。"));
            }
            return binary;
        }

        private static byte[] Request(string file, int maximumBytes)
        {
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            var request = WebRequest.CreateHttp(RawUpdates + file);
            request.AllowAutoRedirect = false;
            request.CachePolicy = new RequestCachePolicy(RequestCacheLevel.Reload);
            request.UserAgent = "PersonalStickyNote/" + DisplayVersion;
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
                        if (output.Length + read > maximumBytes) throw new InvalidDataException(L.T("更新响应超出大小限制。"));
                        output.Write(buffer, 0, read);
                    }
                    return output.ToArray();
                }
            }
            catch (WebException error)
            {
                using (var response = error.Response as HttpWebResponse)
                {
                    if (response != null)
                        throw new IOException(L.T("GitHub 返回 HTTP {0}，请稍后重试。", (int)response.StatusCode), error);
                }
                throw new IOException(L.T("连接 GitHub 失败，请稍后重试。"), error);
            }
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
                    if (process == null) throw new IOException(L.T("无法启动更新程序。"));
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
                throw new InvalidDataException(L.T("更新程序的版本与仓库清单不符。"));
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
                if (newer <= current) throw new IOException(L.T("更新版本没有高于当前版本。"));
                using (var ready = new EventWaitHandle(false, EventResetMode.ManualReset, EventName(id.ToString("N"))))
                {
                    SwapFiles(target, staged, backup);
                    swapped = true;
                    using (var child = Start(target, "--updated " + id.ToString("N")))
                    {
                        if (child == null) throw new IOException(L.T("无法启动新版。"));
                        for (int attempt = 0; attempt < 150; attempt++)
                        {
                            if (ready.WaitOne(100))
                            {
                                canRestore = false;
                                File.WriteAllText(Path.Combine(stage, "complete"), "");
                                return;
                            }
                            if (child.HasExited) throw new IOException(L.T("新版未能启动。"));
                        }
                        canRestore = false;
                        throw new IOException(L.T("新版仍在启动，旧版 EXE 已留在更新目录中。"));
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
                        result += L.T("\n已恢复旧版本。");
                    }
                    catch (Exception rollback) { result += L.T("\n旧版程序保留在 {0}", backup) + "\n" + rollback.Message; }
                }
                MessageBox.Show(L.T("更新未完成。\n\n") + result, Program.Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                if (!previous.WaitForExit(20000)) throw new IOException(L.T("旧版窗口尚未退出，请关闭后重试。"));
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
            public int size { get; set; }
            [ScriptIgnore] public Version ReleaseVersion { get; set; }
        }

    }
}
