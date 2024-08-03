using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime.Framework
{
    internal class PlayerStates
    {
        private class PlayerState
        {
            private bool _isModified = true;
            private bool _isVoteForPauseAffirmative;
            private bool _isEventActive;
            private int _tickInterval = Game1.realMilliSecondsPerGameTenMinutes;
            private int _tickOfLastPause;
            private int _ticksOfPriorPause;
            private bool _isPaused;
            private AutoFreezeReason _autoFreezeReason = AutoFreezeReason.None;

            internal bool IsPaused => _isPaused || _autoFreezeReason != AutoFreezeReason.None;

            internal bool IsModified
            {
                get { return _isModified; }
                set { _isModified = value; }
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

                    return _ticksOfPriorPause;
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

        public PlayerStates
        (
            ModConfig config,
            IMonitor monitor,
            IModHelper helper,
            IManifest manifest
        )
        {
            _config = config;
            _monitor = monitor;
            _helper = helper;
            _manifest = manifest;
        }

        internal void Add(long playerId)
        {
            bool all_votes_are_yes = _playerStates.Values.All(s => s.IsVoteForPauseAffirmative);
            PlayerState state = new();
            _playerStates.Add(playerId, state);
            var location = Game1.getFarmer(playerId)?.currentLocation;
            if (location == null)
            {
                _monitor.Log
                (
                    $"Location missing for player. PlayerID={playerId}, Farmer={Game1.getFarmer(playerId)}, Name={Game1.getFarmer(playerId)?.Name}",
                    LogLevel.Info
                );
            }
            else
            {
                state.UpdateBasedOnLocation(location, _config);
            }

            if (playerId != Game1.MasterPlayer.UniqueMultiplayerID)
            {
                _helper.Multiplayer.SendMessage
                (
                    _config.LockMonsters,
                    Messages.SetLockMonstersMode,
                    new string[1] { _manifest.UniqueID },
                    new long[1] { playerId }
                );
                if (_config.EnableVotePause)
                {
                    state.IsVoteForPauseAffirmative = all_votes_are_yes;
                    _helper.Multiplayer.SendMessage
                    (
                        all_votes_are_yes,
                        Messages.SetVoteState,
                        new string[1] { _manifest.UniqueID },
                        new long[1] { playerId }
                    );
                    if (all_votes_are_yes)
                    {
                        _helper.Multiplayer.SendMessage
                        (
                            I18n.Message_PlayerJoinedAPausedGame(Game1.getFarmer(playerId).Name),
                            Messages.VoteUpdateMessage,
                            new string[1] { _manifest.UniqueID }
                        );
                        if (_config.DisplayVotePauseMessages)
                        {
                            Notifier.NotifyInChatBox
                            (
                                I18n.Message_PlayerJoinedAPausedGame(Game1.getFarmer(playerId).Name)
                            );
                        }
                    }
                }
            }

            _recomputationRequired = true;
            UpdateAllClients();
        }

        internal void Remove(long playerId)
        {
            _playerStates.Remove(playerId);
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
            _monitor.Log($"SetFreezeOverride({_freezeOverride}): _recomputationRequired={_recomputationRequired}");
            UpdateAllClients();
        }

        internal void PollForLocationUpdates()
        {
            bool late_to_send_updates = _recomputationRequired;
            bool modified_state = false;
            long main_player_id = Game1.MasterPlayer?.UniqueMultiplayerID ?? 0;
            foreach (var (player_id, state) in _playerStates)
            {
                if (player_id == main_player_id)
                {
                    // skip main player, because main player is updated on Warped event
                    continue;
                }

                if (Game1.getFarmer(player_id)?.currentLocation is GameLocation location)
                {
                    state.UpdateBasedOnLocation(location, _config);
                    if (state.IsModified)
                    {
                        modified_state = true;
                        _recomputationRequired = true;
                    }
                }
            }

            // Only update clients every other poll to avoid sending 3 messages while zoning
            if (late_to_send_updates && modified_state)
            {
                UpdateAllClients();
            }
        }

        internal void UpdateLocation(long playerId, GameLocation location)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                _monitor.Log($"Found no player with ID={playerId}", LogLevel.Error);
                return;
            }

            state.UpdateBasedOnLocation(location, _config);
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdateTimeSpeedSettings
        (
            long playerId,
            int? tickInterval = null,
            AutoFreezeReason? autoFreeze = null,
            bool skipUpdate = false
        )
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                _monitor.Log($"Found no player with ID={playerId}", LogLevel.Error);
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

        internal void AdjustTickInterval(long playerId, int change)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                _monitor.Log($"Found no player with ID={playerId}", LogLevel.Error);
                return;
            }

            if (change < 0)
            {
                int min_allowed = Math.Min(state.TickInterval, -change);
                state.TickInterval = Math.Max(min_allowed, state.TickInterval + change);
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

        internal void UpdatePauseState(long playerId, bool requestingPause)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                _monitor.Log($"Found no player with ID={playerId}", LogLevel.Error);
                return;
            }

            state.UpdatePauseState(requestingPause);
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdateEventActivity(long playerId, bool isEventActive)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                _monitor.Log($"Found no player with ID={playerId}", LogLevel.Error);
                return;
            }

            state.IsEventActive = isEventActive;
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            UpdateAllClients();
        }

        internal void UpdateVote(long playerId, bool newVote)
        {
            if (!_playerStates.TryGetValue(playerId, out var state))
            {
                _monitor.Log($"Found no player with ID={playerId}", LogLevel.Error);
                return;
            }

            state.IsVoteForPauseAffirmative = newVote;
            if (state.IsModified)
            {
                _recomputationRequired = true;
            }

            NotifyOfVoteUpdate(playerId, newVote);
            UpdateAllClients();
        }

        private int NotifyOfVoteUpdate(long playerId, bool newVote)
        {
            int voted_yes = _playerStates.Values.Count(s => s.IsVoteForPauseAffirmative);
            if (_config.EnableVotePause)
            {
                if (newVote)
                {
                    _helper.Multiplayer.SendMessage
                    (
                        I18n.Message_PlayerVotedToPause
                        (
                            Game1.getFarmer(playerId).Name,
                            voted_yes,
                            Math.Ceiling(_config.VoteThreshold * _playerStates.Count)
                        ),
                        Messages.VoteUpdateMessage,
                        new[] { _manifest.UniqueID }
                    );
                    if (_config.DisplayVotePauseMessages)
                    {
                        Notifier.NotifyInChatBox
                        (
                            I18n.Message_PlayerVotedToPause
                            (
                                Game1.getFarmer(playerId).Name,
                                voted_yes,
                                Math.Ceiling(_config.VoteThreshold * _playerStates.Count)
                            )
                        );
                    }
                }
                else
                {
                    _helper.Multiplayer.SendMessage
                    (
                        I18n.Message_PlayerVotedToUnpause
                        (
                            Game1.getFarmer(playerId).Name,
                            voted_yes,
                            Math.Ceiling(_config.VoteThreshold * _playerStates.Count)
                        ),
                        Messages.VoteUpdateMessage,
                        new[] { _manifest.UniqueID }
                    );
                    if (_config.DisplayVotePauseMessages)
                    {
                        Notifier.NotifyInChatBox
                        (
                            I18n.Message_PlayerVotedToUnpause
                            (
                                Game1.getFarmer(playerId).Name,
                                voted_yes,
                                Math.Ceiling(_config.VoteThreshold * _playerStates.Count)
                            )
                        );
                    }
                }
            }

            return voted_yes;
        }

        internal void UpdateAllClients()
        {
            if (!_recomputationRequired)
            {
                return;
            }

            int prior_tick_interval = _cachedTickInterval;
            bool? prior_manual_freeze = _cachedManualFreeze;
            AutoFreezeReason prior_auto_freeze = _cachedAutoFreeze;
            GetSharedTimeSpeedSettings
            (
                out int tick_interval,
                out bool? manual_freeze,
                out AutoFreezeReason auto_freeze
            );

            if (prior_tick_interval != tick_interval ||
                prior_manual_freeze != manual_freeze ||
                prior_auto_freeze != auto_freeze)
            {
                if (Context.IsMultiplayer)
                {
                    SendSetTimeSpeedCommand
                    (
                        tick_interval,
                        manual_freeze,
                        auto_freeze
                    );
                }

                TimeMaster.TimeSpeed.UpdateTimeSpeed
                (
                    tickInterval: tick_interval,
                    autoFreeze: auto_freeze,
                    manualOverride: manual_freeze,
                    clearPreviousOverrides: manual_freeze == null,
                    notifyOfUpdates: _config.TimeFlowChangeNotifications,
                    notifyOfMultiplayerUpdates: _config.TimeFlowChangeNotificationsMultiplayer
                );
            }
        }

        private void SendSetTimeSpeedCommand
        (
            int tickInterval,
            bool? manualFreeze,
            AutoFreezeReason autoFreeze,
            long? playerId = null
        )
        {
            long[]? player_i_ds = null;
            if (playerId != null)
            {
                player_i_ds = new[] { playerId.Value };
            }

            var message = new SetTimeSpeedCommand
            (
                tickInterval,
                manualFreeze,
                autoFreeze
            );
            _helper.Multiplayer.SendMessage
            (
                message,
                Messages.SetTimeSpeed,
                new string[1] { _manifest.UniqueID },
                player_i_ds
            );
        }

        internal void GetSharedTimeSpeedSettings
        (
            out int tickInterval,
            out bool? manualFreeze,
            out AutoFreezeReason autoFreeze
        )
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
            if (_freezeOverride != null &&
                autoFreeze == AutoFreezeReason.None &&
                (manualFreeze == null || manualFreeze == false))
            {
                _monitor.Log($"Clearing _freezeOverride: ({_freezeOverride}). manualFreeze={manualFreeze}");
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
                        _monitor.LogOnce("Can't find Host player state", LogLevel.Error);
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
                    _monitor.LogOnce
                    (
                        $"Unknown TimeSpeedMode config.json value: {_config.TimeSpeedMode}",
                        LogLevel.Alert
                    );
                    result = TimeHelper.CurrentDefaultTickInterval;
                    break;
            }

            if (_config.RelativeTimeSpeed)
            {
                double num_players_paused = _playerStates.Values.Count(s => s.IsPaused);
                double num_players = _playerStates.Count;
                result *= num_players / Math.Max(1, num_players - num_players_paused);
            }

            return (int)result;
        }

        private (bool?, AutoFreezeReason) RecomputeFrozenState()
        {
            AutoFreezeReason auto_freeze = AutoFreezeReason.None;
            bool pause_vote_succeeded = true;
            bool event_is_active = false;
            bool fair_pause = PauseMode.Fair == _config.PauseMode;
            bool any_pause = PauseMode.Any == _config.PauseMode;
            bool all_pause = PauseMode.All == _config.PauseMode;
            bool half_pause = PauseMode.Half == _config.PauseMode;
            bool majority_pause = PauseMode.Majority == _config.PauseMode;
            int pause_requested_count = 0;
            int pause_not_requested_count = 0;
            int pause_voted_count = 0;
            int pause_not_voted_count = 0;
            int min = int.MaxValue;
            PlayerState? min_player_state = null;
            foreach (var player_state in _playerStates.Values)
            {
                if (player_state.AutoFreezeReason == AutoFreezeReason.FrozenAtTime)
                {
                    auto_freeze = AutoFreezeReason.FrozenAtTime;
                }

                if (
                    auto_freeze == AutoFreezeReason.None &&
                    player_state.AutoFreezeReason == AutoFreezeReason.FrozenForLocation
                )
                {
                    auto_freeze = AutoFreezeReason.FrozenForLocation;
                }

                bool player_voted_pause = player_state.IsVoteForPauseAffirmative;
                if (player_voted_pause)
                {
                    pause_voted_count += 1;
                }
                else
                {
                    pause_not_voted_count += 1;
                }

                if (player_state.IsEventActive)
                {
                    event_is_active = true;
                }

                bool player_requested_pause = player_state.IsPaused;
                if (player_requested_pause)
                {
                    pause_requested_count += 1;
                }
                else
                {
                    pause_not_requested_count += 1;
                }

                if (fair_pause)
                {
                    min = Math.Min(min, player_state.TotalTicksPaused);
                    if (min == player_state.TotalTicksPaused)
                    {
                        min_player_state = player_state;
                    }
                }
            }

            pause_vote_succeeded = pause_voted_count >= Math.Ceiling(_config.VoteThreshold * _playerStates.Count);

            if (_freezeOverride != null)
            {
                return (_freezeOverride, auto_freeze);
            }

            if (auto_freeze == AutoFreezeReason.FrozenAtTime)
            {
                return (null, auto_freeze);
            }

            if (pause_vote_succeeded && _config.EnableVotePause)
            {
                return (true, auto_freeze);
            }

            if (event_is_active && _config.AnyCutscenePauses)
            {
                return (true, auto_freeze);
            }

            if (PauseMode.Host == _config.PauseMode)
            {
                if (_playerStates.TryGetValue(Game1.MasterPlayer.UniqueMultiplayerID, out var host_state))
                {
                    return (host_state.IsPaused, auto_freeze);
                }

                _monitor.LogOnce("Can't find Host player state", LogLevel.Error);
                return (null, auto_freeze);
            }

            if (fair_pause)
            {
                if (min_player_state == null)
                {
                    _monitor.LogOnce("Unable to find player with minimum pause time.", LogLevel.Error);
                    return (null, auto_freeze);
                }

                return (min_player_state.IsPaused, auto_freeze);
            }

            if (any_pause)
            {
                return (pause_requested_count > 0, auto_freeze);
            }

            if (all_pause)
            {
                return (pause_not_requested_count == 0, auto_freeze);
            }

            if (half_pause)
            {
                return (pause_requested_count >= pause_not_requested_count, auto_freeze);
            }

            if (majority_pause)
            {
                return (pause_requested_count > pause_not_requested_count, auto_freeze);
            }

            _monitor.LogOnce($"Invalid PauseMode configuration: {_config.PauseMode}", LogLevel.Alert);
            return (null, auto_freeze);
        }
    }
}