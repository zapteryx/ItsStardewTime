namespace ItsStardewTime.Framework
{
    internal class TimeSpeedChangedEventArgs : EventArgs
    {
        public int PreviousTickInterval { get; }

        public int NewTickInterval { get; }

        public bool TimeIntervalChanged => NewTickInterval != PreviousTickInterval;

        public bool PreviousFreezeState { get; }

        public bool NewFreezeState { get; }

        public bool FreezeStateChanged => PreviousFreezeState != NewFreezeState;

        public TimeSpeedChangedEventArgs(int previousTickInterval, int newTickInterval, bool previousFreezeState, bool newFreezeState)
        {
            PreviousTickInterval = previousTickInterval;
            NewTickInterval = newTickInterval;
            PreviousFreezeState = previousFreezeState;
            NewFreezeState = newFreezeState;
        }
    }
}
