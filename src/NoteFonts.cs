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

    internal sealed class FontPicker : Form
    {
        private static readonly string[] Common =
        {
            "Microsoft YaHei UI", "微软雅黑", "Microsoft YaHei", "宋体", "SimSun",
            "仿宋", "FangSong", "楷体", "KaiTi", "幼圆", "YouYuan", "黑体", "SimHei",
            "等线", "DengXian", "新宋体", "NSimSun", "Segoe UI"
        };

        private readonly NoteFonts fonts;
        private readonly ComboBox families = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 326, Location = new Point(14, 45), AccessibleName = "字体名称" };
        public string Family { get { return families.SelectedItem as string; } }

        public FontPicker(Form owner, NoteFonts fonts, string selected)
        {
            this.fonts = fonts;
            Text = "选择字体";
            Font = owner.Font;
            ClientSize = new Size(354, 132);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            var description = new Label { AutoSize = true, Location = new Point(14, 13),
                Text = "应用到选中文字，或用于接下来的输入。" };
            var import = new Button { Text = "导入字体…", Location = new Point(14, 86), Size = new Size(105, 30) };
            var ok = new Button { Text = "应用", Location = new Point(179, 86), Size = new Size(76, 30),
                DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", Location = new Point(264, 86), Size = new Size(76, 30),
                DialogResult = DialogResult.Cancel };
            import.Click += (sender, args) => ImportFont();
            Controls.AddRange(new Control[] { description, families, import, ok, cancel });
            AcceptButton = ok;
            CancelButton = cancel;
            RefreshFamilies(selected);
        }

        private void RefreshFamilies(string selected)
        {
            var names = fonts.Families();
            var installed = Common.Where(name => names.Contains(name, StringComparer.OrdinalIgnoreCase));
            var ordered = installed.Concat(names).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            families.BeginUpdate();
            families.Items.Clear();
            families.Items.AddRange(ordered);
            families.SelectedIndex = Math.Max(0, Array.FindIndex(ordered,
                name => string.Equals(name, selected, StringComparison.OrdinalIgnoreCase)));
            families.EndUpdate();
        }

        private void ImportFont()
        {
            using (var picker = new OpenFileDialog { Title = "导入字体",
                Filter = "字体文件|*.ttf;*.otf;*.ttc" })
            {
                if (picker.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    var names = fonts.Import(picker.FileName);
                    RefreshFamilies(names.FirstOrDefault() ?? Family);
                    if (names.Length == 0)
                        MessageBox.Show(this, "字体已导入，请按字体名称在列表中选择。", Text);
                }
                catch (Exception error)
                {
                    MessageBox.Show(this, "无法导入字体。\n\n" + error.Message, Text,
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }
    }
}
