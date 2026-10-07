using System.Collections.Generic;
using System.IO;
using mEmEmE_Act.Game.Charts;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Input;

namespace mEmEmE_Act.Game
{
    public partial class MainScreen : Screen, IRequireHighFrequencyMousePosition
    {
        private Playfield playfield;

        /// <summary>All receptors: index 0 is the main body (follows the mouse), index 1 is SPT's mirror clone.</summary>
        private readonly List<Receptor> receptors = new List<Receptor>();

        /// <summary>The "left edge of each band" list, reused every frame (avoids allocating a new one each frame).</summary>
        private readonly List<float> bands = new List<float>();

        /// <summary>The mouse position in screen space. IRequireHighFrequencyMousePosition keeps it refreshed.</summary>
        private Vector2 mousePosition;

        /// <summary>Set when a .me4 package supplied cover art, so the prepare overlay can show it.</summary>
        private Texture coverArt;

        /// <summary>Create a texture from a file on disk, or null when it cannot be read.</summary>
        private static Texture LoadPng(string path, IRenderer renderer)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path) || renderer == null)
                return null;

            try
            {
                var storage = new osu.Framework.Platform.NativeStorage(Path.GetDirectoryName(path));
                var loader = new TextureLoaderStore(
                    new osu.Framework.IO.Stores.StorageBackedResourceStore(storage));
                return new TextureStore(renderer, loader).Get(Path.GetFileName(path));
            }
            catch
            {
                return null;
            }
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    Colour4.FromHex("#666666"),
                    Colour4.FromHex("#a0a0a0")
                )
            });

            // Receptors: index 0 is the main body (follows the mouse); index 1 is the mirror clone that is
            // shown only while SPT is active. Both are added up front and show/hide is controlled via Alpha
            // (SPT is a switch that can be flipped at any time).
            for (int i = 0; i < 2; i++)
            {
                var r = new Receptor { Alpha = 0 };
                AddInternal(r);
                receptors.Add(r);
            }

            AddInternal(playfield = new Playfield());

            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.X,
                Width = 1,
                Height = 50,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                Colour = Colour4.FromHex("#ff5858"),
            });

            var chartsDir = Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "Charts"
            );

            // Prefer a .me4 package: chart + music + art + metadata in one file.
            // Check a Songs folder next to the executable first, then the bundled Charts folder.
            var me4 = FindFirstMe4(Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Songs"))
                      ?? FindFirstMe4(chartsDir);

            string title = null, artist = null, charter = null, illustrator = null;

            if (me4 != null)
            {
                var package = Me4Package.Load(me4);

                if (package != null)
                {
                    playfield.LoadPackage(package);
                    coverArt = LoadPng(package.ArtPath, renderer);

                    title = package.Title;
                    artist = package.Artist;
                    charter = package.Charter;
                    illustrator = package.Illustrator;
                }
            }

            // Fall back to the loose .me3 + Resources\Audio layout.
            if (title == null)
            {
                var chartPath = Path.Combine(chartsDir, "1_1_1.me3");

                if (File.Exists(chartPath))
                {
                    var parser = new ChartParser();
                    playfield.LoadChart(parser.Parse(chartPath), "Audio/1_1_1.mp3");
                }
            }

            // The prepare overlay covers the running game and starts the chart on click.
            AddInternal(new PrepareOverlay(title ?? "", artist ?? "", charter ?? "", illustrator ?? "", coverArt)
            {
                StartRequested = playfield.Begin,
            });
        }

        /// <summary>First .me4 in a folder, or null when the folder is missing or holds none.</summary>
        private static string FindFirstMe4(string folder)
        {
            if (!Directory.Exists(folder))
                return null;

            var found = Directory.GetFiles(folder, "*.me4");
            return found.Length > 0 ? found[0] : null;
        }

        protected override void Update()
        {
            base.Update();

            // Feed the "logical mouse" into chart parameter evaluation (me_x / me_y) every frame.
            // This has to happen in Update (not only in OnMouseMove) because REV / SPT can be flipped by the
            // chart at any time, and at that instant it must change immediately even if the mouse did not move.
            playfield.UpdateMouse(logicalMouse);

            UpdateReceptors();

            // Feed the receptor positions into judgement every frame. Judgement only recognises "where the
            // receptor is right now", so this must be updated continuously rather than only on mouse movement.
            playfield.SetReceptorBands(bandLeftEdges, currentBandWidth);
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            // Only records the "real mouse position"; the logical mouse (including the REV mirror) is computed centrally in Update
            mousePosition = ToLocalSpace(e.ScreenSpaceMousePosition);
            return true;
        }

        /// <summary>
        /// Left mouse button down = one tE click.
        /// The **click position** has to be handed to judgement as well — with a double press (one lane on
        /// each side) judgement relies on it to know which lane you hit, otherwise you get "clicked left,
        /// judged right".
        /// The click position here uses the **logical mouse**: while REV is active the receptor sits at the
        /// mirrored position, so that is the side the player "sees".
        /// </summary>
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button == MouseButton.Left)
                playfield.RegisterClick(logicalMouse);

            return true;
        }

        /// <summary>
        /// Z / X down = one tE click (use the current logical mouse position as the click position).
        /// System key repeat is ignored so that holding the key down is not taken as repeated clicking.
        /// </summary>
        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Key != Key.Z && e.Key != Key.X)
                return true;

            if (!e.Repeat)
                playfield.RegisterClick(logicalMouse);

            return true;
        }

        /// <summary>
        /// Logical mouse = the real mouse position after REV is applied.
        /// While REV is active X is mirrored (the equivalent of 0 - mouse x: screen width - mouse x), Y is
        /// unchanged. The receptor position, the tE click position and me_x inside the chart all use this one
        /// value, guaranteeing that the "position you see" and the "position that is judged" always agree.
        /// </summary>
        private Vector2 logicalMouse
        {
            get
            {
                var m = mousePosition;

                if (playfield != null && playfield.RevActive)
                    m.X = DrawSize.X - m.X;

                return m;
            }
        }

        /// <summary>
        /// Left edge X of each receptor band.
        /// The main body (the first band) follows the logical mouse; while SPT is active there is one extra
        /// **mirror clone** band — it is mirrored about the **screen centre line**: when the mouse is exactly
        /// centred the two bands coincide, and the further off-centre the mouse is, the further the two halves
        /// are pulled apart. (So SPT is the same kind of thing as REV: both are "symmetric / mirrored".)
        /// While SPT is active **each band is half as wide**: one receptor is split into two halves, each only
        /// half as wide as the original.
        /// </summary>
        private List<float> bandLeftEdges
        {
            get
            {
                float width = currentBandWidth;
                float main = logicalMouse.X - width / 2f;

                bands.Clear();
                bands.Add(main);

                if (playfield != null && playfield.SptActive)
                {
                    // Mirror: the main body's left edge is flipped about the screen centre line
                    float mirror = DrawSize.X - main - width;
                    bands.Add(mirror);
                }

                return bands;
            }
        }

        /// <summary>Current width of each receptor band: 150 normally; 75 each once SPT has split it in two.</summary>
        private float currentBandWidth
            => playfield != null && playfield.SptActive
                ? Receptor.ReceptorWidth / 2f
                : Receptor.ReceptorWidth;

        private void UpdateReceptors()
        {
            var edges = bandLeftEdges;
            float width = currentBandWidth;

            for (int i = 0; i < receptors.Count; i++)
            {
                bool active = i < edges.Count;
                var r = receptors[i];

                if (active)
                {
                    r.X = edges[i];
                    r.Width = width;
                }

                // The clone is only displayed while SPT is active
                r.Alpha = active ? 1f : 0f;
            }
        }
    }
}
