using System.Windows;
using System.Windows.Threading;

namespace PS5_Controller_Tools
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            DispatcherUnhandledException += App_DispatcherUnhandledException;
            base.OnStartup(e);

            AppLogger.Info(nameof(App), $"Application démarrée. Log: {AppLogger.GetLogFilePath()}");
            AppLogger.Info(nameof(App), $"Dossier des logs: {AppLogger.GetLogDirectoryPath()}");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            AppLogger.Info(nameof(App), "Application fermée.");
            AppDomain.CurrentDomain.UnhandledException -= CurrentDomain_UnhandledException;
            DispatcherUnhandledException -= App_DispatcherUnhandledException;
            base.OnExit(e);
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            AppLogger.Error(nameof(App), "Exception UI non gérée.", e.Exception);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                AppLogger.Error(nameof(App), "Exception domaine non gérée.", ex);
            }
            else
            {
                AppLogger.Error(nameof(App), "Exception domaine non gérée sans objet Exception.");
            }
        }
    }
}
