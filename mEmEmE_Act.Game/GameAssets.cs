using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;

namespace mEmEmE_Act.Game
{
    /// <summary>
    /// Where the game's own runtime assets (font, music, hit sound, textures, hit effects) come from.
    ///
    /// They are embedded into mEmEmE_Act.Game.dll rather than read from a Resources folder next to the
    /// executable, because that folder only exists on desktop. On Android (and iOS) there is no such
    /// directory — the build output lives inside the app package — so every path built from
    /// AppDomain.BaseDirectory silently resolves to nothing there, and DynamicText then threw
    /// FileNotFoundException on the missing font, taking the whole game down at startup.
    ///
    /// A loose file still wins when one is present, so a desktop build can swap artwork or music
    /// without rebuilding. Paths are relative to Resources, with forward slashes:
    /// "Textures/START.png", "Sound/menu.mp3", "Fonts/SourceHanSansSC-Regular.otf".
    ///
    /// Manifest names are looked up directly rather than through DllResourceStore: the mapping from a
    /// name like "Textures/START.png" to "mEmEmE_Act.Game.Resources.Textures.START.png" is then right
    /// here to be read instead of being a rule to be trusted.
    /// </summary>
    public static class GameAssets
    {
        private static readonly Assembly assembly = typeof(GameAssets).Assembly;
        private static readonly string resourcePrefix = $"{typeof(GameAssets).Namespace}.Resources.";

        /// <summary>Raw bytes, or null when the asset exists in neither the embedded set nor on disk.</summary>
        public static byte[] Get(string path)
        {
            // Embedded first, deliberately: it is the copy that ships on every platform, so the code
            // path exercised on desktop is the same one Android uses.
            var bytes = GetEmbedded(path);

            if (bytes != null)
                return bytes;

            var file = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Resources", path.Replace('/', Path.DirectorySeparatorChar));

            try
            {
                return File.Exists(file) ? File.ReadAllBytes(file) : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>A readable stream for the asset, or null. The caller owns the stream.</summary>
        public static Stream GetStream(string path)
        {
            var bytes = Get(path);

            return bytes == null ? null : new MemoryStream(bytes, writable: false);
        }

        /// <summary>
        /// Every embedded asset under a folder, as (name relative to Resources, bytes). Used by the
        /// level library, which has to discover what is bundled rather than ask for a known name.
        /// </summary>
        public static IEnumerable<(string Path, byte[] Bytes)> Enumerate(string folder)
        {
            var prefix = resourcePrefix + folder.Trim('/').Replace('/', '.') + ".";

            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal))
                    continue;

                var relative = folder.Trim('/') + "/" + name.Substring(prefix.Length);
                var bytes = ReadManifest(name);

                if (bytes != null)
                    yield return (relative, bytes);
            }
        }

        /// <summary>A texture store over the game's own assets.</summary>
        public static TextureStore CreateTextureStore(IRenderer renderer)
            => new TextureStore(renderer, new TextureLoaderStore(new GameAssetStore()));

        /// <summary>
        /// The asset set as a resource store, for the audio stores (AudioManager.GetTrackStore /
        /// GetSampleStore). A fresh instance per call: those stores take ownership of what they are
        /// handed and disposing a shared one would break every other user of it.
        /// </summary>
        public static IResourceStore<byte[]> CreateResourceStore() => new GameAssetStore();

        private static byte[] GetEmbedded(string path)
            => ReadManifest(resourcePrefix + path.Replace('/', '.'));

        private static byte[] ReadManifest(string manifestName)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(manifestName);

                if (stream == null)
                    return null;

                using var buffer = new MemoryStream();
                stream.CopyTo(buffer);

                return buffer.ToArray();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// The asset set as an osu!framework resource store, so it can back the audio stores
        /// (AudioManager.GetTrackStore / GetSampleStore) and anything else expecting one.
        /// </summary>
        private sealed class GameAssetStore : IResourceStore<byte[]>
        {
            public byte[] Get(string name) => GameAssets.Get(name);

            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(name));

            public Stream GetStream(string name) => GameAssets.GetStream(name);

            public IEnumerable<string> GetAvailableResources()
                => assembly.GetManifestResourceNames().Where(n => n.StartsWith(resourcePrefix, StringComparison.Ordinal));

            public void Dispose()
            {
            }
        }
    }
}
