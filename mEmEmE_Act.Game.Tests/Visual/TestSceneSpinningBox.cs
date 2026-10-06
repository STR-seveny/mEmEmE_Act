using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace mEmEmE_Act.Game.Tests.Visual
{
    [TestFixture]
    public partial class TestSceneSpinningBox : mEmEmE_ActTestScene
    {
        // Add visual tests to ensure correct behaviour of your game: https://github.com/ppy/osu-framework/wiki/Development-and-Testing
        // You can make changes to classes associated with the tests and they will recompile and update immediately.

        public TestSceneSpinningBox()
        {
            Add(new Box
            {
                Anchor = Anchor.Centre,
            });
        }
    }
}
