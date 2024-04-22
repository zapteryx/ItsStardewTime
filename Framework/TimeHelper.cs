using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime.Framework
{
    /// <summary>Provides helper methods for tracking time flow.</summary>
    internal class TimeHelper
    {
        /*********
        ** Fields
        *********/
        /// <summary>The previous tick progress.</summary>
        private double _previousProgress;

        /// <summary>The handlers to notify when the tick progress changes.</summary>
        private event EventHandler<TickProgressChangedEventArgs>? Handlers;

        private double _tickProgressBackingStore;
        private int _lastGameTime = Game1.timeOfDay;
        private int _lastGameTimeInterval;
        private readonly bool _useGameTimeInterval;

        /*********
        ** Accessors
        *********/
        /// <summary>The game's default tick interval in milliseconds for the current location.</summary>
        public static double CurrentDefaultTickInterval => Game1.realMilliSecondsPerGameTenMinutes + (Game1.MasterPlayer.currentLocation?.ExtraMillisecondsPerInGameMinute ?? 0);

        /// <summary>The percentage of the <see cref="CurrentDefaultTickInterval"/> that's elapsed since the last tick.</summary>
        public double TickProgress
        {
            get => (double)TickProgressBackingStore / CurrentDefaultTickInterval;
            set => TickProgressBackingStore = value * CurrentDefaultTickInterval;
        }

        private double TickProgressBackingStore
        {
            get
            {
                    return _tickProgressBackingStore;
            }
            set
            {
                _tickProgressBackingStore = value;
                if (_useGameTimeInterval && (!Context.IsMultiplayer || Context.IsMainPlayer))
                {
                    Game1.gameTimeInterval = (int)value;
                }
            }
        }

        public TimeHelper(bool useGameTimeInterval)
        {
            _useGameTimeInterval = useGameTimeInterval;
        }


        /*********
        ** Public methods
        *********/
        /// <summary>Update the time tracking.</summary>
        public void Update()
        {
            int time = Game1.timeOfDay;
            if (time != _lastGameTime || (_useGameTimeInterval ? Game1.gameTimeInterval < _lastGameTimeInterval : TickProgress >= 1.0))
            {
                _lastGameTime = time;
                _tickProgressBackingStore = 0;
            }

            double previous_progress = _previousProgress;
            double tick_progress = TickProgress;

            if (previous_progress != tick_progress)
            {
                Handlers?.Invoke(null, new TickProgressChangedEventArgs(previous_progress, tick_progress));
            }

            _previousProgress = TickProgress;
            _tickProgressBackingStore += Game1.currentGameTime.ElapsedGameTime.Milliseconds;
            _lastGameTimeInterval = Game1.gameTimeInterval;
        }

        /// <summary>Register an event handler to notify when the <see cref="TickProgress"/> changes.</summary>
        /// <param name="handler">The event handler to notify.</param>
        public void WhenTickProgressChanged(EventHandler<TickProgressChangedEventArgs> handler)
        {
            Handlers += handler;
        }

        public void SetTime(double tickProgress)
        {
            TickProgress = tickProgress;
            _previousProgress = tickProgress;
        }
    }
}
