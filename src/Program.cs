using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace MistakeLocker
{
    internal static class Program
    {
        private const string MutexName = "MistakeLocker_Mutex_v1";
        private const string ShowEventName = "MistakeLocker_ShowWindow_v1";

        [STAThread]
        private static void Main(string[] args)
        {
            bool selftest = false;
            bool silent = false;
            bool lockNow = false;
            string importFile = null;

            foreach (string a in args)
            {
                string arg = a ?? "";
                if (arg == "--selftest") selftest = true;
                else if (arg == "--silent") silent = true;
                else if (arg == "--lock") lockNow = true;
                else if (arg.StartsWith("--import=")) importFile = arg.Substring("--import=".Length);
            }

            Store.Init();

            if (selftest)
            {
                RunSelfTest();
                return;
            }

            // ---------- 单实例 ----------
            bool createdNew;
            Mutex mutex = new Mutex(true, MutexName, out createdNew);
            if (!createdNew)
            {
                try
                {
                    EventWaitHandle ev = EventWaitHandle.OpenExisting(ShowEventName);
                    ev.Set();
                    ev.Close();
                }
                catch { }
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Win32.ShowTaskbar();   // 修正上次异常退出可能留下的隐藏任务栏

            Application.ThreadException += delegate(object s, ThreadExceptionEventArgs e)
            {
                LogCrash(e.Exception);
                MessageBox.Show("程序出了点问题：\r\n\r\n" + e.Exception.Message,
                    "错题锁屏", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object s, UnhandledExceptionEventArgs e)
            {
                LogCrash(e.ExceptionObject as Exception);
            };

            Store.Load();
            if (Store.Data.Settings.LastLockUtc == DateTime.MinValue)
                Store.Data.Settings.LastLockUtc = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(importFile) && File.Exists(importFile))
            {
                try
                {
                    string info;
                    Store.ImportText(File.ReadAllText(importFile), true, out info);
                }
                catch { }
            }

            MainForm main = new MainForm();

            // 监听「再启动一次就打开主窗口」
            try
            {
                EventWaitHandle showEv = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
                Thread th = new Thread(delegate()
                {
                    while (true)
                    {
                        try
                        {
                            showEv.WaitOne();
                            if (main.IsDisposed) break;
                            try { main.BeginInvoke(new Action(main.ShowFromOtherInstance)); }
                            catch { }
                        }
                        catch { break; }
                    }
                });
                th.IsBackground = true;
                th.Start();
            }
            catch { }

            Application.Run(new AppContext(main, silent, lockNow));

            try { mutex.ReleaseMutex(); } catch { }
        }

        private static void LogCrash(Exception ex)
        {
            try
            {
                string p = Path.Combine(Store.DirPath, "error.log");
                File.AppendAllText(p,
                    "[" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "] " + (ex == null ? "unknown" : ex.ToString()) + "\r\n\r\n");
            }
            catch { }
        }

        // ------------------------------------------------------------ 自检

        private static void RunSelfTest()
        {
            StringBuilder sb = new StringBuilder();
            bool ok = true;
            string probePath = Path.Combine(Path.GetTempPath(), "mistakelock_selftest_data.json");

            try
            {
                // 自检绝不能碰真实题库：把数据文件指到临时路径
                try { if (File.Exists(probePath)) File.Delete(probePath); } catch { }
                Store.DataPath = probePath;

                sb.AppendLine("== 环境 ==");
                sb.AppendLine("exe: " + Application.ExecutablePath);
                sb.AppendLine("dataDir: " + Store.DirPath);
                sb.AppendLine("dataFile: " + Store.DataPath);
                sb.AppendLine("font: " + Ui.FontFamilyName);

                sb.AppendLine();
                sb.AppendLine("== 存储 ==");
                Store.Load();
                sb.AppendLine("loadOk: " + Store.LastLoadOk + "  err: " + Store.LastError);
                sb.AppendLine("questions: " + Store.Data.Questions.Count);

                sb.AppendLine();
                sb.AppendLine("== JSON 往返 ==");
                AppData probe = new AppData();
                probe.Settings.IntervalMinutes = 45;
                probe.Settings.SubjectFilter = "数学";
                Question q1 = Question.CreateNew("数学");
                q1.Text = "求 lim(x→0) sin x / x";
                q1.Answer = "1";
                q1.WrongCount = 3;
                probe.Questions.Add(q1);
                Question q2 = Question.CreateNew("英语");
                q2.Text = "abandon 是什么意思？";
                q2.Options = new List<string> { "放弃", "建造", "喜欢", "比较" };
                q2.CorrectIndex = 0;
                probe.Questions.Add(q2);

                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppData));
                string json;
                using (MemoryStream ms = new MemoryStream())
                {
                    ser.WriteObject(ms, probe);
                    json = Encoding.UTF8.GetString(ms.ToArray());
                }
                sb.AppendLine("jsonBytes: " + Encoding.UTF8.GetByteCount(json));
                sb.AppendLine("jsonHead: " + (json.Length > 160 ? json.Substring(0, 160) : json));

                AppData back;
                using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    back = (AppData)ser.ReadObject(ms);
                }
                bool roundTrip = back != null
                    && back.Settings.IntervalMinutes == 45
                    && back.Questions.Count == 2
                    && back.Questions[0].Text == q1.Text
                    && back.Questions[1].Options.Count == 4
                    && back.Questions[1].CorrectIndex == 0;
                sb.AppendLine("roundTrip: " + roundTrip);
                if (!roundTrip) ok = false;

                sb.AppendLine();
                sb.AppendLine("== 抽题 ==");
                Store.Data.Questions.Clear();
                foreach (Question q in probe.Questions) Store.Data.Questions.Add(q);
                List<Question> picked = Store.Pick(5, "", new Random(1));
                sb.AppendLine("picked: " + picked.Count + "  first: " + picked[0].ShortText);
                if (picked.Count == 0) ok = false;

                sb.AppendLine();
                sb.AppendLine("== 文本导入 ==");
                string sample = "#数学\r\nQ: 1+1=?\r\nA: 2\r\nE: 送分题\r\n\r\nQ: 2*3=?\r\nA: 6\r\n";
                Store.Data.Questions.Clear();
                string info;
                int added = Store.ImportText(sample, true, out info);
                sb.AppendLine("imported: " + added + "  info: " + info);
                if (added != 2) ok = false;

                sb.AppendLine();
                sb.AppendLine("== 落盘往返 ==");
                Store.Save();
                Store.Load();
                sb.AppendLine("reloaded questions: " + Store.Data.Questions.Count);
                if (Store.Data.Questions.Count != 2) ok = false;
                if (Store.LastLoadOk == false) { ok = false; sb.AppendLine("load err: " + Store.LastError); }
            }
            catch (Exception ex)
            {
                ok = false;
                sb.AppendLine("EXCEPTION: " + ex);
            }
            finally
            {
                try { if (File.Exists(probePath)) File.Delete(probePath); } catch { }
            }

            sb.AppendLine();
            sb.AppendLine(ok ? "RESULT: PASS" : "RESULT: FAIL");

            string outPath = Path.Combine(Path.GetTempPath(), "mistakelock_selftest.txt");
            try { File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(false)); }
            catch { }
        }
    }

    /// <summary>托盘常驻 + 可选主窗口的启动上下文。</summary>
    internal class AppContext : ApplicationContext
    {
        private readonly MainForm _main;

        public AppContext(MainForm main, bool silent, bool lockNow)
        {
            _main = main;

            if (silent)
            {
                // 用 Opacity=0 走一遍 Show 创建窗口句柄，再藏起来（不闪）
                main.ShowInTaskbar = false;
                main.Opacity = 0;
                main.Show();
                main.Hide();
                main.Opacity = 1;
            }
            else
            {
                main.Show();
            }

            if (lockNow || Store.Data.Settings.LockOnStartup)
            {
                try
                {
                    if (main.IsHandleCreated)
                        main.BeginInvoke(new Action(delegate { try { main.LockNowFromStartup(); } catch { } }));
                }
                catch { }
            }
        }
    }
}
