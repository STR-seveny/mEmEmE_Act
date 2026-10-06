using System;
using System.Collections.Generic;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace mEmEmE_Act.Game
{
    public partial class BugNote : CompositeDrawable
    {
        /// <summary>Font size of the REV / SPT label text. The glyphs overflow the note itself; that is intentional.</summary>
        private const float LabelFontSize = 35f;

        /// <summary>Transparent margin (in pixels) left around the label texture.</summary>
        private const int LabelPadding = 4;

        private static readonly object fontLock = new object();
        private static Font labelFont;

        /// <summary>
        /// Label texture cache: each label is rendered only once, after which every bug note of the same type shares it.
        ///
        /// Without the cache, every bug note would have to run "load font + ImageSharp render + GPU upload" when it
        /// appears, which is heavy work on the main thread — the first bug note to show up would visibly hitch.
        /// </summary>
        private static readonly Dictionary<string, Texture> labelTextures = new Dictionary<string, Texture>();

        public double HitTime { get; set; }
        public float Speed { get; set; } = 0.5f;
        public float JudgementY { get; set; } = 500;
        public string BugType { get; set; }
        public Colour4 BugColour { get; set; } = Colour4.Magenta;

        private Playfield playfield;
        private bool triggered;

        private IRenderer renderer;
        private Texture labelTexture;

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            this.renderer = renderer;

            Size = new Vector2(150, 15);
            Origin = Anchor.TopCentre;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = BugColour,
                },
            };
        }

        /// <summary>
        /// Only known bug types get the centre label drawn.
        /// If the chart names something else (e.g. XXX), the note still falls normally but triggers nothing and shows
        /// no text, so a "colour block with no label" is itself the hint that this bug type was misspelled.
        /// </summary>
        private static bool hasLabel(string type)
            => type == "REV" || type == "SPT";

        protected override void LoadComplete()
        {
            base.LoadComplete();

            labelTexture = getLabelTexture(BugType);

            if (labelTexture != null)
            {
                AddInternal(new Sprite
                {
                    Texture = labelTexture,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(labelTexture.DisplayWidth, labelTexture.DisplayHeight),
                });
            }
        }

        /// <summary>
        /// Prewarm at chart load: build the textures for the common labels up front.
        /// That way a bug note actually appearing costs nothing and will not hitch.
        /// </summary>
        public static void PrewarmLabels(IRenderer renderer)
        {
            if (renderer == null) return;

            getLabelTexture("REV", renderer);
            getLabelTexture("SPT", renderer);
        }

        private Texture getLabelTexture(string type) => getLabelTexture(type, renderer);

        /// <summary>Get the label texture (reuse the cache if there is one, render only if there is not).</summary>
        private static Texture getLabelTexture(string type, IRenderer renderer)
        {
            if (string.IsNullOrEmpty(type) || !hasLabel(type) || renderer == null)
                return null;

            lock (fontLock)
            {
                if (labelTextures.TryGetValue(type, out var cached) && cached != null)
                    return cached;

                var texture = createLabelTexture(type, renderer);

                if (texture != null)
                    labelTextures[type] = texture;

                return texture;
            }
        }

        private static Texture createLabelTexture(string type, IRenderer renderer)
        {
            try
            {
                lock (fontLock)
                {
                    if (labelFont == null)
                    {
                        var fontPath = Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            "Resources", "Fonts", "SourceHanSansSC-Regular.otf");

                        if (!File.Exists(fontPath))
                            return null;

                        var collection = new FontCollection();
                        var family = collection.Add(fontPath);
                        labelFont = family.CreateFont(LabelFontSize);
                    }
                }

                // Measure once from (0,0) to get the glyph's true bounds relative to the draw origin.
                // This step matters: a glyph's left and top bounds are usually not 0, and without
                // accounting for them the text is drawn too far down and to the right, with an uneven ring of blank space around it.
                var measureOptions = new RichTextOptions(labelFont) { Origin = PointF.Empty };
                var bounds = TextMeasurer.Measure(type, measureOptions).Bounds;

                int width = (int)Math.Ceiling(bounds.Width) + LabelPadding * 2;
                int height = (int)Math.Ceiling(bounds.Height) + LabelPadding * 2;

                // Move the draw origin so that the glyph lands exactly on (LabelPadding, LabelPadding)
                var drawOptions = new RichTextOptions(labelFont)
                {
                    Origin = new PointF(LabelPadding - bounds.Left, LabelPadding - bounds.Top)
                };

                var image = new Image<Rgba32>(width, height);
                image.Mutate(ctx =>
                {
                    ctx.Paint(canvas =>
                    {
                        canvas.DrawText(drawOptions, type, Brushes.Solid(Color.White), pen: null);
                    });
                });

                var texture = renderer.CreateTexture(width, height, manualMipmaps: false);
                texture.SetData(new TextureUpload(image));
                return texture;
            }
            catch
            {
                return null;
            }
        }

        public void AttachPlayfield(Playfield pf) => playfield = pf;

        public void MoveTo(Vector2 target) => Position = target;

        protected override void Update()
        {
            base.Update();

            if (playfield == null) return;

            double chartTime = playfield.CurrentChartTime;
            double timeUntilHit = HitTime - chartTime;
            Y = JudgementY - (float)(timeUntilHit * Speed);

            // The top edge reaches the topmost edge of the judgement zone (that is, its trigger point):
            // fire the effect, then **immediately remove it** — a bug note is not a judgeable note,
            // so once it reaches the judgement zone it must not keep drifting downwards.
            if (!triggered && timeUntilHit <= 0)
            {
                triggered = true;
                playfield.ToggleBug(BugType);
                Expire();
                return;
            }

            Alpha = 1;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            // Note: labelTexture is a globally shared cache and must **not** be disposed here,
            // otherwise once the first bug note leaves, later notes of the same type would have no label.
        }
    }
}
