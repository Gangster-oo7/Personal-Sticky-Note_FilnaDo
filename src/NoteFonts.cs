using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class NoteFonts : IDisposable
    {
        private const uint PrivateFont = 0x10;
        private readonly string directory;
        private readonly HashSet<string> loaded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceEx(string file, uint flags, IntPtr reserved);
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool RemoveFontResourceEx(string file, uint flags, IntPtr reserved);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct LogFont
        {
            public int Height, Width, Escapement, Orientation, Weight;
            public byte Italic, Underline, StrikeOut, Charset, OutPrecision, ClipPrecision, Quality, PitchAndFamily;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string FaceName;
        }

        private delegate int FontCallback(IntPtr logFont, IntPtr textMetric, uint type, IntPtr parameter);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int EnumFontFamiliesEx(IntPtr dc, ref LogFont logFont,
            FontCallback callback, IntPtr parameter, uint flags);

        public NoteFonts(string directory)
        {
            this.directory = directory;
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.EnumerateFiles(directory).Where(file => Supported(Path.GetExtension(file))))
                Load(file);
        }

        public string[] Families()
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var filter = new LogFont { Charset = 1, FaceName = "" }; // DEFAULT_CHARSET
            using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
            {
                var dc = graphics.GetHdc();
                try
                {
                    EnumFontFamiliesEx(dc, ref filter, (font, metric, type, data) =>
                    {
                        var info = (LogFont)Marshal.PtrToStructure(font, typeof(LogFont));
                        if (!string.IsNullOrEmpty(info.FaceName) && !info.FaceName.StartsWith("@"))
                            names.Add(info.FaceName);
                        return 1;
                    }, IntPtr.Zero, 0);
                }
                finally { graphics.ReleaseHdc(dc); }
            }
            return names.OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        public string[] Import(string source)
        {
            string extension = Path.GetExtension(source).ToLowerInvariant();
            if (!Supported(extension)) throw new IOException("请选择 TTF、OTF 或 TTC 字体文件。");
            var existing = Families();
            Directory.CreateDirectory(directory);
            using (var input = File.OpenRead(source))
            using (var hash = SHA256.Create())
            {
                var destination = Path.Combine(directory,
                    BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "") + extension);
                if (!File.Exists(destination))
                {
                    input.Position = 0;
                    string temporary = destination + ".tmp";
                    using (var output = File.Create(temporary)) input.CopyTo(output);
                    File.Move(temporary, destination);
                }
                if (!Load(destination))
                {
                    File.Delete(destination);
                    throw new IOException("Windows 无法读取此字体文件。");
                }
            }
            return Families().Except(existing, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private bool Load(string file)
        {
            return loaded.Contains(file) || (AddFontResourceEx(file, PrivateFont, IntPtr.Zero) > 0 && loaded.Add(file));
        }

        private static bool Supported(string extension)
        {
            switch (extension.ToLowerInvariant())
            {
                case ".ttf": case ".otf": case ".ttc": return true;
                default: return false;
            }
        }

        public void Dispose()
        {
            foreach (var file in loaded) RemoveFontResourceEx(file, PrivateFont, IntPtr.Zero);
        }
    }
}
