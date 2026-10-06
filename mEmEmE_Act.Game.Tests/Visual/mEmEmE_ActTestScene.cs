using osu.Framework.Testing;

namespace mEmEmE_Act.Game.Tests.Visual
{
    public abstract partial class mEmEmE_ActTestScene : TestScene
    {
        protected override ITestSceneTestRunner CreateRunner() => new mEmEmE_ActTestSceneTestRunner();

        private partial class mEmEmE_ActTestSceneTestRunner : mEmEmE_ActGameBase, ITestSceneTestRunner
        {
            private TestSceneTestRunner.TestRunner runner;

            protected override void LoadAsyncComplete()
            {
                base.LoadAsyncComplete();
                Add(runner = new TestSceneTestRunner.TestRunner());
            }

            public void RunTestBlocking(TestScene test) => runner.RunTestBlocking(test);
        }
    }
}
