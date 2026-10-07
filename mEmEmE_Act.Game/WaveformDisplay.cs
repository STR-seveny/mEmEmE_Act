using System;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osuTK;
using osuTK.Graphics;

namespace mEmEmE_Act.Game
{
    /// <summary>
    /// A row of vertical bars that follow the music's frequency amplitudes — the classic
    /// rhythm-game equaliser, as on the main menu.
    ///
    /// Bars do not snap to the raw values: each one eases toward its target, so they rise
    /// quickly on a beat and settle back smoothly instead of flickering.
    /// </summary>
    public partial class WaveformDisplay : CompositeDrawable
    {
        // ================= Tuning =================

        /// <summary>How many bars are drawn across the width.</summary>
        private const int BarCount = 48;

        /// <summary>Space between bars, in pixels.</summary>
        private const float BarGap = 6f;

        /// <summary>Bar corner radius (half the bar width gives a fully rounded cap).</summary>
        private const float BarRadius = 3f;

        /// <summary>Shortest a bar can ever be, so the row still reads as a waveform at rest.</summary>
        private const float MinHeight = 14f;

        /// <summary>Fraction of the remaining gap closed per frame when a bar rises.</summary>
        private const float RiseRate = 0.45f;

        /// <summary>Fraction of the remaining gap closed per frame when a bar falls — slower, for a natural decay.</summary>
        private const float FallRate = 0.12f;


        // ================= State =================

        private readonly Container[] bars;
        private readonly float[] currentHeights;

        private Track menuTrack;

        /// <summary>
        /// Slowly-decaying reference peak used to normalise the amplitudes.
        /// Lives here rather than being passed around the loop.
        /// </summary>
        private float peakReference = 0.05f;

        public WaveformDisplay()
        {
            RelativeSizeAxes = Axes.Both;

            bars = new Container[BarCount];
            currentHeights = new float[BarCount];
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            // The menu track is optional: without it the bars simply rest at MinHeight, so a
            // fresh clone with no audio still shows a (static) menu rather than breaking.
            var storage = new osu.Framework.Platform.NativeStorage(
                System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Sound"));

            menuTrack = audio.GetTrackStore(
                new osu.Framework.IO.Stores.StorageBackedResourceStore(storage)).Get("menu.mp3");

            var container = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
            };

            for (int i = 0; i < BarCount; i++)
            {
                // Bars grow downward from the top edge, as in the design.
                // Masking + CornerRadius live on Container, not Box, hence the wrapper.
                container.Add(bars[i] = new Container
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Width = 1,
                    Height = MinHeight,
                    Masking = true,
                    CornerRadius = BarRadius,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                });

                currentHeights[i] = MinHeight;
            }

            AddInternal(container);

            if (menuTrack != null)
            {
                // The menu theme runs for as long as the menu is up; SuspendTrack/ResumeTrack below
                // take it down and bring it back when another screen covers this one.
                menuTrack.Looping = true;
                menuTrack.Start();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (DrawSize.X <= 0)
                return;

            // Lay the bars out to fill the current width.
            float slot = DrawSize.X / BarCount;
            float barWidth = Math.Max(1f, slot - BarGap);

            var frequencies = menuTrack?.CurrentAmplitudes.FrequencyAmplitudes;

            int freqLength = frequencies?.Length ?? 0;

            // Auto-gain: track the loudest band seen recently, so the bars use the full height
            // regardless of how hot the track is mastered. Without this the raw values are tiny
            // (often 0.01-0.13) and every bar collapses into a near-constant stub.
            float peak = 0.0001f;

            if (freqLength > 0)
            {
                var sp = frequencies!.Value.Span;

                for (int i = 0; i < freqLength; i++)
                {
                    if (sp[i] > peak)
                        peak = sp[i];
                }
            }

            // Decay the peak slowly so the display settles rather than strobing.
            peakReference = Math.Max(peak, peakReference * 0.94f);

            float usableHeight = Math.Max(1f, DrawSize.Y - MinHeight);

            for (int i = 0; i < BarCount; i++)
            {
                // Rest height when there is nothing to show, so the strip never looks empty.
                float target = MinHeight + usableHeight * IdleShape(i);

                if (freqLength > 0)
                {
                    // Logarithmic spacing: the low bands carry most of the energy, so a linear
                    // mapping makes the left half jump and the right half sit still.
                    float t = BarCount == 1 ? 0f : i / (float)(BarCount - 1);

                    // Sample roughly the lower third of the spectrum, where the music actually is.
                    float position = (float)Math.Pow(t, 1.7) * 0.34f;
                    int index = Math.Clamp((int)(position * (freqLength - 1)), 0, freqLength - 1);

                    var sp = frequencies!.Value.Span;
                    float normalised = Math.Clamp(sp[index] / peakReference, 0f, 1f);

                    // Square root lifts quiet passages so the bars keep moving between hits.
                    normalised = (float)Math.Sqrt(normalised);

                    target = MinHeight + usableHeight * normalised;
                }

                // Rise fast, fall slowly.
                float rate = target > currentHeights[i] ? RiseRate : FallRate;
                currentHeights[i] += (target - currentHeights[i]) * rate;

                var bar = bars[i];
                bar.X = i * slot + BarGap / 2f;
                bar.Width = barWidth;
                bar.Height = currentHeights[i];
            }
        }

        /// <summary>
        /// Resting silhouette used when there is no music: a repeating long/short pattern so the
        /// strip still reads as a waveform instead of a row of identical stubs.
        /// </summary>
        private static float IdleShape(int index)
        {
            // Deterministic pseudo-random so the shape is stable between frames.
            int h = (index * 1103515245 + 12345) & 0x7fffffff;

            return 0.10f + (h % 1000) / 1000f * 0.75f;
        }

        /// <summary>
        /// Silences the menu music. The track belongs to the menu, not to the game, so it has to be
        /// taken down when another screen covers this one — otherwise it plays on over the chart.
        /// </summary>
        public void SuspendTrack()
        {
            menuTrack?.Stop();
        }

        /// <summary>Starts the menu music again, from the top, once the menu is back in front.</summary>
        public void ResumeTrack()
        {
            menuTrack?.Restart();
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            menuTrack?.Dispose();
        }
    }
}
