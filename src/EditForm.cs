using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace MistakeLocker
{
    public class QuestionEditForm : Form
    {
        private Question _q;
        public Question Result;

        private ComboBox _cboSubject;
        private TextBox _txtTags;
        private RadioButton _rdoSubjective;
        private RadioButton _rdoChoice;
        private TextBox _txtText;
        private Panel _panelType;
        private Panel _panelSubjective;
        private Panel _panelChoice;
        private TextBox _txtAnswer;
        private TextBox[] _opt = new TextBox[4];
        private ComboBox _cboCorrect;
        private TextBox _txtExplanation;

        private const int Pad = 26;

        public QuestionEditForm(Question q, List<string> subjects)
        {
            _q = q;
            BuildUi(subjects);
            LoadData();
        }

        private void BuildUi(List<string> subjects)
        {
            this.Text = "编辑题目";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowInTaskbar = false;
            this.ClientSize = new Size(700, 700);
            this.BackColor = Ui.Bg;
            this.Font = Ui.F(10f);
            this.Padding = new Padding(0);

            int w = this.ClientSize.Width - Pad * 2;

            // 科目
            Label lblSubject = Ui.MakeLabel("科目", 9.5f, Ui.TextMuted);
            lblSubject.Location = new Point(Pad, 24);
            this.Controls.Add(lblSubject);

            _cboSubject = new ComboBox();
            _cboSubject.Location = new Point(Pad + 58, 18);
            _cboSubject.Width = 220;
            _cboSubject.DropDownStyle = ComboBoxStyle.DropDown;
            _cboSubject.Font = Ui.F(10f);
            _cboSubject.Items.Add("数学");
            _cboSubject.Items.Add("语文");
            _cboSubject.Items.Add("英语");
            _cboSubject.Items.Add("物理");
            _cboSubject.Items.Add("化学");
            _cboSubject.Items.Add("生物");
            _cboSubject.Items.Add("历史");
            _cboSubject.Items.Add("地理");
            _cboSubject.Items.Add("政治");
            if (subjects != null)
            {
                foreach (string s in subjects)
                    if (!_cboSubject.Items.Contains(s)) _cboSubject.Items.Add(s);
            }
            this.Controls.Add(_cboSubject);

            // 标签
            Label lblTags = Ui.MakeLabel("标签", 9.5f, Ui.TextMuted);
            lblTags.Location = new Point(Pad + 300, 24);
            this.Controls.Add(lblTags);

            _txtTags = new TextBox();
            _txtTags.Location = new Point(Pad + 358, 18);
            _txtTags.Width = w - 358;
            _txtTags.Font = Ui.F(10f);
            _txtTags.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_txtTags);

            // 题型
            Label lblType = Ui.MakeLabel("题型", 9.5f, Ui.TextMuted);
            lblType.Location = new Point(Pad, 62);
            this.Controls.Add(lblType);

            _rdoSubjective = new RadioButton();
            _rdoSubjective.Text = "主观题（自己对照答案）";
            _rdoSubjective.Location = new Point(Pad + 58, 58);
            _rdoSubjective.AutoSize = true;
            _rdoSubjective.Font = Ui.F(10f);
            _rdoSubjective.Checked = true;
            _rdoSubjective.CheckedChanged += delegate { SyncType(); };
            this.Controls.Add(_rdoSubjective);

            _rdoChoice = new RadioButton();
            _rdoChoice.Text = "选择题（自动判分）";
            _rdoChoice.Location = new Point(Pad + 300, 58);
            _rdoChoice.AutoSize = true;
            _rdoChoice.Font = Ui.F(10f);
            this.Controls.Add(_rdoChoice);

            // 题目
            Label lblText = Ui.MakeLabel("题目", 9.5f, Ui.TextMuted);
            lblText.Location = new Point(Pad, 104);
            this.Controls.Add(lblText);

            _txtText = new TextBox();
            _txtText.Location = new Point(Pad, 126);
            _txtText.Width = w;
            _txtText.Height = 130;
            _txtText.Multiline = true;
            _txtText.ScrollBars = ScrollBars.Vertical;
            _txtText.Font = Ui.F(10f);
            _txtText.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_txtText);

            // 类型相关区域
            _panelType = new Panel();
            _panelType.Location = new Point(Pad, 268);
            _panelType.Width = w;
            _panelType.Height = 214;
            _panelType.BackColor = Ui.Bg;
            this.Controls.Add(_panelType);

            // 主观题：答案
            _panelSubjective = new Panel();
            _panelSubjective.Dock = DockStyle.Fill;
            _panelSubjective.BackColor = Ui.Bg;
            Label lblAnswer = Ui.MakeLabel("答案", 9.5f, Ui.TextMuted);
            lblAnswer.Location = new Point(0, 2);
            _panelSubjective.Controls.Add(lblAnswer);

            _txtAnswer = new TextBox();
            _txtAnswer.Location = new Point(0, 24);
            _txtAnswer.Width = w;
            _txtAnswer.Height = 180;
            _txtAnswer.Multiline = true;
            _txtAnswer.ScrollBars = ScrollBars.Vertical;
            _txtAnswer.Font = Ui.F(10f);
            _txtAnswer.BorderStyle = BorderStyle.FixedSingle;
            _panelSubjective.Controls.Add(_txtAnswer);
            _panelType.Controls.Add(_panelSubjective);

            // 选择题：选项
            _panelChoice = new Panel();
            _panelChoice.Dock = DockStyle.Fill;
            _panelChoice.BackColor = Ui.Bg;
            _panelChoice.Visible = false;

            Label lblOpt = Ui.MakeLabel("选项", 9.5f, Ui.TextMuted);
            lblOpt.Location = new Point(0, 2);
            _panelChoice.Controls.Add(lblOpt);

            for (int i = 0; i < 4; i++)
            {
                Label lb = Ui.MakeLabel(((char)('A' + i)) + "、", 10f, Ui.TextDark);
                lb.Location = new Point(0, 26 + i * 42 + 6);
                _panelChoice.Controls.Add(lb);

                TextBox tb = new TextBox();
                tb.Location = new Point(34, 26 + i * 42);
                tb.Width = w - 180;
                tb.Font = Ui.F(10f);
                tb.BorderStyle = BorderStyle.FixedSingle;
                _panelChoice.Controls.Add(tb);
                _opt[i] = tb;
            }

            Label lblCorrect = Ui.MakeLabel("正确答案", 9.5f, Ui.TextMuted);
            lblCorrect.Location = new Point(w - 130, 30);
            _panelChoice.Controls.Add(lblCorrect);

            _cboCorrect = new ComboBox();
            _cboCorrect.Location = new Point(w - 130, 52);
            _cboCorrect.Width = 120;
            _cboCorrect.DropDownStyle = ComboBoxStyle.DropDownList;
            _cboCorrect.Font = Ui.F(10f);
            _cboCorrect.Items.Add("A");
            _cboCorrect.Items.Add("B");
            _cboCorrect.Items.Add("C");
            _cboCorrect.Items.Add("D");
            _cboCorrect.SelectedIndex = 0;
            _panelChoice.Controls.Add(_cboCorrect);

            _panelType.Controls.Add(_panelChoice);

            // 解析
            Label lblExp = Ui.MakeLabel("解析（可选，帮助自己记住为什么不选别的）", 9.5f, Ui.TextMuted);
            lblExp.Location = new Point(Pad, 490);
            this.Controls.Add(lblExp);

            _txtExplanation = new TextBox();
            _txtExplanation.Location = new Point(Pad, 512);
            _txtExplanation.Width = w;
            _txtExplanation.Height = 100;
            _txtExplanation.Multiline = true;
            _txtExplanation.ScrollBars = ScrollBars.Vertical;
            _txtExplanation.Font = Ui.F(10f);
            _txtExplanation.BorderStyle = BorderStyle.FixedSingle;
            this.Controls.Add(_txtExplanation);

            // 按钮
            Button btnSave = Ui.PrimaryButton("保存");
            btnSave.Location = new Point(this.ClientSize.Width - Pad - 110, 634);
            btnSave.Width = 110;
            btnSave.Height = 40;
            btnSave.Click += delegate { TrySave(); };
            this.Controls.Add(btnSave);

            Button btnCancel = Ui.GhostButton("取消");
            btnCancel.Location = new Point(this.ClientSize.Width - Pad - 110 - 120, 634);
            btnCancel.Width = 110;
            btnCancel.Height = 40;
            btnCancel.Click += delegate { this.DialogResult = DialogResult.Cancel; this.Close(); };
            this.Controls.Add(btnCancel);

            this.AcceptButton = btnSave;
            this.CancelButton = btnCancel;
        }

        private void SyncType()
        {
            bool choice = _rdoChoice.Checked;
            _panelSubjective.Visible = !choice;
            _panelChoice.Visible = choice;
        }

        private void LoadData()
        {
            _cboSubject.Text = _q.Subject ?? "";
            _txtTags.Text = _q.Tags ?? "";
            _txtText.Text = _q.Text ?? "";
            _txtAnswer.Text = _q.Answer ?? "";
            _txtExplanation.Text = _q.Explanation ?? "";

            if (_q.IsChoice)
            {
                _rdoChoice.Checked = true;
                for (int i = 0; i < 4; i++)
                {
                    if (_q.Options != null && i < _q.Options.Count)
                        _opt[i].Text = _q.Options[i] ?? "";
                }
                if (_q.CorrectIndex >= 0 && _q.CorrectIndex < 4)
                    _cboCorrect.SelectedIndex = _q.CorrectIndex;
            }
            else
            {
                _rdoSubjective.Checked = true;
            }
            SyncType();
        }

        private void TrySave()
        {
            string text = _txtText.Text.Trim();
            if (text.Length == 0)
            {
                MessageBox.Show(this, "题目内容不能为空。", "还差一点", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _txtText.Focus();
                return;
            }

            Question q = _q;
            q.Subject = (_cboSubject.Text ?? "").Trim();
            q.Tags = (_txtTags.Text ?? "").Trim();
            q.Text = text;
            q.Explanation = _txtExplanation.Text ?? "";

            if (_rdoChoice.Checked)
            {
                List<string> opts = new List<string>();
                int hole = -1;
                for (int i = 0; i < 4; i++)
                {
                    string s = _opt[i].Text.Trim();
                    if (s.Length > 0)
                    {
                        if (hole >= 0)
                        {
                            MessageBox.Show(this, "选项要按 A、B、C、D 的顺序填写，中间不能空着。", "选项顺序", MessageBoxButtons.OK, MessageBoxIcon.Information);
                            return;
                        }
                        opts.Add(s);
                    }
                    else if (hole < 0 && opts.Count > 0)
                    {
                        hole = i;
                    }
                }
                if (opts.Count < 2)
                {
                    MessageBox.Show(this, "选择题至少要有 2 个选项。", "还差一点", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                int correct = _cboCorrect.SelectedIndex;
                if (correct >= opts.Count)
                {
                    MessageBox.Show(this, "正确答案超出了已填写的选项范围。", "检查一下", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                q.Options = opts;
                q.CorrectIndex = correct;
                // 不把选项原文塞进 Answer，否则锁屏时「正确答案」会重复显示一遍
            }
            else
            {
                q.Options = new List<string>();
                q.CorrectIndex = -1;
                q.Answer = _txtAnswer.Text ?? "";
            }

            Result = q;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
