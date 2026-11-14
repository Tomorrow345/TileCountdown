using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Threading;
using Windows.Data.Xml.Dom;
using Windows.Globalization.DateTimeFormatting;
using Windows.Storage;
using Windows.UI.Notifications;
using Windows.UI.StartScreen;
using WinRT.Interop;
using Windows.ApplicationModel;

namespace TileCountdown
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
        
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_DLGMODALFRAME = 0x0001;

        private DateTime _lastUpdateDate = DateTime.Today;


        private string AnnivName = "";
        private string DefaultAN = "元旦";

        private DateTime TargetDate = new DateTime();
        private int DefaultYear = 2027;
        private int DefaultMonth = 1;
        private int DefaultDate = 1; 

        private int RemainDays = 0;
        private string OldOrNew = "";

        private string docPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "TileCountdown", "Anniv.json");

        public MainWindow()
        {
            InitializeComponent();

            UpdateTile();

            if (File.Exists(docPath))
            {
                try
                {
                    string json = File.ReadAllText(docPath);
                    using JsonDocument doc = JsonDocument.Parse(json);

                    AnnivName = doc.RootElement.GetProperty("AnvName").GetString() ?? DefaultAN;
                    string STgtYear = doc.RootElement.GetProperty("TgtYear").GetString() ?? $"{DefaultYear}";
                    string STgtMonth = doc.RootElement.GetProperty("TgtMonth").GetString() ?? $"{DefaultMonth}";
                    string STgtDate = doc.RootElement.GetProperty("TgtDate").GetString() ?? $"{DefaultDate}";
                    string SSURefresh = doc.RootElement.GetProperty("StartUpRefresh").GetString() ?? "false";

                    int TgtYear = int.Parse(STgtYear);
                    int TgtMonth = int.Parse(STgtMonth);
                    int TgtDate = int.Parse(STgtDate);

                    this.Loaded += async (s, e) =>
                    {
                        try
                        {
                            bool SURefresh = (SSURefresh == "true");

                            if (SURefresh)
                            {
                                UpdateTile();
                                Application.Current.Shutdown();
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("初始化失败: " + ex.Message);
                        }
                    };

                    TargetDate = new DateTime(TgtYear, TgtMonth, TgtDate);
                    AddAnniv(AnnivName, TargetDate);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"日期报错！请检查Anniv.json！\n{e}");
                }
            
            } else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(docPath) ?? "");

                AddAnniv(DefaultAN, new DateTime(DefaultYear, DefaultMonth, DefaultDate));

                File.WriteAllText(docPath, 
                    "{" +
                    $"  \"AnvName\": \"{DefaultAN}\", " +
                    $"  \"TgtYear\": \"{DefaultYear}\", " +
                    $"  \"TgtMonth\": \"{DefaultMonth}\", " +
                    $"  \"TgtDate\": \"{DefaultDate}\", " +
                    $"  \"StartUpRefresh\": \"false\"" +
                    "}");
            }

            this.Activated += (s, e) =>
            {
                UpdateTile();
            };

            SettingsButton.Click += async (s, e) => Process.Start("notepad.exe", docPath);
        }

        private async Task<bool> IsStartupEnabled()
        {
            try
            {
                var task = await StartupTask.GetAsync("TileCountdownStartup");

                return task.State == StartupTaskState.Enabled;
            }
            catch
            {
                return false;
            }
        }

        private void AddAnniv(string annivName, DateTime targetDate)
        {
            AnnivName = annivName;

            DateTime today = DateTime.Today;

            if (today > targetDate)
            {
                OldOrNew = "O";
                TimeSpan difference = today - targetDate;
                RemainDays = difference.Days;
            } else if (today < targetDate)
            {
                OldOrNew = "N";
                TimeSpan difference = targetDate - today;
                RemainDays = difference.Days;
            } else
            {
                OldOrNew = "M";
            }
        }

        private void UpdateTile()
        {
            try
            {
                if (DateTime.Today != _lastUpdateDate)
                {
                    AddAnniv(AnnivName, TargetDate);
                    _lastUpdateDate = DateTime.Today; 
                }

                var tileMedium = TileUpdateManager.GetTemplateContent(TileTemplateType.TileSquare150x150Text04);
                var tileWide = TileUpdateManager.GetTemplateContent(TileTemplateType.TileWideText04);

                var mediumBinding = tileMedium.GetElementsByTagName("binding")[0];
                var clonedBinding = tileWide.ImportNode(mediumBinding, true);

                var wideVisual = tileWide.GetElementsByTagName("visual")[0];
                wideVisual.AppendChild(clonedBinding);

                var allTextNodes = tileWide.GetElementsByTagName("text");

                if (allTextNodes.Length >= 1)
                {
                    var wideTextNode = (XmlElement)allTextNodes[0];
                    if (OldOrNew == "N")
                    {
                        wideTextNode.InnerText = $"距离{AnnivName} 还有\n{RemainDays} 天";
                    } else if (OldOrNew == "O")
                    {
                        wideTextNode.InnerText = $"距离{AnnivName} 过去了\n{RemainDays} 天";
                    } else
                    {
                        wideTextNode.InnerText = $"今天\n{AnnivName}";
                    }

                    wideTextNode.SetAttribute("hint-style", "subtitle");
                }

                if (allTextNodes.Length >= 2)
                {
                    var mediumTextNode = (XmlElement)allTextNodes[1];
                    if (OldOrNew == "N")
                    {
                        mediumTextNode.InnerText = $"{AnnivName} 还有\n{RemainDays}";
                    } else if (OldOrNew == "O")
                    {
                        mediumTextNode.InnerText = $"{AnnivName} 过去了\n{RemainDays}";
                    } else
                    {
                        mediumTextNode.InnerText = $"今天\n{AnnivName}";
                    }

                    mediumTextNode.SetAttribute("hint-style", "base");
                }

                var notification = new TileNotification(tileWide);
                TileUpdateManager.CreateTileUpdaterForApplication().Update(notification);
            }
            catch (Exception ex)
            {
                MessageBox.Show("更新失败: " + ex.Message);
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            var hwnd = new WindowInteropHelper(this).Handle;

            int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);

            SetWindowLong(hwnd, GWL_EXSTYLE, exStyle | WS_EX_DLGMODALFRAME);

            SendMessage(hwnd, 0x0027, IntPtr.Zero, IntPtr.Zero);
        }

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);
    }
}