using System;
using Game.Content;
using UnityEngine;

namespace Game.UI
{
    [Serializable]
    public sealed class MapOpponent
    {
        [SerializeField] private ActorView actor;
        [SerializeField] private OpponentSO opponent;
        [SerializeField] private Sprite portrait;

        public MapOpponent()
        {
        }

        public MapOpponent(ActorView actorView, OpponentSO opponentData, Sprite portraitSprite)
        {
            actor = actorView;
            opponent = opponentData;
            portrait = portraitSprite;
        }

        public ActorView Actor => actor;

        public OpponentSO Opponent => opponent;

        public Sprite Portrait => portrait;

        public bool IsPlaced => actor && actor.Walker != null && opponent;
    }
}
