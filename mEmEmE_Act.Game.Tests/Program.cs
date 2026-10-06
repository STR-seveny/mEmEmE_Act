using osu.Framework;
using osu.Framework.Platform;

namespace mEmEmE_Act.Game.Tests
{
    public static class Program
    {
        public static void Main()
        {
            using (GameHost host = Host.GetSuitableDesktopHost("visual-tests"))
            using (var game = new mEmEmE_ActTestBrowser())
                host.Run(game);
        }
    }
}
