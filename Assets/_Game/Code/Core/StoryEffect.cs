namespace Game.Core
{
    public readonly struct StoryEffect
    {
        public const string DefaultValue = "true";

        public readonly string Flag;
        public readonly string Value;

        public StoryEffect(string flag, string value = DefaultValue)
        {
            Flag = flag;
            Value = string.IsNullOrEmpty(value) ? DefaultValue : value;
        }

        public bool TryApply(SaveData save, out string error) => SaveFlags.TrySet(save, Flag, Value, out error);

        public override string ToString() => $"{Flag}={Value}";
    }
}
