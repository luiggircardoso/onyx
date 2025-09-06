using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace onyx_app
{
    public sealed partial class MainWindow : Window
    {
        private string? selectedFolderPath;
        public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;

            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            var presenter = appWindow.Presenter as OverlappedPresenter;

            appWindow.Resize(new SizeInt32(300, 400));
            presenter.IsResizable = false;
            presenter.IsMaximizable = false;

            if (DownloadProgressBar == null)
            {
                DownloadProgressBar = new ProgressBar
                {
                    Minimum = 0,
                    Maximum = 100,
                    Value = 0
                };
            }
        }
        private async void SetFolderPath(object sender, RoutedEventArgs e)
        {
            var folderPicker = new FolderPicker();

            IntPtr hwnd = WindowNative.GetWindowHandle(this);

            InitializeWithWindow.Initialize(folderPicker, hwnd);

            folderPicker.ViewMode = PickerViewMode.Thumbnail;
            folderPicker.SuggestedStartLocation = PickerLocationId.Desktop;

            StorageFolder folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
            {
                selectedFolderPath = folder.Path; 

                locationPath.Text = selectedFolderPath;
            }
        }
        private void DownloadVideo(object sender, Microsoft.UI.Xaml.RoutedEventArgs e)
        {
            string videoUrl = URLinput.Text;
            if (string.IsNullOrWhiteSpace(videoUrl)) return;

            DownloadButton.IsEnabled = false;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "yt-dlp.exe",
                Arguments = $"-P \"{selectedFolderPath}\" \"-o\" \"%(title)s.%(ext)s\" \"{videoUrl}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
          
            var dispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();

            var progressRegex = new Regex(@"\[download\]\s+(\d+\.\d+)%");

            process.OutputDataReceived += (s, ev) =>
            {
                if (ev.Data != null)
                {
                    Match match = progressRegex.Match(ev.Data);
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, out double progress))
                        {
                            dispatcherQueue.TryEnqueue(() =>
                            {
                                DownloadButton.IsEnabled = false;
                                DownloadProgressBar.Visibility = Visibility.Visible;
                                DownloadProgressBar.Value = progress;
                                
                            });
                        }
                    }
                }
            };

            process.EnableRaisingEvents = true;
            process.Exited += (s, ev) =>
            {
                dispatcherQueue.TryEnqueue(() =>
                {
                    DownloadButton.IsEnabled = true;
                    DownloadProgressBar.Visibility = Visibility.Collapsed;

                    AppNotification notification = new AppNotificationBuilder()
                        .AddText("Download Finished")
                        .AddText("The download was saved to the selected path.")
                        .BuildNotification();

                    AppNotificationManager.Default.Show(notification);

                });
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine(); 
        }
    }
}
