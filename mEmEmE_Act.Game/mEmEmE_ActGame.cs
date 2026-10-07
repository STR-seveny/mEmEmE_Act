using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Screens;

namespace mEmEmE_Act.Game
{
    public partial class mEmEmE_ActGame : mEmEmE_ActGameBase
    {
        private ScreenStack screenStack;

        [BackgroundDependencyLoader]
        private void load()
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    screenStack = new ScreenStack { RelativeSizeAxes = Axes.Both },

                    new DynamicText
                    {
                        Text = "Copyright © STRstudio2175 版权所有",
                        FontSize = 30,
                        Margin = new MarginPadding { Right = 10, Bottom = 10 },
                        Alpha = 0.5f,
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            screenStack.Push(new MainMenuScreen());
        }
    }
}
