using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using FirewallControl.App.Services;
using FirewallControl.App.Views;
using FirewallControl.Core.Interfaces;
using FirewallControl.Infrastructure.Database;
using FirewallControl.Infrastructure.Repositories;
using FirewallControl.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;

namespace FirewallControl.App;

public partial class App : Application
{
    private static int _crispDefaultsRegistered;

    static App()
    {
        EnsureCrispWindowDefaults();
    }

    public static void EnsureCrispWindowDefaults()
    {
        if (Interlocked.Exchange(ref _crispDefaultsRegistered, 1) != 0)
            return;

        EventManager.RegisterClassHandler(
            typeof(Window),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window w)
                    AppIconHelper.ApplyCrispRendering(w);
            }),
            handledEventsToo: true);
    }

    public IServiceProvider ServiceProvider { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        EnsureCrispWindowDefaults();
        AppIconHelper.InitializeProcessAppUserModelId();
        base.OnStartup(e);

        DispatcherUnhandledException += (_, args) =>
        {
            TryWriteCrashLog("DispatcherUnhandledException", args.Exception);
            MessageBox.Show(
                $"Error controlado en la aplicación:\n{args.Exception.Message}",
                "Sefiros POS (.NET 10)",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            args.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            TryWriteCrashLog("UnobservedTaskException", args.Exception);
            args.SetObserved();
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                TryWriteCrashLog("AppDomainUnhandledException", ex);
        };

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        var configService = ServiceProvider.GetRequiredService<IConfigService>();
        ThemeService.ApplyTheme(
            configService.Preferences.TemaAtelier,
            configService.Preferences.TemaOscuroConfiguracion,
            configService.Preferences.PaletaColores);
        LocalizationService.ApplyLanguage(configService.Preferences.Idioma);

        ShowSplashThenMainWindow();
    }

    private void ShowSplashThenMainWindow()
    {
        var splash = new SefirosSplashWindow();
        MainWindow? preloadedMain = null;
        bool splashFinished = false;
        bool splashAlreadyClosed = false;

        MainWindow EnsureMainWindowCreated()
            => preloadedMain ??= ServiceProvider.GetRequiredService<MainWindow>();

        void FinishSplashOnce()
        {
            if (splashFinished)
                return;
            splashFinished = true;

            var mainWindow = EnsureMainWindowCreated();
            MainWindow = mainWindow;
            ShutdownMode = ShutdownMode.OnMainWindowClose;

            // Cerrar la ventana splash antes del primer Show/Layout de MainWindow evita que
            // permanezca abierta en segundo plano mientras MainWindow construye su árbol visual.
            if (!splashAlreadyClosed)
            {
                try
                {
                    if (splash.IsVisible)
                        splash.Hide();
                    if (splash.IsLoaded)
                        splash.Close();
                }
                catch { }
            }

            if (!mainWindow.IsVisible)
                mainWindow.Show();
            mainWindow.Activate();
        }

        splash.PresentationReadyToTransition += () => _ = EnsureMainWindowCreated();
        splash.PresentationCompleted += FinishSplashOnce;
        splash.Closed += (_, _) =>
        {
            splashAlreadyClosed = true;
            FinishSplashOnce();
        };

        splash.Show();

        Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
        {
            if (!splashFinished && preloadedMain == null)
                _ = EnsureMainWindowCreated();
        }));
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IConfigService, ConfigService>();
        services.AddSingleton<ILicenseService, VfpsLicenseService>();
        services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
        services.AddSingleton<IVfpDbfService, VfpDbfService>();
        services.AddSingleton<ITicketPrintService, TicketPrintService>();
        services.AddSingleton<ICargaRapidaIaService, CargaRapidaIaService>();

        services.AddSingleton<IProductRepository, ProductRepository>();
        services.AddSingleton<ISalesRepository, SalesRepository>();
        services.AddSingleton<ICustomerRepository, CustomerRepository>();
        services.AddSingleton<IPurchaseRepository, PurchaseRepository>();
        services.AddSingleton<ICashRepository, CashRepository>();
        services.AddSingleton<ISystemRepository, SystemRepository>();
        services.AddSingleton<IStage3Repository, Stage3Repository>();

        services.AddSingleton<IWindowManagerService, WindowManagerService>();
        services.AddSingleton<MainWindow>();
    }

    private static void TryWriteCrashLog(string source, Exception ex)
    {
        try
        {
            string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sefiros_error.log");
            string entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {ex}{Environment.NewLine}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch
        {
            // No bloquear ni fallar si la carpeta es de solo lectura
        }
    }
}
