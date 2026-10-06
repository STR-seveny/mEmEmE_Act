using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace mEmEmE_Act.Game
{
    public partial class Receptor : CompositeDrawable
    {
        // The sizes are geometry: they are used while drawing and also while judging, so they are exposed as
        // constants, guaranteeing that the "judgement zone you see" and the "judgement zone used for judging"
        // are always the exact same piece of screen.
        public const float ReceptorWidth = 150;
        public const float GoodHeight = 30;
        public const float BadHeight = 30;
        public const float BottomOffset = 150;

        /// <summary>Total height of the judgement zone (yellow + blue).</summary>
        public const float ZoneHeight = GoodHeight + BadHeight;

        /// <summary>Number of pixels from the highest edge of the judgement zone to the bottom edge of the parent container.</summary>
        public const float ZoneTopFromBottom = BottomOffset + ZoneHeight;

        private static readonly Colour4 ReceptorColour = Colour4.FromHex("#8b8b8b");
        private static readonly Colour4 GoodColour = Colour4.FromHex("#f6e075");
        private static readonly Colour4 BadColour = Colour4.FromHex("#3d8fff");

        public Receptor()
        {
            Width = ReceptorWidth;
            RelativeSizeAxes = Axes.Y;
            Height = 1;
            Anchor = Anchor.TopLeft;
            Origin = Anchor.TopLeft;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ReceptorColour,
                },
                new Box
                {
                    // Use a relative width: when SPT splits it into two halves, the judgement bar narrows along
                    // with the receptor. (If ReceptorWidth were hardcoded, the receptor would get narrower but
                    // the judgement bar would still be full width.)
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = GoodHeight,
                    Colour = GoodColour,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Y = -BottomOffset - BadHeight,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Width = 1,
                    Height = BadHeight,
                    Colour = BadColour,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Y = -BottomOffset,
                },
            };
        }
    }
}
