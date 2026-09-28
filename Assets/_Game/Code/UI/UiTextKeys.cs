using System.Text;
using Game.Battle;
using Game.Core;

namespace Game.UI
{
    public static class UiTextKeys
    {
        public const string ChooseFallacy = "menu.choose_fallacy";
        public const string ChoosePremise = "menu.choose_premise";
        public const string CoherenceCaption = "hud.coherence";
        public const string AudienceCaption = "hud.audience";
        public const string MoraleLabel = "hud.morale";
        public const string GameSubtitle = "system.game_subtitle";

        private const string ActionPrefix = "action.";
        private const string RejectionPrefix = "rejection.";

        public static string For(ActionId action) => ActionPrefix + SnakeCase(action.ToString());

        public static string For(RejectionReason reason) => RejectionPrefix + SnakeCase(reason.ToString());

        public static string SnakeCase(string pascalCase)
        {
            var builder = new StringBuilder(pascalCase.Length + 4);
            for (var index = 0; index < pascalCase.Length; index++)
            {
                var character = pascalCase[index];
                if (char.IsUpper(character) && index > 0)
                {
                    builder.Append('_');
                }

                builder.Append(char.ToLowerInvariant(character));
            }

            return builder.ToString();
        }
    }
}
