using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using MinecraftLaunch.Modules.Models.Download;
using MinecraftLaunch.Modules.Toolkits;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using wonderlab.Class.AppData;
using wonderlab.Class.Enum;
using wonderlab.Class.Utils;
using wonderlab.control.Controls.Bar;
using wonderlab.control.Controls.Dialog;
using wonderlab.ViewModels.Windows;
using wonderlab.Views.Pages;
using static wonderlab.control.Controls.Bar.MessageTipsBar;

namespace wonderlab.Views.Windows {
    public partial class MainWindow : Window {
        public double WindowHeight, WindowWidth;

        public bool IsOpen, CanParallax;

        public static MainWindowViewModel ViewModel { get; set; }

        public MainWindow() {
            Initialized += DataInitialized;

            InitializeComponent();
            AddHandler(DragDrop.DropEvent, DropAction);
            try {
                ThemeUtils.Init();
                WindowWidth = Width;
                WindowHeight = Height;

                Closed += (_, x) => {
                    JsonUtils.WriteLaunchInfoJson();
                    JsonUtils.WriteLauncherInfoJson();
                };

                PointerMoved += OnPointerMoved;
                PropertyChanged += OnPropertyChanged;

                close.Click += (_, _) => Close();
                Mini.Click += (_, _) => WindowState = WindowState.Minimized;
                DialogHost.MainDialog.CustomButtonClick += (_, _) => DialogHost.MainDialog.HideDialog();
                DialogHost.MainDialog.CloseButtonClick += (_, _) => Close();
                NotificationCenterButton.Click += (_, _) => NotificationCenter.Open();
                JsonUtils.CreateLaunchInfoJson();
                JsonUtils.CreateLauncherInfoJson();
            }
            catch (Exception ex) {
                $"初始化信息失败，错误信息如下：{ex.Message}".ShowMessage("Error");
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e) {
            if (CanParallax) {
                Point position = e.GetPosition(BackgroundImage);
                int xOffset = 50, yOffset = 50;

                double num = BackgroundImage.DesiredSize.Height - position.X / xOffset - BackgroundImage.DesiredSize.Height;
                double num2 = BackgroundImage.DesiredSize.Width - position.Y / yOffset - BackgroundImage.DesiredSize.Width;

                if (!(BackgroundImage.RenderTransform is TranslateTransform)) {
                    BackgroundImage.RenderTransform = new TranslateTransform(num, num2);
                    return;
                }

                TranslateTransform translateTransform = (TranslateTransform)BackgroundImage.RenderTransform;
                if (xOffset > 0) {
                    translateTransform.X = num;
                }

                if (yOffset > 0) {
                    translateTransform.Y = num2;
                }
            } else {
                BackgroundImage.RenderTransform = new TranslateTransform(0, 0);
            }
        }

        private async void DataInitialized(object? sender, EventArgs e) {
            await Task.Delay(500);
            try {
                await Task.Run(async () => {
                    CacheResources.Accounts = (await AccountUtils.GetAsync(true)
                                                                 .ToListAsync())
                                                                 .ToObservableCollection();
                });

                if (GlobalResources.LauncherData.CurrentDownloadAPI is DownloadApiType.Mcbbs) {
                    APIManager.Current = APIManager.Mcbbs;
                } else if (GlobalResources.LauncherData.CurrentDownloadAPI is DownloadApiType.Bmcl) {
                    APIManager.Current = APIManager.Bmcl;
                } else if (GlobalResources.LauncherData.CurrentDownloadAPI is DownloadApiType.Mojang) {
                    APIManager.Current = APIManager.Mojang;
                } else {
                    GlobalResources.LauncherData.CurrentDownloadAPI = DownloadApiType.Mojang;
                }

                BackgroundImage.IsVisible = GlobalResources.LauncherData.BakgroundType is "图片背景";
                ThemeUtils.SetAccentColor(GlobalResources.LauncherData.AccentColor);
                CanParallax = GlobalResources.LauncherData.ParallaxType is not "无";

                if (BackgroundImage.IsVisible && !string.IsNullOrEmpty(GlobalResources.LauncherData.ImagePath)) {
                    BackgroundImage.Source = new Bitmap(GlobalResources.LauncherData.ImagePath);
                }

                CacheResources.GetWebModpackInfoData();
            }
            catch {
                if (GlobalResources.LauncherData.IsNull()) {
                    GlobalResources.LauncherData = new();
                }
            }
        }

        private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e) {
            if (e.Property == HeightProperty) {
                if (ViewModel.CurrentPage is HomePage) {
                    if (HomePage.ViewModel.Isopen) {
                        HomePage.ViewModel.PanelHeight = Height - 180;
                    }
                }

                WindowHeight = e.NewValue!.ToDouble();
            } else if (e.Property == WidthProperty) {
                WindowWidth = e.NewValue!.ToDouble();
            }
        }

        private async void WindowsInitialized(object? sender, EventArgs e) {
            await Task.Delay(500);
            
            try {
                Drop.PointerPressed += OnPointerPressed;
                TopInfoBar.PointerPressed += OnPointerPressed;
                TopInfoBar1.PointerPressed += OnPointerPressed;
                TopInfoBar2.PointerPressed += OnPointerPressed;

                var result = await UpdateUtils.GetLatestVersionInfoAsync();

                if (result is not null && UpdateUtils.Index.Latest.Replace(".","").ToInt32() > UpdateUtils.LocalVersion.Replace(".", "").ToInt32() && SystemUtils.IsWindows) {
                    DialogHost.UpdateDialog.Message = string.Join("\n",result.UpdateMessage);
                    DialogHost.UpdateDialog.Author = $"于 {result.Date}  发布，发行分支：{GlobalResources.LauncherData.IssuingBranch}";
                    DialogPage.ViewModel.info = result;
                    await Task.Delay(1000);
                    DialogHost.UpdateDialog.ShowDialog();
                }
            }
            catch (Exception ex) {
                $"{ex.Message}".ShowMessage();
            }
        }
        
        private void OnPointerPressed(object? sender, PointerPressedEventArgs e) {
            try {
                BeginMoveDrag(e);
            }
            catch (Exception) {
                "错误，在拖动窗口时遇到了点小麻烦".ShowMessage();
            }
        }

        public async void DropAction(object? sender, DragEventArgs e) {
            try {
                "检测到拖入！".ShowLog();
                var result = e.Data.GetFiles();

                if (!result.IsNull() && !result.Any()) {
                    return;
                }

                if (result!.Count() > 1) {
                    "一次只能拖入一个文件".ShowMessage("提示");
                    return;
                }

                "开始分析文件类型，此过程会持续两到三秒，请耐心等待".ShowMessage("提示");
                var file = result!.FirstOrDefault()!.ToFile();
                if (!(file.Name.EndsWith(".zip") || file.Name.EndsWith(".mrpack"))) {
                    $"WonderLab 未能确定文件 {file.Name} 的格式应当执行的相关操作".ShowMessage("错误");
                    return;
                }
                var type = ModpacksUtils.ModpacksTypeAnalysis(file.FullName);
                if (type is ModpacksType.Unknown)
                {
                    "未知整合包类型".ShowMessage();
                    return;
                }
                await ModpacksUtils.ModpacksInstallAsync(file.FullName, type);
            }
            catch (Exception) {
                "Fuck".ShowLog(LogLevel.Error);
            }
        }

        public void ShowInfoBar(string title, string message, HideOfRunAction action) {
            tipBarView.Add(title,message);
        }

        public void ShowInfoBar(string title, string message) {
            Dispatcher.UIThread.Post(() => {
                tipBarView.Add(title, message);
            }, DispatcherPriority.Background);
        }

        public async void Navigation(UserControl control) {
            try {
                Page.Opacity = 0;
                await Task.Delay(200);
                Page.Content = control;
                await Task.Delay(150);
                Page.Opacity = 1;
            }
            catch (Exception) { }
        }

        public async void CloseTopBar() {
            TopBar1.Margin = new(0, -150, 0, 0);
            await Task.Delay(50);
            TopBar2.Margin = new(0, -100, 0, 0);
            await Task.Delay(50);
            TopBar3.Margin = new(0, -100, 0, 0);
        }

        public async void OpenTopBar() {
            TopBar3.Margin = new(0);
            await Task.Delay(50);
            TopBar2.Margin = new(0);
            await Task.Delay(50);
            TopBar1.Margin = new(0);
        }
    }
}


