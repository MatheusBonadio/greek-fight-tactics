using Game.Core;

namespace Game.Battle
{
    public interface IArgumentSource
    {
        bool TryNext(Debater opponent, out Argument argument);
    }
}
