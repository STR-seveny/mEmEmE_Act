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
        /// Every level in the levels folder, read from each package's data.txt.
        /// metadata is read straight from the zip (no extraction needed).
        /// </summary>
        public static List<LevelInfo> Scan()
        {
            var result = new List<LevelInfo>();

            if (!Directory.Exists(LevelsDirectory))
                return result;

            foreach (var path in Directory.GetFiles(LevelsDirectory, "*.me4"))
            {
                var info = Read(path);

                if (info != null)
                    result.Add(info);
            }

            result.Sort((a, b) => string.Compare(a.DisplayTitle, b.DisplayTitle, StringComparison.OrdinalIgnoreCase));

            return result;
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
