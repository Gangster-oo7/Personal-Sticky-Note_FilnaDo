using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PersonalStickyNote
{
    internal sealed class PaletteDialog : Form
    {
        private readonly Notebook book;
        private readonly CheckBox share = new CheckBox { Text = "列表和新便笺使用同一色系", AutoSize = true };
        private readonly PaletteRow list;
        private readonly PaletteRow note;

        public PaletteDialog(Form owner, Notebook book)
        {
            this.book = book;
            Text = "默认配色";
            Font = owner.Font;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = ShowInTaskbar = false;
            ClientSize = new Size(430, 200);
            AutoScaleDimensions = new SizeF(96, 96);
            AutoScaleMode = AutoScaleMode.Dpi;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14),
                RowCount = 4, ColumnCount = 1 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 33));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 43));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            share.Checked = book.SharedPaper;
            list = new PaletteRow(this, "便签列表", book.ListPaper, book.ListCustomColor);
            note = new PaletteRow(this, "新便笺", book.NewNotePaper, book.NewNoteCustomColor);
            share.CheckedChanged += (sender, args) => note.Enabled = !share.Checked;
            note.Enabled = !share.Checked;
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
            var save = new Button { Text = "保存", DialogResult = DialogResult.OK };
            var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel };
            buttons.Controls.AddRange(new Control[] { cancel, save });
            layout.Controls.Add(share, 0, 0);
            layout.Controls.Add(list, 0, 1);
            layout.Controls.Add(note, 0, 2);
            layout.Controls.Add(buttons, 0, 3);
            Controls.Add(layout);
            AcceptButton = save;
            CancelButton = cancel;
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
            private readonly ComboBox choice = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 101 };
            private readonly Panel swatch = new Panel { Size = new Size(26, 26), BorderStyle = BorderStyle.FixedSingle };
            public string Paper { get { return Papers[Math.Max(0, choice.SelectedIndex)].Id; } }
            public int Custom { get; private set; }

            public PaletteRow(Form owner, string label, string paper, int custom)
            {
                Dock = DockStyle.Fill;
                WrapContents = false;
                Controls.Add(new Label { Text = label, Width = 81, Height = 28, TextAlign = ContentAlignment.MiddleLeft });
                choice.Items.AddRange(Papers.Select(item => item.Label).ToArray());
                choice.AccessibleName = label + "色系";
                choice.SelectedIndex = Math.Max(0, Array.FindIndex(Papers, item => item.Id == paper));
                Custom = custom;
                choice.SelectionChangeCommitted += (sender, args) => { Custom = 0; RefreshSwatch(); };
                Controls.Add(choice);
                var pick = new Button { Text = "调色盘…", Width = 86, Height = 28 };
                pick.Click += (sender, args) =>
                {
                    using (var picker = new ColorDialog { FullOpen = true, Color = swatch.BackColor })
                        if (picker.ShowDialog(owner) == DialogResult.OK)
                        {
                            Custom = picker.Color.ToArgb();
                            RefreshSwatch();
                        }
                };
                var reset = new Button { Text = "预设", Width = 50, Height = 28 };
                reset.Click += (sender, args) => { Custom = 0; RefreshSwatch(); };
                Controls.AddRange(new Control[] { pick, swatch, reset });
                RefreshSwatch();
            }

            private void RefreshSwatch() { swatch.BackColor = NoteStyle.Background(Paper, Custom, false); }
        }
    }
}
