using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace MistakeLocker
{
    /// <summary>全局配色 / 字体 / 控件工厂。</summary>
    public static class Ui
    {
        // ---- 浅色主题（题库管理界面）----
        public static readonly Color Bg = ColorTranslator.FromHtml("#F4F6F9");
        public static readonly Color Card = Color.White;
        public static readonly Color BorderSoft = ColorTranslator.FromHtml("#E3E7EE");
        public static readonly Color TextDark = ColorTranslator.FromHtml("#1F2430");
        public static readonly Color TextMuted = ColorTranslator.FromHtml("#6B7280");
        public static readonly Color Accent = ColorTranslator.FromHtml("#2F6FED");
        public static readonly Color AccentDown = ColorTranslator.FromHtml("#1E5FD8");
        public static readonly Color Danger = ColorTranslator.FromHtml("#E5484D");
        public static readonly Color Success = ColorTranslator.FromHtml("#27A96C");
        public static readonly Color Warn = ColorTranslator.FromHtml("#D9822B");

        // ---- 深色主题（锁屏界面）----
        public static readonly Color DarkBg = ColorTranslator.FromHtml("#0E1116");
        public static readonly Color DarkCard = ColorTranslator.FromHtml("#171B22");
        public static readonly Color DarkBorder = ColorTranslator.FromHtml("#262B35");
        public static readonly Color DarkText = ColorTranslator.FromHtml("#E7E9EE");
        public static readonly Color DarkMuted = ColorTranslator.FromHtml("#7C838F");
        public static readonly Color DarkAccent = ColorTranslator.FromHtml("#5B9BFF");

        private static string _family;
        private static bool _probed;

        public static string FontFamilyName
        {
            get
            {
                if (!_probed)
                {
                    _probed = true;
                    _family = PickFamily();
                }
                return _family;
            }
        }

        private static string PickFamily()
        {
            string[] wanted = { "Microsoft YaHei UI", "Microsoft YaHei", "微软雅黑", "Segoe UI" };
            try
            {
                List<string> names = new List<string>();
                using (InstalledFontCollection col = new InstalledFontCollection())
                {
                    foreach (FontFamily f in col.Families) names.Add(f.Name);
                }
                foreach (string w in wanted)
                {
                    if (names.Contains(w)) return w;
                }
            }
            catch { }
            return "Segoe UI";
        }

        public static Font F(float size)
        {
            return new Font(FontFamilyName, size, FontStyle.Regular, GraphicsUnit.Point);
        }

        public static Font F(float size, FontStyle style)
        {
            return new Font(FontFamilyName, size, style, GraphicsUnit.Point);
        }

        // ---- 控件工厂 ----

        public static Button PrimaryButton(string text)
        {
            Button b = BaseButton(text);
            b.BackColor = Accent;
            b.ForeColor = Color.White;
            b.FlatAppearance.MouseOverBackColor = AccentDown;
            b.FlatAppearance.MouseDownBackColor = AccentDown;
            b.Font = F(10f, FontStyle.Bold);
            return b;
        }

        public static Button GhostButton(string text)
        {
            Button b = BaseButton(text);
            b.BackColor = Card;
            b.ForeColor = TextDark;
            b.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#EEF1F6");
            b.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#E3E8F0");
            b.FlatAppearance.BorderColor = BorderSoft;
            b.FlatAppearance.BorderSize = 1;
            return b;
        }

        public static Button DangerButton(string text)
        {
            Button b = BaseButton(text);
            b.BackColor = ColorTranslator.FromHtml("#FDECEC");
            b.ForeColor = Danger;
            b.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#FADADA");
            b.FlatAppearance.MouseDownBackColor = ColorTranslator.FromHtml("#F5C8C8");
            return b;
        }

        private static Button BaseButton(string text)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.Font = F(9.5f);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.AutoSize = false;
            b.Height = 34;
            b.Padding = new Padding(10, 0, 10, 0);
            b.TextAlign = ContentAlignment.MiddleCenter;
            return b;
        }

        /// <summary>深色大按钮（锁屏用）。</summary>
        public static Button DarkButton(string text, Color back, Color fore)
        {
            Button b = new Button();
            b.Text = text;
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = back;
            b.ForeColor = fore;
            b.Font = F(12f, FontStyle.Bold);
            b.Cursor = Cursors.Hand;
            b.UseVisualStyleBackColor = false;
            b.Height = 52;
            b.MinimumSize = new Size(150, 52);
            b.Margin = new Padding(8, 0, 8, 0);
            return b;
        }

        public static Label MakeLabel(string text, float size, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = F(size);
            l.ForeColor = color;
            l.BackColor = Color.Transparent;
            l.AutoSize = true;
            return l;
        }

        public static Panel Card2(Color back, Color border)
        {
            Panel p = new Panel();
            p.BackColor = back;
            p.Padding = new Padding(1);
            p.Paint += delegate(object s, PaintEventArgs e)
            {
                Control c = (Control)s;
                using (Pen pen = new Pen(border))
                    e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
            };
            return p;
        }

        public static void StyleInput(Control c)
        {
            c.Font = F(10f);
            if (c is TextBox)
            {
                TextBox t = (TextBox)c;
                t.BorderStyle = BorderStyle.FixedSingle;
                t.BackColor = Color.White;
                t.ForeColor = TextDark;
            }
        }

        public static void StyleGrid(ListView lv)
        {
            lv.View = View.Details;
            lv.FullRowSelect = true;
            lv.GridLines = false;
            lv.HideSelection = false;
            lv.MultiSelect = false;
            lv.HeaderStyle = ColumnHeaderStyle.Nonclickable;
            lv.BorderStyle = BorderStyle.None;
            lv.BackColor = Color.White;
            lv.ForeColor = TextDark;
            lv.Font = F(10f);
            // 用一张 1x30 的透明图撑高行高
            ImageList il = new ImageList();
            il.ImageSize = new Size(1, 30);
            il.ColorDepth = ColorDepth.Depth32Bit;
            il.Images.Add(new Bitmap(1, 30));
            lv.SmallImageList = il;
        }

        /// <summary>画一枚圆形「错」字图标。</summary>
        public static Icon MakeAppIcon(int size)
        {
            try
            {
                if (size < 8) size = 8;
                Bitmap bmp = new Bitmap(size, size);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
                    g.Clear(Color.Transparent);
                    using (SolidBrush b = new SolidBrush(Accent))
                        g.FillEllipse(b, 0, 0, size - 1, size - 1);
                    using (Font f = new Font(FontFamilyName, size * 0.54f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (SolidBrush b = new SolidBrush(Color.White))
                    using (StringFormat sf = new StringFormat())
                    {
                        sf.Alignment = StringAlignment.Center;
                        sf.LineAlignment = StringAlignment.Center;
                        g.DrawString("错", f, b, new RectangleF(0, 1, size, size), sf);
                    }
                }
                return Icon.FromHandle(bmp.GetHicon());
            }
            catch
            {
                return SystemIcons.Application;
            }
        }
    }
}
