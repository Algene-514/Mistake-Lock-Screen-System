using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace MistakeLocker
{
    /// <summary>
    /// 低级键盘钩子：锁屏期间屏蔽 Alt+Tab / Win / Alt+F4 / Ctrl+Esc 等逃逸键。
    /// 注意：必须保证一定 Dispose，否则键盘会被一直吞掉。
    /// </summary>
    public sealed class HotkeyBlocker : IDisposable
    {
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("user32.dll")]
        private static extern short GetAsyncKeyState(int vKey);

        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        private IntPtr _hookId = IntPtr.Zero;
        private LowLevelKeyboardProc _proc;
        private readonly HashSet<int> _blockedAlone = new HashSet<int>();
        private readonly HashSet<int> _blockedWithAlt = new HashSet<int>();

        public bool IsActive { get { return _hookId != IntPtr.Zero; } }

        private const int VK_TAB = 0x09;
        private const int VK_ESCAPE = 0x1B;
        private const int VK_F4 = 0x73;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;
        private const int VK_APPS = 0x5D;
        private const int VK_MENU = 0x12;
        private const int VK_CONTROL = 0x11;
        private const int VK_SHIFT = 0x10;

        public void Start()
        {
            if (_hookId != IntPtr.Zero) return;

            // 单独按下就吞掉：Win 键、右键菜单键
            _blockedAlone.Add(VK_LWIN);
            _blockedAlone.Add(VK_RWIN);
            _blockedAlone.Add(VK_APPS);

            // 需要配合 Alt 才吞：Tab / F4 / Esc
            _blockedWithAlt.Add(VK_TAB);
            _blockedWithAlt.Add(VK_F4);
            _blockedWithAlt.Add(VK_ESCAPE);

            _proc = HookCallback;
            IntPtr hMod = GetModuleHandle(null);
            _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hMod, 0);
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0)
                {
                    int msg = wParam.ToInt32();
                    if (msg == WM_KEYDOWN || msg == WM_SYSKEYDOWN)
                    {
                        int vk = Marshal.ReadInt32(lParam);
                        if (ShouldBlock(vk)) return (IntPtr)1;
                    }
                }
            }
            catch { }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        private bool ShouldBlock(int vk)
        {
            if (_blockedAlone.Contains(vk)) return true;

            bool altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
            bool ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

            if (altDown && _blockedWithAlt.Contains(vk)) return true;

            // Ctrl+Esc = 开始菜单
            if (ctrlDown && vk == VK_ESCAPE) return true;

            // Alt+Space = 窗口菜单
            if (altDown && vk == 0x20) return true;

            return false;
        }

        public void Dispose()
        {
            if (_hookId != IntPtr.Zero)
            {
                try { UnhookWindowsHookEx(_hookId); } catch { }
                _hookId = IntPtr.Zero;
            }
            _proc = null;
        }
    }

    /// <summary>任务栏显隐 / 置顶等窗口 API。</summary>
    public static class Win32
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string className, string windowName);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool SetProcessDPIAware();

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public static void HideTaskbar()
        {
            try
            {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                if (h != IntPtr.Zero) ShowWindow(h, SW_HIDE);
                IntPtr h2 = FindWindow("Shell_SecondaryTrayWnd", null);
                if (h2 != IntPtr.Zero) ShowWindow(h2, SW_HIDE);
            }
            catch { }
        }

        public static void ShowTaskbar()
        {
            try
            {
                IntPtr h = FindWindow("Shell_TrayWnd", null);
                if (h != IntPtr.Zero) ShowWindow(h, SW_SHOW);
                IntPtr h2 = FindWindow("Shell_SecondaryTrayWnd", null);
                if (h2 != IntPtr.Zero) ShowWindow(h2, SW_SHOW);
            }
            catch { }
        }
    }
}
