using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace mEmEmE_Act.Game.Charts
{
    /// <summary>
    /// A loaded .me4 package.
    ///
    /// A .me4 file is a plain zip archive that has simply been renamed:
    ///     chart.me3   the chart (exactly the existing .me3 syntax)
    ///     music.wav   the track (.mp3 / .ogg also accepted)
    ///     art.png     cover art (square)
    ///     data.txt    metadata, one value per line: title / artist / charter / illustrator
    ///
    /// The contents are extracted into a per-package cache folder so the rest of the game can keep
    /// loading audio and images from ordinary file paths — no need to teach every loader about zip.
    /// Extraction is skipped when the cache is already up to date.
    /// </summary>
    public sealed class Me4Package
    {
        /// <summary>Metadata lines, in data.txt order. Missing lines become empty strings.</summary>
        public string Title { get; private set; } = "";
        public string Artist { get; private set; } = "";
        public string Charter { get; private set; } = "";
        public string Illustrator { get; private set; } = "";

        /// <summary>Full path to the extracted chart file.</summary>
        public string ChartPath { get; private set; }

        /// <summary>Full path to the extracted track, or null when the package has no playable audio.</summary>
        public string AudioPath { get; private set; }

        /// <summary>Full path to the extracted cover art, or null when absent.</summary>
        public string ArtPath { get; private set; }

        private static readonly string[] AudioNames = { "music.wav", "music.mp3", "music.ogg" };

        private Me4Package() { }

        /// <summary>
        /// Extracts the package into its cache folder and reads its contents.
        /// Returns null if the file cannot be read as a package.
        /// </summary>
        public static Me4Package Load(string me4Path)
        {
            if (string.IsNullOrEmpty(me4Path) || !File.Exists(me4Path))
                return null;

            try
            {
                var package = new Me4Package();
                var cacheDir = CacheDirFor(me4Path);

                // Re-extract only when the archive is newer than the cache folder.
                if (!Directory.Exists(cacheDir) ||
                    Directory.GetLastWriteTimeUtc(cacheDir) < File.GetLastWriteTimeUtc(me4Path))
                {
                    Extract(me4Path, cacheDir);
                }

                package.ChartPath = Path.Combine(cacheDir, "chart.me3");
                package.ArtPath = Path.Combine(cacheDir, "art.png");

                if (!File.Exists(package.ChartPath))
                    return null;

                if (!File.Exists(package.ArtPath))
                    package.ArtPath = null;

                foreach (var name in AudioNames)
                {
                    var candidate = Path.Combine(cacheDir, name);

                    if (File.Exists(candidate))
                    {
                        package.AudioPath = candidate;
                        break;
                    }
                }

                package.ReadMetadata(Path.Combine(cacheDir, "data.txt"));

                return package;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[me4] 无法读取 {me4Path}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads data.txt: title / artist / charter / illustrator, one per line.
        /// Fewer lines (or blank ones) are allowed; anything missing stays empty.
        /// </summary>
        private void ReadMetadata(string dataPath)
        {
            if (!File.Exists(dataPath))
                return;

            var lines = File.ReadAllLines(dataPath);

            Title = At(lines, 0);
            Artist = At(lines, 1);
            Charter = At(lines, 2);
            Illustrator = At(lines, 3);

            static string At(string[] lines, int i)
                => i < lines.Length ? lines[i].Trim() : "";
        }

        /// <summary>
        /// Cache folder for a package: named after the file, so two packages never collide,
        /// and tied to its timestamp so editing the .me4 forces a re-extract.
        /// </summary>
        private static string CacheDirFor(string me4Path)
        {
            var name = Path.GetFileNameWithoutExtension(me4Path);

            // Strip characters that are not valid in a folder name.
            foreach (var c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');

            var stamp = File.GetLastWriteTimeUtc(me4Path).Ticks;

            var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(root, "mEmEmE_Act", "cache", $"{name}_{stamp}");
        }

        private static void Extract(string me4Path, string cacheDir)
        {
            // Clear first so a stale file cannot survive a re-extract.
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, recursive: true);

            Directory.CreateDirectory(cacheDir);

            using var archive = ZipFile.OpenRead(me4Path);

            foreach (var entry in archive.Entries)
            {
                // Directory entries carry an empty Name.
                if (string.IsNullOrEmpty(entry.Name))
                    continue;

                // Flatten: only the file name is used, so nothing can escape the cache folder.
                var target = Path.Combine(cacheDir, Path.GetFileName(entry.Name));

                entry.ExtractToFile(target, overwrite: true);
            }
        }
    }
}
