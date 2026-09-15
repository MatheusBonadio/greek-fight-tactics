using System;
using Game.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Game.Net
{
    public static class SettingsJson
    {
        public const string VolumeField = "master_volume";
        public const string TextSpeedField = "text_speed";
        public const string AssistedModeField = "assisted_mode";

        public static string Serialize(GameSettings settings)
        {
            if (settings == null)
            {
                throw new ArgumentNullException(nameof(settings));
            }

            return new JObject
            {
                [VolumeField] = Math.Round(settings.MasterVolume, 2),
                [TextSpeedField] = settings.TextSpeed.ToString().ToLowerInvariant(),
                [AssistedModeField] = settings.AssistedMode
            }.ToString(Formatting.Indented);
        }

        public static GameSettings Deserialize(string json)
        {
            var settings = new GameSettings();
            if (!JsonFields.TryParseObject(json, out var document))
            {
                return settings;
            }

            if (document[VolumeField] is JValue { Type: JTokenType.Float or JTokenType.Integer } volume)
            {
                settings.MasterVolume = volume.Value<float>();
            }

            if (document[TextSpeedField] is JValue { Type: JTokenType.String } speedValue &&
                Enum.TryParse(speedValue.Value<string>(), true, out TextSpeed speed) &&
                Enum.IsDefined(typeof(TextSpeed), speed))
            {
                settings.TextSpeed = speed;
            }

            if (document[AssistedModeField] is JValue { Type: JTokenType.Boolean } assisted)
            {
                settings.AssistedMode = assisted.Value<bool>();
            }

            return settings;
        }
    }
}
