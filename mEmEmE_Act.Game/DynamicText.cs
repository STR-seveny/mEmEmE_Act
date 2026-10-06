using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Layout;
using osuTK;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace mEmEmE_Act.Game
{
    public partial class DynamicText : CompositeDrawable
    {
        private Font font;
        private string text = "";
        private Texture texture;
        private IRenderer renderer;
        private Sprite sprite;

        private readonly LayoutValue layout = new LayoutValue(
            Invalidation.RequiredParentSizeToFit | Invalidation.DrawSize,
            InvalidationSource.Child
        );

        public float FontSize { get; set; } = 24;

        public DynamicText()
        {
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            AutoSizeAxes = Axes.Both;

            AddLayout(layout);
        }

        public string Text
        {
            get => text;
            set
            {
                if (text == value) return;
                text = value;
                if (font != null && renderer != null)
                    updateTexture();
            }
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            this.renderer = renderer;

            var fontPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "Fonts", "SourceHanSansSC-Regular.otf"
            );
            if (!File.Exists(fontPath))
                throw new FileNotFoundException($"字体文件找不到: {fontPath}");

            var collection = new FontCollection();
            var family = collection.Add(fontPath);
            font = family.CreateFont(FontSize);

            InternalChild = sprite = new Sprite
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.TopLeft,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateTexture();
        }

        private void updateTexture()
        {
            if (font == null || renderer == null || string.IsNullOrEmpty(text))
            {
                texture = null;
                if (sprite != null)
                {
                    sprite.Texture = null;
                    sprite.Size = Vector2.Zero;
                }
                return;
            }

            var options = new RichTextOptions(font)
            {
                Origin = new PointF(5, 5)
            };
            var metrics = TextMeasurer.Measure(text, options);
            var bounds = metrics.Bounds;

            int width = (int)Math.Ceiling(bounds.Width) + 10;
            int height = (int)Math.Ceiling(bounds.Height) + 10;

            var image = new Image<Rgba32>(width, height);
            image.Mutate(ctx =>
            {
                ctx.Paint(canvas =>
                {
                    canvas.DrawText(options, text, Brushes.Solid(Color.White), pen: null);
                });
            });

            texture?.Dispose();
            texture = renderer.CreateTexture(width, height);
            texture.SetData(new TextureUpload(image));

            if (sprite != null)
            {
                sprite.Size = new Vector2(width, height);
                sprite.Texture = texture;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            texture?.Dispose();
        }
    }
}
