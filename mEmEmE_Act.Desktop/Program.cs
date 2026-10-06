using mEmEmE_Act.Game;
using osu.Framework;
using osu.Framework.Platform;

namespace mEmEmE_Act.Desktop
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost(@"mEmEmE(Act)"))
            using (osu.Framework.Game game = new mEmEmE_ActGame())
                host.Run(game);
        }
    }
}
