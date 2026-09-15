using Game.Core;
using UnityEngine;

namespace Game.UI
{
    public sealed class BattleCameraDirector : MonoBehaviour
    {
        [SerializeField] private ExplorationCamera explorationCamera;

        private GameStateMachine machine;

        public void Configure(ExplorationCamera camera)
        {
            explorationCamera = camera;
        }

        public void Bind(GameStateMachine stateMachine)
        {
            Unbind();
            machine = stateMachine;
            if (machine != null)
            {
                machine.Changed += OnModeChanged;
            }
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            if (machine != null)
            {
                machine.Changed -= OnModeChanged;
            }

            machine = null;
        }

        private void OnModeChanged(GameMode from, GameMode to)
        {
            if (explorationCamera == null)
            {
                return;
            }

            if (to == GameMode.BattleIntro)
            {
                explorationCamera.FrameBattle();
            }
            else if (from == GameMode.BattleOutro && to == GameMode.Exploration)
            {
                explorationCamera.FrameExploration();
            }
        }
    }
}
