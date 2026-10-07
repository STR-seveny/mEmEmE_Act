using System;
using System.Collections.Generic;
using System.Globalization;
using mEmEmE_Act.Game.Charts;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Rendering;
using osuTK;

namespace mEmEmE_Act.Game
{
    public partial class Playfield : CompositeDrawable
    {
        private List<ChartEvent> events = new List<ChartEvent>();
        private int nextEventIndex;
        private readonly Dictionary<int, Note> activeObjects = new Dictionary<int, Note>();
        private readonly Random random = new Random();

        private const float PIXELS_PER_SPEED_UNIT = 0.05f;
        private const float MIN_SPEED_UNIT = 10f;
        private const float LOGICAL_WIDTH = 640f;
        private const double SPAWN_OFFSET_MS = 1800;

        public float CurrentSpeedUnits { get; private set; } = MIN_SPEED_UNIT;
        public Dictionary<string, string> Variables { get; } = new Dictionary<string, string>();
        public float JudgementY { get; set; } = 500;

        public Vector2 MousePosition { get; private set; } = Vector2.Zero;

        public bool RevActive { get; private set; }
        public bool SptActive { get; private set; }

        // Note colours are a game-level setting, independent of the chart: players identify note
        // types by colour, so every chart uses the same palette. To recolour, edit these four lines.
        public static readonly Colour4 ColourDE = Colour4.FromHex("#ffd200"); // dE
        public static readonly Colour4 ColourTE = Colour4.FromHex("#00a9ff"); // tE
        public static readonly Colour4 ColourNE = Colour4.FromHex("#ff0000"); // nE
        public static readonly Colour4 ColourBUG = Colour4.FromHex("#4f4f4f"); // bug (REV and SPT share a colour; the label distinguishes them)

        private static readonly Colour4 DefaultNoteColour = ColourDE;

        public static Colour4 GetNoteColour(string type) => type switch
        {
            "dE" => ColourDE,
            "tE" => ColourTE,
            "nE" => ColourNE,
            _ => DefaultNoteColour,
        };

        public static Colour4 GetBugColour() => ColourBUG;

        private Track track;
        private bool musicStarted;
        private double musicOffsetMs;
        private double chartLoadTime;
        private bool playTriggered;

        private Sample hitSample;

        // Notes awaiting judgement. Each note is judged exactly once, then removed.
        private readonly List<Note> notesToJudge = new List<Note>();

        // Player clicks (left mouse button / Z / X) are queued here and resolved once per Update,
        // together with the click position (in Playfield space) — tE needs it to tell which note
        // was aimed at. Judging straight from the input callback would pit the input thread
        // against the render thread.
        private readonly Queue<Vector2> pendingClicks = new Queue<Vector2>();

        // Left edge X of every receptor, fed in by MainScreen each frame.
        // Normally one band; two while SPT is active (main + mirrored split).
        private readonly List<float> receptorBandX = new List<float>();

        /// <summary>Width of each receptor band (normally Receptor.ReceptorWidth; halved per band while SPT is active).</summary>
        private float receptorBandWidth = Receptor.ReceptorWidth;

        /// <summary>Height of the red line in pixels, measured up from the bottom of the screen. A note reaching it uncaught is a Miss.</summary>
        private const float RedLineHeight = 50;

        /// <summary>
        /// Maximum number of sub-steps per frame (each step being 1 pixel).
        /// A normal frame only needs a few pixels to a few dozen; the cap only guards against pathological hitches.
        /// </summary>
        private const int MaxSubStepsPerFrame = 60;

        /// <summary>
        /// Click window for tE, in pixels of slack above and below the judgement zone (yellow + blue).
        ///
        /// 0 = the note must be sitting inside the yellow/blue band for a click to count
        /// (a window the height of the 60px zone, quite tight).
        /// Raising it makes slightly early or late clicks valid, i.e. a more forgiving feel.
        /// </summary>
        private const float ClickWindowHeight = 25;

        /// <summary>
        /// Draw depth of the hit effect: a large value so it renders above every note and bug note.
        /// </summary>
        private const float EffectDepth = 1000f;

        [Resolved]
        private AudioManager audioManager { get; set; }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            RelativeSizeAxes = Axes.Both;

            // Pre-build the bug-note label textures (REV / SPT).
            // Without prewarming, the first bug note has to load the font, rasterise the text and
            // upload the texture on the main thread all at once, which causes a visible hitch.
            BugNote.PrewarmLabels(renderer);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // Hit sound: an embedded asset (Sound/hit.mp3), shared by Good and Bad. A Resources folder
            // on disk takes over when one is present; on Android there is none, hence the embedding.
            var sampleStore = audioManager.GetSampleStore(GameAssets.CreateResourceStore());
            hitSample = sampleStore.Get("Sound/hit.mp3");
        }

        public void LoadChart(List<ChartEvent> chartEvents, string audioPath = null)
        {
            StoreChart(chartEvents);

            track?.Dispose();
            track = null;

            if (!string.IsNullOrEmpty(audioPath))
                track = audioManager.GetTrackStore(GameAssets.CreateResourceStore()).Get(audioPath);
        }

        /// <summary>
        /// Loads a .me4 package: chart, and the track when the package carries one.
        /// Unlike <see cref="LoadChart"/> the files come from the package's own extracted folder,
        /// so their full paths (not game-relative paths) are used.
        /// </summary>
        public void LoadPackage(Me4Package package)
        {
            if (package == null)
                return;

            StoreChart(new ChartParser().Parse(package.ChartPath));

            track?.Dispose();
            track = null;

            if (string.IsNullOrEmpty(package.AudioPath) || !System.IO.File.Exists(package.AudioPath))
                return;

            var audioDir = System.IO.Path.GetDirectoryName(package.AudioPath);
            var audioFile = System.IO.Path.GetFileName(package.AudioPath);

            var storage = new osu.Framework.Platform.NativeStorage(audioDir);
            var resourceStore = new osu.Framework.IO.Stores.StorageBackedResourceStore(storage);
            track = audioManager.GetTrackStore(resourceStore).Get(audioFile);
        }

        /// <summary>Resets all per-chart state. Shared by both load paths.</summary>
        private void StoreChart(List<ChartEvent> chartEvents)
        {
            events = chartEvents ?? new List<ChartEvent>();
            nextEventIndex = 0;
            activeObjects.Clear();
            notesToJudge.Clear();
            Variables.Clear();
            RevActive = false;
            SptActive = false;
            CurrentSpeedUnits = MIN_SPEED_UNIT;
            musicStarted = false;
            playTriggered = false;
            chartLoadTime = Clock.CurrentTime;
        }

        public void UpdateMouse(Vector2 position) => MousePosition = position;

        /// <summary>
        /// How many extra milliseconds nE must be spawned ahead of schedule, which doubles as the
        /// offset applied to its position time-base.
        ///
        /// nE is judged at the missLine (lower than the judgement zone), so it falls that much
        /// further than dE. Its position time-base is therefore shifted forward: at any given
        /// moment it sits lower and enters the screen earlier.
        ///
        /// The lead is (missLine - judgement line) / fall speed, i.e. exactly the time it takes to
        /// cover that extra distance.
        /// </summary>
        private double nE_ExtraLeadMs()
        {
            float distance = (DrawSize.Y - RedLineHeight) - JudgementY;
            float pixelsPerMs = SpeedUnitsToPixelsPerMs(CurrentSpeedUnits);

            if (pixelsPerMs <= 0 || distance <= 0)
                return 0;

            return distance / pixelsPerMs;
        }

        protected override void Update()
        {
            base.Update();

            // The judgement zone tracks screen height, so its screen Y is recomputed every Update.
            // Note.Update reads this to decide where to draw itself.
            JudgementY = DrawSize.Y - Receptor.ZoneTopFromBottom;

            // Until the player clicks to begin, nothing runs at all: the chart simply waits.
            // (The play() command used to auto-fire here once the chart had been loaded long
            // enough, which meant music started on its own before the prepare overlay was dismissed.)
            if (!playTriggered)
                return;

            // After play fires: the audio clock drives all events
            double currentTime = GetChartTime();
            while (nextEventIndex < events.Count)
            {
                var ev = events[nextEventIndex];
                double triggerTime = ev.Time;

                if (ev is NoteEvent || ev is BugEvent)
                    triggerTime -= SPAWN_OFFSET_MS;

                // nE is judged at the missLine (lower than the judgement zone) and has to fall
                // that much further, so it is spawned this much earlier as well (its position
                // time-base is shifted too — see SpawnNote).
                if (ev is NoteEvent noteEvent && noteEvent.Type == "nE")
                    triggerTime -= nE_ExtraLeadMs();

                if (triggerTime > currentTime)
                    break;

                ev.Execute(this);
                nextEventIndex++;
            }

            JudgeNotes();
            ResolveClicks();
        }

        /// <summary>Player pressed an attack key (left mouse button / Z / X). Only queued here; resolved in Update.</summary>
        public void RegisterClick(Vector2 position) => pendingClicks.Enqueue(position);

        /// <summary>
        /// Resolves clicks: only a tE currently inside the click window can be hit —
        /// yellow band = Good, blue band = Bad. Clicking early or after the window does nothing.
        ///
        /// **One click judges one tE per receptor band**: with SPT active there are two bands
        /// (main + mirrored split), and a single click judges both at once.
        ///
        /// Why the click must not be required to land on the band itself: the mirrored band sits on
        /// the opposite side of the screen and the mouse can only be in one place, so the player can
        /// never put the cursor on the split. The split therefore has to share the main band's input
        /// (a "mirrored twin", exactly like REV).
        ///
        /// The split is only judged when the click is on the main band's half of the screen, so
        /// having the mouse on the right does not also clear notes far away on the left.
        /// </summary>
        private void ResolveClicks()
        {
            while (pendingClicks.Count > 0)
            {
                var clickPos = pendingClicks.Dequeue();

                float mainCentre = receptorBandX.Count > 0
                    ? receptorBandX[0] + receptorBandWidth / 2f
                    : DrawSize.X / 2f;

                for (int b = 0; b < receptorBandX.Count; b++)
                {
                    float bandCentre = receptorBandX[b] + receptorBandWidth / 2f;

                    // The split (b > 0) only comes along when the click is on the main band's side
                    if (b > 0 && !sameSideAsMain(clickPos.X, mainCentre))
                        continue;

                    var (note, zone) = nearestTEInBand(bandCentre);

                    if (note == null)
                        continue;

                    note.ApplyClick(zone);
                    playFeedback(note);
                    notesToJudge.Remove(note);
                }
            }
        }

        /// <summary>Whether the click is on the same half of the screen as the main receptor.</summary>
        private bool sameSideAsMain(float clickX, float mainCentre)
            => (clickX <= DrawSize.X / 2f) == (mainCentre <= DrawSize.X / 2f);

        /// <summary>
        /// Finds the closest tE that is currently inside the click window and belongs to the given
        /// receptor band.
        ///
        /// The split sits on the far side of the screen, so deciding "does this note belong to this
        /// band" has to compare against the **band centre**, never the mouse position — the mouse is
        /// always on the main side, so comparing against it would make the split side unfindable.
        /// </summary>
        private (NoteTE note, Zone zone) nearestTEInBand(float bandCentre)
        {
            NoteTE best = null;
            var bestZone = Zone.None;
            float bestOffset = float.MaxValue;

            foreach (var n in notesToJudge)
            {
                if (n is not NoteTE te || te.Judged)
                    continue;

                var zone = clickZoneOf(te);

                if (zone == Zone.None)
                    continue;

                float offset = Math.Abs(te.Position.X - bandCentre);

                if (offset > receptorBandWidth / 2f + Note.NoteWidth / 2f)
                    continue;

                if (offset < bestOffset)
                {
                    bestOffset = offset;
                    best = te;
                    bestZone = zone;
                }
            }

            return (best, bestZone);
        }

        /// <summary>
        /// Click window for tE (ClickWindowHeight pixels of slack above and below the judgement zone).
        /// Returns which band counts right now: Yellow = Good, Blue = Bad, None = outside the window.
        ///
        /// This is a **vertical** window with no horizontal component — tE is judged on timing and
        /// does not require the receptor to be horizontally aligned.
        /// </summary>
        private Zone clickZoneOf(Note note)
        {
            float tip = note.TipY;

            if (tip < JudgementY - ClickWindowHeight)
                return Zone.None;

            if (tip > JudgementY + Receptor.ZoneHeight + ClickWindowHeight)
                return Zone.None;

            float yellowBottom = JudgementY + Receptor.GoodHeight;

            return tip <= yellowBottom ? Zone.Yellow : Zone.Blue;
        }

        /// <summary>
        /// Receptor positions: the arguments are the **left edge X of every band** (one band
        /// normally, two while SPT is active) and the **width of each band** (halved while SPT is
        /// active). Fed in by MainScreen every frame, which has already applied REV / SPT.
        /// </summary>
        public void SetReceptorBands(List<float> bandLeftEdges, float bandWidth)
        {
            receptorBandX.Clear();

            if (bandLeftEdges != null)
                receptorBandX.AddRange(bandLeftEdges);

            receptorBandWidth = bandWidth;
        }

        /// <summary>Packs this step's environment for the note, so notes never need a Playfield reference.</summary>
        private JudgeContext buildContext(Note note)
        {
            float centre0 = 0f, centre1 = 0f;
            int count = receptorBandX.Count;

            if (count > 0)
                centre0 = receptorBandX[0] + receptorBandWidth / 2f;

            if (count > 1)
                centre1 = receptorBandX[1] + receptorBandWidth / 2f;

            return new JudgeContext(
                JudgementY,
                DrawSize.Y - RedLineHeight,
                SweptZoneOf(note),
                centre0,
                receptorBandWidth,
                centre1,
                receptorBandWidth,
                Math.Max(count, 1));
        }

        /// <summary>
        /// Swept check: does the path travelled from PrevY to PreviewY this step cross the judgement zone?
        ///
        /// Sub-stepping normally moves 1 pixel at a time and never needs this; it only matters when
        /// a frame hitch coarsens the step size (possibly dozens of pixels), which could otherwise
        /// skip over the whole zone.
        /// </summary>
        private Zone SweptZoneOf(Note note)
        {
            float from = Math.Min(note.PrevY, note.PreviewY) + Note.NoteHeight;
            float to = Math.Max(note.PrevY, note.PreviewY) + Note.NoteHeight;

            float zoneTop = JudgementY;
            float zoneBottom = JudgementY + Receptor.ZoneHeight;

            // Entirely above or below the judgement zone: it never crossed
            if (to < zoneTop || from > zoneBottom)
                return Zone.None;

            // It crossed: check whether the path covered the yellow band
            float yellowBottom = JudgementY + Receptor.GoodHeight;

            bool touchedYellow = from <= yellowBottom && to >= zoneTop;

            return touchedYellow ? Zone.Yellow : Zone.Blue;
        }

        /// <summary>
        /// Judging is each note's own job (polymorphic: dE / NoteTE / NoteNE each implement their
        /// own rules). This method only advances them by **pixel sub-stepping**: the distance a note
        /// travels in one frame is split into 1-pixel steps, and every step is queried.
        ///
        /// This follows Rx, which uses a Scratch custom block ("repeat N times" + run without screen
        /// refresh) to move a note 1 pixel at a time and check after each move. Benefits:
        ///   1. positions always land on exact pixel boundaries, so the geometry used for judging is
        ///      precise and no "trajectory intersection" maths is needed;
        ///   2. no matter how the frame rate stutters or how fast a note falls, it can never
        ///      "skip over the judgement zone in one step";
        ///   3. the judgement zone is only 60 pixels tall, so a per-pixel check cannot miss it.
        ///
        /// The naive "only look at the head of the queue and stop at the first unresolved note" shape
        /// must not be used either: if the head happens to be a tE waiting for a click, it blocks
        /// every note behind it (with dense notes that is a bug on every one of them).
        /// </summary>
        private void JudgeNotes()
        {
            float redLineTop = DrawSize.Y - RedLineHeight;

            // How far the furthest note has to travel this frame
            float maxStep = 0f;

            foreach (var n in notesToJudge)
            {
                float d = n.Y - n.PrevY;
                if (d > maxStep) maxStep = d;
            }

            // Distance covered per step.
            //
            // Normally (less than 60 pixels per frame) this is 1 pixel per step, exactly like Rx.
            // When a hitch makes a frame travel further, the distance is **divided evenly up to the
            // cap** — the crucial part is that the final step lands exactly on the note's real
            // position (= where it is drawn).
            //
            // An earlier version capped the number of steps but still moved 1 pixel each, so a
            // hitched note was only pushed part of the way: the PreviewY used for judging stopped
            // above the judgement zone while rendering had already fallen past it. The symptom was
            // "during a hitch notes fall straight through and nothing is judged".
            float stepSize = maxStep <= MaxSubStepsPerFrame
                ? 1f
                : maxStep / MaxSubStepsPerFrame;

            int steps = (int)Math.Ceiling(maxStep / stepSize);

            for (int step = 1; step <= steps; step++)
            {
                float offset = step * stepSize;

                for (int i = notesToJudge.Count - 1; i >= 0; i--)
                {
                    var note = notesToJudge[i];

                    if (note.Judged)
                        continue;

                    // Move the note to this step's position first, so it is judged at that exact coordinate
                    float y = note.PrevY + offset;

                    // The last step always lands on the note's real position
                    note.PreviewY = offset >= (note.Y - note.PrevY) ? note.Y : y;

                    var ctx = buildContext(note);

                    if (!note.Judge(ctx))
                        continue;

                    playFeedback(note);
                    notesToJudge.RemoveAt(i);
                }
            }

            // Safety net: a note that somehow escaped judging must not linger in the list or the scene.
            // Anything reaching here has fallen past the missLine, so it is closed out as a Miss.
            for (int i = notesToJudge.Count - 1; i >= 0; i--)
            {
                var note = notesToJudge[i];

                if (note.Y <= redLineTop + Note.NoteHeight * 2)
                    continue;

                if (!note.Judged)
                {
                    note.PreviewY = note.Y;
                    note.Judge(buildContext(note));
                    playFeedback(note);
                }

                activeObjects.Remove(note.Id);
                notesToJudge.RemoveAt(i);
            }
        }

        /// <summary>Feedback after a judgement: Miss is silent; Good / Bad share the sound plus their own coloured effect.</summary>
        private void playFeedback(Note note)
        {
            if (note.Result != NoteResult.Miss)
            {
                hitSample?.Play();
                spawnHitEffect(note);
            }

            activeObjects.Remove(note.Id);
        }

        /// <summary>
        /// Hit effect, using the sprites from Rx.
        ///
        /// **The sprite is chosen by judgement result, not by which band was touched** —
        /// a dE caught in the blue band is still Good and must show the yellow effect; the blue
        /// effect belongs to Bad only.
        ///
        /// The outer frame's start angle comes from whether the note sits left or right of the
        /// receptor centre: negative to the left, positive to the right, larger the further out.
        /// </summary>
        private void spawnHitEffect(Note note)
        {
            bool perfect = note.Result == NoteResult.Good;

            float receptorCenterX = receptorBandX.Count > 0
                ? receptorBandX[0] + Receptor.ReceptorWidth / 2f
                : DrawSize.X / 2f;
            float offsetX = note.Position.X - receptorCenterX;

            float angle = HitEffect.BaseAngle + offsetX * HitEffect.AnglePerPixel;
            angle = Math.Clamp(angle, -HitEffect.MaxAngle, HitEffect.MaxAngle);

            AddInternal(new HitEffect(perfect, angle)
            {
                Position = new Vector2(note.Position.X, note.Y + Note.NoteHeight / 2f),

                // The effect always draws on top. This cannot rely on "added last" — notes keep
                // spawning, and a note spawned later would sort above the effect and cover it.
                Depth = EffectDepth,
            });
        }

        private double GetChartTime()
        {
            if (!playTriggered)
                return Clock.CurrentTime - chartLoadTime;

            if (musicStarted && track != null)
                return musicOffsetMs + track.CurrentTime;

            return Clock.CurrentTime - chartLoadTime;
        }

        private float LogicalToScreenX(float logicalX)
            => (logicalX + LOGICAL_WIDTH / 2f) / LOGICAL_WIDTH * DrawSize.X;

        private float SpeedUnitsToPixelsPerMs(float speedUnits)
            => speedUnits * PIXELS_PER_SPEED_UNIT;



        public float Evaluate(string param)
        {
            if (string.IsNullOrEmpty(param)) return 0;
            param = param.Trim();

            if (param == "me_x") return MousePosition.X;
            if (param == "me_y") return MousePosition.Y;

            if (param.StartsWith("var."))
            {
                var key = param.Substring(4);
                if (Variables.TryGetValue(key, out var val))
                {
                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
                        return result;
                }
                return 0;
            }

            if (param.EndsWith("_my_x") || param.EndsWith("_my_y"))
            {
                bool isX = param.EndsWith("_my_x");
                string idPart = param.Substring(0, param.Length - 5);
                if (int.TryParse(idPart, out var targetId))
                {
                    if (activeObjects.TryGetValue(targetId, out var obj) && obj != null)
                        return isX ? obj.Position.X : obj.Position.Y;
                }
                return 0;
            }

            if (param.StartsWith("random(") && param.EndsWith(")"))
            {
                var inner = param.Substring(7, param.Length - 8);
                var parts = inner.Split('/');
                if (parts.Length == 2 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var min) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var max))
                {
                    return min + (float)random.NextDouble() * (max - min);
                }
            }

            if (float.TryParse(param, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                return num;

            return 0;
        }



        public void SpawnNote(int id, string xParam, string type, double hitTime)
        {
            float rawX = Evaluate(xParam);
            float screenX = LogicalToScreenX(rawX);

            // Create the note class matching the type: the rules live in each class, this only picks one.
            Note note = type switch
            {
                "tE" => new NoteTE(),
                "nE" => new NoteNE(),
                _ => new Note(),
            };

            note.Id = id;
            note.Type = type;
            note.HitTime = hitTime;
            note.JudgementY = JudgementY;
            note.Position = new Vector2(screenX, -JudgementY);
            note.PrevY = -JudgementY;
            note.PreviewY = -JudgementY;
            note.Speed = SpeedUnitsToPixelsPerMs(CurrentSpeedUnits);

            // nE shifts its position time-base forward (it has to fall the extra judgement-line to
            // missLine distance), so it appears on screen earlier than a dE at the same time.
            // (SPT only adds a mirrored horizontal band and does not move the vertical judgement
            // point, so it needs no handling here.)
            if (type == "nE")
                note.TimeBaseOffsetMs = nE_ExtraLeadMs();
            note.NoteColour = GetNoteColour(type);

            note.AttachPlayfield(this);
            activeObjects[id] = note;
            notesToJudge.Add(note);
            AddInternal(note);
        }

        public void SpawnBug(int id, string xParam, string type, double hitTime)
        {
            float rawX = Evaluate(xParam);
            float screenX = LogicalToScreenX(rawX);

            var bugNote = new BugNote
            {
                HitTime = hitTime,
                JudgementY = JudgementY,
                Position = new Vector2(screenX, -JudgementY),
                Speed = SpeedUnitsToPixelsPerMs(CurrentSpeedUnits),
                BugType = type,
                BugColour = GetBugColour(),
            };
            bugNote.AttachPlayfield(this);
            activeObjects[id] = null;
            AddInternal(bugNote);
        }

        public void ToggleBug(string type)
        {
            switch (type)
            {
                case "REV": RevActive = !RevActive; break;
                case "SPT": SptActive = !SptActive; break;
            }
        }

        public void MoveObject(int id, string xParam, string yParam)
        {
            if (activeObjects.TryGetValue(id, out var obj) && obj != null)
            {
                float x = Evaluate(xParam);
                float y = Evaluate(yParam);
                obj.MoveTo(new Vector2(x, y));
            }
        }

        public void ChangeSpeed(string speedParam)
        {
            float units = Evaluate(speedParam);
            if (units < MIN_SPEED_UNIT) units = MIN_SPEED_UNIT;
            CurrentSpeedUnits = units;
        }

        public void StartMusic(double playTime, double start)
        {
            playTriggered = true;
            musicOffsetMs = 0;
            musicStarted = true;

            if (track != null)
            {
                track.Seek(start);
                track.Start();
            }
        }

        /// <summary>
        /// Lets the chart begin: events start firing from this moment.
        ///
        /// Called when the prepare overlay is dismissed. The track itself is still started by the
        /// chart's own play() command, so the wait between "player clicks" and "music starts" stays
        /// exactly as the chart author wrote it.
        /// </summary>
        public void Begin()
        {
            if (playTriggered)
                return;

            playTriggered = true;
            chartLoadTime = Clock.CurrentTime;
        }

        public double CurrentChartTime => GetChartTime();

        /// <summary>
        /// Silences the chart audio. Playfield does not own the screen's lifetime, so leaving the game
        /// screen has to take the music down explicitly — otherwise the chart keeps playing over the
        /// menu it just returned to.
        /// </summary>
        public void StopMusic()
        {
            track?.Stop();
        }



        public void PlayEffect(string type, string sizeParam) { /* TODO */ }
        public void MoveCamera(string xParam, string yParam) { /* TODO */ }
        public void SetVariable(string name, string value) => Variables[name] = value;
        public void EndGame() { /* TODO */ }
    }
}
