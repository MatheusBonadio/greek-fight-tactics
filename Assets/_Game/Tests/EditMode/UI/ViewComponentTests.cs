using Game.Core;
using Game.Grid;
using Game.Tests.EditMode.Grid;
using Game.UI;
using NUnit.Framework;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.Tests.EditMode.UI
{
    public sealed class ViewComponentTests
    {
        private GameObject host;

        [TearDown]
        public void TearDown()
        {
            if (host != null)
            {
                Object.DestroyImmediate(host);
            }
        }

        [TestCase(Facing.SouthEast, false)]
        [TestCase(Facing.NorthEast, false)]
        [TestCase(Facing.SouthWest, true)]
        [TestCase(Facing.NorthWest, true)]
        public void ActorView_MirrorsWestFacingsInCode(Facing facing, bool flipped)
        {
            host = new GameObject("actor");
            var renderer = host.AddComponent<SpriteRenderer>();
            var view = host.AddComponent<ActorView>();

            view.Configure(new Vector2Int(3, 1), facing, renderer, null);

            Assert.AreEqual(flipped, renderer.flipX);
            Assert.AreEqual((Vector3)IsoMath.GridToWorld(new Coord(3, 1)), host.transform.position);
        }

        [Test]
        public void ActorView_FollowsTheBoundWalker()
        {
            host = new GameObject("actor");
            var renderer = host.AddComponent<SpriteRenderer>();
            var view = host.AddComponent<ActorView>();
            view.Configure(new Vector2Int(2, 2), Facing.SouthEast, renderer, null);
            var walker = new GridWalker(GridFactory.Open(6, 6), 1, new Coord(2, 2));

            view.Bind(walker);
            walker.Follow(new[] { new Coord(1, 2) });
            view.Bind(walker);

            Assert.IsTrue(renderer.flipX);
            Assert.AreEqual(Facing.SouthWest, walker.Facing);
        }

        [Test]
        public void ExplorationCamera_ExposesTheBattleZoomHook()
        {
            host = new GameObject("camera");
            var virtualCamera = host.AddComponent<CinemachineCamera>();
            var explorationCamera = host.AddComponent<ExplorationCamera>();
            explorationCamera.Configure(virtualCamera, 7f, 5f);
            var requested = 0f;
            explorationCamera.ZoomRequested += size => requested = size;

            explorationCamera.FrameBattle();

            Assert.AreEqual(5f, requested);
            Assert.AreEqual(5f, virtualCamera.Lens.OrthographicSize);

            explorationCamera.FrameExploration();

            Assert.AreEqual(7f, explorationCamera.TargetOrthographicSize);
            Assert.AreEqual(7f, virtualCamera.Lens.OrthographicSize);
        }
    }
}
