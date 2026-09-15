using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Game.UI
{
    public sealed class ExplorationCamera : MonoBehaviour
    {
        [SerializeField] private CinemachineCamera virtualCamera;
        [SerializeField] private float explorationOrthographicSize = 7f;
        [SerializeField] private float battleOrthographicSize = 5f;

        public event Action<float> ZoomRequested;

        public CinemachineCamera VirtualCamera => virtualCamera;

        public float ExplorationOrthographicSize => explorationOrthographicSize;

        public float BattleOrthographicSize => battleOrthographicSize;

        public float TargetOrthographicSize { get; private set; }

        public void Configure(CinemachineCamera camera, float explorationSize, float battleSize)
        {
            virtualCamera = camera;
            explorationOrthographicSize = explorationSize;
            battleOrthographicSize = battleSize;
            TargetOrthographicSize = explorationSize;
        }

        public void Follow(Transform target)
        {
            virtualCamera.Follow = target;
        }

        public void FrameBattle() => RequestZoom(battleOrthographicSize);

        public void FrameExploration() => RequestZoom(explorationOrthographicSize);

        private void RequestZoom(float size)
        {
            TargetOrthographicSize = size;
            var lens = virtualCamera.Lens;
            lens.OrthographicSize = size;
            virtualCamera.Lens = lens;
            ZoomRequested?.Invoke(size);
        }
    }
}
