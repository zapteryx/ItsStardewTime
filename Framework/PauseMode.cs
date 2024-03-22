namespace ItsStardewTime.Framework
{
    public enum PauseMode
    {
        Fair,
        Any,
        All,
        Host,
        Half,
        Majority,

        Auto, // Deprecated name. Renamed to Fair but keeping around so that old configs don't break
    }

    static class PauseModeMethods
    {
        public static bool IsDeprecated(this PauseMode value)
        {
            return value == PauseMode.Auto;
        }
    }
}
