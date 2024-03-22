namespace ItsStardewTime.Framework
{
    /// <summary>Contains information about a change to the <see cref="TimeHelper.TickProgress"/> value.</summary>
    internal class TickProgressChangedEventArgs : EventArgs
    {
        /*********
        ** Accessors
        *********/
        /// <summary>The previous progress value.</summary>
        public double PreviousProgress { get; }

        /// <summary>The new progress value.</summary>
        public double NewProgress { get; }

        /// <summary>Whether a new tick occurred since the last check.</summary>
        public bool TimeChanged => NewProgress < PreviousProgress;


        /*********
        ** Public methods
        *********/
        /// <summary>Construct an instance.</summary>
        /// <param name="previousProgress">The previous progress value.</param>
        /// <param name="newProgress">The new progress value.</param>
        public TickProgressChangedEventArgs(double previousProgress, double newProgress)
        {
            PreviousProgress = previousProgress;
            NewProgress = newProgress;
        }
    }
}
