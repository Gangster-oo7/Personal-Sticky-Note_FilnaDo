using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class PaletteDialog : Form
    {
        private readonly Notebook book;
        private readonly CheckBox share = new CheckBox { Text = L.T("列表和新便笺使用同一色系"), AutoSize = true };
        private readonly PaletteRow list;
        private readonly PaletteRow note;

        public PaletteDialog(Form owner, Notebook book, bool dark)
        {
            SuspendLayout();
            this.book = book;
            Text = L.T("默认配色");
            Font = owner.Font;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14),
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, RowCount = 4, ColumnCount = 1 };
            for (int row = 0; row < layout.RowCount; row++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            share.Checked = book.SharedPaper;
            list = new PaletteRow(this, L.T("便签列表"), book.ListPaper, book.ListCustomColor, dark);
            note = new PaletteRow(this, L.T("新便笺"), book.NewNotePaper, book.NewNoteCustomColor, dark);
            share.CheckedChanged += (sender, args) => RefreshPreviews();
            list.ColorChanged += RefreshPreviews;
            note.ColorChanged += RefreshPreviews;
            var buttons = new FlowLayoutPanel { Anchor = AnchorStyles.Right, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink, WrapContents = false, Margin = new Padding(0, 10, 0, 0) };
            var save = DialogButton(L.T("保存"), DialogResult.OK);
            var cancel = DialogButton(L.T("取消"), DialogResult.Cancel);
            buttons.Controls.AddRange(new Control[] { save, cancel });
            layout.Controls.Add(share, 0, 0);
            layout.Controls.Add(list, 0, 1);
            layout.Controls.Add(note, 0, 2);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
            RefreshPreviews();
            ResumeLayout(false);
        }

        private static Button DialogButton(string text, DialogResult result)
        {
            return new Button { Text = text, DialogResult = result, AutoSize = true, MinimumSize = new Size(84, 34) };
        }

        private void RefreshPreviews()
        {
            list.ShowPreview(list.SelectedColor);
            note.SetEditable(!share.Checked);
            note.ShowPreview(share.Checked ? list.SelectedColor : note.SelectedColor);
        }

        public void Save()
        {
            book.SharedPaper = share.Checked;
            book.ListPaper = list.Paper;
            book.ListCustomColor = list.Custom;
            book.NewNotePaper = note.Paper;
            book.NewNoteCustomColor = note.Custom;
        }

        private sealed class PaletteRow : FlowLayoutPanel
        {
            private static readonly Paper[] Papers = new[] { NoteStyle.Neutral }.Concat(NoteStyle.Papers).ToArray();
            private readonly ComboBox choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 120 };
            private readonly Label swatch = new Label { MinimumSize = new Size(96, 34), AutoSize = true,
                BorderStyle = BorderStyle.FixedSingle, TextAlign = ContentAlignment.MiddleCenter };
            private readonly Button pick = new Button { Text = L.T("调色盘…"), AutoSize = true, MinimumSize = new Size(85, 34) };
            private readonly bool dark;
            public event Action ColorChanged;
            public string Paper { get; private set; }
            public int Custom { get; private set; }
            public Color SelectedColor { get { return NoteStyle.Background(Paper, Custom, dark); } }

            public PaletteRow(Form owner, string label, string paper, int custom, bool dark)
            {
                this.dark = dark;
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                WrapContents = false;
                Controls.Add(new Label { Text = label, AutoSize = true, MinimumSize = new Size(97, 34),
                    TextAlign = ContentAlignment.MiddleLeft });
                choice.Items.AddRange(Papers.Select(item => item.Label).ToArray());
                choice.AccessibleName = L.T("{0}色系", label);
                choice.SelectedIndex = Math.Max(0, Array.FindIndex(Papers, item => item.Id == paper));
                Paper = Papers[choice.SelectedIndex].Id;
                if (custom != 0) SetCustomColor(custom);
                choice.SelectionChangeCommitted += (sender, args) =>
                {
                    int index = choice.SelectedIndex;
                    if (index < 0 || index >= Papers.Length) return;
                    Paper = Papers[index].Id;
                    Custom = 0;
                    if (choice.Items.Count > Papers.Length) choice.Items.RemoveAt(Papers.Length);
                    ColorChanged();
                };
                pick.Click += (sender, args) =>
                {
                    using (var picker = new ColorDialog { FullOpen = true, Color = SelectedColor })
                        if (picker.ShowDialog(owner) == DialogResult.OK)
                        {
                            SetCustomColor(picker.Color.ToArgb());
                            ColorChanged();
                        }
                };
                swatch.AccessibleName = L.T("{0}颜色预览", label);
                Controls.AddRange(new Control[] { choice, pick, swatch });
                foreach (Control control in Controls) control.Anchor = AnchorStyles.None;
            }

            private void SetCustomColor(int color)
            {
                Custom = color;
                if (choice.Items.Count == Papers.Length) choice.Items.Add(L.T("自定义"));
                choice.SelectedIndex = Papers.Length;
            }

            public void SetEditable(bool editable) { choice.Enabled = pick.Enabled = editable; }

            public void ShowPreview(Color color)
            {
                swatch.BackColor = color;
                swatch.ForeColor = NoteStyle.Contrast(color);
                swatch.Text = string.Format("#{0:X6}", color.ToArgb() & 0xffffff);
            }
        }
    }
}
