using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Android.App;
using Android.Content.PM;
using Android.OS;
using mEmEmE_Act.Game;
using osu.Framework.Android;
using osu.Framework.Logging;

namespace mEmEmE_Act.Android
{
    /// <summary>
    /// The Android entry point.
    ///
    /// osu!framework's AndroidGameActivity (a subclass of SDLActivity) runs the game loop; the only
    /// thing it asks of us is CreateGame, which is protected abstract on the base class.
    ///
    /// It also mirrors crashes into Download/mememeact-log.txt (see AndroidLogFile). A failure during
    /// startup leaves no trace the phone itself can show — no Java crash means no crash dialog — and the
    /// game's own log file sits in the app's private directory where only adb can reach it.
    ///
    /// Two naming traps, both from living in namespace mEmEmE_Act.Android:
    ///   * a bare "Game" resolves to the sibling namespace mEmEmE_Act.Game, not osu.Framework.Game
    ///     (CS0118) — hence the fully spelled-out return type on CreateGame;
    ///   * "Android.OS.Build" would resolve to mEmEmE_Act.Android.OS.Build, and "Environment" is
    ///     ambiguous between System.Environment and Android.OS.Environment — hence System.Environment.
    ///     osu!lazer writes osu.Framework.Game in its Android head for the same first reason.
    /// </summary>
    [Activity(
        Label = "mEmEmE(Act)",
        MainLauncher = true,
        Theme = "@android:style/Theme.NoTitleBar",
        ScreenOrientation = ScreenOrientation.SensorLandscape,
        ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize
                               | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.UiMode)]
    public class MainActivity : AndroidGameActivity
    {
        protected override void OnCreate(Bundle savedInstanceState)
        {
            report($"{System.Environment.NewLine}===== 启动 {DateTime.Now:yyyy-MM-dd HH:mm:ss} =====" +
                   $"{System.Environment.NewLine}设备: {Build.Manufacturer} {Build.Model}, Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})" +
                   $"{System.Environment.NewLine}ABI: {string.Join(", ", Build.SupportedAbis ?? new List<string>())}");

            AppDomain.CurrentDomain.UnhandledException += (_, e) => report($"[未捕获异常] {e.ExceptionObject}");

            TaskScheduler.UnobservedTaskException += (_, e) => report($"[后台任务异常] {e.Exception}");

            // Only errors and "important" lines are mirrored: a MediaStore insert per log line would be
            // slow, and this file exists to explain a crash, not to duplicate the whole log.
            Logger.NewEntry += entry =>
            {
                if (entry.Level != LogLevel.Error && entry.Level != LogLevel.Important && entry.Exception == null)
                    return;

                report($"[{entry.Level}] {entry.LoggerName}: {entry.Message}" +
                       (entry.Exception != null ? $"{System.Environment.NewLine}{entry.Exception}" : string.Empty));
            };

            base.OnCreate(savedInstanceState);
        }

        private static void report(string text)
            => AndroidLogFile.Append(text + System.Environment.NewLine);

        protected override osu.Framework.Game CreateGame() => new mEmEmE_ActGame();
    }
}
