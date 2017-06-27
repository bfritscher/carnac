using System;
using System.Reactive.Linq;
using System.Windows;
using Carnac.Logic;
using Carnac.Logic.KeyMonitor;
using Carnac.Logic.Models;
using Carnac.UI;
using Carnac.Utilities;
using SettingsProviderNet;
using Squirrel;
using EventHook;
using EventHook.Hooks;



namespace Carnac
{
    public partial class App
    {
        readonly SettingsProvider settingsProvider;
        readonly IMessageProvider messageProvider;
        readonly PopupSettings settings;
        KeyShowView keyShowView;
        CarnacTrayIcon trayIcon;
        KeysController carnac;

#if !DEBUG
        readonly string carnacUpdateUrl = "https://github.com/Code52/carnac";
#endif

        public App()
        {
            var keyProvider = new KeyProvider(InterceptKeys.Current, new PasswordModeService(), new DesktopLockEventService());
            settingsProvider = new SettingsProvider(new RoamingAppDataStorage("Carnac"));
            settings = settingsProvider.GetSettings<PopupSettings>();
            messageProvider = new MessageProvider(new ShortcutProvider(), keyProvider, settings);
        }


        protected override void OnStartup(StartupEventArgs e)
        {
            // Check if there was instance before this. If there was-close the current one.
            if (ProcessUtilities.ThisProcessIsAlreadyRunning())
            {
                ProcessUtilities.SetFocusToPreviousInstance("Carnac");
                Shutdown();
                return;
            }

            trayIcon = new CarnacTrayIcon();
            trayIcon.OpenPreferences += TrayIconOnOpenPreferences;
            var keyShowViewModel = new KeyShowViewModel(settings);

            keyShowView = new KeyShowView(keyShowViewModel);
            keyShowView.Show();

            carnac = new KeysController(keyShowViewModel.Messages, messageProvider, new ConcurrencyService(), settingsProvider);
            carnac.Start();

            MouseWatcher.OnMouseInput += (s, me) =>
            {
                Dispatcher.Invoke(() =>
                {
                    if (!keyShowView.IsVisible) return;
                    keyShowViewModel.CursorPosition = keyShowView.PointFromScreen(new System.Windows.Point(me.Point.x, me.Point.y));
                    if (me.Message == MouseMessages.WM_LBUTTONUP)
                    {
                        keyShowView.LeftClick();
                    }
                    if (me.Message == MouseMessages.WM_RBUTTONUP)
                    {
                        keyShowView.RightClick();
                    }
                });
            };

            if (settings.ShowMouseClicks)
            {
                MouseWatcher.Start();
            }
            settings.PropertyChanged += (s, se) => {
                switch (se.PropertyName)
                {
                    case "ShowMouseClicks":
                        if (this.settings.ShowMouseClicks) {
                            MouseWatcher.Start();
                        } else {
                            MouseWatcher.Stop();
                        }
                        break;
                }
            };

#if !DEBUG
            Observable
                .Timer(TimeSpan.FromMinutes(5))
                .Subscribe(async x =>
                {
                    try
                    {
                        using (var mgr = UpdateManager.GitHubUpdateManager(carnacUpdateUrl))
                        {
                            await mgr.Result.UpdateApp();
                        }
        }
                    catch
                    {
                        // Do something useful with the exception
                    }
                });
#endif

            base.OnStartup(e);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            trayIcon.Dispose();
            carnac.Dispose();
            MouseWatcher.Stop();
            ProcessUtilities.DestroyMutex();

            base.OnExit(e);
        }

        void TrayIconOnOpenPreferences()
        {
            var preferencesViewModel = new PreferencesViewModel(settingsProvider, new ScreenManager());
            var preferencesView = new PreferencesView(preferencesViewModel);
            preferencesView.Show();
        }
    }
}
