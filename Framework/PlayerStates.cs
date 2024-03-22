using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime.Framework
{
    internal class PlayerStates
    {
        private class PlayerState
        {
            private bool _isModified = true;
            private bool _isVoteForPauseAffirmative = false;
            private bool _isEventActive = false;
            private int _tickInterval = Game1.realMilliSecondsPerGameTenMinutes;
            private int _tickOfLastPause = 0;
            private int _ticksOfPriorPause = 0;
            private bool _isPaused = false;
            private AutoFreezeReason _autoFreezeReason = AutoFreezeReason.None;

            internal bool IsPaused => _isPaused || _autoFreezeReason != AutoFreezeReason.None;

            internal bool IsModified
            {
                get
                {
                    return _isModified;
                }
                set
                {
                    _isModified = value;
                }
            }

            internal int TotalTicksPaused
            {
                get
                {
                    if (_tickOfLastPause == 0)
                    {
                        return 0;
                    }

                    if (IsPaused)
                    {
                        return _ticksOfPriorPause + (Game1.ticks - _tickOfLastPause);
                    }
                    else
                    {
                        return _ticksOfPriorPause;
                    }
                }
            }

            internal bool IsVoteForPauseAffirmative
            {
                get => _isVoteForPauseAffirmative;
                set
                {
                    if (_isVoteForPauseAffirmative != value)
                    {
                        _isModified = true;
                    }
                    _isVoteForPauseAffirmative = value;
                }
            }

            internal bool IsEventActive
            {
                get => _isEventActive;
                set
                {
                    if (_isEventActive != value)
                    {
                        _isModified = true;
                    }
                    _isEventActive = value;
                }
            }

            internal int TickInterval
            {
                get => _tickInterval;
                set
                {
                    if (_tickInterval != value)
                    {
                        _isModified = true;
                    }
                    _tickInterval = value;
                }
            }

            internal AutoFreezeReason AutoFreezeReason
            {
                get => _autoFreezeReason;
                set
                {
                    if (_autoFreezeReason != value)
                    {
                        _isModified = true;
                    }
                    _autoFreezeReason = value;
                }
            }

            internal void UpdateBasedOnLocation(GameLocation location, ModConfig config)
            {
                AutoFreezeReason = config.ShouldFreeze(Game1.timeOfDay, location);
                TickInterval = config.GetMillisecondsPerTenMinuteInterval(location);
            }

            internal void UpdatePauseState(bool requestingPause)
            {
                if (_isPaused != requestingPause)
                {
                    _isModified = true;
                }

                _isPaused = requestingPause;
                if (requestingPause)
                {
                    _tickOfLastPause = Game1.ticks;
                }
                else if (_tickOfLastPause > 0)
                {
                    _ticksOfPriorPause += Game1.ticks - _tickOfLastPause;
                }
            }

            internal void DayStarted()
            {
                _ticksOfPriorPause = 0;
                if (_isPaused)
                {
                    _tickOfLastPause = Game1.ticks;
                }
                else
                {
                    _tickOfLastPause = 0;
                }
            }
        }

        private readonly Dictionary<long, PlayerState> _playerStates = new(4);
        private readonly ModConfig _config;
        private readonly IMonitor _monitor;
        private readonly IModHelper _helper;
        private readonly IManifest _manifest;
        private bool _recomputationRequired = true;
        private int _cachedTickInterval;
        private bool? _cachedManualFreeze;
        private AutoFreezeReason _cachedAutoFreeze;
        private bool? _freezeOverride;

        public PlayerStates(ModConfig config, IMonitor monitor, IModHelper helper, IManifest manifest)
        {
            _config = config;
            _monitor = monitor;
            _helper = helper;
            _manifest = manifest;
        }

        internal void Add(long playerID)
        {
            bool allVotesAreYes = _playerStates.Values.All(s => s.IsVoteForPauseAffirmative);
            PlayerState state = new();
            _playerStates.Add(playerID, state);
            var location = Game1.getFarmer(playerID)?.currentLocation;
            if (location == null)
            {
                _monitor.Log($"Location missing for player. PlayerID={playerID}, Farmer={Game1.getFarmer(playerID)}, Name={Game1.getFarmer(playerID)?.Name}", LogLevel.Info);
            }
            else
            {
                state.UpdateBasedOnLocation(location, _config);
            }

            if (playerID != Game1.MasterPlayer.UniqueMultiplayerID)
            {
                _helper.Multiplayer.SendMessage(_config.LockMonsters, Messages.SetLockMonstersMode, new string[1] { _manifest.UniqueID }, new long[1] { playerID });
                if (_config.EnableVotePause)
                {
                    state.IsVoteForPauseAffirmative = allVotesAreYes;
                    _helper.Multiplayer.SendMessage(allVotesAreYes, Messages.SetVoteState, new string[1] { _manifest.UniqueID }, new long[1] { playerID });
                    if (allVotesAreYes)
                    {
                        _helper.Multiplayer.SendMessage(I18n.Message_PlayerJoinedAPausedGame(Game1.getFarmer(playerID).Name), Messages.VoteUpdateMessage, new string[1] { _manifest.UniqueID });
                        if (_config.DisplayVotePauseMessages)
                        {
                            Notifier.NotifyInChatBox(I18n.Message_PlayerJoinedAPausedGame(Game1.getFarmer(playerID).Name));
                        }
                    }
                }
            }

            _recomputationRequired = true;
            UpdateAllClients();
        }

        internal void Remove(long playerID)
        {
            _playerStates.Remove(playerID);
            _recomputationRequired = true;
            UpdateAllClients();
        }

        internal void Clear()
        {
            _playerStates.Clear();
            _recomputationRequired = false;
        }

        internal void DayStarted()
        {
            foreach (var state in _playerStates.Values)
            {
                state.DayStarted();
                if (state.IsModified)
                {
                    _recomputationRequired = true;
                }
            }

            UpdateAllClients();
        }

        internal void SetFreezeOverride(bool? freezeOverride)
        {
            if (freezeOverride != _freezeOverride)
            {
                _recomputationRequired = true;
            }
            _freezeOverride = freezeOverride;
            _monitor.Log($"SetFreezeOverride({_freezeOverride}): _recomputationRequired={_recomputationRequired}", LogLevel.Trace);
            UpdateAllClients();
        }

        internal void PollForLocationUpdates()
        {
            bool lateToSendUpdates = _recomputationRequired;
            bool modifiedState = false;
            long mainPlayerID = Game1.MasterPlayer?.UniqueMultiplayerID ?? 0;
            foreach (var (playerID, state) in _playerStates)
            {
                if (playerID == mainPlayerID)
                {
                    // skip main player, because main player is updated on Warped event
                    continue;
                }

                if (Game1.getFarmer(playerID)?.currentLocation is GameLocation location)
                {
                    state.UpdateBasedOnLocation(location, _config);
                    if (state.IsModified)
                    {
                        modifiedState = true;
                        _recomputationRequired = true;
                    }
                }
            }

            // Only update clients every other poll to avoid sending 3 messages while zoning
            if (lateToSendUpdates && modifiedState)
            {
                UpdateAllClients();
            }
        }

        internal void UpdateLocation(long playerID, GameLocation location)
        {
            if (!_playerStates.TryGetValue(playerID, out var state))
            {
                _monitor.Log($"Found no player with ID={playerID}", LogLevel.Error);
                return;
            }

            state.UpdateBasedOnLocation(location, _config);
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdateTimeSpeedSettings(long playerID, int? tickInterval = null, AutoFreezeReason? autoFreeze = null, bool skipUpdate = false)
        {
            if (!_playerStates.TryGetValue(playerID, out var state))
            {
                _monitor.Log($"Found no player with ID={playerID}", LogLevel.Error);
                return;
            }

            if (tickInterval != null)
            {
                state.TickInterval = tickInterval.Value;
            }

            if (autoFreeze != null)
            {
                state.AutoFreezeReason = autoFreeze.Value;
            }

            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            if (!skipUpdate)
            {
                UpdateAllClients();
            }
        }

        internal void AdjustTickInterval(long playerID, int change)
        {
            if (!_playerStates.TryGetValue(playerID, out var state))
            {
                _monitor.Log($"Found no player with ID={playerID}", LogLevel.Error);
                return;
            }

            if (change < 0)
            {
                int minAllowed = Math.Min(state.TickInterval, -change);
                state.TickInterval = Math.Max(minAllowed, state.TickInterval + change);
            }
            else
            {
                state.TickInterval += change;
            }

            _monitor.Log($"Local tick length set to {state.TickInterval / 1000d: 0.##} seconds.", LogLevel.Info);
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdatePauseState(long playerID, bool requestingPause)
        {
            if (!_playerStates.TryGetValue(playerID, out var state))
            {
                _monitor.Log($"Found no player with ID={playerID}", LogLevel.Error);
                return;
            }

            state.UpdatePauseState(requestingPause);
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdateEventActivity(long playerID, bool isEventActive)
        {
            if (!_playerStates.TryGetValue(playerID, out var state))
            {
                _monitor.Log($"Found no player with ID={playerID}", LogLevel.Error);
                return;
            }

            state.IsEventActive = isEventActive;
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdateVote(long playerID, bool newVote)
        {
            if (!_playerStates.TryGetValue(playerID, out var state))
            {
                _monitor.Log($"Found no player with ID={playerID}", LogLevel.Error);
                return;
            }

            state.IsVoteForPauseAffirmative = newVote;
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            NotifyOfVoteUpdate(playerID, newVote);
            UpdateAllClients();
        }

        private int NotifyOfVoteUpdate(long playerID, bool newVote)
        {
            int votedYes = _playerStates.Values.Count(s => s.IsVoteForPauseAffirmative);
            if (_config.EnableVotePause)
            {
                if (newVote)
                {
                    _helper.Multiplayer.SendMessage(I18n.Message_PlayerVotedToPause(Game1.getFarmer(playerID).Name, votedYes, _playerStates.Count), Messages.VoteUpdateMessage, new[] { _manifest.UniqueID });
                    if (_config.DisplayVotePauseMessages)
                    {
                        Notifier.NotifyInChatBox(I18n.Message_PlayerVotedToPause(Game1.getFarmer(playerID).Name, votedYes, _playerStates.Count));
                    }
                }
                else
                {
                    _helper.Multiplayer.SendMessage(I18n.Message_PlayerVotedToUnpause(Game1.getFarmer(playerID).Name, votedYes, _playerStates.Count), Messages.VoteUpdateMessage, new[] { _manifest.UniqueID });
                    if (_config.DisplayVotePauseMessages)
                    {
                        Notifier.NotifyInChatBox(I18n.Message_PlayerVotedToUnpause(Game1.getFarmer(playerID).Name, votedYes, _playerStates.Count));
                    }
                }
            }

            return votedYes;
        }

        internal void UpdateAllClients()
        {
            if (!_recomputationRequired)
            {
                return;
            }

            int priorTickInterval = _cachedTickInterval;
            bool? priorManualFreeze = _cachedManualFreeze;
            AutoFreezeReason priorAutoFreeze = _cachedAutoFreeze;
            GetSharedTimeSpeedSettings(out int tickInterval, out bool? manualFreeze, out AutoFreezeReason autoFreeze);

            if (priorTickInterval != tickInterval || priorManualFreeze != manualFreeze || priorAutoFreeze != autoFreeze)
            {
                if (Context.IsMultiplayer)
                {
                    SendSetTimeSpeedCommand(tickInterval, manualFreeze, autoFreeze);
                }
                TimeMaster.TimeSpeed.UpdateTimeSpeed(
                    tickInterval: tickInterval,
                    autoFreeze: autoFreeze,
                    manualOverride: manualFreeze,
                    clearPreviousOverrides: manualFreeze == null,
                    notifyOfUpdates: _config.TimeFlowChangeNotifications,
                    notifyOfMultiplayerUpdates: _config.TimeFlowChangeNotificationsMultiplayer);
            }
        }

        private void SendSetTimeSpeedCommand(int tickInterval, bool? manualFreeze, AutoFreezeReason autoFreeze, long? playerID = null)
        {
            long[]? playerIDs = null;
            if (playerID != null)
            {
                playerIDs = new[] { playerID.Value };
            }

            var message = new SetTimeSpeedCommand(tickInterval, manualFreeze, autoFreeze);
            _helper.Multiplayer.SendMessage(message, Messages.SetTimeSpeed, new string[1] { _manifest.UniqueID }, playerIDs);
        }

        internal void GetSharedTimeSpeedSettings(out int tickInterval, out bool? manualFreeze, out AutoFreezeReason autoFreeze)
        {
            if (!_recomputationRequired)
            {
                tickInterval = _cachedTickInterval;
                manualFreeze = _cachedManualFreeze;
                autoFreeze = _cachedAutoFreeze;
                return;
            }

            tickInterval = RecomputeTickInterval();
            (manualFreeze, autoFreeze) = RecomputeFrozenState();

            // clear manual unfreeze if it's no longer needed
            if (_freezeOverride != null && autoFreeze == AutoFreezeReason.None && (manualFreeze == null || manualFreeze == false))
            {
                _monitor.Log($"Clearing _freezeOverride: ({_freezeOverride}). manualFreeze={manualFreeze}", LogLevel.Trace);
                _freezeOverride = null;
            }

            _cachedTickInterval = tickInterval;
            _cachedManualFreeze = manualFreeze;
            _cachedAutoFreeze = autoFreeze;
            _recomputationRequired = false;
            foreach (var s in _playerStates.Values)
            {
                s.IsModified = false;
            }
        }

        internal void ForceRecompute()
        {
            _recomputationRequired = true;
        }

        private int RecomputeTickInterval()
        {
            double result;
            switch (_config.TimeSpeedMode)
            {
                case TimeSpeedMode.Average:
                    result = _playerStates.Values.Average(s => s.TickInterval);
                    break;

                case TimeSpeedMode.Host:
                    if (_playerStates.TryGetValue(Game1.MasterPlayer.UniqueMultiplayerID, out var state))
                    {
                        result = state.TickInterval;
                    }
                    else
                    {
                        _monitor.LogOnce($"Can't find Host player state", LogLevel.Error);
                        result = TimeHelper.CurrentDefaultTickInterval;
                    }
                    break;

                case TimeSpeedMode.Max:
                    result = _playerStates.Values.Max(s => s.TickInterval);
                    break;

                case TimeSpeedMode.Min:
                    result = _playerStates.Values.Min(s => s.TickInterval);
                    break;

                default:
                    _monitor.LogOnce($"Unknown TimeSpeedMode config.json value: {_config.TimeSpeedMode}", LogLevel.Alert);
                    result = TimeHelper.CurrentDefaultTickInterval;
                    break;
            }

            if (_config.RelativeTimeSpeed)
            {
                double numPlayersPaused = _playerStates.Values.Count(s => s.IsPaused);
                double numPlayers = _playerStates.Count;
                result *= numPlayers / Math.Max(1, numPlayers - numPlayersPaused);
            }

            return (int)result;
        }

        private (bool?, AutoFreezeReason) RecomputeFrozenState()
        {
            AutoFreezeReason autoFreeze = AutoFreezeReason.None;
            bool pauseVoteSucceeded = true;
            bool eventIsActive = false;
            bool fairPause = PauseMode.Fair == _config.PauseMode;
            bool anyPause = PauseMode.Any == _config.PauseMode;
            bool allPause = PauseMode.All == _config.PauseMode;
            bool halfPause = PauseMode.Half == _config.PauseMode;
            bool majorityPause = PauseMode.Majority == _config.PauseMode;
            int pauseRequestedCount = 0;
            int pauseNotRequestedCount = 0;
            int min = int.MaxValue;
            PlayerState? minPlayerState = null;
            foreach (var playerState in _playerStates.Values)
            {
                if (playerState.AutoFreezeReason == AutoFreezeReason.FrozenAtTime)
                {
                    autoFreeze = AutoFreezeReason.FrozenAtTime;
                }

                if (autoFreeze == AutoFreezeReason.None && playerState.AutoFreezeReason == AutoFreezeReason.FrozenForLocation)
                {
                    autoFreeze = AutoFreezeReason.FrozenForLocation;
                }

                if (!playerState.IsVoteForPauseAffirmative)
                {
                    pauseVoteSucceeded = false;
                }

                if (playerState.IsEventActive)
                {
                    eventIsActive = true;
                }

                bool playerRequestedPause = playerState.IsPaused;
                if (playerRequestedPause)
                {
                    pauseRequestedCount += 1;
                }
                else
                {
                    pauseNotRequestedCount += 1;
                }

                if (fairPause)
                {
                    min = Math.Min(min, playerState.TotalTicksPaused);
                    if (min == playerState.TotalTicksPaused)
                    {
                        minPlayerState = playerState;
                    }
                }
            }

            if (_freezeOverride != null)
            {
                return (_freezeOverride, autoFreeze);
            }
            else if (autoFreeze == AutoFreezeReason.FrozenAtTime)
            {
                return (null, autoFreeze);
            }
            else if (pauseVoteSucceeded && _config.EnableVotePause)
            {
                return (true, autoFreeze);
            }
            else if (eventIsActive && _config.AnyCutscenePauses)
            {
                return (true, autoFreeze);
            }
            else if (PauseMode.Host == _config.PauseMode)
            {
                if (_playerStates.TryGetValue(Game1.MasterPlayer.UniqueMultiplayerID, out var hostState))
                {
                    return (hostState.IsPaused, autoFreeze);
                }
                else
                {
                    _monitor.LogOnce($"Can't find Host player state", LogLevel.Error);
                    return (null, autoFreeze);
                }
            }
            else if (fairPause)
            {
                if (minPlayerState == null)
                {
                    _monitor.LogOnce($"Unable to find player with minimum pause time.", LogLevel.Error);
                    return (null, autoFreeze);
                }

                return (minPlayerState.IsPaused, autoFreeze);
            }
            else if (anyPause)
            {
                return (pauseRequestedCount > 0, autoFreeze);
            }
            else if (allPause)
            {
                return (pauseNotRequestedCount == 0, autoFreeze);
            }
            else if (halfPause)
            {
                return (pauseRequestedCount >= pauseNotRequestedCount, autoFreeze);
            }
            else if (majorityPause)
            {
                return (pauseRequestedCount > pauseNotRequestedCount, autoFreeze);
            }
            else
            {
                _monitor.LogOnce($"Invalid PauseMode configuration: {_config.PauseMode}", LogLevel.Alert);
                return (null, autoFreeze);
            }
        }
    }
}