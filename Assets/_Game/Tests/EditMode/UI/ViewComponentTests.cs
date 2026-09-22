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
        public void ExplorationCamera_FramesTheDuelAboveTheHudWithAnUnboundCameraAndFollowsThePlayerAfter()
        {
            host = new GameObject("camera");
            var player = new GameObject("player").transform;
            player.SetParent(host.transform);
            player.position = new Vector3(1f, 0.5f, 0f);
            var focus = new GameObject(ExplorationCamera.DuelFocusName).transform;
            focus.SetParent(host.transform);
            var duelHost = new GameObject(ExplorationCamera.DuelCameraName);
            duelHost.transform.SetParent(host.transform);
            var virtualCamera = host.AddComponent<CinemachineCamera>();
            var duelCamera = duelHost.AddComponent<CinemachineCamera>();
            var explorationCamera = host.AddComponent<ExplorationCamera>();
            explorationCamera.Configure(virtualCamera, duelCamera, 7f, focus);
            virtualCamera.Follow = player;

            Assert.IsFalse(duelCamera.enabled, "the duel camera waits for a duel");
            Assert.AreSame(focus, duelCamera.Follow);
            Assert.Greater((int)duelCamera.Priority, (int)virtualCamera.Priority, "the duel camera wins while it is on");
            Assert.IsNull(duelHost.GetComponent<CinemachineConfiner2D>(), "the duel camera ignores the map bounds");

            explorationCamera.FrameDuel(new Vector2(3.04f, 1.5f));

            Assert.IsTrue(explorationCamera.IsFramingDuel);
            Assert.IsTrue(duelCamera.enabled);
            Assert.AreSame(focus, explorationCamera.FollowTarget, "the camera follows the duel, not Kleia");
            Assert.AreSame(player, virtualCamera.Follow, "the exploration camera keeps Kleia for the way back");
            Assert.AreEqual(new Vector3(3.0625f, 1.5f - HudLayout.BottomStripFraction * 7f, 0f), focus.position, "the duel sits in the middle of the area above the hud, snapped to the art pixel grid");
            Assert.AreEqual(7f, virtualCamera.Lens.OrthographicSize, "the battle keeps the exploration zoom");
            Assert.AreEqual(7f, duelCamera.Lens.OrthographicSize, "the battle keeps the exploration zoom");

            explorationCamera.FrameExploration();

            Assert.IsFalse(explorationCamera.IsFramingDuel);
            Assert.IsFalse(duelCamera.enabled);
            Assert.AreSame(player, explorationCamera.FollowTarget);
            Assert.AreEqual(7f, virtualCamera.Lens.OrthographicSize);
        }
    }
}
