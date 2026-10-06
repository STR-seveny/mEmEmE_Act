namespace mEmEmE_Act.Game.Charts
{
    public abstract class ChartEvent
    {
        public double Time { get; set; }
        public abstract void Execute(Playfield playfield);
    }

    public class NoteEvent : ChartEvent
    {
        public double HitTime { get; set; }
        public string Type { get; set; }
        public string X { get; set; }
        public int Id { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.SpawnNote(Id, X, Type, HitTime);
    }

    public class BugEvent : ChartEvent
    {
        public double HitTime { get; set; }
        public string Type { get; set; }
        public string X { get; set; }
        public int Id { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.SpawnBug(Id, X, Type, HitTime);
    }

    public class MoveEvent : ChartEvent
    {
        public int Id { get; set; }
        public string X { get; set; }
        public string Y { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.MoveObject(Id, X, Y);
    }

    public class SpeedEvent : ChartEvent
    {
        public string Speed { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.ChangeSpeed(Speed);
    }

    public class EffectEvent : ChartEvent
    {
        public string Type { get; set; }
        public string Size { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.PlayEffect(Type, Size);
    }

    public class CameraEvent : ChartEvent
    {
        public string X { get; set; }
        public string Y { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.MoveCamera(X, Y);
    }

    public class PlayEvent : ChartEvent
    {
        public double Start { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.StartMusic(Time, Start);
    }

    public class EndEvent : ChartEvent
    {
        public override void Execute(Playfield playfield)
            => playfield.EndGame();
    }

    public class VarEvent : ChartEvent
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public override void Execute(Playfield playfield)
            => playfield.SetVariable(Name, Value);
    }
}
