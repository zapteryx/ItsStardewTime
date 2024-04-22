using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace ItsStardewTime.Framework
{
    internal class TimeSpeed
    {
        /// <summary>Provides helper methods for tracking time flow.</summary>
        internal readonly TimeHelper TimeHelper = new(useGameTimeInterval: true);

        private readonly TimeHelper _objectTimeHelper = new(useGameTimeInterval: false);

        private readonly IMonitor _monitor;
        private readonly int _owningScreenId;

        /// <summary>Whether the player has manually frozen (<c>true</c>) or resumed (<c>false</c>) time.</summary>
        private bool? _manualFreeze;

        public bool ManualFreeze
        {
            get => _manualFreeze ?? false;
        }

        /// <summary>The reason time would be frozen automatically if applicable, regardless of <see cref="_manualFreeze"/>.</summary>
        public AutoFreezeReason AutoFreeze { get; private set; } = AutoFreezeReason.None;

        /// <summary>
        /// Whether time should be frozen.
        /// Freeze time when ManualFreeze is <c>true</c> or AutoFreeze is not <see cref="AutoFreezeReason.None"/>.
        /// I.e. when a manual freeze is requested or when the game is paused due to automatically detected conditions.
        /// </summary>
        internal bool IsTimeFrozen => ManualFreeze || AutoFreeze != AutoFreezeReason.None;

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
            _monitor = monitor;
            _owningScreenId = Context.ScreenId;

            TimeHelper.WhenTickProgressChanged(OnTickProgressed);
            _objectTimeHelper.WhenTickProgressChanged(OnObjectTickProgressed);
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
            _objectTimeHelper.Update();
        }

        /// <summary>Raised after the <see cref="TimeHelper.TickProgress"/> value changes.</summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void OnTickProgressed(object? sender, TickProgressChangedEventArgs e)
        {
            if (IsTimeFrozen)
            {
                TimeHelper.TickProgress = e.TimeChanged ? 0 : e.PreviousProgress;
                return;
            }

            if (e.TimeChanged)
            {
                TimeHelper.TickProgress = ScaleTickProgress(TimeHelper.TickProgress, TickInterval);
            }
            else
            {
                TimeHelper.TickProgress =
                    e.PreviousProgress +
                    ScaleTickProgress(e.NewProgress - e.PreviousProgress, TickInterval);
            }
        }

        private void OnObjectTickProgressed(object? sender, TickProgressChangedEventArgs e)
        {
            if (!IsTimeFrozen)
            {
                _objectTimeHelper.TickProgress = 0;
                return;
            }

            if (e.TimeChanged)
            {
                _objectTimeHelper.TickProgress = ScaleTickProgress(_objectTimeHelper.TickProgress, TickInterval);
                FrozenTick?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _objectTimeHelper.TickProgress =
                    e.PreviousProgress +
                    ScaleTickProgress(e.NewProgress - e.PreviousProgress, TickInterval);
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
            if (!CurrentPlayerOwnsInstance()) return false;

            return Context.IsWorldReady;
        }

        private bool CurrentPlayerOwnsInstance()
        {
            return _owningScreenId == Context.ScreenId;
        }

        /// <summary>
        /// Update the <see cref="TickInterval"/> and <see cref="AutoFreeze"/> and <see cref="_manualFreeze"/> values.
        /// </summary>
        /// <param name="tickInterval"></param>
        /// <param name="autoFreeze"></param>
        /// <param name="manualOverride">An explicit freeze (<c>true</c>) or unfreeze (<c>false</c>) requested by the player, if applicable.</param>
        /// <param name="clearPreviousOverrides">Whether to clear any previous explicit overrides.</param>
        /// <param name="notifyOfUpdates"></param>
        /// <param name="notifyOfMultiplayerUpdates"></param>
        internal void UpdateTimeSpeed
        (
            int? tickInterval = null,
            AutoFreezeReason? autoFreeze = null,
            bool? manualOverride = null,
            bool clearPreviousOverrides = false,
            bool notifyOfUpdates = false,
            bool notifyOfMultiplayerUpdates = false
        )
        {
            bool? was_manual_freeze = _manualFreeze;
            AutoFreezeReason was_auto_freeze = AutoFreeze;
            bool was_frozen = IsTimeFrozen;
            int prior_tick_interval = TickInterval;

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
                _manualFreeze = manualOverride.Value;
            else if (clearPreviousOverrides)
                _manualFreeze = null;

            // clear manual unfreeze if it's no longer needed
            if (_manualFreeze == false && AutoFreeze == AutoFreezeReason.None)
                _manualFreeze = null;

            if (was_auto_freeze != AutoFreeze)
                _monitor.Log($"Auto freeze changed from {was_auto_freeze} to {AutoFreeze}.");
            if (was_manual_freeze != _manualFreeze)
                _monitor.Log(
                    $"Manual freeze changed from {was_manual_freeze?.ToString() ?? "null"} to {_manualFreeze?.ToString() ?? "null"}.");
            if (prior_tick_interval != TickInterval)
                _monitor.Log($"TickInterval changed from {prior_tick_interval} to {TickInterval}.");


            if (notifyOfUpdates || notifyOfMultiplayerUpdates && Context.IsMultiplayer)
            {
                switch (AutoFreeze)
                {
                    case AutoFreezeReason.FrozenAtTime when IsTimeFrozen && !was_frozen:
                        _monitor.Log($"Time automatically set to frozen at {Game1.timeOfDay}.", LogLevel.Debug);
                        Notifier.ShortNotify(I18n.Message_OnTimeChange_TimeStopped());
                        break;

                    case AutoFreezeReason.FrozenAtTime when IsTimeFrozen &&
                                                            (!Context.IsMultiplayer ||
                                                             notifyOfMultiplayerUpdates && !was_frozen):
                        Notifier.ShortNotify(I18n.Message_OnLocationChange_TimeStoppedGlobally());
                        break;

                    case AutoFreezeReason.FrozenForLocation when IsTimeFrozen &&
                                                                 (!Context.IsMultiplayer ||
                                                                  notifyOfMultiplayerUpdates && !was_frozen):
                        Notifier.ShortNotify(I18n.Message_OnLocationChange_TimeStoppedHere());
                        break;

                    case AutoFreezeReason.None when !IsTimeFrozen && (was_frozen || prior_tick_interval != TickInterval) &&
                                                    (!Context.IsMultiplayer || notifyOfMultiplayerUpdates):
                        Notifier.ShortNotify(I18n.Message_OnLocationChange_TimeSpeedHere(seconds: TickInterval / 1000));
                        break;

                    default:
                        break;
                }
            }
        }
    }
}