using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MistakeLocker
{
    public class MainForm : Form
    {
        public NotifyIcon Tray;
        private ContextMenuStrip _trayMenu;

        private Label _lblCountdown;
        private TabControl _tabs;

        // 题库页
        private ListBox _lbSubject;
        private ListView _lvQuestions;
        private Label _lblSummary;
        private Panel _emptyHint;

        // 设置页
        private NumericUpDown _numInterval;
        private NumericUpDown _numPerLock;
        private NumericUpDown _numStay;
        private CheckBox _chkBlockHotkeys;
        private CheckBox _chkHideTaskbar;
        private CheckBox _chkSound;
        private CheckBox _chkAutoStart;
        private CheckBox _chkLockOnStartup;
        private ComboBox _cboSubjectFilter;
        private Label _lblDataPath;
        private Label _lblStats;

        private Timer _tick;
        private Random _rnd = new Random();
        private LockForm _lockForm;
        private bool _exiting;
        private bool _loading;
        private bool _warned5Min;

        public MainForm()
        {
            BuildUi();
            BuildTray();
            RefreshSubjects();
            RefreshList();

            _tick = new Timer();
            _tick.Interval = 1000;
            _tick.Tick += delegate { Heartbeat(); };
            _tick.Start();

            this.Shown += delegate
            {
                Heartbeat();
            };
        }

        // ============================================================ UI

        private void BuildUi()
        {
            string subj = Store.Data.Settings.SubjectFilter;
            string title = "错题锁屏";
            if (!string.IsNullOrEmpty(subj)) title += " · " + subj;
            this.Text = title;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.ClientSize = new Size(1060, 720);
            this.MinimumSize = new Size(920, 620);
            this.BackColor = Ui.Bg;
            this.Font = Ui.F(10f);
            this.Icon = Ui.MakeAppIcon(32);

            // ---------- 顶部栏 ----------
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 64;
            top.BackColor = Color.White;
            top.Padding = new Padding(24, 0, 24, 0);
            top.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Ui.BorderSoft))
                    e.Graphics.DrawLine(p, 0, ((Control)s).Height - 1, ((Control)s).Width, ((Control)s).Height - 1);
            };

            Label lblTitle = new Label();
            lblTitle.Text = "错题锁屏";
            lblTitle.Font = Ui.F(14f, FontStyle.Bold);
            lblTitle.ForeColor = Ui.TextDark;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(24, 18);
            top.Controls.Add(lblTitle);

            Label lblSub = new Label();
            lblSub.Text = "定时锁屏 · 只让你看见错题";
            lblSub.Font = Ui.F(9.5f);
            lblSub.ForeColor = Ui.TextMuted;
            lblSub.AutoSize = true;
            lblSub.Location = new Point(112, 24);
            top.Controls.Add(lblSub);

            _lblCountdown = new Label();
            _lblCountdown.Text = "";
            _lblCountdown.Font = Ui.F(10f, FontStyle.Bold);
            _lblCountdown.ForeColor = Ui.Accent;
            _lblCountdown.AutoSize = false;
            _lblCountdown.TextAlign = ContentAlignment.MiddleRight;
            _lblCountdown.Width = 320;
            _lblCountdown.Height = 30;
            _lblCountdown.Top = 17;
            _lblCountdown.Left = 300;
            top.Controls.Add(_lblCountdown);

            top.Resize += delegate { _lblCountdown.Left = top.Width - 24 - _lblCountdown.Width; };

            // ---------- 主体 ----------
            _tabs = new TabControl();
            _tabs.Dock = DockStyle.Fill;
            _tabs.Font = Ui.F(10f);
            _tabs.Padding = new Point(18, 6);

            TabPage pageList = new TabPage("题库");
            pageList.BackColor = Ui.Bg;
            pageList.Padding = new Padding(0);
            BuildListPage(pageList);

            TabPage pageSet = new TabPage("设置");
            pageSet.BackColor = Ui.Bg;
            pageSet.Padding = new Padding(0);
            BuildSettingsPage(pageSet);

            _tabs.TabPages.Add(pageList);
            _tabs.TabPages.Add(pageSet);

            this.Controls.Add(_tabs);
            this.Controls.Add(top);
        }

        private void BuildListPage(TabPage page)
        {
            // 左：科目
            Panel left = new Panel();
            left.Dock = DockStyle.Left;
            left.Width = 190;
            left.BackColor = Color.White;
            left.Padding = new Padding(14, 14, 14, 14);

            Label lblSubjTitle = new Label();
            lblSubjTitle.Text = "科目";
            lblSubjTitle.Font = Ui.F(9.5f, FontStyle.Bold);
            lblSubjTitle.ForeColor = Ui.TextMuted;
            lblSubjTitle.Dock = DockStyle.Top;
            lblSubjTitle.Height = 34;
            lblSubjTitle.TextAlign = ContentAlignment.MiddleLeft;
            lblSubjTitle.Padding = new Padding(2, 0, 0, 0);

            _lbSubject = new ListBox();
            _lbSubject.Dock = DockStyle.Fill;
            _lbSubject.BorderStyle = BorderStyle.None;
            _lbSubject.Font = Ui.F(10.5f);
            _lbSubject.BackColor = Color.White;
            _lbSubject.ForeColor = Ui.TextDark;
            _lbSubject.SelectedIndexChanged += delegate { RefreshList(); };

            left.Controls.Add(_lbSubject);
            left.Controls.Add(lblSubjTitle);

            // 右：工具栏 + 列表
            Panel right = new Panel();
            right.Dock = DockStyle.Fill;
            right.Padding = new Padding(16, 0, 0, 0);
            right.BackColor = Ui.Bg;

            FlowLayoutPanel bar = new FlowLayoutPanel();
            bar.Dock = DockStyle.Top;
            bar.Height = 58;
            bar.FlowDirection = FlowDirection.LeftToRight;
            bar.WrapContents = false;
            bar.Padding = new Padding(0, 12, 0, 12);
            bar.BackColor = Ui.Bg;

            Button btnAdd = Ui.PrimaryButton("＋ 新增题目");
            btnAdd.Width = 120;
            btnAdd.Height = 36;
            btnAdd.Margin = new Padding(0, 0, 8, 0);
            btnAdd.Click += delegate { AddQuestion(); };

            Button btnEdit = Ui.GhostButton("编辑");
            btnEdit.Width = 76;
            btnEdit.Height = 36;
            btnEdit.Margin = new Padding(0, 0, 8, 0);
            btnEdit.Click += delegate { EditSelected(); };

            Button btnDel = Ui.DangerButton("删除");
            btnDel.Width = 76;
            btnDel.Height = 36;
            btnDel.Margin = new Padding(0, 0, 8, 0);
            btnDel.Click += delegate { DeleteSelected(); };

            Button btnImport = Ui.GhostButton("导入题库");
            btnImport.Width = 104;
            btnImport.Height = 36;
            btnImport.Margin = new Padding(0, 0, 8, 0);
            btnImport.Click += delegate { ImportFile(); };

            Button btnExport = Ui.GhostButton("导出");
            btnExport.Width = 76;
            btnExport.Height = 36;
            btnExport.Margin = new Padding(0, 0, 8, 0);
            btnExport.Click += delegate { ExportFile(); };

            Button btnLockNow = Ui.GhostButton("立刻复习");
            btnLockNow.Width = 100;
            btnLockNow.Height = 36;
            btnLockNow.Margin = new Padding(0, 0, 0, 0);
            btnLockNow.Click += delegate { DoLock(true); };

            bar.Controls.Add(btnAdd);
            bar.Controls.Add(btnEdit);
            bar.Controls.Add(btnDel);
            bar.Controls.Add(btnImport);
            bar.Controls.Add(btnExport);
            bar.Controls.Add(btnLockNow);

            Panel lvWrap = new Panel();
            lvWrap.Dock = DockStyle.Fill;
            lvWrap.BackColor = Color.White;
            lvWrap.Padding = new Padding(1);

            _lvQuestions = new ListView();
            _lvQuestions.Dock = DockStyle.Fill;
            Ui.StyleGrid(_lvQuestions);
            _lvQuestions.Columns.Add("科目", 92);
            _lvQuestions.Columns.Add("题目", 430);
            _lvQuestions.Columns.Add("出现", 64);
            _lvQuestions.Columns.Add("答错", 64);
            _lvQuestions.Columns.Add("状态", 84);
            _lvQuestions.DoubleClick += delegate { EditSelected(); };
            _lvQuestions.Resize += delegate { FitColumns(); };

            // 空列表时的引导
            _emptyHint = new Panel();
            _emptyHint.Dock = DockStyle.Fill;
            _emptyHint.BackColor = Color.White;
            Label eh = new Label();
            eh.Dock = DockStyle.Fill;
            eh.TextAlign = ContentAlignment.MiddleCenter;
            eh.Font = Ui.F(10.5f);
            eh.ForeColor = Ui.TextMuted;
            eh.Text = "还没有题目\r\n\r\n点左上角「＋ 新增题目」，或者用「导入题库」批量导入\r\n\r\n加好之后，程序会按设定的时间自动锁屏，把这些题推到你眼前";
            _emptyHint.Controls.Add(eh);

            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("编辑", null, delegate { EditSelected(); });
            menu.Items.Add("标记为已掌握", null, delegate { ToggleMastered(); });
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("删除", null, delegate { DeleteSelected(); });
            _lvQuestions.ContextMenuStrip = menu;

            lvWrap.Controls.Add(_lvQuestions);
            lvWrap.Controls.Add(_emptyHint);

            _lblSummary = new Label();
            _lblSummary.Dock = DockStyle.Bottom;
            _lblSummary.Height = 30;
            _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            _lblSummary.Font = Ui.F(9.5f);
            _lblSummary.ForeColor = Ui.TextMuted;
            _lblSummary.BackColor = Ui.Bg;
            _lblSummary.Padding = new Padding(2, 0, 0, 0);

            right.Controls.Add(lvWrap);
            right.Controls.Add(_lblSummary);
            right.Controls.Add(bar);

            page.Controls.Add(right);
            page.Controls.Add(left);
        }

        private void BuildSettingsPage(TabPage page)
        {
            _setRow = 0;
            _loading = true;   // 初始化期间不要触发「写回存储」

            Panel scroll = new Panel();
            scroll.Dock = DockStyle.Fill;
            scroll.AutoScroll = true;
            scroll.BackColor = Ui.Bg;
            scroll.Padding = new Padding(24, 16, 24, 24);

            TableLayoutPanel t = new TableLayoutPanel();
            t.Dock = DockStyle.Top;
            t.AutoSize = true;
            t.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            t.ColumnCount = 2;
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250f));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            t.BackColor = Ui.Bg;

            AddSection(t, "锁定节奏");

            _numInterval = new NumericUpDown();
            _numInterval.Minimum = 1;
            _numInterval.Maximum = 600;
            _numInterval.Value = Store.Data.Settings.IntervalMinutes;
            _numInterval.Width = 100;
            _numInterval.Font = Ui.F(10f);
            _numInterval.ValueChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.IntervalMinutes = (int)_numInterval.Value;
                Store.Data.Settings.LastLockUtc = DateTime.UtcNow;
                _warned5Min = false;
                Store.Save();
                Heartbeat();
            };
            AddRow(t, "每隔多少分钟锁定一次", Compact(_numInterval, " 分钟"));

            _numPerLock = new NumericUpDown();
            _numPerLock.Minimum = 1;
            _numPerLock.Maximum = 20;
            _numPerLock.Value = Store.Data.Settings.QuestionsPerLock;
            _numPerLock.Width = 100;
            _numPerLock.Font = Ui.F(10f);
            _numPerLock.ValueChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.QuestionsPerLock = (int)_numPerLock.Value;
                Store.Save();
            };
            AddRow(t, "每次要过几道题才解锁", Compact(_numPerLock, " 道"));

            _numStay = new NumericUpDown();
            _numStay.Minimum = 0;
            _numStay.Maximum = 600;
            _numStay.Value = Store.Data.Settings.MinStaySeconds;
            _numStay.Width = 100;
            _numStay.Font = Ui.F(10f);
            _numStay.ValueChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.MinStaySeconds = (int)_numStay.Value;
                Store.Save();
            };
            AddRow(t, "做完后强制停留（0=不限制）", Compact(_numStay, " 秒"));

            AddSection(t, "强制手段");

            _chkBlockHotkeys = new CheckBox();
            _chkBlockHotkeys.Text = "锁屏时屏蔽 Alt+Tab、Win 键、Alt+F4（强烈建议开启）";
            _chkBlockHotkeys.AutoSize = true;
            _chkBlockHotkeys.Font = Ui.F(10f);
            _chkBlockHotkeys.Checked = Store.Data.Settings.BlockHotkeys;
            _chkBlockHotkeys.CheckedChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.BlockHotkeys = _chkBlockHotkeys.Checked;
                Store.Save();
            };
            AddRow(t, "屏蔽系统切换热键", _chkBlockHotkeys);

            _chkHideTaskbar = new CheckBox();
            _chkHideTaskbar.Text = "锁屏时隐藏任务栏";
            _chkHideTaskbar.AutoSize = true;
            _chkHideTaskbar.Font = Ui.F(10f);
            _chkHideTaskbar.Checked = Store.Data.Settings.HideTaskbar;
            _chkHideTaskbar.CheckedChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.HideTaskbar = _chkHideTaskbar.Checked;
                Store.Save();
            };
            AddRow(t, "隐藏任务栏", _chkHideTaskbar);

            _chkSound = new CheckBox();
            _chkSound.Text = "锁定时播放提示音";
            _chkSound.AutoSize = true;
            _chkSound.Font = Ui.F(10f);
            _chkSound.Checked = Store.Data.Settings.SoundOnLock;
            _chkSound.CheckedChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.SoundOnLock = _chkSound.Checked;
                Store.Save();
            };
            AddRow(t, "提示音", _chkSound);

            AddSection(t, "复习范围");

            _cboSubjectFilter = new ComboBox();
            _cboSubjectFilter.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboSubjectFilter.Width = 220;
            _cboSubjectFilter.Font = Ui.F(10f);
            _cboSubjectFilter.SelectedIndexChanged += delegate
            {
                if (_loading) return;
                string v = _cboSubjectFilter.SelectedIndex <= 0 ? "" : (_cboSubjectFilter.SelectedItem as string);
                Store.Data.Settings.SubjectFilter = v ?? "";
                Store.Save();
                RefreshCountdownText();
            };
            AddRow(t, "只考某一门（空=全部）", _cboSubjectFilter);

            AddSection(t, "启动");

            _chkAutoStart = new CheckBox();
            _chkAutoStart.Text = "开机自动启动（后台常驻）";
            _chkAutoStart.AutoSize = true;
            _chkAutoStart.Font = Ui.F(10f);
            _chkAutoStart.Checked = Store.Data.Settings.AutoStart;
            _chkAutoStart.CheckedChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.AutoStart = _chkAutoStart.Checked;
                Store.Save();
                ApplyAutoStart(_chkAutoStart.Checked);
            };
            AddRow(t, "开机自启", _chkAutoStart);

            _chkLockOnStartup = new CheckBox();
            _chkLockOnStartup.Text = "程序启动后先复习一轮";
            _chkLockOnStartup.AutoSize = true;
            _chkLockOnStartup.Font = Ui.F(10f);
            _chkLockOnStartup.Checked = Store.Data.Settings.LockOnStartup;
            _chkLockOnStartup.CheckedChanged += delegate
            {
                if (_loading) return;
                Store.Data.Settings.LockOnStartup = _chkLockOnStartup.Checked;
                Store.Save();
            };
            AddRow(t, "启动即锁定", _chkLockOnStartup);

            AddSection(t, "数据");

            _lblStats = new Label();
            _lblStats.AutoSize = true;
            _lblStats.Font = Ui.F(10f);
            _lblStats.ForeColor = Ui.TextMuted;
            AddRow(t, "累计答题 / 答错", _lblStats);

            _lblDataPath = new Label();
            _lblDataPath.AutoSize = false;
            _lblDataPath.Width = 520;
            _lblDataPath.Height = 40;
            _lblDataPath.Font = Ui.F(9f);
            _lblDataPath.ForeColor = Ui.TextMuted;
            _lblDataPath.Text = Store.DataPath;
            AddRow(t, "题库文件位置", _lblDataPath);

            FlowLayoutPanel dataBar = new FlowLayoutPanel();
            dataBar.AutoSize = true;
            dataBar.FlowDirection = FlowDirection.LeftToRight;
            dataBar.WrapContents = false;

            Button btnOpenFolder = Ui.GhostButton("打开数据文件夹");
            btnOpenFolder.Width = 130;
            btnOpenFolder.Click += delegate
            {
                try { System.Diagnostics.Process.Start("explorer.exe", Store.DirPath); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message); }
            };
            dataBar.Controls.Add(btnOpenFolder);

            Button btnClear = Ui.DangerButton("清空题库");
            btnClear.Width = 96;
            btnClear.Margin = new Padding(8, 0, 0, 0);
            btnClear.Click += delegate
            {
                int n = Store.Data.Questions.Count;
                if (n == 0)
                {
                    MessageBox.Show(this, "题库本来就是空的。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (MessageBox.Show(this, "确定要清空全部 " + n + " 道题吗？\r\n\r\n建议先点「导出」备份一份。",
                        "清空题库", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                Store.Data.Questions.Clear();
                Store.Save();
                RefreshSubjects();
                RefreshSubjectFilter();
                RefreshList();
            };
            dataBar.Controls.Add(btnClear);

            Button btnResetTimer = Ui.GhostButton("重新计时");
            btnResetTimer.Width = 96;
            btnResetTimer.Margin = new Padding(8, 0, 0, 0);
            btnResetTimer.Click += delegate
            {
                Store.Data.Settings.LastLockUtc = DateTime.UtcNow;
                _warned5Min = false;
                Store.Save();
                Heartbeat();
                MessageBox.Show(this, "计时已重新开始。", "好", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            dataBar.Controls.Add(btnResetTimer);

            Button btnLockNow2 = Ui.PrimaryButton("立即复习一轮");
            btnLockNow2.Width = 130;
            btnLockNow2.Margin = new Padding(8, 0, 0, 0);
            btnLockNow2.Click += delegate { DoLock(true); };
            dataBar.Controls.Add(btnLockNow2);

            AddRow(t, "", dataBar);

            scroll.Controls.Add(t);
            page.Controls.Add(scroll);

            RefreshSubjectFilter();
            RefreshSettingsLabels();
            _loading = false;
        }

        private int _setRow;

        private void AddSection(TableLayoutPanel t, string title)
        {
            Label l = new Label();
            l.Text = title;
            l.Font = Ui.F(11f, FontStyle.Bold);
            l.ForeColor = Ui.TextDark;
            l.AutoSize = true;
            l.Margin = new Padding(0, 18, 0, 8);
            t.Controls.Add(l, 0, _setRow);
            t.SetColumnSpan(l, 2);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _setRow++;
        }

        private void AddRow(TableLayoutPanel t, string label, Control control)
        {
            Label l = new Label();
            l.Text = label;
            l.Font = Ui.F(9.8f);
            l.ForeColor = Ui.TextMuted;
            l.AutoSize = false;
            l.Width = 240;
            l.Height = 32;
            l.TextAlign = ContentAlignment.MiddleLeft;
            l.Margin = new Padding(0, 4, 0, 4);

            control.Margin = new Padding(0, 4, 0, 4);

            t.Controls.Add(l, 0, _setRow);
            t.Controls.Add(control, 1, _setRow);
            t.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            _setRow++;
        }

        private Control Compact(Control c, string suffix)
        {
            FlowLayoutPanel p = new FlowLayoutPanel();
            p.AutoSize = true;
            p.FlowDirection = FlowDirection.LeftToRight;
            p.WrapContents = false;
            c.Margin = new Padding(0, 2, 6, 0);
            p.Controls.Add(c);
            Label l = Ui.MakeLabel(suffix, 9.5f, Ui.TextMuted);
            l.Margin = new Padding(0, 8, 0, 0);
            p.Controls.Add(l);
            return p;
        }

        // ============================================================ 托盘

        private void BuildTray()
        {
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Font = Ui.F(10f);
            _trayMenu.Items.Add("打开错题本", null, delegate { ShowMain(); });
            _trayMenu.Items.Add("立刻复习一轮", null, delegate { DoLock(true); });
            _trayMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem pause = new ToolStripMenuItem("暂停定时锁定");
            pause.Click += delegate { TogglePause(); };
            _trayMenu.Items.Add(pause);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add("退出", null, delegate { ExitApp(); });

            Tray = new NotifyIcon();
            Tray.Icon = Ui.MakeAppIcon(16);
            Tray.Text = "错题锁屏";
            Tray.Visible = true;
            Tray.ContextMenuStrip = _trayMenu;
            Tray.DoubleClick += delegate { ShowMain(); };
        }

        private void TogglePause()
        {
            Store.Data.Settings.TimerEnabled = !Store.Data.Settings.TimerEnabled;
            if (Store.Data.Settings.TimerEnabled)
                Store.Data.Settings.LastLockUtc = DateTime.UtcNow;
            Store.Save();
            _warned5Min = false;
            Heartbeat();
            Tray.ShowBalloonTip(1800, "错题锁屏",
                Store.Data.Settings.TimerEnabled ? "定时锁定已恢复" : "定时锁定已暂停",
                ToolTipIcon.Info);
        }

        private void ShowMain()
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            this.Show();
            this.Activate();
        }

        /// <summary>用户再次双击 exe 时，把已经运行的窗口拉到前台。</summary>
        public void ShowFromOtherInstance()
        {
            ShowMain();
            try { if (_tabs != null) _tabs.SelectedIndex = 0; } catch { }
            this.BringToFront();
        }

        /// <summary>启动时（--lock 或设置里勾了「启动即锁定」）锁一轮。</summary>
        public void LockNowFromStartup()
        {
            DoLock(true);
        }

        // ============================================================ 心跳

        private void Heartbeat()
        {
            RefreshCountdownText();

            Settings s = Store.Data.Settings;
            if (_lockForm != null && !_lockForm.IsDisposed) return;
            if (!s.TimerEnabled) return;
            if (_exiting) return;

            DateTime now = DateTime.UtcNow;
            DateTime next = s.LastLockUtc.AddMinutes(s.IntervalMinutes);

            double leftSec = (next - now).TotalSeconds;
            if (leftSec <= 300 && leftSec > 0 && !_warned5Min)
            {
                _warned5Min = true;
                Tray.ShowBalloonTip(2600, "准备复习",
                    "还有 5 分钟就要锁屏了，先把手上这段做完。", ToolTipIcon.Info);
            }

            if (now >= next && Store.Data.Questions.Count > 0)
            {
                DoLock(false);
            }
        }

        private void RefreshCountdownText()
        {
            if (_lblCountdown == null) return;
            Settings s = Store.Data.Settings;

            if (!s.TimerEnabled)
            {
                _lblCountdown.Text = "定时锁定已暂停";
                _lblCountdown.ForeColor = Ui.TextMuted;
                return;
            }
            if (Store.Data.Questions.Count == 0)
            {
                _lblCountdown.Text = "题库是空的，先加几道题吧";
                _lblCountdown.ForeColor = Ui.Warn;
                return;
            }

            DateTime next = s.LastLockUtc.AddMinutes(s.IntervalMinutes);
            TimeSpan left = next - DateTime.UtcNow;
            if (left.TotalSeconds < 0) left = TimeSpan.Zero;

            string txt;
            if (left.TotalHours >= 1)
                txt = string.Format("距离下次锁定 {0} 小时 {1} 分", (int)left.TotalHours, left.Minutes);
            else
                txt = string.Format("距离下次锁定 {0}:{1:00}", left.Minutes, left.Seconds);

            _lblCountdown.Text = txt;
            _lblCountdown.ForeColor = left.TotalSeconds <= 60 ? Ui.Danger : Ui.Accent;
        }

        // ============================================================ 锁屏

        private void DoLock(bool manual)
        {
            if (_lockForm != null && !_lockForm.IsDisposed) { _lockForm.Activate(); return; }

            int n = Store.Data.Settings.QuestionsPerLock;
            if (n < 1) n = 1;
            string subject = Store.Data.Settings.SubjectFilter;

            List<Question> qs = Store.Pick(n, subject, _rnd);
            if (qs.Count == 0) qs = Store.Pick(n, "", _rnd);

            _wasVisible = this.Visible;
            this.Hide();

            _lockForm = new LockForm(qs, Store.Data.Settings);
            _lockForm.LockCompleted += delegate { OnLockCompleted(); };
            _lockForm.FormClosed += delegate
            {
                _lockForm = null;
                if (!_exiting)
                {
                    Store.Data.Settings.LastLockUtc = DateTime.UtcNow;
                    _warned5Min = false;
                    Store.Save();
                    if (_wasVisible) ShowMain();
                    RefreshList();
                }
            };
            _lockForm.Show();
            _lockForm.Begin();
        }

        private bool _wasVisible = true;

        private void OnLockCompleted()
        {
            RefreshList();
        }

        // ============================================================ 题库操作

        private void RefreshSubjects()
        {
            if (_lbSubject == null) return;
            string keep = _lbSubject.SelectedItem as string;
            _lbSubject.Items.Clear();
            _lbSubject.Items.Add("全部科目");
            foreach (string s in Store.Subjects()) _lbSubject.Items.Add(s);

            if (keep != null && _lbSubject.Items.Contains(keep))
                _lbSubject.SelectedItem = keep;
            else
                _lbSubject.SelectedIndex = 0;
        }

        private void RefreshSubjectFilter()
        {
            if (_cboSubjectFilter == null) return;
            _cboSubjectFilter.Items.Clear();
            _cboSubjectFilter.Items.Add("全部科目");
            foreach (string s in Store.Subjects()) _cboSubjectFilter.Items.Add(s);

            string cur = Store.Data.Settings.SubjectFilter;
            int idx = 0;
            if (!string.IsNullOrEmpty(cur))
            {
                for (int i = 1; i < _cboSubjectFilter.Items.Count; i++)
                    if (string.Equals(_cboSubjectFilter.Items[i] as string, cur, StringComparison.OrdinalIgnoreCase)) { idx = i; break; }
            }
            _cboSubjectFilter.SelectedIndex = idx;
        }

        private void RefreshSettingsLabels()
        {
            if (_lblStats != null)
            {
                Settings s = Store.Data.Settings;
                _lblStats.Text = string.Format("已答 {0} 次 · 答错 {1} 次", s.TotalAnswered, s.TotalWrong);
            }
            if (_lblDataPath != null) _lblDataPath.Text = Store.DataPath;
            if (_lblCountdown != null) RefreshCountdownText();
        }

        private string CurrentSubjectFilter()
        {
            string s = _lbSubject.SelectedItem as string;
            if (string.IsNullOrEmpty(s) || s == "全部科目") return "";
            return s;
        }

        private void RefreshList()
        {
            if (_lvQuestions == null) return;
            string filter = CurrentSubjectFilter();

            _lvQuestions.BeginUpdate();
            _lvQuestions.Items.Clear();

            IEnumerable<Question> src = Store.Data.Questions;
            if (filter.Length > 0)
                src = src.Where(delegate(Question q) { return string.Equals((q.Subject ?? "").Trim(), filter, StringComparison.OrdinalIgnoreCase); });

            int n = 0;
            foreach (Question q in src.OrderByDescending(delegate(Question x) { return x.WrongCount * 100 - x.Level * 10; }))
            {
                ListViewItem it = new ListViewItem(string.IsNullOrEmpty(q.Subject) ? "未分类" : q.Subject);
                it.SubItems.Add(q.ShortText);
                it.SubItems.Add(q.ShownCount.ToString());
                it.SubItems.Add(q.WrongCount.ToString());
                it.SubItems.Add(q.LevelText);
                it.Tag = q;
                it.ImageIndex = 0;
                if (q.Mastered) it.ForeColor = Ui.TextMuted;
                else if (q.ShownCount == 0) it.ForeColor = Ui.TextDark;   // 还没考过：中性色
                else if (q.Level <= 0) it.ForeColor = Ui.Danger;
                else if (q.Level == 1) it.ForeColor = Ui.Warn;
                else it.ForeColor = Ui.Success;
                _lvQuestions.Items.Add(it);
                n++;
            }
            _lvQuestions.EndUpdate();

            if (_lblSummary != null)
            {
                int total = Store.Data.Questions.Count;
                _lblSummary.Text = string.Format("共 {0} 道题 · 当前显示 {1} 道 · 双击一行可以修改", total, n);
            }
            if (_emptyHint != null) _emptyHint.Visible = (n == 0);
            FitColumns();
            RefreshSettingsLabels();
        }

        private void FitColumns()
        {
            if (_lvQuestions == null || _lvQuestions.Columns.Count < 5) return;
            int w = _lvQuestions.ClientSize.Width;
            if (w <= 0) return;
            int fixedW = _lvQuestions.Columns[0].Width + _lvQuestions.Columns[2].Width
                       + _lvQuestions.Columns[3].Width + _lvQuestions.Columns[4].Width;
            int rest = w - fixedW - 4;
            if (rest > 160) _lvQuestions.Columns[1].Width = rest;
        }

        private Question SelectedQuestion()
        {
            if (_lvQuestions.SelectedItems.Count == 0) return null;
            return _lvQuestions.SelectedItems[0].Tag as Question;
        }

        private void AddQuestion()
        {
            Question q = Question.CreateNew("");
            q.Subject = CurrentSubjectFilter();
            using (QuestionEditForm f = new QuestionEditForm(q, Store.Subjects()))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    Store.Data.Questions.Add(q);
                    Store.Save();
                    RefreshSubjects();
                    RefreshList();
                }
            }
        }

        private void EditSelected()
        {
            Question q = SelectedQuestion();
            if (q == null) { MessageBox.Show(this, "先选一道题。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            using (QuestionEditForm f = new QuestionEditForm(q, Store.Subjects()))
            {
                if (f.ShowDialog(this) == DialogResult.OK)
                {
                    Store.Save();
                    RefreshSubjects();
                    RefreshList();
                }
            }
        }

        private void DeleteSelected()
        {
            Question q = SelectedQuestion();
            if (q == null) { MessageBox.Show(this, "先选一道题。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            if (MessageBox.Show(this, "删除这道题？\r\n\r\n" + q.ShortText, "确认删除", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            Store.Data.Questions.Remove(q);
            Store.Save();
            RefreshSubjects();
            RefreshList();
        }

        private void ToggleMastered()
        {
            Question q = SelectedQuestion();
            if (q == null) return;
            q.Mastered = !q.Mastered;
            if (q.Mastered) q.Level = 2;
            Store.Save();
            RefreshList();
        }

        private void ImportFile()
        {
            using (OpenFileDialog d = new OpenFileDialog())
            {
                d.Title = "选择题库文件";
                d.Filter = "文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*";
                if (d.ShowDialog(this) != DialogResult.OK) return;

                string content;
                try
                {
                    content = File.ReadAllText(d.FileName, EncodingDetect(d.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "读取失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (Store.Data.Questions.Count > 0)
                {
                    DialogResult r = MessageBox.Show(this,
                        "当前已有 " + Store.Data.Questions.Count + " 道题。\r\n\r\n点「是」= 追加进来\r\n点「否」= 清空后只保留导入的题",
                        "导入方式", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                    if (r == DialogResult.Cancel) return;
                    string info;
                    Store.ImportText(content, r == DialogResult.Yes, out info);
                    MessageBox.Show(this, info, "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    string info;
                    Store.ImportText(content, true, out info);
                    MessageBox.Show(this, info, "导入完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }

                RefreshSubjects();
                RefreshSubjectFilter();
                RefreshList();
            }
        }

        private System.Text.Encoding EncodingDetect(string path)
        {
            byte[] head = new byte[3];
            using (FileStream fs = File.OpenRead(path))
            {
                if (fs.Length >= 3) fs.Read(head, 0, 3);
            }
            if (head[0] == 0xEF && head[1] == 0xBB && head[2] == 0xBF) return new System.Text.UTF8Encoding(true);
            return System.Text.Encoding.Default;
        }

        private void ExportFile()
        {
            using (SaveFileDialog d = new SaveFileDialog())
            {
                d.Title = "导出题库";
                d.Filter = "文本文件 (*.txt)|*.txt";
                d.FileName = "错题本-" + DateTime.Now.ToString("yyyyMMdd") + ".txt";
                if (d.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(d.FileName, Store.ExportText(), new System.Text.UTF8Encoding(true));
                    MessageBox.Show(this, "已导出到：\r\n" + d.FileName, "导出完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void ApplyAutoStart(bool on)
        {
            try
            {
                string exe = Application.ExecutablePath;
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                if (key == null) return;
                if (on) key.SetValue("MistakeLocker", "\"" + exe + "\" --silent");
                else if (key.GetValue("MistakeLocker") != null) key.DeleteValue("MistakeLocker", false);
                key.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "设置开机自启失败：" + ex.Message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ============================================================ 关闭 / 退出

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
                Tray.ShowBalloonTip(2200, "错题锁屏还在运行",
                    "关掉窗口不会退出程序，定时锁屏照常工作。要退出请右键托盘图标。", ToolTipIcon.Info);
                return;
            }
            base.OnFormClosing(e);
        }

        public void ExitApp()
        {
            _exiting = true;
            try { _tick.Stop(); } catch { }
            Store.Save();
            try { if (_lockForm != null && !_lockForm.IsDisposed) _lockForm.ForceClose(); } catch { }
            try { Win32.ShowTaskbar(); } catch { }
            try { Tray.Visible = false; Tray.Dispose(); } catch { }
            Application.Exit();
        }
    }
}
