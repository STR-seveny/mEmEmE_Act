using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osuTK;

namespace mEmEmE_Act.Game
{
    /// <summary>
    /// The "prepare" overlay: covers the running game while the chart waits for the player.
    ///
    /// It is not a separate Screen — the game itself keeps running underneath (notes loaded, receptor
    /// following the mouse). Clicking anywhere dismisses the overlay and the chart starts.
    ///
    /// Left: title / artist / charter / illustrator from the package metadata.
    /// Right: the cover art in a frame.
    /// </summary>
    public partial class PrepareOverlay : CompositeDrawable
    {
        /// <summary>Raised once, when the player clicks to begin.</summary>
        public Action StartRequested;

        private readonly string title;
        private readonly string artist;
        private readonly string charter;
        private readonly string illustrator;
        private readonly Texture art;

        /// <summary>
        /// Backdrop opacity. Low enough that the running game (receptor, judgement zone, notes)
        /// shows through, high enough that the metadata text stays readable.
        /// </summary>
        private const float BackdropAlpha = 0.55f;

        private const float TextColumnCentreX = 300f;
        private const float ArtCentreX = 1030f;
        private const float ArtSize = 420f;

        public PrepareOverlay(string title, string artist, string charter, string illustrator, Texture art)
        {
            this.title = title;
            this.artist = artist;
            this.charter = charter;
            this.illustrator = illustrator;
            this.art = art;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var children = new System.Collections.Generic.List<Drawable>
            {
                // Backdrop
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("#3a3a3a"),
                    Alpha = BackdropAlpha,
                },
            };

            // ---- Left column: metadata ----
            children.Add(Line(title, 0, 52, TextColumnCentreX, 250));
            children.Add(Line($"曲师: {artist}", 1, 30, TextColumnCentreX, 320));
            children.Add(Line($"谱师: {charter}", 2, 30, TextColumnCentreX, 365));
            children.Add(Line($"曲绘: {illustrator}", 3, 30, TextColumnCentreX, 410));
            children.Add(Line("按下鼠标来开始", 4, 30, TextColumnCentreX, 500));

            // ---- Right column: cover art in a frame ----
            if (art != null)
            {
                children.Add(FramedArt());
            }

            InternalChildren = children.ToArray();
        }

        /// <summary>One metadata line, centred horizontally on the left column.</summary>
        private static Drawable Line(string content, int index, float fontSize, float centreX, float centreY)
        {
            if (string.IsNullOrEmpty(content))
                return new Container { Alpha = 0 };

            return new DynamicText
            {
                Text = content,
                FontSize = fontSize,
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(centreX, centreY),
            };
        }

        /// <summary>The cover art with a light border around it, as in Rx's prepare screen.</summary>
        private Drawable FramedArt()
        {
            const float border = 6f;
            const float padding = 10f;
            float outer = ArtSize + (padding + border) * 2;

            return new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Position = new Vector2(ArtCentreX, 340),
                Size = new Vector2(outer),

                Children = new Drawable[]
                {
                    // White border
                    new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                    },

                    // Inner gap so the border reads as a frame
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(ArtSize + padding * 2),
                        Colour = Colour4.FromHex("#3a3a3a"),
                    },

                    // The art itself, kept square
                    new Sprite
                    {
                        Texture = art,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(ArtSize),
                        FillMode = FillMode.Fit,
                    },
                },
            };
        }

        protected override bool OnMouseDown(MouseDownEvent e)
        {

            if (e.Button != osuTK.Input.MouseButton.Left)
                return true;

            // Fire once, then let the game take over.
            var callback = StartRequested;
            StartRequested = null;
            callback?.Invoke();

            Expire();

            return true;
        }


        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // The texture belongs to the texture store, which manages its lifetime.
            // Disposing it here would break any other user of the same store.
        }
    }
}
