using mEmEmE_Act.Game;
using osu.Framework.iOS;

namespace mEmEmE_Act.iOS
{
    /// <inheritdoc />
    public class AppDelegate : GameApplicationDelegate
    {
        /// <inheritdoc />
        protected override osu.Framework.Game CreateGame() => new mEmEmE_ActGame();
    }
}
