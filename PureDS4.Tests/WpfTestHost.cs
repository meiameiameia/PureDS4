using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace DS4WindowsTests
{
    /// <summary>
    /// One STA thread with a running dispatcher and one WPF
    /// <see cref="Application"/> for the whole test assembly.
    ///
    /// WPF allows a single <see cref="Application"/> per AppDomain, and an
    /// <see cref="Application"/> stays bound to the thread that created it.
    /// Every test class that constructs windows or reads application resources
    /// therefore has to share one host thread; a class that spins up its own
    /// either fails to construct a second Application or blocks reaching the
    /// first one across threads.
    /// </summary>
    [TestClass]
    public static class WpfTestHost
    {
        private static readonly TimeSpan OperationTimeout =
            TimeSpan.FromSeconds(30);

        private static Thread hostThread;
        private static Dispatcher hostDispatcher;

        [AssemblyInitialize]
        public static void Start(TestContext context)
        {
            using ManualResetEventSlim ready = new ManualResetEventSlim();

            hostThread = new Thread(() =>
            {
                EnsureApplication();

                hostDispatcher = Dispatcher.CurrentDispatcher;
                ready.Set();
                Dispatcher.Run();
            })
            {
                IsBackground = true,
                Name = "WPF test host",
            };

            hostThread.SetApartmentState(ApartmentState.STA);
            hostThread.Start();

            Assert.IsTrue(ready.Wait(OperationTimeout),
                "The WPF test host thread did not start.");
        }

        [AssemblyCleanup]
        public static void Stop()
        {
            hostDispatcher?.InvokeShutdown();
            hostThread?.Join(OperationTimeout);
        }

        /// <summary>
        /// Runs <paramref name="body"/> on the shared UI thread and rethrows
        /// its failure on the calling test thread.
        /// </summary>
        internal static void Run(Action body)
        {
            Assert.IsNotNull(hostDispatcher,
                "The WPF test host was not started.");

            Exception failure = null;
            hostDispatcher.Invoke(() =>
            {
                try
                {
                    body();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            }, DispatcherPriority.Normal, CancellationToken.None,
                OperationTimeout);

            if (failure != null)
            {
                throw new AssertFailedException(failure.ToString(), failure);
            }
        }

        /// <summary>
        /// The single WPF <see cref="Application"/> the tests share.
        ///
        /// Closing the last window would otherwise shut the application down
        /// and null <see cref="Application.Current"/>, which breaks every UI
        /// test that runs afterwards. Tests that exercise a dialog's accept
        /// path do close windows, so the shutdown mode is pinned explicitly
        /// and the application is recreated if it ever does go away.
        /// </summary>
        /// <summary>
        /// The single WPF <see cref="Application"/> the tests share.
        ///
        /// This is deliberately a plain <see cref="Application"/> and never
        /// <c>DS4WinWPF.App</c>. Constructing the real application subclass on
        /// a thread that then pumps a dispatcher runs Application_Startup: the
        /// product's actual startup, which discovers configuration, writes to
        /// the real %AppData%\PureDS4\Logs, shows modal dialogs, and calls
        /// Shutdown when it fails. A test run must never do any of that.
        /// </summary>
        private static void EnsureApplication()
        {
            if (Application.Current != null)
            {
                return;
            }

            _ = new Application
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown,
            };
        }

        /// <summary>
        /// Merges the application theme dictionaries the windows under test
        /// expect. Call from inside <see cref="Run"/>.
        /// </summary>
        internal static void LoadApplicationThemes(bool replaceExisting)
        {
            Application application = Application.Current;
            Assert.IsNotNull(application,
                "The shared WPF application is gone. A test shut it down.");
            if (replaceExisting)
            {
                application.Resources.MergedDictionaries.Clear();
            }

            foreach (string theme in new[]
            {
                "/PureDS4;component/DS4Forms/Themes/DefaultTheme.xaml",
                "/PureDS4;component/DS4Forms/Themes/Foundation.xaml",
                "/PureDS4;component/DS4Forms/Themes/BridgeShellStyles.xaml",
            })
            {
                LoadDictionary(theme);
            }
        }

        internal static void LoadDictionary(string source)
        {
            ResourceDictionary dictionary = new ResourceDictionary();
            Application.Current.Resources.MergedDictionaries.Add(dictionary);
            dictionary.Source = new Uri(source, UriKind.Relative);
        }
    }
}
