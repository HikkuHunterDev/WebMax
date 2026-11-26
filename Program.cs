using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WebMax
{
    static class Program
    {
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);
        
        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);
        
        private const int SW_RESTORE = 9;
        private const int WM_COPYDATA = 0x004A;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        [STAThread]
        static void Main(string[] args)
        {
            // Уникальное имя мьютекса (поменяй на своё уникальное)
            const string mutexName = "WebMax_SingleInstance_Mutex_{B4E2D8A1-3F5C-4E8D-9A2B-1C7F8E6D4B3A}";
            bool createdNew;
            
            using (Mutex mutex = new Mutex(true, mutexName, out createdNew))
            {
                if (createdNew)
                {
                    // Первый экземпляр — запускаем приложение
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    
                    var form = new Form1();
                    
                    // Если файл передан через аргументы при первом запуске
                    if (args.Length > 0 && File.Exists(args[0]))
                    {
                        form.SetFileToOpen(args[0]);
                    }
                    
                    Application.Run(form);
                }
                else
                {
                    // Второй экземпляр — находим и активируем существующее окно
                    IntPtr hWnd = FindWindow(null, "WebMax");
                    if (hWnd != IntPtr.Zero)
                    {
                        // Разворачиваем окно если оно свернуто
                        ShowWindow(hWnd, SW_RESTORE);
                        SetForegroundWindow(hWnd);
                        
                        // Передаём путь к файлу
                        if (args.Length > 0 && File.Exists(args[0]))
                        {
                            SendFilePath(hWnd, args[0]);
                        }
                    }
                }
            }
        }

        private static void SendFilePath(IntPtr hWnd, string filePath)
        {
            var chars = (filePath.Length + 1) * sizeof(char);
            var ptr = Marshal.StringToHGlobalUni(filePath);
            try
            {
                var cds = new COPYDATASTRUCT
                {
                    dwData = (IntPtr)1,
                    cbData = chars,
                    lpData = ptr
                };
                SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ref cds);
            }
            finally
            {
                Marshal.FreeHGlobal(ptr);
            }
        }
    }
}
