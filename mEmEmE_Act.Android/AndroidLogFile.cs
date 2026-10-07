using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Provider;

namespace mEmEmE_Act.Android
{
    /// <summary>
    /// Writes startup and crash information somewhere a human can actually reach on the phone.
    ///
    /// On Android the game's own log lives in the app's private data directory, which needs adb (or
    /// root) to read — useless when all you have is the phone. So anything logged here is appended to
    /// "Download/mememeact-log.txt", reachable from the file manager, and to the app's external files
    /// directory as a second copy in case MediaStore is refused.
    ///
    /// This exists purely for diagnosing a device that crashes on launch; it is not part of the game.
    /// Note that inside namespace mEmEmE_Act.Android a qualified name such as Android.OS.Build does NOT
    /// resolve to the platform type — "Android" matches the enclosing namespace first — so platform
    /// types are reached through usings or global:: instead.
    /// </summary>
    internal static class AndroidLogFile
    {
        private const string file_name = "mememeact-log.txt";

        /// <summary>MediaStore relative path of the public Downloads folder.</summary>
        private const string downloads_relative_path = "Download";

        private static readonly object write_lock = new object();

        public static void Append(string text)
        {
            lock (write_lock)
            {
                writeViaMediaStore(text);
                writeToExternalFiles(text);
            }
        }

        /// <summary>Public Download folder — what the phone's file manager shows.</summary>
        private static void writeViaMediaStore(string text)
        {
            try
            {
                if (Build.VERSION.SdkInt < BuildVersionCodes.Q)
                    return;

                var resolver = Application.Context.ContentResolver;

                if (resolver == null)
                    return;

                var values = new ContentValues();
                values.Put(MediaStore.IMediaColumns.DisplayName, file_name);
                values.Put(MediaStore.IMediaColumns.MimeType, "text/plain");
                values.Put(MediaStore.IMediaColumns.RelativePath, downloads_relative_path);

                var uri = resolver.Insert(MediaStore.Downloads.ExternalContentUri, values);

                if (uri == null)
                    return;

                using var stream = resolver.OpenOutputStream(uri, "wa");
                using var writer = new StreamWriter(stream);

                writer.Write(text);
                writer.Flush();
            }
            catch
            {
            }
        }

        /// <summary>App-specific external directory: /sdcard/Android/data/&lt;package&gt;/files/</summary>
        private static void writeToExternalFiles(string text)
        {
            try
            {
                var dir = Application.Context.GetExternalFilesDir(null);

                if (dir == null)
                    return;

                Directory.CreateDirectory(dir.AbsolutePath);

                File.AppendAllText(Path.Combine(dir.AbsolutePath, file_name), text);
            }
            catch
            {
            }
        }
    }
}
