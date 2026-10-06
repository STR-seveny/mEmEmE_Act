using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;

namespace mEmEmE_Act.Game
{
    public enum Zone
    {
        None,
        Yellow,
        Blue,
    }

    public enum NoteResult
    {
        Good,
        Bad,
        Miss,
    }

    /// <summary>
    /// The environment information judging needs. The note computes from this data itself, so it does not need to hold a Playfield reference.
    /// </summary>
    public readonly struct JudgeContext
    {
        public readonly float JudgementY;

        public readonly float RedLineY;

        /// <summary>
        /// Whether this step's trajectory crossed the judgement zone (fallback).
        /// Substepping is normally 1 pixel per step so nothing is missed; but when the game stalls the step length
        /// gets pulled coarser, coarse enough to cross the 60 pixel high judgement zone in a single step, and then
        /// this is what catches it.
        /// </summary>
        public readonly Zone SweptZone;

        /// <summary>
        /// How many receptor bands there are right now: normally 1; 2 while SPT is active (main body + mirror split).
        /// </summary>
        public readonly int BandCount;

        private readonly float band0Centre, band0Width;
        private readonly float band1Centre, band1Width;

        public JudgeContext(float judgementY, float redLineY, Zone sweptZone,
                            float band0Centre, float band0Width,
                            float band1Centre, float band1Width, int bandCount)
        {
            JudgementY = judgementY;
            RedLineY = redLineY;
            SweptZone = sweptZone;

            this.band0Centre = band0Centre;
            this.band0Width = band0Width;
            this.band1Centre = band1Centre;
            this.band1Width = band1Width;
            BandCount = bandCount;
        }

        public float BandCentreX(int i) => i == 0 ? band0Centre : band1Centre;

        public float BandWidth(int i) => i == 0 ? band0Width : band1Width;

        /// <summary>
        /// Half width of receptor band i — the horizontal threshold used for judging.
        /// It is "receptor half width + note half width", so that the two squares just touch (consistent with Rx).
        /// </summary>
        public float BandHalfWidth(int i) => (BandWidth(i) + Note.NoteWidth) / 2f;
    }

    /// <summary>
    /// Note base class, which is also dE:
    ///   touching the yellow or blue zone (horizontally lined up) -> Good; never lining up and reaching missLine -> Miss.
    /// tE / nE each inherit it and override <see cref="Judge"/>.
    /// </summary>
    public partial class Note : CompositeDrawable
    {
        // The sizes are geometric quantities: used when drawing and used when judging as well.
        public const float NoteWidth = 150;
        public const float NoteHeight = 15;

        public int Id { get; set; }
        public string Type { get; set; } = "dE";
        public double HitTime { get; set; }
        public float Speed { get; set; } = 0.5f;
        public float JudgementY { get; set; } = 500;
        public Colour4 NoteColour { get; set; } = Colour4.White;

        /// <summary>
        /// Time base offset (in milliseconds) for the position calculation.
        ///
        /// nE uses it: its judging point is on missLine (lower than the judgement zone), so its position base has to
        /// be shifted forward, making it sit lower and enter the screen earlier at the same instant — which is Rx's
        /// "spawn ahead of time".
        /// Normal notes are 0.
        /// </summary>
        public double TimeBaseOffsetMs { get; set; }

        /// <summary>The previous frame's Y. Judging uses the distance "this frame travelled from PrevY to Y" for pixel by pixel substepping.</summary>
        public float PrevY { get; set; }

        /// <summary>
        /// The current position during substepping. Judging looks at it, not at Y.
        ///
        /// Following Rx's approach: the distance a frame has to travel is split into 1 pixel steps, and at every step
        /// the note is moved to that step's position before testing, so the coordinates used for judging are always
        /// exact integer pixel positions and it can never "skip the judgement zone in one step".
        /// </summary>
        public float PreviewY { get; set; }

        public bool Judged { get; private set; }

        public NoteResult Result { get; private set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Size = new Vector2(NoteWidth, NoteHeight);
            Origin = Anchor.TopCentre;
            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = NoteColour,
            };
        }

        public void AttachPlayfield(Playfield pf) => playfield = pf;

        private Playfield playfield;

        /// <summary>Screen Y of the note's top edge (front tip). Judging always looks at it.</summary>
        public float TipY => PreviewY + NoteHeight;

        /// <summary>
        /// Horizontal judging: whether the note overlaps the visible area of "some receptor band".
        ///
        /// You cannot just look at a single receptor centre — with SPT active the receptor is split into two halves
        /// (main body + mirror split), the note touching either half counts as touching, and it has to be computed per
        /// half that is pressed.
        ///
        /// When several bands cover it, the one **nearest the note** is taken (usually only one of them covers it).
        /// </summary>
        public bool OverlapsReceptor(in JudgeContext ctx)
        {
            if (ctx.BandCount <= 0)
                return false;

            float bestDistance = float.MaxValue;

            for (int i = 0; i < ctx.BandCount; i++)
            {
                float centre = ctx.BandCentreX(i);
                float d = Math.Abs(Position.X - centre);

                if (d <= ctx.BandHalfWidth(i) && d < bestDistance)
                    bestDistance = d;
            }

            return bestDistance != float.MaxValue;
        }

        private float bandOffset(in JudgeContext ctx, int band)
            => Math.Abs(Position.X - ctx.BandCentreX(band));

        /// <summary>
        /// Vertical + horizontal + "which receptor band it is pressed on" computed together.
        ///
        /// **Whichever band it lands on judges it**: with SPT active there are two (main body + mirror split), and the
        /// one nearest the note that it can reach horizontally is taken.
        /// </summary>
        protected Zone EffectiveZone(in JudgeContext ctx)
        {
            float tip = TipY;
            float yellowTop = ctx.JudgementY;
            float yellowBottom = yellowTop + Receptor.GoodHeight;
            float blueTop = yellowBottom;
            float blueBottom = blueTop + Receptor.BadHeight;

            // Not pressed into the judgement zone vertically -> fall back to the sweep (a make-up judgement for when a stall makes one step cross the judgement zone)
            if (tip < yellowTop || tip > blueBottom)
                return OverlapsReceptor(ctx) ? ctx.SweptZone : Zone.None;

            int band = nearestBand(ctx);

            if (band < 0)
            {
                // The current position touches no band, but this step's trajectory may have crossed it (stall / skipped frame)
                return OverlapsReceptor(ctx) ? ctx.SweptZone : Zone.None;
            }

            return tip <= yellowBottom ? Zone.Yellow : Zone.Blue;
        }

        private int nearestBand(in JudgeContext ctx)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < ctx.BandCount; i++)
            {
                float d = bandOffset(ctx, i);

                if (d > ctx.BandHalfWidth(i))
                    continue;

                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>
        /// dE's judgement: touching horizontally + pressed into the judgement zone = Good; reaching missLine = Miss.
        /// Subclasses override it to implement their own rules.
        /// </summary>
        public virtual bool Judge(in JudgeContext ctx)
        {
            var zone = EffectiveZone(ctx);

            if (zone != Zone.None && OverlapsReceptor(ctx))
            {
                ApplyResult(true, zone);
                return true;
            }

            if (TipY >= ctx.RedLineY)
            {
                ApplyResult(false, Zone.None);
                return true;
            }

            return false;
        }

        protected void ApplyResult(bool good, Zone zone)
        {
            if (Judged) return;

            Judged = true;

            if (good)
                Result = NoteResult.Good;
            else
                Result = zone == Zone.Blue ? NoteResult.Bad : NoteResult.Miss;

            Expire();
        }

        protected override void Update()
        {
            base.Update();

            if (playfield == null || Judged) return;

            // Record the position before moving: judging uses "where this frame walked from and to" for sweep
            // detection, otherwise one stall would make the note skip the whole judgement zone in a single frame and
            // it would never be sampled.
            float prevY = Y;

            double chartTime = playfield.CurrentChartTime;

            // TimeBaseOffsetMs: nE uses it to shift the position base forward,
            // so that at the same instant it sits lower and enters the screen earlier (this is what Rx's "spawn ahead of time" relies on).
            double timeUntilHit = HitTime - TimeBaseOffsetMs - chartTime;
            Y = JudgementY - (float)(timeUntilHit * Speed);

            PrevY = prevY;

            if (timeUntilHit < -500) Alpha = 0;
            else Alpha = 1;
        }
    }

    /// <summary>tE: instant single point. Judged only once the player clicks inside the judgement window; if they do not click, it reaches missLine and counts as Miss.</summary>
    public partial class NoteTE : Note
    {
        public override bool Judge(in JudgeContext ctx)
        {
            // It does not "judge on touch" itself — it waits for Playfield to call ApplyClick when it receives a click
            if (TipY < ctx.RedLineY)
                return false;

            ApplyResult(false, Zone.None);
            return true;
        }

        /// <summary>The player clicked, and it is inside the judgement window at this moment: yellow zone Good, blue zone Bad.</summary>
        public void ApplyClick(Zone zone) => ApplyResult(zone == Zone.Yellow, zone);
    }

    /// <summary>
    /// nE: does it the other way round.
    ///   touching horizontally + pressed on the yellow bar -> Miss; touching horizontally + pressed on the blue bar -> Bad;
    ///   not touching horizontally and falling all the way to missLine -> Good.
    /// </summary>
    public partial class NoteNE : Note
    {
        public override bool Judge(in JudgeContext ctx)
        {
            var zone = EffectiveZone(ctx);

            if (zone != Zone.None && OverlapsReceptor(ctx))
            {
                ApplyResult(false, zone);
                return true;
            }

            if (TipY >= ctx.RedLineY)
            {
                ApplyResult(true, zone);
                return true;
            }

            return false;
        }
    }
}
