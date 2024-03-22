namespace ItsStardewTime.Framework
{
    public enum TimeSpeedMode
    {
        Average,
        Host,
        Min,
        Max,
    }

    static class TimeSpeedModeMethods
    {
        public static bool IsDeprecated(this TimeSpeedMode _)
        {
            return false;
        }
    }
}
