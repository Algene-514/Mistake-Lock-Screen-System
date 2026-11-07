using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

namespace MistakeLocker
{
    // ============================ 数据模型 ============================

    [DataContract]
    public class Question
    {
        [DataMember] public string Id = "";
        [DataMember] public string Subject = "";
        [DataMember] public string Text = "";
        [DataMember] public string Answer = "";
        [DataMember] public string Explanation = "";
        [DataMember] public List<string> Options = new List<string>();
        [DataMember] public int CorrectIndex = -1;
        [DataMember] public int ShownCount;
        [DataMember] public int WrongCount;
        [DataMember] public int RightCount;
        [DataMember] public int Level;              // 0 生疏 / 1 一般 / 2 熟练
        [DataMember] public bool Mastered;
        [DataMember] public string Tags = "";

        // 用 ticks 存时间：DateTime.MinValue 直接序列化成 JSON 会抛异常
        [DataMember(Name = "LastShownUtc")] private long _lastShownUtcTicks;

        public DateTime LastShownUtc
        {
            get
            {
                if (_lastShownUtcTicks <= 0) return DateTime.MinValue;
                try { return new DateTime(_lastShownUtcTicks, DateTimeKind.Utc); }
                catch { return DateTime.MinValue; }
            }
            set
            {
                if (value == DateTime.MinValue || value.Ticks <= 0) _lastShownUtcTicks = 0;
                else _lastShownUtcTicks = value.Ticks;
            }
        }

        public bool IsChoice
        {
            get
            {
                return Options != null && Options.Count >= 2
                       && CorrectIndex >= 0 && CorrectIndex < Options.Count;
            }
        }

        public string ShortText
        {
            get
            {
                string t = (Text ?? "").Replace("\r", " ").Replace("\n", " ").Trim();
                if (t.Length > 80) return t.Substring(0, 80) + "…";
                return t;
            }
        }

        public string LevelText
        {
            get
            {
                if (Mastered) return "已掌握";
                if (Level <= 0) return "生疏";
                if (Level == 1) return "一般";
                return "熟练";
            }
        }

        /// <summary>出题权重：错得越多、越久没出现，越容易被抽中。</summary>
        public double Weight()
        {
            if (Mastered) return 0.15;
            double w = 1.0 + WrongCount * 2.0 + (RightCount > 0 ? 0.0 : 0.5);
            // 越久没出现，权重越高（最多 +4）
            double days = 30;
            if (LastShownUtc != DateTime.MinValue)
                days = (DateTime.UtcNow - LastShownUtc).TotalDays;
            if (days > 30) days = 30;
            w += days / 30.0 * 4.0;
            // 已答对过的轻微降权
            w -= Level * 0.7;
            return Math.Max(0.1, w);
        }

        public static Question CreateNew(string subject)
        {
            Question q = new Question();
            q.Id = Guid.NewGuid().ToString("N");
            q.Subject = subject ?? "";
            return q;
        }
    }

    [DataContract]
    public class Settings
    {
        [DataMember] public int IntervalMinutes = 30;      // 每隔多少分钟锁一次
        [DataMember] public int QuestionsPerLock = 1;      // 每次要过几道题
        [DataMember] public int MinStaySeconds = 0;        // 做完后最少停留几秒才能解锁
        [DataMember] public bool BlockHotkeys = true;      // 屏蔽 Alt+Tab / Win 等
        [DataMember] public bool HideTaskbar = true;       // 锁屏时隐藏任务栏
        [DataMember] public bool SoundOnLock = true;       // 锁定提示音
        [DataMember] public bool AutoStart;                // 开机自启
        [DataMember] public bool LockOnStartup;            // 启动即锁一次
        [DataMember] public bool TimerEnabled = true;      // 定时锁屏总开关
        [DataMember] public string SubjectFilter = "";     // 只考某科目，空=全部

        [DataMember(Name = "LastLockUtc")] private long _lastLockUtcTicks;

        public DateTime LastLockUtc
        {
            get
            {
                if (_lastLockUtcTicks <= 0) return DateTime.MinValue;
                try { return new DateTime(_lastLockUtcTicks, DateTimeKind.Utc); }
                catch { return DateTime.MinValue; }
            }
            set
            {
                if (value == DateTime.MinValue || value.Ticks <= 0) _lastLockUtcTicks = 0;
                else _lastLockUtcTicks = value.Ticks;
            }
        }

        [DataMember] public int EscapeCount;               // 中途强退次数
        [DataMember] public int TotalAnswered;
        [DataMember] public int TotalWrong;
    }

    [DataContract]
    public class AppData
    {
        [DataMember] public Settings Settings = new Settings();
        [DataMember] public List<Question> Questions = new List<Question>();
    }

    // ============================ 存储 ============================

    public static class Store
    {
        public static string DirPath = "";
        public static string DataPath = "";
        public static AppData Data = new AppData();
        public static string LastError = "";
        public static bool LastLoadOk = true;

        private static readonly object _sync = new object();

        public static void Init()
        {
            List<string> candidates = new List<string>();

            // 1) 环境变量显式指定（调试 / 便携使用）
            string custom = Environment.GetEnvironmentVariable("MISTAKELOCKER_DATA");
            if (!string.IsNullOrWhiteSpace(custom)) candidates.Add(custom);

            // 2) 标准位置 %APPDATA%\错题锁屏
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrWhiteSpace(appData)) candidates.Add(Path.Combine(appData, "错题锁屏"));

            // 3) exe 旁边的 data 目录（绿色版 / 漫游目录不可写时）
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                if (!string.IsNullOrWhiteSpace(baseDir)) candidates.Add(Path.Combine(baseDir, "data"));
            }
            catch { }

            // 4) 临时目录兜底
            try { candidates.Add(Path.Combine(Path.GetTempPath(), "错题锁屏")); } catch { }

            foreach (string dir in candidates)
            {
                if (TryUseDir(dir)) return;
            }

            // 全都不行也只能选第一个，让调用方看到错误
            DirPath = candidates.Count > 0 ? candidates[0] : Path.GetTempPath();
            DataPath = Path.Combine(DirPath, "questions.json");
        }

        private static bool TryUseDir(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".writetest");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                DirPath = dir;
                DataPath = Path.Combine(dir, "questions.json");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void Load()
        {
            LastError = "";
            LastLoadOk = true;
            try
            {
                if (!File.Exists(DataPath))
                {
                    Data = new AppData();
                    Save();
                    return;
                }
                byte[] raw = File.ReadAllBytes(DataPath);
                if (raw.Length == 0) { Data = new AppData(); return; }

                DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppData));
                using (MemoryStream ms = new MemoryStream(raw))
                {
                    AppData d = (AppData)ser.ReadObject(ms);
                    if (d == null) throw new Exception("题库文件内容为空");
                    if (d.Settings == null) d.Settings = new Settings();
                    if (d.Questions == null) d.Questions = new List<Question>();
                    foreach (Question q in d.Questions)
                    {
                        if (q.Options == null) q.Options = new List<string>();
                        if (q.Id == null || q.Id.Length == 0) q.Id = Guid.NewGuid().ToString("N");
                        if (q.Text == null) q.Text = "";
                        if (q.Answer == null) q.Answer = "";
                        if (q.Explanation == null) q.Explanation = "";
                        if (q.Subject == null) q.Subject = "";
                        if (q.Tags == null) q.Tags = "";
                    }
                    Data = d;
                }
                if (Data.Settings.LastLockUtc == DateTime.MinValue)
                    Data.Settings.LastLockUtc = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                LastLoadOk = false;
                LastError = ex.Message;
                // 备份损坏的文件，避免直接覆盖丢失
                try
                {
                    if (File.Exists(DataPath))
                    {
                        string bak = DataPath + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                        File.Copy(DataPath, bak, true);
                    }
                }
                catch { }
                Data = new AppData();
            }
        }

        public static bool Save()
        {
            lock (_sync)
            {
                try
                {
                    if (Data == null) Data = new AppData();
                    DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(AppData));
                    string json;
                    using (MemoryStream ms = new MemoryStream())
                    {
                        ser.WriteObject(ms, Data);
                        json = Encoding.UTF8.GetString(ms.ToArray());
                    }
                    string tmp = DataPath + ".tmp";
                    File.WriteAllText(tmp, json, new UTF8Encoding(false));
                    if (File.Exists(DataPath)) File.Delete(DataPath);
                    File.Move(tmp, DataPath);
                    return true;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    return false;
                }
            }
        }

        public static List<string> Subjects()
        {
            List<string> list = new List<string>();
            foreach (Question q in Data.Questions)
            {
                string s = (q.Subject ?? "").Trim();
                if (s.Length > 0 && !list.Contains(s)) list.Add(s);
            }
            list.Sort(StringComparer.CurrentCulture);
            return list;
        }

        /// <summary>按权重随机抽题。</summary>
        public static List<Question> Pick(int count, string subject, Random rnd)
        {
            if (rnd == null) rnd = new Random();

            Func<Question, bool> match = delegate(Question q)
            {
                if (string.IsNullOrEmpty(subject)) return true;
                return string.Equals((q.Subject ?? "").Trim(), subject, StringComparison.OrdinalIgnoreCase);
            };

            List<Question> pool = Data.Questions.Where(match).ToList();
            if (pool.Count == 0) return new List<Question>();

            List<Question> picked = new List<Question>();
            List<Question> remain = new List<Question>(pool);

            while (picked.Count < count && remain.Count > 0)
            {
                double total = 0;
                foreach (Question q in remain) total += q.Weight();

                double r = rnd.NextDouble() * total;
                int idx = remain.Count - 1;
                double acc = 0;
                for (int i = 0; i < remain.Count; i++)
                {
                    acc += remain[i].Weight();
                    if (acc >= r) { idx = i; break; }
                }
                picked.Add(remain[idx]);
                remain.RemoveAt(idx);
            }
            return picked;
        }

        // ---------------- 文本导入 / 导出 ----------------
        // 格式：
        //   #数学              <- 切换科目
        //   Q: 题目内容         <- 也可以写 问：
        //   A: 答案             <- 也可以写 答：
        //   E: 解析             <- 也可以写 解：
        // 其余非空行会接在上一个字段后面（支持多行题目）

        public static int ImportText(string content, bool append, out string info)
        {
            int added = 0;
            int bad = 0;
            List<Question> parsed = new List<Question>();

            string subject = "";
            Question cur = null;
            int field = 0; // 0 none, 1 text, 2 answer, 3 explanation

            string[] lines = content.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.TrimEnd();
                string trimmed = line.Trim();

                if (trimmed.Length == 0) continue;

                if (trimmed.StartsWith("#"))
                {
                    subject = trimmed.TrimStart('#').Trim();
                    continue;
                }

                string head = null;
                string rest = null;

                string[] prefixes = { "Q:", "q:", "问:", "问：", "题目:", "题目：", "问题:", "问题：" };
                foreach (string p in prefixes)
                {
                    if (trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                    {
                        head = "Q";
                        rest = trimmed.Substring(p.Length).Trim();
                        break;
                    }
                }

                if (head == null)
                {
                    string[] answerPrefixes = { "A:", "a:", "答:", "答：", "答案:", "答案：" };
                    foreach (string p in answerPrefixes)
                    {
                        if (trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        {
                            head = "A";
                            rest = trimmed.Substring(p.Length).Trim();
                            break;
                        }
                    }
                }

                if (head == null)
                {
                    string[] expPrefixes = { "E:", "e:", "解:", "解：", "解析:", "解析：", "解释:", "解释：" };
                    foreach (string p in expPrefixes)
                    {
                        if (trimmed.StartsWith(p, StringComparison.OrdinalIgnoreCase))
                        {
                            head = "E";
                            rest = trimmed.Substring(p.Length).Trim();
                            break;
                        }
                    }
                }

                if (head == "Q")
                {
                    if (cur != null && cur.Text.Trim().Length > 0) parsed.Add(cur);
                    else if (cur != null) bad++;
                    cur = Question.CreateNew(subject);
                    cur.Text = rest ?? "";
                    field = 1;
                }
                else if (head == "A")
                {
                    if (cur == null) { cur = Question.CreateNew(subject); bad++; }
                    cur.Answer = rest ?? "";
                    field = 2;
                }
                else if (head == "E")
                {
                    if (cur == null) { cur = Question.CreateNew(subject); bad++; }
                    cur.Explanation = rest ?? "";
                    field = 3;
                }
                else
                {
                    // 续行
                    if (cur == null) { cur = Question.CreateNew(subject); bad++; }
                    if (field == 1) cur.Text = cur.Text + "\n" + trimmed;
                    else if (field == 2) cur.Answer = cur.Answer + "\n" + trimmed;
                    else if (field == 3) cur.Explanation = cur.Explanation + "\n" + trimmed;
                    else cur.Text = cur.Text + "\n" + trimmed;
                }
            }
            if (cur != null)
            {
                if (cur.Text.Trim().Length > 0) parsed.Add(cur);
                else bad++;
            }

            if (!append) Data.Questions.Clear();
            foreach (Question q in parsed)
            {
                Data.Questions.Add(q);
                added++;
            }
            Save();

            info = string.Format("导入完成：新增 {0} 道题", added);
            if (bad > 0) info += string.Format("，跳过 {0} 道格式不完整的题", bad);
            return added;
        }

        public static string ExportText()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("# 错题本导出 " + DateTime.Now.ToString("yyyy-MM-dd HH:mm"));
            sb.AppendLine();
            var groups = Data.Questions.GroupBy(delegate(Question q) { return (q.Subject ?? "").Trim().Length == 0 ? "未分类" : q.Subject.Trim(); });
            foreach (var g in groups)
            {
                sb.AppendLine("#" + g.Key);
                foreach (Question q in g)
                {
                    sb.AppendLine("Q: " + (q.Text ?? "").Replace("\n", "\n   "));
                    sb.AppendLine("A: " + (q.Answer ?? "").Replace("\n", "\n   "));
                    if (!string.IsNullOrEmpty(q.Explanation))
                        sb.AppendLine("E: " + q.Explanation.Replace("\n", "\n   "));
                    if (q.IsChoice)
                    {
                        sb.AppendLine("   选项：");
                        for (int i = 0; i < q.Options.Count; i++)
                        {
                            string mark = (i == q.CorrectIndex) ? "  ✔" : "";
                            sb.AppendLine("     " + (char)('A' + i) + ". " + q.Options[i] + mark);
                        }
                    }
                    sb.AppendLine();
                }
            }
            return sb.ToString();
        }
    }
}
