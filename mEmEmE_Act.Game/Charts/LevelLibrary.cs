using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace mEmEmE_Act.Game.Charts
{
    /// <summary>
    /// One available level: where its .me4 lives plus the metadata from its data.txt.
    /// </summary>
    public sealed class LevelInfo
    {
        public string Path { get; init; } = "";
        public string Title { get; init; } = "";
        public string Artist { get; init; } = "";
        public string Charter { get; init; } = "";
        public string Illustrator { get; init; } = "";

        /// <summary>Full path to the extracted cover art, or null when the package has none.</summary>
        public string ArtPath { get; set; }

        /// <summary>
        /// The loaded package. Populated by <see cref="LevelLibrary.Scan"/> so that starting a level
        /// does not have to unpack it a second time.
        /// </summary>
        public Me4Package Package { get; set; }

        /// <summary>Title shown in the list; falls back to the file name when data.txt is missing or blank.</summary>
        public string DisplayTitle
            => string.IsNullOrWhiteSpace(Title) ? System.IO.Path.GetFileNameWithoutExtension(Path) : Title;
    }

    /// <summary>
    /// Finds the .me4 packages available to the game.
    ///
    /// Packages are deliberately not in the repository, so a fresh clone has none — the menu then
    /// simply shows an empty list rather than failing.
    /// </summary>
    public static class LevelLibrary
    {
        /// <summary>Folder that holds levels, next to the executable.</summary>
        public static string LevelsDirectory => System.IO.Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Resources", "Levels");

        /// <summary>
        /// Every level: loose files in the levels folder plus the ones bundled into the assembly.
        /// metadata is read straight from the zip (no extraction needed).
        /// </summary>
        public static List<LevelInfo> Scan()
        {
            var result = new List<LevelInfo>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Loose files first, so a level dropped in by the user wins over a bundled one of the same
            // name. On Android this folder does not exist at all and the loop simply does nothing.
            if (Directory.Exists(LevelsDirectory))
            {
                foreach (var path in Directory.GetFiles(LevelsDirectory, "*.me4"))
                    Add(result, seen, path);
            }

            // Levels embedded in mEmEmE_Act.Game.dll. They are the only source on Android, where there
            // is no levels folder beside the executable — the build output lives inside the app package.
            foreach (var (path, bytes) in GameAssets.Enumerate("Levels"))
            {
                if (!path.EndsWith(".me4", StringComparison.OrdinalIgnoreCase))
                    continue;

                Add(result, seen, ExtractBundled(path, bytes));
            }

            result.Sort((a, b) => string.Compare(a.DisplayTitle, b.DisplayTitle, StringComparison.OrdinalIgnoreCase));

            return result;
        }

        private static void Add(List<LevelInfo> result, HashSet<string> seen, string path)
        {
            if (path == null || !seen.Add(System.IO.Path.GetFileName(path)))
                return;

            var info = Read(path);

            if (info != null)
                result.Add(info);
        }

        /// <summary>
        /// Writes a bundled level out as a real file so it can go through exactly the same zip-reading
        /// and extraction path as a loose one. Rewritten only when its size changes, so this is not a
        /// copy on every launch.
        /// </summary>
        private static string ExtractBundled(string relativePath, byte[] bytes)
        {
            try
            {
                var dir = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "mEmEmE_Act", "bundled");

                Directory.CreateDirectory(dir);

                var file = System.IO.Path.Combine(dir, System.IO.Path.GetFileName(relativePath));

                if (!File.Exists(file) || new FileInfo(file).Length != bytes.Length)
                    File.WriteAllBytes(file, bytes);

                return file;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[levels] 无法写出内置关卡 {relativePath}: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reads one package's metadata, or null when it cannot be read.
        /// Extraction happens here (via Me4Package) so the cover art path is known for the list,
        /// and so starting the level later costs nothing extra.
        /// </summary>
        private static LevelInfo Read(string path)
        {
            try
            {
                // The package holds the chart/audio/art; data.txt is small enough to read directly.
                var package = Me4Package.Load(path);

                if (package == null)
                    return null;

                string[] lines;

                using (var archive = ZipFile.OpenRead(path))
                    lines = ReadDataTxt(archive);

                return new LevelInfo
                {
                    Path = path,
                    Title = At(lines, 0),
                    Artist = At(lines, 1),
                    Charter = At(lines, 2),
                    Illustrator = At(lines, 3),
                    ArtPath = package.ArtPath,
                    Package = package,
                };
            }
            catch (Exception e)
            {
                Console.WriteLine($"[levels] 无法读取 {path}: {e.Message}");
                return null;
            }
        }

        /// <summary>data.txt as lines; empty array when missing.</summary>
        private static string[] ReadDataTxt(ZipArchive archive)
        {
            var entry = archive.GetEntry("data.txt");

            if (entry == null)
                return Array.Empty<string>();

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8);

            var lines = new List<string>();

            while (reader.ReadLine() is { } line)
                lines.Add(line.Trim());

            return lines.ToArray();
        }

        private static string At(string[] lines, int index)
            => index < lines.Length ? lines[index] : "";
    }
}
