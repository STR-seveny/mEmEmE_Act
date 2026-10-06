using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace mEmEmE_Act.Game.Charts
{
    public class ChartParser
    {
        private const double SEC_TO_MS = 1000.0;

        public List<ChartEvent> Parse(string filePath)
        {
            var events = new List<ChartEvent>();

            foreach (var rawLine in File.ReadLines(filePath))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrEmpty(line) || line.StartsWith("//") || line.StartsWith("=me3"))
                    continue;

                var ev = ParseLine(line);
                if (ev != null)
                    events.Add(ev);
            }

            return events.OrderBy(e => e.Time).ToList();
        }

        private ChartEvent ParseLine(string line)
        {
            int openParen = line.IndexOf('(');
            int closeParen = line.LastIndexOf(')');

            if (openParen < 0 || closeParen < openParen)
                return null;

            string command = line.Substring(0, openParen).Trim();
            string argsRaw = line.Substring(openParen + 1, closeParen - openParen - 1).Trim();

            var args = argsRaw.Length == 0
                ? Array.Empty<string>()
                : argsRaw.Split(',').Select(a => a.Trim()).ToArray();

            try
            {
                return command switch
                {
                    "play" => ParsePlay(args),
                    "note" => ParseNote(args),
                    "move" => ParseMove(args),
                    "speed" => ParseSpeed(args),
                    "effect" => ParseEffect(args),
                    "camera" => ParseCamera(args),
                    "bug" => ParseBug(args),
                    "end" => ParseEnd(args),
                    "var" => ParseVar(args),
                    _ => null
                };
            }
            catch
            {
                return null;
            }
        }

        private static double ToSeconds(string s)
            => double.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

        private ChartEvent ParsePlay(string[] a)
        {
            if (a.Length < 1) return null;
            return new PlayEvent
            {
                Time = ToSeconds(a[0]) * SEC_TO_MS,
                Start = a.Length > 1 ? ToSeconds(a[1]) * SEC_TO_MS : 0
            };
        }

        private ChartEvent ParseNote(string[] a)
        {
            if (a.Length < 4) return null;
            return new NoteEvent
            {
                Time = ToSeconds(a[0]) * SEC_TO_MS,
                Type = a[1],
                X = a[2],
                Id = int.Parse(a[3]),
                HitTime = ToSeconds(a[0]) * SEC_TO_MS,
            };
        }

        private ChartEvent ParseMove(string[] a)
        {
            if (a.Length < 4) return null;
            return new MoveEvent
            {
                Time = ToSeconds(a[0]) * SEC_TO_MS,
                Id = int.Parse(a[1]),
                X = a[2],
                Y = a[3]
            };
        }

        private ChartEvent ParseSpeed(string[] a)
        {
            if (a.Length < 2) return null;
            return new SpeedEvent { Time = ToSeconds(a[0]) * SEC_TO_MS, Speed = a[1] };
        }

        private ChartEvent ParseEffect(string[] a)
        {
            if (a.Length < 3) return null;
            return new EffectEvent { Time = ToSeconds(a[0]) * SEC_TO_MS, Type = a[1], Size = a[2] };
        }

        private ChartEvent ParseCamera(string[] a)
        {
            if (a.Length < 3) return null;
            return new CameraEvent { Time = ToSeconds(a[0]) * SEC_TO_MS, X = a[1], Y = a[2] };
        }

        private ChartEvent ParseBug(string[] a)
        {
            if (a.Length < 4) return null;
            return new BugEvent
            {
                Time = ToSeconds(a[0]) * SEC_TO_MS,
                Type = a[1],
                X = a[2],
                Id = int.Parse(a[3]),
                HitTime = ToSeconds(a[0]) * SEC_TO_MS,
            };
        }

        private ChartEvent ParseEnd(string[] a)
        {
            if (a.Length < 1) return null;
            return new EndEvent { Time = ToSeconds(a[0]) * SEC_TO_MS };
        }

        private ChartEvent ParseVar(string[] a)
        {
            if (a.Length < 3) return null;
            return new VarEvent { Time = ToSeconds(a[0]) * SEC_TO_MS, Name = a[1], Value = a[2] };
        }
    }
}
