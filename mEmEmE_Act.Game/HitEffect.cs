using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace mEmEmE_Act.Game
{
    /// <summary>
    /// Hit effect, using the sprites from Rx.
    ///
    /// The inner frame has three windup stages:
    ///   1. vertical bar (windup sprite, original orientation)
    ///   2. horizontal bar (same sprite, rotated 90°)
    ///   3. box (frame sprite)
    ///
    /// The outer frame is not part of the windup: it appears rotated, settles upright, then fades out.
    ///
    /// Colour is not tinted in code — the sprites come in yellow (Good) and blue (Bad) variants
    /// and are picked by judgement result.
    /// </summary>
    public partial class HitEffect : CompositeDrawable
    {
        // Sizes follow Scratch's "size" semantics: 100 = the sprite's native size.
        // e.g. the frame sprite is 84x84, so size 150 draws it at 84 * 1.5 = 126 px.

        /// <summary>Inner frame (box) size, as a percentage.</summary>
        private const float FrameSizePercent = 140;

        /// <summary>
        /// Windup bar size, as a percentage. Smaller than the frame on purpose —
        /// a full-size vertical bar on spawn is too eye-catching; the windup should
        /// be a quick flick before the box shows up.
        /// </summary>
        private const float WindupSizePercent = 60;

        /// <summary>Outer frame size, as a percentage (Rx grows it to 200).</summary>
        private const float OuterSizePercent = 200;

        /// <summary>Outer frame opacity (kept lighter than the inner frame).</summary>
        private const float OuterAlpha = 1f;

        /// <summary>Outer frame start angle when the note lands dead centre on the receptor.</summary>
        public const float BaseAngle = -90f;

        /// <summary>Extra start angle per pixel of offset from the receptor centre.</summary>
        public const float AnglePerPixel = 0.1f;

        /// <summary>Cap on the start angle, so large offsets don't spin it like a windmill.</summary>
        public const float MaxAngle = 60f;

        /// <summary>Outer frame scale on spawn (Rx sets size to 10, i.e. it grows from near nothing).</summary>
        private const float OuterStartScale = 0f;

        /// <summary>
        /// Outer frame fade-out duration in milliseconds.
        /// Must not be too short — at 330ms the frame had already faded to invisible halfway
        /// through its rotation, which looked like it never rotated at all.
        /// 1200ms keeps the whole rotation visible.
        /// </summary>
        private const double OuterFadeDuration = 1200;

        /// <summary>
        /// Inner frame scale on spawn: 0 = grows from nothing.
        /// Must not start at 0.5 — the first frame would already be half size, hiding the "grow from nothing" part.
        /// </summary>
        private const float InnerStartScale = 0f;

        /// <summary>
        /// Exponential-approach rate: each frame closes a fraction of the remaining gap.
        ///
        /// Rx uses 0.45~0.5, but that is 30fps Scratch — the same formula runs twice as fast at 60fps,
        /// jumping to 50% on the very first frame and finishing within ~7 frames, too fast for the eye
        /// (it looks like it appears at full size instantly).
        /// Lowered to 0.08 here: about 55 frames (~900ms) at 60fps, so the growth is clearly visible.
        /// </summary>
        private const float ApproachRate = 0.08f;

        /// <summary>Outer frame approach rate: 0.45 in Rx, slightly slower than the inner frame's 0.5.</summary>
        private const float OuterApproachRate = 0.09f;

        /// <summary>How long each windup stage is held, in milliseconds. Rx is one frame, invisible at 60fps; three frames here.</summary>
        private const double StageDuration = 50;

        /// <summary>
        /// Approach rate used for rotating upright. Rx uses 0.25, but that is likewise too fast to see —
        /// lowered to 0.08, so 90° takes about 55 frames (~900ms) and the rotation is actually visible.
        /// </summary>
        private const float RotationApproachRate = 0.08f;

        /// <summary>Once less than this fraction of the gap remains, treat the growth as complete and start counting the hold.</summary>
        private const float SettleThreshold = 0.01f;

        /// <summary>How long the fully-grown inner frame is held before it disappears, in milliseconds.</summary>
        private const double FrameHoldDuration = 150;

        private readonly bool perfect;

        private Sprite outerFrame;
        private Sprite windup;
        private Sprite frame;

        /// <summary>Windup stage: 0 not started, 1 vertical bar, 2 horizontal bar, 3 box.</summary>
        private int stage;

        private double stageTime;
        private double? settleTime;

        /// <summary>Guards against calling Expire more than once.</summary>
        private bool expired;

        private bool frameGone;
        private bool outerGone;

        public HitEffect(bool perfect, float angle)
        {
            this.perfect = perfect;

            Origin = Anchor.Centre;
            Alpha = 0;

            // All three sprites are plain Sprite children positioned against the effect centre.
            // Deliberately not wrapped in an AutoSize container: such a container depends on the
            // parent's size, and if the parent size is 0 (e.g. mounted under a zero-sized node)
            // the whole effect fails to draw.
            InternalChildren = new Drawable[]
            {
                outerFrame = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = OuterAlpha,
                    Rotation = angle,
                    Scale = new Vector2(OuterStartScale),
                },
                frame = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Alpha = 0,
                    Scale = new Vector2(InnerStartScale),
                },
                windup = new Sprite
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            // The effects are embedded assets (Effects\*.png); GameAssets falls back to the loose files
            // in the output directory when they are there, which is also how they used to be read.
            var store = GameAssets.CreateTextureStore(renderer);

            var name = perfect ? "perfect" : "slow";

            var windupTexture = store.Get($"Effects/hit_{name}_windup");
            var frameTexture = store.Get($"Effects/hit_{name}_frame");
            var outerTexture = store.Get($"Effects/hit_{name}_outer");

            if (windupTexture != null)
            {
                windup.Texture = windupTexture;

                // The windup sprite is a thin 8x84 strip: scale it proportionally.
                // Setting Size directly would stretch it horizontally by an order of magnitude.
                windup.Size = windupTexture.Size * (WindupSizePercent / 100f);
            }

            if (frameTexture != null)
            {
                frame.Texture = frameTexture;
                frame.Size = frameTexture.Size * (FrameSizePercent / 100f);
            }

            if (outerTexture != null)
            {
                outerFrame.Texture = outerTexture;
                outerFrame.Size = outerTexture.Size * (OuterSizePercent / 100f);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            windup.Alpha = 0;
            frame.Alpha = 0;
            windup.Rotation = 0;

            this.FadeTo(1, 60);

            outerFrame.Scale = new Vector2(OuterStartScale);
            outerFrame.Alpha = OuterAlpha;
        }

        /// <summary>
        /// Every animation is advanced frame by frame using exponential approach, rather than
        /// keyframe interpolation such as ScaleTo/RotateTo.
        ///
        /// Rx does "size += (target - size) * 0.45" each frame: it closes a fraction of the
        /// remaining gap, so it **never quite arrives**, and that soft settling is what makes it
        /// feel smooth. Keyframe interpolation snaps to the target over a fixed duration, which
        /// has a different shape and looks stiff.
        ///
        /// Windup (vertical bar → horizontal bar → box): Rx uses "wait 0 seconds" to force one
        /// stage frame. At Scratch's 30fps that frame is 33ms and barely visible; here at 60fps a
        /// frame is only 16ms, so each stage is held for StageDuration instead.
        /// </summary>
        protected override void Update()
        {
            base.Update();

            if (stage < 3)
            {
                if (Time.Current - stageTime >= StageDuration)
                {
                    stageTime = Time.Current;

                    switch (stage)
                    {
                        case 0:
                            windup.Alpha = 1;
                            windup.Rotation = 0;
                            break;

                        case 1:
                            windup.Rotation = 90;
                            break;

                        case 2:
                            windup.Alpha = 0;
                            frame.Alpha = 1;
                            break;
                    }

                    stage++;
                }
            }

            if (stage >= 3 && !expired)
            {
                float next = frame.Scale.X + (1f - frame.Scale.X) * ApproachRate;

                // Close enough: stop growing and start counting the hold.
                if (1f - next < SettleThreshold)
                {
                    frame.Scale = new Vector2(1f);
                    settleTime ??= Time.Current;
                }
                else
                {
                    frame.Scale = new Vector2(next);
                }

                // After the hold the inner frame removes itself outright — no fade, and it does
                // not hold back the outer frame.
                if (settleTime.HasValue && Time.Current - settleTime.Value >= FrameHoldDuration)
                {
                    frame.Expire();
                    frameGone = true;
                    stage = 4;
                }
            }

            if (outerFrame != null && !outerGone)
            {
                if (outerFrame.Alpha > 0)
                {
                    float nextScale = outerFrame.Scale.X + (1f - outerFrame.Scale.X) * OuterApproachRate;
                    outerFrame.Scale = new Vector2(nextScale);

                    float nextRotation = outerFrame.Rotation * (1f - RotationApproachRate);
                    outerFrame.Rotation = nextRotation;
                }

                outerFrame.Alpha -= (float)(Time.Elapsed / OuterFadeDuration);

                if (outerFrame.Alpha <= 0)
                {
                    outerFrame.Alpha = 0;
                    outerFrame.Expire();
                    outerGone = true;
                }
            }

            if (frameGone && outerGone)
            {
                expired = true;
                Expire();
            }
        }
    }
}
