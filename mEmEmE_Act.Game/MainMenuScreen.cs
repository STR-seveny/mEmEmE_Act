using System;
using System.Collections.Generic;
using System.IO;
using mEmEmE_Act.Game.Charts;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using LinePath = osu.Framework.Graphics.Lines.Path;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace mEmEmE_Act.Game
{
    /// <summary>
    /// The main menu, which is also the song select.
    ///
    /// At rest it shows the waveform, the logo and a START button. Clicking START slides that whole
    /// panel upward (fast at first, then easing out) so the button settles at the top and the level
    /// list is revealed underneath — the two are the same page, not two screens.
    /// </summary>
    public partial class MainMenuScreen : Screen
    {
        // ================= Layout =================
        // Every value below is expressed in the coordinate space of the canvas that
        // DrawSizePreservingFillContainer lays out (TargetDrawSize 1366x768). The canvas is exactly
        // 768 tall, but its width is whatever the window's aspect ratio asks for (about 1393 at
        // 1936x1087, more on a wider window), which is why the panel is sized relatively.

        /// <summary>Height of the waveform strip across the top. It spans the full width and never moves.</summary>
        private const float WaveformHeight = 210f;

        /// <summary>Y of the logo's centre. It sits behind the panel and gets covered as the panel rises.</summary>
        private const float LogoCentreY = 330f;

        // ----- The panel: the START.png artwork, used as-is -----
        //
        // The panel artwork is used directly: START.png is 1280x720 and already contains both the
        // handle (a trapezoid rising out of the body) and the body itself.
        //
        // Measured from the file: the trapezoid spans y 0-236 and tapers from about 424px wide at the
        // top to about 597px at the bottom; the solid body starts there and runs to the bottom at full
        // width. Nothing is redrawn in code — earlier attempts to draw the trapezoid with a LinePath
        // never matched the artwork.
        //
        // The panel is stretched to the CANVAS rather than to a fixed 1366x768 (RelativeSizeAxes.Both
        // plus FillMode.Fill). Fixed numbers left a band of background down each side of the panel
        // wherever the window was wider than 16:9, which looked like the window was bigger than its
        // contents. Stretching costs a couple of percent of horizontal distortion and nothing else.

        /// <summary>Design height of the canvas; the artwork's 720 maps onto this.</summary>
        private const float CanvasHeight = 768f;

        /// <summary>Height of the handle portion of the artwork; the body starts here.</summary>
        private const float HandleRegionHeight = 252f;

        /// <summary>Clickable width across the handle, a little wider than its base.</summary>
        private const float HandleClickWidth = 680f;

        /// <summary>
        /// How much of the handle stays on screen while the menu is closed, in canvas units. The START
        /// label sits at canvas y 55-124, so at least ~140 is needed to show it whole: anything less
        /// slices the label in half along the bottom edge of the window, which reads as a clipping bug
        /// rather than as a button.
        /// </summary>
        private const float ClosedHandleHeight = 170f;

        /// <summary>
        /// Y of the panel's top edge while the menu is closed, as a fraction of the canvas height
        /// (RelativePositionAxes.Y). A fraction rather than an absolute Y because the panel's height is
        /// the canvas height, whatever the window size: 1 would put its top edge on the bottom edge of
        /// the screen, so the distance up from there is what stays visible.
        /// </summary>
        private const float PanelClosedY = 1f - ClosedHandleHeight / CanvasHeight;

        /// <summary>
        /// Y of the panel once open: its top edge flush with the top of the canvas. MoveToY takes an
        /// ABSOLUTE target, not an offset — passing -PanelTravel (an offset) sent the panel twice as
        /// far and pushed it off the top of the screen.
        /// </summary>
        private const float PanelOpenY = 0f;

        /// <summary>Slide duration in milliseconds. Easing out gives the fast-then-slow feel.</summary>
        private const double SlideDuration = 900;

        /// <summary>Gap between the handle and the first row of cards.</summary>
        private const float ListTopPadding = 30f;

        /// <summary>Side inset of the level list inside the panel body.</summary>
        private const float ListSideInset = 75f;

        // ================= Card layout =================
        // Two columns of CardWidth plus one gap. The list is as wide as the canvas minus the side
        // insets, so on a 1366-wide canvas it is 1216 wide and the columns fit it exactly.

        private const float CardArtSize = 160f;
        private const float CardWidth = 583f;
        private const float CardHeight = 182f;
        private const float CardColumnGap = 50f;

        // ================= State =================

        /// <summary>The sliding assembly: the START handle plus the body holding the level list.</summary>
        private Container panel;

        /// <summary>Also owns the menu music, which has to be stopped when the menu is covered.</summary>
        private WaveformDisplay waveform;

        private Drawable logo;

        private bool menuOpen;

        public MainMenuScreen()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            var levels = LevelLibrary.Scan();

            // ---------- Background: gradient + waveform + logo, none of which move ----------
            AddInternal(new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = ColourInfo.GradientHorizontal(
                    Colour4.FromHex("#464646"),
                    Colour4.FromHex("#5f5f5f")
                ),
            });

            // The waveform spans the full width. The rising handle covers its middle, leaving the two
            // ends visible — exactly as in the design.
            AddInternal(waveform = new WaveformDisplay
            {
                RelativeSizeAxes = Axes.X,
                Height = WaveformHeight,
            });

            // The logo sits behind the panel, so the panel covers it as it rises.
            AddInternal(logo = BuildLogo(renderer));

            // ---------- The panel ----------
            // Added last so it draws over everything, and it owns the level list: while the panel is
            // parked off-screen the list is off-screen with it. Keeping the list outside the panel
            // (an earlier attempt) left the cards visible on the main menu.
            panel = BuildPanel(levels, renderer);
            AddInternal(panel);
        }

        /// <summary>
        /// The sliding panel, built from the START.png artwork plus the level list.
        ///
        /// The artwork supplies the whole shape (handle and body), so nothing here draws it: the
        /// sprite is the panel, the list is laid over its body area, and an invisible click strip
        /// sits across the handle.
        /// </summary>
        private Container BuildPanel(List<LevelInfo> levels, IRenderer renderer)
        {
            var body = new Container
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                // Y is a fraction of the canvas height here, so PanelClosedY stays correct whatever
                // the canvas turns out to be; MoveToY(0) means the same thing under either axis mode.
                RelativeSizeAxes = Axes.Both,
                RelativePositionAxes = Axes.Y,
                Y = PanelClosedY,
            };

            var texture = LoadTexture(Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Textures", "START.png"), renderer);

            if (texture != null)
            {
                // Size must be written out explicitly as (1,1) of the parent. A Sprite whose Size is
                // still zero adopts the texture's pixel size the moment Texture is assigned — and if
                // RelativeSizeAxes is set afterwards that 1280x720 is read as a MULTIPLIER, blowing the
                // sprite up to 1280 screens wide so that all that shows is one transparent corner of
                // the artwork. Setting Size before the initializer can do it keeps auto-sizing out.
                body.Add(new Sprite
                {
                    Texture = texture,
                    RelativeSizeAxes = Axes.Both,
                    Size = Vector2.One,
                    FillMode = FillMode.Fill,
                });
            }
            else
            {
                // Fallback so the panel is still usable if the artwork is missing.
                body.Add(new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Colour4.FromHex("#6a6a6a"),
                });
            }

            // The level list lives inside the panel, below the handle portion of the artwork.
            body.Add(new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding
                {
                    Top = HandleRegionHeight + ListTopPadding,
                    Left = ListSideInset,
                    Right = ListSideInset,
                    Bottom = ListSideInset,
                },
                Child = BuildLevelList(levels, renderer),
            });

            // Invisible click strip over the handle. Its own content is empty: the artwork behind it
            // already shows the START tab, so nothing should be drawn here.
            body.Add(new ClickArea(new Container(), OnStartClicked)
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Size = new Vector2(HandleClickWidth, HandleRegionHeight),
            });

            return body;
        }

        /// <summary>Raises the panel: the handle travels until its top edge reaches the top of the screen.</summary>
        private void OnStartClicked()
        {
            if (menuOpen)
                return;

            menuOpen = true;

            panel.MoveToY(PanelOpenY, SlideDuration, Easing.OutQuint);
        }

        /// <summary>
        /// The menu music goes with the menu. Pushing the gameplay screen on top suspends this one, and
        /// without stopping the track here it carries on over the chart.
        /// </summary>
        public override void OnSuspending(ScreenTransitionEvent e)
        {
            waveform.SuspendTrack();
            base.OnSuspending(e);
        }

        /// <summary>Coming back from the gameplay screen, the menu music starts over.</summary>
        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            waveform.ResumeTrack();
        }

        /// <summary>Lowers the panel back to its resting position.</summary>
        private void ClosePanel()
        {
            if (!menuOpen)
                return;

            menuOpen = false;

            panel.MoveToY(PanelClosedY, SlideDuration, Easing.OutQuint);
        }

        /// <summary>
        /// Clicks that no child claimed — the blank parts of the panel, the insets beside the list, the
        /// space under it — send the panel back down. Mouse events bubble up until something handles
        /// them, and a level card does handle its own, so this only ever sees what is left over.
        /// </summary>
        protected override bool OnMouseDown(MouseDownEvent e)
        {
            if (e.Button != MouseButton.Left || !menuOpen)
                return base.OnMouseDown(e);

            ClosePanel();
            return true;
        }

        /// <summary>
        /// The same collapse, one step later in the event chain, because the list's scroll container
        /// swallows OnMouseDown as the start of a drag-scroll gesture and never lets it bubble. It does
        /// not touch OnClick, so a plain click on an empty patch of the list still arrives here.
        /// </summary>
        protected override bool OnClick(ClickEvent e)
        {
            if (e.Button != MouseButton.Left || !menuOpen)
                return base.OnClick(e);

            ClosePanel();
            return true;
        }

        /// <summary>Click target for the handle, so only the START trapezoid responds to clicks.</summary>
        private partial class ClickArea : CompositeDrawable
        {
            private readonly Action onClick;

            public ClickArea(Drawable content, Action onClick)
            {
                this.onClick = onClick;

                InternalChild = content;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return base.OnMouseDown(e);

                onClick?.Invoke();
                return true;
            }

            protected override bool OnClick(ClickEvent e)
            {
                // Swallow the click as well, or it carries on up to the screen and the screen's
                // "click empty space to collapse" handler undoes the open this just triggered.
                return true;
            }
        }


        /// <summary>
        /// The logo.
        ///
        /// Placeholder for now: the real artwork is a custom-drawn wordmark, so it will be swapped for
        /// an image once exported. Drop it at Resources/Textures/logo.png and it is used automatically.
        /// </summary>
        private Drawable BuildLogo(IRenderer renderer)
        {
            var logoPath = Path.Combine(
                System.AppDomain.CurrentDomain.BaseDirectory, "Resources", "Textures", "logo.png");

            var texture = LoadTexture(logoPath, renderer);

            if (texture != null)
            {
                // Sized by width alone: FillMode.Fit then preserves the artwork's own aspect ratio,
                // so the height follows and the wordmark is never stretched.
                const float logoWidth = 640f;

                return new Sprite
                {
                    Texture = texture,
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Y = LogoCentreY,
                    Size = new Vector2(logoWidth, logoWidth / (texture.Width / (float)texture.Height)),
                    FillMode = FillMode.Fit,
                };
            }

            return new DynamicText
            {
                Text = "mEmEmE",
                FontSize = 110,
                Anchor = Anchor.TopCentre,
                Origin = Anchor.Centre,
                Position = new Vector2(0, LogoCentreY),
            };
        }

        /// <summary>Two-column, scrollable grid of level cards.</summary>
        private Drawable BuildLevelList(List<LevelInfo> levels, IRenderer renderer)
        {
            var grid = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Full,
                Spacing = new Vector2(CardColumnGap, 28),
            };

            var cards = new List<Drawable>();

            foreach (var level in levels)
                cards.Add(BuildCard(level, renderer));

            if (cards.Count == 0)
            {
                cards.Add(new DynamicText
                {
                    Text = "Resources/Levels 里没有 .me4 关卡",
                    FontSize = 36,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                });
            }

            grid.AddRange(cards);

            return new BasicScrollContainer
            {
                RelativeSizeAxes = Axes.Both,
                ScrollbarOverlapsContent = false,
                Child = grid,
            };
        }

        /// <summary>One level card: cover art on the left, metadata on the right.</summary>
        private Drawable BuildCard(LevelInfo level, IRenderer renderer)
        {
            var card = new Container
            {
                Size = new Vector2(CardWidth, CardHeight),
            };

            // Cover art in a white frame.
            card.Add(new Container
            {
                Size = new Vector2(CardArtSize + 12),
                Masking = true,
                Anchor = Anchor.CentreLeft,
                Origin = Anchor.CentreLeft,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.White },
                    new Box
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(CardArtSize),
                        Colour = Colour4.FromHex("#4a4a4a"),
                    },
                    ArtFor(level, renderer),
                },
            });

            // Metadata lines.
            const float textX = CardArtSize + 43;
            float textY = 15;

            foreach (var (text, size) in new[]
            {
                ($"{level.DisplayTitle} - {level.Artist}", 36f),
                (level.Charter, 32f),
                (level.Illustrator, 32f),
            })
            {
                card.Add(new DynamicText
                {
                    Text = text,
                    FontSize = size,
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Position = new Vector2(textX, textY),
                });

                textY += size + 15;
            }

            return new CardHitArea(card, () => StartLevel(level))
            {
                Size = new Vector2(CardWidth, CardHeight),
            };
        }

        /// <summary>The card's cover art, centred inside its frame; empty when the level has none.</summary>
        private static Drawable ArtFor(LevelInfo level, IRenderer renderer)
        {
            var texture = LoadTexture(level.ArtPath, renderer);

            if (texture == null)
            {
                return new DynamicText
                {
                    Text = "曲绘",
                    FontSize = 30,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }

            return new Sprite
            {
                Texture = texture,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(CardArtSize),
                FillMode = FillMode.Fit,
            };
        }

        /// <summary>
        /// Starts a level: pushes the gameplay screen on top of this one. Nothing is wired up for the
        /// way back — the gameplay screen pops ITSELF (Screen.Exit), because exiting the menu instead
        /// throws ScreenHasChildException: the menu still has the gameplay screen sitting on it.
        /// </summary>
        private void StartLevel(LevelInfo level)
        {
            this.Push(new GameplayScreen(level));
        }

        /// <summary>Create a texture from a file on disk, or null when it cannot be read.</summary>
        private static Texture LoadTexture(string path, IRenderer renderer)
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

        /// <summary>
        /// A card's clickable area. Kept as its own Drawable so each card can handle its own clicks
        /// without the screen having to hit-test the whole grid.
        /// </summary>
        private partial class CardHitArea : CompositeDrawable
        {
            private readonly Action onClick;

            public CardHitArea(Drawable content, Action onClick)
            {
                this.onClick = onClick;

                InternalChild = content;
            }

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                if (e.Button != MouseButton.Left)
                    return base.OnMouseDown(e);

                onClick?.Invoke();
                return true;
            }

            protected override bool OnClick(ClickEvent e)
            {
                // A card click starts a level; it must not also trip the screen's collapse handler on
                // its way up the hierarchy.
                return true;
            }
        }
    }
}
