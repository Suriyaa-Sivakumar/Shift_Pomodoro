using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using FlowWindows.WindowsIntegration;

namespace FlowWindows_App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    private MainWindow? _mainWindow;
    private SingleInstanceManager? _singleInstanceManager;

    public App()
    {
        InitializeComponent();

        UnhandledException += (sender, e) =>
        {
            LogCrash("XAML UnhandledException", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
        {
            LogCrash("AppDomain UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (sender, e) =>
        {
            LogCrash("TaskScheduler UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    private static void LogCrash(string category, Exception? ex)
    {
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logDir = Path.Combine(appData, "FlowWindows", "Logs");
            Directory.CreateDirectory(logDir);
            var msg = $"[{DateTime.UtcNow:O}] [{category}] {ex}{Environment.NewLine}";
            File.AppendAllText(Path.Combine(logDir, "crash.log"), msg);
            File.AppendAllText(Path.Combine(logDir, "app.log"), msg);
        }
        catch { }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        LogCrash("App.OnLaunched invoked", null);
        try
        {
            _singleInstanceManager = new SingleInstanceManager();
            bool isFirstInstance = _singleInstanceManager.TryAcquire(() =>
            {
                // Callback invoked when a second instance tries to launch
                _mainWindow?.DispatcherQueue.TryEnqueue(() =>
                {
                    _mainWindow.ShowAndActivateMainWindow();
                });
            });

            LogCrash($"SingleInstanceManager result: isFirstInstance = {isFirstInstance}", null);

            if (!isFirstInstance)
            {
                LogCrash("Exiting secondary instance.", null);
                Environment.Exit(0);
                return;
            }

            LogCrash("Instantiating MainWindow...", null);
            _mainWindow = new MainWindow();
            LogCrash("Activating MainWindow...", null);
            _mainWindow.Activate();
            _mainWindow.ShowAndActivateMainWindow();
            LogCrash("MainWindow.Activate() succeeded.", null);
        }
        catch (Exception ex)
        {
            LogCrash("OnLaunched Exception", ex);
            throw;
        }
    }
}
