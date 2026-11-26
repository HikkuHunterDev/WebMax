#nullable enable
using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Web.WebView2.WinForms;
using Microsoft.Web.WebView2.Core;

namespace WebMax
{
    public partial class Form1 : Form
    {
        private WebView2? webView = null!;
        private NotifyIcon? trayIcon = null!;
        private ContextMenuStrip? trayMenu = null!;
        
        // ДОБАВЛЕНО: поле для хранения файла, если WebView ещё не инициализирован
        private string? pendingFile = null;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_OLD = 19;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWA_TEXT_COLOR = 36;
        private const int WM_COPYDATA = 0x004A;
        private const int WM_APPCOMMAND = 0x0319;
        private const int APPCOMMAND_BROWSER_BACKWARD = 1;
        private const int APPCOMMAND_BROWSER_FORWARD = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct COPYDATASTRUCT
        {
            public IntPtr dwData;
            public int cbData;
            public IntPtr lpData;
        }

        public Form1()
        {
            InitializeComponent();
            InitializeForm();
            CreateTrayIcon();
            InitializeWebView();
        }

        private void InitializeForm()
        {
            this.Text = "WebMax";
            this.Size = new Size(1280, 800);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.Sizable;
            this.BackColor = Color.FromArgb(32, 32, 32);
            this.MinimumSize = new Size(800, 600);
            this.KeyPreview = true;
            
            // При закрытии - сворачиваем в трей
            this.FormClosing += Form1_FormClosing;
            
            try
            {
                using var stream = GetEmbeddedResource("ico.ico");
                if (stream != null)
                {
                    this.Icon = new Icon(stream);
                }
            }
            catch { }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplySystemTitleBarTheme();
        }

        private void ApplySystemTitleBarTheme()
        {
            if (!IsHandleCreated) return;

            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dark, sizeof(int));
                DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, ref dark, sizeof(int));

                int caption = ColorToColorRef(Color.FromArgb(32, 32, 32));
                int text = ColorToColorRef(Color.FromArgb(240, 240, 240));
                DwmSetWindowAttribute(Handle, DWMWA_CAPTION_COLOR, ref caption, sizeof(int));
                DwmSetWindowAttribute(Handle, DWMWA_TEXT_COLOR, ref text, sizeof(int));
            }
            catch
            {
                // На старых версиях Windows DWM-атрибуты могут быть недоступны.
            }
        }

        private static int ColorToColorRef(Color color) => color.R | (color.G << 8) | (color.B << 16);

        private void CreateTrayIcon()
        {
            trayMenu = new ContextMenuStrip();
            
            var showItem = new ToolStripMenuItem("Открыть WebMax");
            showItem.Click += (s, e) => ShowFromTray();
            showItem.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            
            var separator = new ToolStripSeparator();
            
            var exitItem = new ToolStripMenuItem("Закрыть");
            exitItem.Click += (s, e) => ExitApplication();
            
            trayMenu.Items.Add(showItem);
            trayMenu.Items.Add(separator);
            trayMenu.Items.Add(exitItem);

            trayIcon = new NotifyIcon
            {
                Text = "WebMax",
                ContextMenuStrip = trayMenu,
                Visible = false
            };

            try
            {
                using var stream = GetEmbeddedResource("ico.ico");
                if (stream != null)
                {
                    trayIcon.Icon = new Icon(stream);
                }
            }
            catch { }

            trayIcon.DoubleClick += (s, e) => ShowFromTray();
        }

        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            // При закрытии на крестик - сворачиваем в трей
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                HideToTray();
            }
        }

        private void HideToTray()
        {
            this.Hide();
            if (trayIcon != null)
            {
                trayIcon.Visible = true;
                trayIcon.ShowBalloonTip(3000, "WebMax", "Приложение свернуто в трей", ToolTipIcon.Info);
            }
        }

        private void ShowFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
            }
        }

        private void ExitApplication()
        {
            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }
            
            // Отключаем обработчик чтобы действительно закрыть
            this.FormClosing -= Form1_FormClosing;
            Application.Exit();
        }

        private Stream? GetEmbeddedResource(string name)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourcePath = $"WebMax.{name}";
            return assembly.GetManifestResourceStream(resourcePath);
        }

        private async void InitializeWebView()
        {
            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WebMax",
                "WebView2"
            );

            var env = await CoreWebView2Environment.CreateAsync(
                browserExecutableFolder: null,
                userDataFolder: userDataFolder
            );

            webView = new WebView2
            {
                Dock = DockStyle.Fill
            };
            
            this.Controls.Add(webView);
            webView.BringToFront();
            
            try
            {
                await webView.EnsureCoreWebView2Async(env);

                webView.CoreWebView2.Settings.IsStatusBarEnabled = false;

                webView.CoreWebView2.DocumentTitleChanged += (s, e) =>
                {
                    this.Invoke(new Action(() =>
                    {
                        this.Text = webView.CoreWebView2.DocumentTitle ?? "WebMax";
                    }));
                };

                webView.CoreWebView2.NavigationStarting += (s, e) =>
                {
                };

                webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                };

                webView.CoreWebView2.SourceChanged += (s, e) =>
                {
                };

                webView.CoreWebView2.StatusBarTextChanged += (s, e) =>
                {
                    var text = webView.CoreWebView2.StatusBarText;
                };

                webView.CoreWebView2.NewWindowRequested += (s, e) =>
                {
                    var uri = e.Uri;
                    if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed) &&
                        (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps))
                    {
                        e.Handled = true;
                        webView.CoreWebView2.Navigate(uri);
                    }
                };

                webView.CoreWebView2.PermissionRequested += (s, e) =>
                {
                    // Для мессенджера удобнее автоматически разрешать типовые разрешения.
                    if (e.PermissionKind == CoreWebView2PermissionKind.Notifications ||
                        e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        e.PermissionKind == CoreWebView2PermissionKind.Camera)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                    }
                };

                webView.CoreWebView2.Navigate("https://web.max.ru");
                
                // ДОБАВЛЕНО: если есть файл в очереди - открываем его
                if (!string.IsNullOrEmpty(pendingFile))
                {
                    OpenFileInWebView(pendingFile);
                    pendingFile = null;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки: {ex.Message}", "WebMax", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Alt | Keys.Left) || keyData == Keys.BrowserBack)
            {
                if (webView?.CoreWebView2?.CanGoBack == true) webView.CoreWebView2.GoBack();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Right) || keyData == Keys.BrowserForward)
            {
                if (webView?.CoreWebView2?.CanGoForward == true) webView.CoreWebView2.GoForward();
                return true;
            }

            if (keyData == Keys.F5 || keyData == (Keys.Control | Keys.R))
            {
                webView?.Reload();
                return true;
            }

            if (keyData == (Keys.Alt | Keys.Home))
            {
                webView?.CoreWebView2?.Navigate("https://web.max.ru");
                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        // ============= ДОБАВЛЕННЫЕ МЕТОДЫ ДЛЯ РАБОТЫ С ФАЙЛАМИ =============

        /// <summary>
        /// Публичный метод для передачи файла из Program.cs
        /// </summary>
        public void SetFileToOpen(string filePath)
        {
            pendingFile = filePath;
            
            // Если WebView уже загружен - открываем файл сразу
            if (webView?.CoreWebView2 != null && pendingFile != null)
            {
                OpenFileInWebView(pendingFile);
                pendingFile = null;
            }
        }

        /// <summary>
        /// Обработка сообщений Windows (межпроцессная передача файлов, мышиные кнопки назад/вперёд).
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_COPYDATA)
            {
                try
                {
                    var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(m.LParam);
                    if (cds.lpData != IntPtr.Zero && cds.cbData > 0)
                    {
                        var filePath = Marshal.PtrToStringUni(cds.lpData);
                        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
                        {
                            OpenFileInWebView(filePath);
                        }
                    }
                }
                catch
                {
                    // Игнорируем некорректные межпроцессные сообщения
                }

                base.WndProc(ref m);
                return;
            }

            if (m.Msg == WM_APPCOMMAND)
            {
                var command = (int)((m.LParam.ToInt64() >> 16) & 0xFFF);
                if (command == APPCOMMAND_BROWSER_BACKWARD && webView?.CoreWebView2?.CanGoBack == true)
                {
                    webView.CoreWebView2.GoBack();
                    m.Result = (IntPtr)1;
                    return;
                }

                if (command == APPCOMMAND_BROWSER_FORWARD && webView?.CoreWebView2?.CanGoForward == true)
                {
                    webView.CoreWebView2.GoForward();
                    m.Result = (IntPtr)1;
                    return;
                }
            }
            
            base.WndProc(ref m);
        }

        /// <summary>
        /// Метод для открытия файла в WebView2
        /// </summary>
        private void OpenFileInWebView(string filePath)
        {
            // Показываем окно из трея (разворачиваем)
            ShowFromTray();
            
            // Преобразуем путь в URL и открываем
            string url = new Uri(filePath).AbsoluteUri;
            
            // Ждём, пока WebView2 инициализируется, если ещё не готов
            if (webView?.CoreWebView2 == null)
            {
                pendingFile = filePath;
                return;
            }
            
            webView.CoreWebView2.Navigate(url);
        }
    }
}
