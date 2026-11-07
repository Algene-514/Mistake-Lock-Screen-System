using System;
using System.Collections.Generic;
using System.Drawing;
using System.Media;
using System.Windows.Forms;

namespace MistakeLocker
{
    /// <summary>
    /// 全屏锁屏复习窗口。
    /// 强制机制：无边框全屏 + 置顶 + 屏蔽 Alt+Tab/Win/Alt+F4 + 隐藏任务栏 + 定期抢焦点。
    /// </summary>
    public class LockForm : Form
    {
        public event EventHandler LockCompleted;

        private List<Question> _queue = new List<Question>();
        private int _index;
        private int _stage;            // 0 = 未看答案 / 1 = 已看答案
        private int _correctIndex = -1; // 选择题：用户选的
        private bool _answered;
        private bool _finished;
        private bool _allowClose;
        private bool _adjusting;
        private Settings _set;
        private DateTime _finishedAt = DateTime.MinValue;
        private bool _departed;

        private Label _lblHeader;
        private Label _lblProgress;
        private TextBox _txtQuestion;
        private Panel _answerCard;
        private Label _lblAnswerTitle;
        private TextBox _txtAnswer;
        private TableLayoutPanel _grid;
        private FlowLayoutPanel _btnBar;
        private FlowLayoutPanel _optionBar;
        private Panel _optionWrap;
        private Label _lblHint;

        private Button _btnReveal;
        private Button _btnRight;
        private Button _btnWrong;
        private Button _btnNext;
        private Button _btnUnlock;

        private HotkeyBlocker _blocker;
        private Timer _guardTimer;
        private Timer _tickTimer;

        public LockForm(List<Question> queue, Settings set)
        {
            _queue = queue ?? new List<Question>();
            _set = set ?? new Settings();

            BuildUi();
            StartProtection();
            ShowCurrent();
        }

        // ------------------------------------------------------------ UI

        private void BuildUi()
        {
            this.Text = "错题锁屏";
            this.FormBorderStyle = FormBorderStyle.None;
            this.StartPosition = FormStartPosition.Manual;
            this.WindowState = FormWindowState.Normal;
            Rectangle r = Screen.PrimaryScreen.Bounds;
            this.Bounds = new Rectangle(r.X, r.Y, r.Width, r.Height);
            this.BackColor = Ui.DarkBg;
            this.ForeColor = Ui.DarkText;
            this.KeyPreview = true;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.DoubleBuffered = true;
            this.MinimizeBox = false;
            this.MaximizeBox = false;

            // ---------- 顶部 ----------
            Panel top = new Panel();
            top.Dock = DockStyle.Top;
            top.Height = 84;
            top.Padding = new Padding(56, 0, 56, 0);
            top.BackColor = Ui.DarkBg;

            _lblHeader = new Label();
            _lblHeader.AutoSize = true;
            _lblHeader.Font = Ui.F(12f);
            _lblHeader.ForeColor = Ui.DarkMuted;
            _lblHeader.Location = new Point(56, 34);
            top.Controls.Add(_lblHeader);

            _lblProgress = new Label();
            _lblProgress.AutoSize = false;
            _lblProgress.Width = 260;
            _lblProgress.Height = 30;
            _lblProgress.TextAlign = ContentAlignment.TopRight;
            _lblProgress.Font = Ui.F(13f);
            _lblProgress.ForeColor = ColorTranslator.FromHtml("#8E96A3");
            _lblProgress.Left = 200;
            _lblProgress.Top = 32;
            top.Controls.Add(_lblProgress);

            top.Resize += delegate(object s, EventArgs e)
            {
                _lblProgress.Left = top.Width - top.Padding.Right - _lblProgress.Width;
            };

            // ---------- 底部按钮 ----------
            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.AutoSize = true;
            bottom.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            bottom.Padding = new Padding(56, 16, 56, 40);
            bottom.BackColor = Ui.DarkBg;

            _optionWrap = new Panel();
            _optionWrap.Dock = DockStyle.Top;
            _optionWrap.AutoSize = true;
            _optionWrap.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _optionWrap.Visible = false;

            _optionBar = new FlowLayoutPanel();
            _optionBar.Dock = DockStyle.Top;
            _optionBar.FlowDirection = FlowDirection.TopDown;
            _optionBar.WrapContents = false;
            _optionBar.AutoSize = true;
            _optionBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _optionBar.Padding = new Padding(0, 0, 0, 8);
            _optionWrap.Controls.Add(_optionBar);

            _btnBar = new FlowLayoutPanel();
            _btnBar.Dock = DockStyle.Top;
            _btnBar.FlowDirection = FlowDirection.RightToLeft;
            _btnBar.WrapContents = false;
            _btnBar.AutoSize = true;
            _btnBar.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            _btnBar.Padding = new Padding(0, 8, 0, 0);

            _lblHint = new Label();
            _lblHint.AutoSize = true;
            _lblHint.Font = Ui.F(9.5f);
            _lblHint.ForeColor = Ui.DarkMuted;
            _lblHint.Margin = new Padding(24, 16, 0, 0);

            _btnReveal = Ui.DarkButton("显示答案  (空格)", Ui.DarkCard, Ui.DarkText);
            _btnReveal.Click += delegate { RevealAnswer(); };

            _btnRight = Ui.DarkButton("我记住了", ColorTranslator.FromHtml("#1F3D2E"), ColorTranslator.FromHtml("#4ADE80"));
            _btnRight.Click += delegate { FinishQuestion(true); };

            _btnWrong = Ui.DarkButton("还不会", ColorTranslator.FromHtml("#3D2224"), ColorTranslator.FromHtml("#FF7B7B"));
            _btnWrong.Click += delegate { FinishQuestion(false); };

            _btnNext = Ui.DarkButton("下一题  →", Ui.DarkCard, Ui.DarkText);
            _btnNext.Click += delegate { NextQuestion(); };

            _btnUnlock = Ui.DarkButton("解锁并继续", Ui.DarkAccent, Color.White);
            _btnUnlock.Click += delegate
            {
                if (!_departed) Unlock();
            };

            // 所有按钮一次性建好、常驻父容器，之后只用 Visible 切换。
            // （不能在某个按钮的 Click 里把自己的父控件 Clear 掉 —— 那会销毁正在处理消息的窗口句柄，
            //   实测会直接跳到「复习完成」。）
            // RightToLeft 下先添加的靠右：
            _btnBar.Controls.Add(_btnRight);
            _btnBar.Controls.Add(_btnWrong);
            _btnBar.Controls.Add(_btnNext);
            _btnBar.Controls.Add(_btnUnlock);
            _btnBar.Controls.Add(_btnReveal);
            _btnBar.Controls.Add(_lblHint);
            SetButtons(false, false, false, false, "");

            // 注意：Dock=Top 时后添加的先占位，所以 _optionWrap 后加 → 显示在按钮区上方
            bottom.Controls.Add(_btnBar);
            bottom.Controls.Add(_optionWrap);

            // ---------- 正文 ----------
            _grid = new TableLayoutPanel();
            _grid.Dock = DockStyle.Fill;
            _grid.Padding = new Padding(56, 8, 56, 8);
            _grid.ColumnCount = 1;
            _grid.RowCount = 2;
            _grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            _grid.RowStyles.Add(new RowStyle(SizeType.Percent, 0f));
            _grid.BackColor = Ui.DarkBg;

            // 题目卡片
            Panel qCard = Ui.Card2(Ui.DarkCard, Ui.DarkBorder);
            qCard.Dock = DockStyle.Fill;
            qCard.Margin = new Padding(0, 0, 0, 0);
            qCard.Padding = new Padding(1);

            Panel qInner = new Panel();
            qInner.Dock = DockStyle.Fill;
            qInner.BackColor = Ui.DarkCard;
            qInner.Padding = new Padding(36, 28, 36, 28);

            _txtQuestion = new TextBox();
            _txtQuestion.Multiline = true;
            _txtQuestion.ReadOnly = true;
            _txtQuestion.BorderStyle = BorderStyle.None;
            _txtQuestion.BackColor = Ui.DarkCard;
            _txtQuestion.ForeColor = Ui.DarkText;
            _txtQuestion.Font = Ui.F(17f);
            _txtQuestion.Dock = DockStyle.Fill;
            _txtQuestion.ScrollBars = ScrollBars.Vertical;
            _txtQuestion.TabStop = false;
            qInner.Controls.Add(_txtQuestion);
            qCard.Controls.Add(qInner);

            // 答案卡片
            _answerCard = Ui.Card2(Ui.DarkCard, Ui.DarkBorder);
            _answerCard.Dock = DockStyle.Fill;
            _answerCard.Margin = new Padding(0, 14, 0, 0);
            _answerCard.Padding = new Padding(1);
            _answerCard.Visible = false;

            Panel aInner = new Panel();
            aInner.Dock = DockStyle.Fill;
            aInner.BackColor = Ui.DarkCard;
            aInner.Padding = new Padding(36, 22, 36, 24);

            _lblAnswerTitle = new Label();
            _lblAnswerTitle.Dock = DockStyle.Top;
            _lblAnswerTitle.Height = 28;
            _lblAnswerTitle.Font = Ui.F(10.5f, FontStyle.Bold);
            _lblAnswerTitle.ForeColor = Ui.DarkAccent;
            _lblAnswerTitle.Text = "答案";

            _txtAnswer = new TextBox();
            _txtAnswer.Multiline = true;
            _txtAnswer.ReadOnly = true;
            _txtAnswer.BorderStyle = BorderStyle.None;
            _txtAnswer.BackColor = Ui.DarkCard;
            _txtAnswer.ForeColor = Ui.DarkText;
            _txtAnswer.Font = Ui.F(13f);
            _txtAnswer.Dock = DockStyle.Fill;
            _txtAnswer.ScrollBars = ScrollBars.Vertical;
            _txtAnswer.TabStop = false;

            aInner.Controls.Add(_txtAnswer);
            aInner.Controls.Add(_lblAnswerTitle);
            _answerCard.Controls.Add(aInner);

            _grid.Controls.Add(qCard, 0, 0);
            _grid.Controls.Add(_answerCard, 0, 1);

            this.Controls.Add(_grid);
            this.Controls.Add(top);
            this.Controls.Add(bottom);

            // 事件
            this.KeyDown += LockForm_KeyDown;
            this.Resize += delegate { LayoutBar(); };
            this.Shown += delegate { LayoutBar(); };
            _txtQuestion.SizeChanged += delegate { AdjustScroll(_txtQuestion); };
            _txtAnswer.SizeChanged += delegate { AdjustScroll(_txtAnswer); };
        }

        private void LayoutBar()
        {
            if (_lblProgress != null)
                _lblProgress.Left = Math.Max(200, this.Width - 56 - _lblProgress.Width);
        }

        // ------------------------------------------------------------ 流程

        public void Begin()
        {
            if (_queue.Count == 0)
            {
                _lblHeader.Text = "题库里还没有题目";
                SetBodyText(_txtQuestion, "请先在【错题本】里添加题目，或者导入一份题库文件（支持 .txt）。\r\n\r\n添加后，程序就会按设定的时间自动锁屏，把这些题目推到你眼前。");
                _stage = 2;
                _finished = true;
                _finishedAt = DateTime.MinValue;
                ShowUnlockOnly("好，我知道了");
                return;
            }
            _index = 0;
            ShowCurrent();
        }

        private void ShowCurrent()
        {
            if (_index >= _queue.Count) { FinishAll(); return; }

            Question q = _queue[_index];
            _stage = 0;
            _answered = false;
            _correctIndex = -1;

            _lblHeader.Text = (string.IsNullOrEmpty(q.Subject) ? "综合" : q.Subject) + "   ·   第 " + (_index + 1) + " / " + _queue.Count + " 题";
            _lblProgress.Text = ProgressDots(_index, _queue.Count);
            SetBodyText(_txtQuestion, q.Text ?? "");

            _answerCard.Visible = false;
            _grid.RowStyles[0] = new RowStyle(SizeType.Percent, 100f);
            _grid.RowStyles[1] = new RowStyle(SizeType.Percent, 0f);

            // 选项
            _optionBar.Controls.Clear();
            if (q.IsChoice)
            {
                _optionWrap.Visible = true;
                for (int i = 0; i < q.Options.Count; i++)
                {
                    Question optQ = q;
                    int optIndex = i;
                    Button b = new Button();
                    b.Text = ((char)('A' + i)) + "   " + q.Options[i];
                    b.TextAlign = ContentAlignment.MiddleLeft;
                    b.FlatStyle = FlatStyle.Flat;
                    b.FlatAppearance.BorderColor = Ui.DarkBorder;
                    b.FlatAppearance.BorderSize = 1;
                    b.BackColor = Ui.DarkCard;
                    b.ForeColor = Ui.DarkText;
                    b.Font = Ui.F(12.5f);
                    b.Height = 50;
                    b.Width = Math.Max(500, this.Width - 200);
                    b.Margin = new Padding(0, 0, 0, 10);
                    b.Cursor = Cursors.Hand;
                    b.UseVisualStyleBackColor = false;
                    b.Click += delegate { ChooseOption(optQ, optIndex, b); };
                    _optionBar.Controls.Add(b);
                }
            }
            else
            {
                _optionWrap.Visible = false;
            }

            // 按钮
            if (q.IsChoice)
                SetButtons(false, false, false, false, "点击你认为正确的选项");
            else
                SetButtons(true, false, false, false, "把答案想清楚，再点「显示答案」");

            this.ActiveControl = null;
            LayoutBar();
            // 选项区显隐改变后，底部高度需要重新计算，否则会和题目卡片轻微重叠
            this.PerformLayout();
        }

        private void ChooseOption(Question q, int index, Button btn)
        {
            if (_answered) return;
            _answered = true;
            _correctIndex = index;

            bool ok = (index == q.CorrectIndex);

            foreach (Control c in _optionBar.Controls)
            {
                Button b = c as Button;
                if (b == null) continue;
                b.Enabled = false;
                int bi = _optionBar.Controls.IndexOf(b);
                if (bi == q.CorrectIndex)
                {
                    b.BackColor = ColorTranslator.FromHtml("#1F3D2E");
                    b.ForeColor = ColorTranslator.FromHtml("#4ADE80");
                }
                else if (bi == index)
                {
                    b.BackColor = ColorTranslator.FromHtml("#3D2224");
                    b.ForeColor = ColorTranslator.FromHtml("#FF7B7B");
                }
            }

            q.ShownCount++;
            q.LastShownUtc = DateTime.UtcNow;
            if (ok) { q.RightCount++; q.Level = Math.Min(2, q.Level + 1); }
            else { q.WrongCount++; q.Level = 0; }
            Store.Data.Settings.TotalAnswered++;
            if (!ok) Store.Data.Settings.TotalWrong++;
            Store.Save();

            ShowAnswerCard(q, ok ? "回答正确" : "回答错误", ok);

            SetButtons(false, false, true, false, ok ? "答对了，继续保持" : "这次记错了，下面就是正确答案");
        }

        private void RevealAnswer()
        {
            if (_stage == 1) return;
            Question q = _queue[_index];
            _stage = 1;

            q.ShownCount++;
            q.LastShownUtc = DateTime.UtcNow;
            Store.Save();

            ShowAnswerCard(q, "答案", true);

            SetButtons(false, true, false, false, "刚才想的是这个答案吗？");
        }

        private void ShowAnswerCard(Question q, string title, bool good)
        {
            string body = "";

            if (q.IsChoice && q.Options != null && q.Options.Count > 0)
            {
                string letter = q.CorrectIndex >= 0 && q.CorrectIndex < 26
                    ? ((char)('A' + q.CorrectIndex)).ToString()
                    : "?";
                string optText = (q.CorrectIndex >= 0 && q.CorrectIndex < q.Options.Count)
                    ? q.Options[q.CorrectIndex] : "";
                body = "正确答案：" + letter + ".  " + optText;
                // 编辑时自动带上的答案常常就是选项原文，别重复显示
                string extra = (q.Answer ?? "").Trim();
                if (extra.Length > 0 && extra != optText.Trim())
                    body += "\r\n\r\n" + extra;
            }
            else
            {
                body = string.IsNullOrEmpty(q.Answer) ? "（没有填写答案）" : q.Answer;
            }

            if (!string.IsNullOrEmpty(q.Explanation))
                body += "\r\n\r\n解析：\r\n" + q.Explanation;

            SetBodyText(_txtAnswer, body);
            _lblAnswerTitle.Text = title;
            _lblAnswerTitle.ForeColor = good ? Ui.DarkAccent : ColorTranslator.FromHtml("#FF7B7B");

            _answerCard.Visible = true;
            _grid.RowStyles[0] = new RowStyle(SizeType.Percent, 38f);
            _grid.RowStyles[1] = new RowStyle(SizeType.Percent, 62f);
        }

        /// <summary>主观题：会了 / 还不会</summary>
        private void FinishQuestion(bool known)
        {
            Question q = _queue[_index];
            q.ShownCount++;
            q.LastShownUtc = DateTime.UtcNow;
            if (known) { q.RightCount++; q.Level = Math.Min(2, q.Level + 1); }
            else { q.WrongCount++; q.Level = 0; }
            Store.Data.Settings.TotalAnswered++;
            if (!known) Store.Data.Settings.TotalWrong++;
            Store.Save();

            NextQuestion();
        }

        private void NextQuestion()
        {
            _index++;
            if (_index >= _queue.Count) FinishAll();
            else ShowCurrent();
        }

        private void FinishAll()
        {
            _finished = true;
            _finishedAt = DateTime.UtcNow;
            _stage = 2;
            _lblHeader.Text = "本轮复习完成";
            _lblProgress.Text = ProgressDots(_queue.Count, _queue.Count);
            SetBodyText(_txtQuestion, "这一轮 " + _queue.Count + " 道题都过完了。\r\n\r\n错了的题会被记下来，之后更容易再被抽到——\r\n直到你真的把它记住。");
            _optionWrap.Visible = false;
            _answerCard.Visible = false;
            _grid.RowStyles[0] = new RowStyle(SizeType.Percent, 100f);
            _grid.RowStyles[1] = new RowStyle(SizeType.Percent, 0f);
            ShowUnlockOnly(null);
        }

        private void ShowUnlockOnly(string customText)
        {
            string hint = customText ?? "休息一下，继续学习";

            if (_set.MinStaySeconds > 0 && _finishedAt != DateTime.MinValue)
            {
                _btnUnlock.Text = "解锁并继续";
                _btnUnlock.Enabled = false;
                SetButtons(false, false, false, true, hint);
                StartCountdown();
            }
            else
            {
                _btnUnlock.Enabled = true;
                _btnUnlock.Text = "解锁并继续";
                SetButtons(false, false, false, true, hint);
            }
        }

        /// <summary>一次性控制底部按钮的可见性（不销毁控件）。</summary>
        private void SetButtons(bool reveal, bool answerSelfCheck, bool next, bool unlock, string hint)
        {
            if (_btnReveal == null) return;
            _btnReveal.Visible = reveal;
            _btnRight.Visible = answerSelfCheck;
            _btnWrong.Visible = answerSelfCheck;
            _btnNext.Visible = next;
            _btnUnlock.Visible = unlock;
            _lblHint.Text = hint ?? "";
            _lblHint.Visible = true;
            _btnBar.PerformLayout();
        }

        private void StartCountdown()
        {
            _tickTimer = new Timer();
            _tickTimer.Interval = 500;
            _tickTimer.Tick += delegate
            {
                double left = _set.MinStaySeconds - (DateTime.UtcNow - _finishedAt).TotalSeconds;
                if (left <= 0)
                {
                    _btnUnlock.Enabled = true;
                    _btnUnlock.Text = "解锁并继续";
                    _tickTimer.Stop();
                }
                else
                {
                    _btnUnlock.Text = "解锁并继续  (" + ((int)Math.Ceiling(left)) + "s)";
                }
            };
            _tickTimer.Start();
        }

        /// <summary>设置正文；只有在文字真的超出可视区域时才显示滚动条（深色背景下的系统滚动条很难看）。</summary>
        private void SetBodyText(TextBox tb, string text)
        {
            if (tb == null) return;
            tb.Text = text ?? "";
            tb.SelectionStart = 0;
            // 布局（RowStyle 变化 / 卡片显隐）不是同步完成的，等一小会儿再判断要不要滚动条
            DeferAdjust(tb);
        }

        private void DeferAdjust(TextBox tb)
        {
            Timer t = new Timer();
            t.Interval = 260;
            t.Tick += delegate(object s, EventArgs e)
            {
                t.Stop();
                t.Dispose();
                AdjustScroll(tb);
            };
            t.Start();
        }

        private void AdjustScroll(TextBox tb)
        {
            if (tb == null || tb.IsDisposed || _adjusting) return;
            _adjusting = true;
            try
            {
                // 带上滚动条的宽度再测量，避免「加滚动条→宽度变窄→又不需要→来回抖动」
                int w = tb.ClientSize.Width + (tb.ScrollBars == ScrollBars.Vertical ? 17 : 0) - 6;
                int h = tb.ClientSize.Height - 4;
                if (w < 40 || h < 40) return;

                Size need = TextRenderer.MeasureText(tb.Text, tb.Font, new Size(w, int.MaxValue),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl);

                ScrollBars target = (need.Height > h) ? ScrollBars.Vertical : ScrollBars.None;
                if (tb.ScrollBars != target) tb.ScrollBars = target;
            }
            catch { }
            finally { _adjusting = false; }
        }

        private string ProgressDots(int done, int total)
        {
            if (total <= 0) return "";
            int show = Math.Min(total, 12);
            string s = "";
            for (int i = 0; i < show; i++) s += (i < done ? "●" : "○") + " ";
            if (total > 12) s += " " + done + "/" + total;
            return s.TrimEnd();
        }

        // ------------------------------------------------------------ 强制保护

        private void StartProtection()
        {
            if (_set.BlockHotkeys)
            {
                _blocker = new HotkeyBlocker();
                _blocker.Start();
            }

            if (_set.HideTaskbar) Win32.HideTaskbar();

            _guardTimer = new Timer();
            _guardTimer.Interval = 2500;
            _guardTimer.Tick += delegate
            {
                if (_allowClose) return;
                try
                {
                    if (!this.TopMost) this.TopMost = true;
                    if (this.WindowState == FormWindowState.Minimized)
                        this.WindowState = FormWindowState.Normal;
                    if (Form.ActiveForm != this) this.Activate();
                }
                catch { }
            };
            _guardTimer.Start();

            if (_set.SoundOnLock)
            {
                try { SystemSounds.Asterisk.Play(); } catch { }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!_allowClose && e.CloseReason == CloseReason.UserClosing)
            {
                // 用户想偷偷关掉窗口：不允许
                e.Cancel = true;
                if (_lblHint != null) _lblHint.Text = "复习完成前不能关闭这个窗口";
                return;
            }
            base.OnFormClosing(e);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            Release();
            base.OnFormClosed(e);
        }

        private void Release()
        {
            try { if (_guardTimer != null) _guardTimer.Stop(); } catch { }
            try { if (_tickTimer != null) _tickTimer.Stop(); } catch { }
            try { if (_blocker != null) _blocker.Dispose(); } catch { }
            try { Win32.ShowTaskbar(); } catch { }

            // 如果是从锁屏界面直接退出程序（未解锁），记一次逃逸
        }

        public void ForceClose()
        {
            _allowClose = true;
            Release();
            this.Close();
        }

        private void Unlock()
        {
            if (!_finished) return;
            _departed = true;
            _allowClose = true;
            Store.Data.Settings.LastLockUtc = DateTime.UtcNow;
            Store.Save();

            if (!_finished || _queue.Count == 0)
            {
                // 题库为空也算正常解锁
            }

            Release();
            this.Close();

            if (LockCompleted != null) LockCompleted(this, EventArgs.Empty);
        }

        private void LockForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }
            if (e.KeyCode == Keys.Space && !e.Alt && !e.Control)
            {
                if (_stage == 0 && _btnReveal != null && _btnReveal.Visible)
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                    RevealAnswer();
                }
            }
            if (e.KeyCode == Keys.Enter)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (_finished && _btnUnlock != null && _btnUnlock.Visible && _btnUnlock.Enabled && !_departed)
                    Unlock();
                else if (_btnRight != null && _btnRight.Visible)
                    FinishQuestion(true);
                else if (_btnNext != null && _btnNext.Visible)
                    NextQuestion();
            }
        }
    }
}
