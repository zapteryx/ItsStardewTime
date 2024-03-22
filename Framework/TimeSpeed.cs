using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ItsStardewTime.Framework
{
    internal class TimeSpeed
    {
        /// <summary>Provides helper methods for tracking time flow.</summary>
        internal readonly TimeHelper TimeHelper = new(useGameTimeInterval: true);
        private readonly TimeHelper ObjectTimeHelper = new(useGameTimeInterval: false);

        private readonly IMonitor Monitor;
        private readonly int OwningScreenID;

        /// <summary>Whether the player has manually frozen (<c>true</c>) or resumed (<c>false</c>) time.</summary>
        private bool? ManualFreeze;

        /// <summary>The reason time would be frozen automatically if applicable, regardless of <see cref="ManualFreeze"/>.</summary>
        public AutoFreezeReason AutoFreeze { get; private set; } = AutoFreezeReason.None;

        /// <summary>Whether time should be frozen.</summary>
        internal bool IsTimeFrozen =>
            ManualFreeze == true
            || AutoFreeze != AutoFreezeReason.None && ManualFreeze != false;

        /// <summary>Backing field for <see cref="TickInterval"/>.</summary>
        private int _tickInterval = Game1.realMilliSecondsPerGameTenMinutes;

        /// <summary>The number of milliseconds per 10-game-minutes to apply.</summary>
        internal int TickInterval
        {
            get => _tickInterval;
            private set => _tickInterval = Math.Max(value, 1);
        }

        /// <summary>Raised when time has been frozen for a full tick (10 minutes).</summary>
        internal event EventHandler<EventArgs>? FrozenTick;

        /// <inheritdoc />
        public TimeSpeed(IModHelper helper, IMonitor monitor)
        {
            Monitor = monitor;
            OwningScreenID = Context.ScreenId;

            TimeHelper.WhenTickProgressChanged(OnTickProgressed);
            ObjectTimeHelper.WhenTickProgressChanged(OnObjectTickProgressed);
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        /// <inheritdoc cref="IGameLoopEvents.UpdateTicked"/>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
        {
            if (!ShouldEnable())
                return;

            TimeHelper.Update();
            ObjectTimeHelper.Update();
        }

        /// <summary>Raised after the <see cref="TimeHelper.TickProgress"/> value changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTickProgressed(object? sender, TickProgressChangedEventArgs e)
        {
            if (IsTimeFrozen)
            {
                TimeHelper.TickProgress = e.TimeChanged ? 0 : e.PreviousProgress;
            }
            else
            {
                if (e.TimeChanged)
                    TimeHelper.TickProgress = ScaleTickProgress(TimeHelper.TickProgress, TickInterval);
                else
                    TimeHelper.TickProgress = e.PreviousProgress + ScaleTickProgress(e.NewProgress - e.PreviousProgress, TickInterval);
            }
        }

        private void OnObjectTickProgressed(object? sender, TickProgressChangedEventArgs e)
        {
            if (IsTimeFrozen)
            {
                if (e.TimeChanged)
                {
                    ObjectTimeHelper.TickProgress = ScaleTickProgress(ObjectTimeHelper.TickProgress, TickInterval);
                    FrozenTick?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    ObjectTimeHelper.TickProgress = e.PreviousProgress + ScaleTickProgress(e.NewProgress - e.PreviousProgress, TickInterval);
                }
            }
            else
            {
                ObjectTimeHelper.TickProgress = 0;
            }
        }

        /// <summary>Get the adjusted progress towards the next 10-game-minute tick.</summary>
        /// <param name="progress">The current progress.</param>
        /// <param name="newTickInterval">The new tick interval.</param>
        private static double ScaleTickProgress(double progress, int newTickInterval)
        {
            return progress * TimeHelper.CurrentDefaultTickInterval / newTickInterval;
        }

        /// <summary>Get whether time features should be enabled.</summary>
        /// <param name="forInput">Whether to check for input handling.</param>
        private bool ShouldEnable()
        {
            if (!CurrentPlayerOwnsInstance())
            {
                return false;
            }

            if (!Context.IsWorldReady)
            {
                return false;
            }

            return true;
        }

        private bool CurrentPlayerOwnsInstance()
        {
            return OwningScreenID == Context.ScreenId;
        }

        /// <summary>
        /// Update the <see cref="TickInterval"/> and <see cref="AutoFreeze"/> and <see cref="ManualFreeze"/> values.
        /// </summary>
        /// <param name="tickInterval"></param>
        /// <param name="autoFreeze"></param>
        /// <param name="manualOverride">An explicit freeze (<c>true</c>) or unfreeze (<c>false</c>) requested by the player, if applicable.</param>
        /// <param name="clearPreviousOverrides">Whether to clear any previous explicit overrides.</param>
        /// <param name="notifyOfUpdates"></param>
        /// <param name="notifyOfMultiplayerUpdates"></param>
        internal void UpdateTimeSpeed(
            int? tickInterval = null,
            AutoFreezeReason? autoFreeze = null,
            bool? manualOverride = null,
            bool clearPreviousOverrides = false,
            bool notifyOfUpdates = false,
            bool notifyOfMultiplayerUpdates = false)
        {
            bool? wasManualFreeze = ManualFreeze;
            AutoFreezeReason wasAutoFreeze = AutoFreeze;
            bool wasFrozen = IsTimeFrozen;
            int priorTickInterval = TickInterval;

            if (autoFreeze != null)
            {
                AutoFreeze = autoFreeze.Value;
            }

            if (tickInterval != null)
            {
                TickInterval = tickInterval.Value;
            }

            // update manual freeze
            if (manualOverride.HasValue)
                ManualFreeze = manualOverride.Value;
            else if (clearPreviousOverrides)
                ManualFreeze = null;

            // clear manual unfreeze if it's no longer needed
            if (ManualFreeze == false && AutoFreeze == AutoFreezeReason.None)
                ManualFreeze = null;

            if (wasAutoFreeze != AutoFreeze)
                Monitor.Log($"Auto freeze changed from {wasAutoFreeze} to {AutoFreeze}.");
            if (wasManualFreeze != ManualFreeze)
                Monitor.Log($"Manual freeze changed from {wasManualFreeze?.ToString() ?? "null"} to {ManualFreeze?.ToString() ?? "null"}.");
            if (priorTickInterval != TickInterval)
                Monitor.Log($"TickInterval changed from {priorTickInterval} to {TickInterval}.");


            if (notifyOfUpdates || notifyOfMultiplayerUpdates && Context.IsMultiplayer)
            {
                switch (AutoFreeze)
                {
                    case AutoFreezeReason.FrozenAtTime when IsTimeFrozen && !wasFrozen:
                        Monitor.Log($"Time automatically set to frozen at {Game1.timeOfDay}.", LogLevel.Debug);
                        Notifier.ShortNotify(I18n.Message_OnTimeChange_TimeStopped());
                        break;

                    case AutoFreezeReason.FrozenAtTime when IsTimeFrozen && (!Context.IsMultiplayer || notifyOfMultiplayerUpdates && !wasFrozen):
                        Notifier.ShortNotify(I18n.Message_OnLocationChange_TimeStoppedGlobally());
                        break;

                    case AutoFreezeReason.FrozenForLocation when IsTimeFrozen && (!Context.IsMultiplayer || notifyOfMultiplayerUpdates && !wasFrozen):
                        Notifier.ShortNotify(I18n.Message_OnLocationChange_TimeStoppedHere());
                        break;

                    case AutoFreezeReason.None when !IsTimeFrozen && (wasFrozen || priorTickInterval != TickInterval) && (!Context.IsMultiplayer || notifyOfMultiplayerUpdates):
                        Notifier.ShortNotify(I18n.Message_OnLocationChange_TimeSpeedHere(seconds: TickInterval / 1000));
                        break;

                    default:
                        break;
                }
            }
        }
    }
}
