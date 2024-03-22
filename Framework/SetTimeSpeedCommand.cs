using StardewValley;

namespace ItsStardewTime.Framework
{
    internal class SetTimeSpeedCommand
    {
        public double TickProgress;
        public int TickInterval;
        public int TimeOfDay;
        public bool? ManualFreeze;
        public AutoFreezeReason AutoFreeze;
        internal bool IsTimeFrozen =>
            ManualFreeze == true
            || AutoFreeze != AutoFreezeReason.None && ManualFreeze != false;

        // Required for json deserialization
        public SetTimeSpeedCommand()
        {
        }

        public SetTimeSpeedCommand(int tickInterval, bool? manualFreeze, AutoFreezeReason autoFreeze)
        {
            TickProgress = TimeMaster.TimeSpeed.TimeHelper.TickProgress;
            TickInterval = tickInterval;
            TimeOfDay = Game1.timeOfDay;
            ManualFreeze = manualFreeze;
            AutoFreeze = autoFreeze;
        }
    }
}