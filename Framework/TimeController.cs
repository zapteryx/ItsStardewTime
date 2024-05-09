using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Monsters;

namespace ItsStardewTime.Framework
{
    internal class TimeController
    {
        private class ScreenState
        {
            internal bool WasTimeFrozen;
            internal bool PauseIsRequested;
            internal bool LastEventState;
            internal bool IsVoteForPauseAffirmative;
            internal bool ShouldLockMonsters;
            internal int HealthLock = -100;
            internal int FoodDuration = -100;
            internal int DrinkDuration = -100;
            internal readonly Dictionary<Buff, int> OtherBuffsDurations = new();
        }

        // Host managed
        private bool _shouldAdjustTimeSpeed = true;
        private readonly PlayerStates _playerStates;
        private readonly Dictionary<Monster, Vector2> _monsterLocks = new(20);

        // client specific
        private readonly PerScreen<ScreenState> _clientSideState = new(() => new());

        private readonly IManifest _modManifest;
        private readonly IModHelper _helper;
        
        private static ModConfig _config = null!;

        internal static ModConfig Config => _config;
        
        private static IMonitor _monitor = null!;
        internal static IMonitor Monitor => _monitor;

        private static TimeSpeed TimeSpeed => TimeMaster.TimeSpeed;

        internal TimeController(IModHelper helper, IMonitor monitor, IManifest manifest, ModConfig config)
        {
            _helper = helper;
            _monitor = monitor;
            _modManifest = manifest;
            _config = config;
            _playerStates = new(config, monitor, helper, manifest);

            helper.Events.GameLoop.TimeChanged += (s, e) =>
            {
                if (!Context.IsWorldReady || !Context.IsMainPlayer)
                {
                    return;
                }

                AutoFreezeReason auto_freeze = Config.ShouldFreeze(Game1.timeOfDay, Game1.currentLocation);
                _playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, autoFreeze: auto_freeze);
            };

            helper.Events.GameLoop.DayStarted += (s, e) =>
            {
                if (!Context.IsWorldReady || !Context.IsMainPlayer)
                {
                    return;
                }

                _playerStates.DayStarted();

                _shouldAdjustTimeSpeed = Config.ShouldScale(Game1.currentSeason, Game1.dayOfMonth);
                int tick_interval = GetTickInterval(Game1.currentLocation);
                AutoFreezeReason auto_freeze = Config.ShouldFreeze(Game1.timeOfDay, Game1.currentLocation);
                _playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, tickInterval: tick_interval,
                    autoFreeze: auto_freeze);
            };

            helper.Events.Player.Warped += (s, e) =>
            {
                if (!Context.IsWorldReady || !Context.IsMainPlayer || !e.IsLocalPlayer)
                {
                    return;
                }

                int tick_interval = GetTickInterval(e.NewLocation);
                AutoFreezeReason auto_freeze = Config.ShouldFreeze(Game1.timeOfDay, e.NewLocation);
                _playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, tickInterval: tick_interval,
                    autoFreeze: auto_freeze, skipUpdate: true);
                if (Config.TimeFlowChangeNotifications)
                {
                    Monitor.Log(
                        $"Time speed settings for location '{e.NewLocation.Name}': {tick_interval / 1000f:N} seconds per 10 minutes",
                        LogLevel.Debug);
                }
            };

            helper.Events.Input.ButtonPressed += Input_ButtonPressed;
            helper.Events.Multiplayer.PeerDisconnected += Multiplayer_PeerDisconnected;
            helper.Events.Multiplayer.PeerConnected += Multiplayer_PeerConnected;
            helper.Events.Multiplayer.ModMessageReceived += Multiplayer_ModMessageReceived;
            helper.Events.GameLoop.UpdateTicked += GameLoop_UpdateTicked;
            helper.Events.GameLoop.UpdateTicking += GameLoop_UpdateTicking;
            helper.Events.GameLoop.SaveLoaded += (s, e) =>
            {
                if (Context.IsMainPlayer)
                {
                    _clientSideState.Value.ShouldLockMonsters = Config.LockMonsters;
                    _playerStates.Clear();
                    _playerStates.Add(Game1.player.UniqueMultiplayerID);
                }
            };

            helper.Events.GameLoop.Saving += (s, e) =>
            {
                // Reset invincibility as an extra precaution so as not to save
                // a farmhand with invincibility flags enabled.
                Game1.player.temporaryInvincibilityTimer = 0;
                Game1.player.currentTemporaryInvincibilityDuration = 0;
                Game1.player.temporarilyInvincible = false;
            };
        }

        internal void ReloadConfig()
        {
            if (!Context.IsWorldReady || !Context.IsMainPlayer)
            {
                return;
            }

            _clientSideState.Value.ShouldLockMonsters = Config.LockMonsters;
            _helper.Multiplayer.SendMessage(Config.LockMonsters, Messages.SetLockMonstersMode,
                new string[1] { _modManifest.UniqueID });

            _shouldAdjustTimeSpeed = Config.ShouldScale(Game1.currentSeason, Game1.dayOfMonth);
            int tick_interval = GetTickInterval(Game1.currentLocation);
            AutoFreezeReason auto_freeze = Config.ShouldFreeze(Game1.timeOfDay, Game1.currentLocation);
            _playerStates.ForceRecompute();
            _playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, tickInterval: tick_interval,
                autoFreeze: auto_freeze);
        }

        private void GameLoop_UpdateTicked(object? sender, StardewModdingAPI.Events.UpdateTickedEventArgs e)
        {
            if (!e.IsMultipleOf(31u))
            {
                return;
            }

            if (!Context.IsWorldReady || !Context.IsMainPlayer)
            {
                return;
            }

            // Move this to Warped event when it is raised for remote players.
            _playerStates.PollForLocationUpdates();
        }

        private void Input_ButtonPressed(object? sender, StardewModdingAPI.Events.ButtonPressedEventArgs e)
        {
            if (!Context.IsWorldReady)
            {
                return;
            }

            // don't handle input when player isn't free (except in events)
            if (!Context.IsPlayerFree && !Game1.eventUp)
                return;

            // ignore input if a textbox is active
            if (Game1.keyboardDispatcher.Subscriber is not null)
                return;

            if (Config.Keys.VoteForPause.JustPressed())
            {
                VotePauseToggle();
            }

            if (!Context.IsMainPlayer)
            {
                return;
            }

            if (Config.Keys.IncreaseTickInterval.JustPressed())
            {
                ChangeTickInterval(increase: true);
            }
            else if (Config.Keys.DecreaseTickInterval.JustPressed())
            {
                ChangeTickInterval(increase: false);
            }
            else if (Config.Keys.FreezeTime.JustPressed())
            {
                bool freeze_override = !TimeSpeed.IsTimeFrozen;
                _playerStates.SetFreezeOverride(freeze_override);
                if (freeze_override)
                {
                    Monitor.Log("Time is manually frozen.", LogLevel.Info);
                    _helper.Multiplayer.SendMessage("The host has paused.", Messages.VoteUpdateMessage,
                        new[] { _modManifest.UniqueID });
                    if (Config.TimeFlowChangeNotifications)
                    {
                        Notifier.QuickNotify(I18n.Message_TimeStopped());
                    }
                }
                else
                {
                    Monitor.Log($"Time is manually unfrozen at \"{Game1.currentLocation?.Name}\".", LogLevel.Info);
                    _helper.Multiplayer.SendMessage("The host has unpaused.", Messages.VoteUpdateMessage,
                        new[] { _modManifest.UniqueID });
                    if (Config.TimeFlowChangeNotifications)
                    {
                        Notifier.QuickNotify(I18n.Message_TimeResumed());
                    }
                }
            }

            void ChangeTickInterval(bool increase)
            {
                int change = 1000;
                {
                    KeyboardState state = Keyboard.GetState();
                    if (state.IsKeyDown(Keys.LeftControl))
                        change *= 100;
                    else if (state.IsKeyDown(Keys.LeftShift))
                        change *= 10;
                    else if (state.IsKeyDown(Keys.LeftAlt))
                        change /= 10;
                }

                _playerStates.AdjustTickInterval(Game1.MasterPlayer.UniqueMultiplayerID, increase ? change : -change);
            }
        }

        private void Multiplayer_PeerDisconnected(object? sender, StardewModdingAPI.Events.PeerDisconnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                _playerStates.Remove(e.Peer.PlayerID);
            }
        }

        private void Multiplayer_PeerConnected(object? sender, StardewModdingAPI.Events.PeerConnectedEventArgs e)
        {
            if (!Context.IsMainPlayer)
            {
                return;
            }

            IMultiplayerPeerMod? time_master_mod = e.Peer.GetMod(_modManifest.UniqueID);
            if (time_master_mod == null)
            {
                Notifier.NotifyErrorInChatBox(
                    I18n.Message_PlayerDoesNotHaveMod(Game1.getFarmer(e.Peer.PlayerID).Name, _modManifest.Name));
                Monitor.Log(I18n.Message_PlayerDoesNotHaveMod(Game1.getFarmer(e.Peer.PlayerID).Name, _modManifest.Name),
                    LogLevel.Warn);
            }
            else if (!time_master_mod.Version.Equals(_modManifest.Version))
            {
                Notifier.NotifyErrorInChatBox(
                    I18n.Message_PlayerDoesNotHaveCorrectVersion(Game1.getFarmer(e.Peer.PlayerID).Name,
                        _modManifest.Version));
                Monitor.Log(
                    I18n.Message_PlayerDoesNotHaveCorrectVersion(Game1.getFarmer(e.Peer.PlayerID).Name,
                        _modManifest.Version), LogLevel.Warn);
                Notifier.NotifyErrorInChatBox(I18n.Message_PlayerVersionComparison(_modManifest.Version,
                    Game1.getFarmer(e.Peer.PlayerID).Name, time_master_mod.Version));
                Monitor.Log(
                    I18n.Message_PlayerVersionComparison(_modManifest.Version, Game1.getFarmer(e.Peer.PlayerID).Name,
                        time_master_mod.Version), LogLevel.Warn);
            }

            _playerStates.Add(e.Peer.PlayerID);
        }

        private void Multiplayer_ModMessageReceived(object? sender,
            StardewModdingAPI.Events.ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != _modManifest.UniqueID)
            {
                return;
            }

            var client_side_state = _clientSideState.Value;

            try
            {
                switch (e.Type)
                {
                    case Messages.VoteUpdateMessage:
                        if (Config.DisplayVotePauseMessages)
                        {
                            Notifier.NotifyInChatBox(e.ReadAs<string>());
                        }

                        break;

                    case Messages.SetTimeSpeed:
                        var command = e.ReadAs<SetTimeSpeedCommand>();
                        TimeSpeed.UpdateTimeSpeed
                        (
                            tickInterval: command.TickInterval,
                            autoFreeze: command.AutoFreeze,
                            manualOverride: command.ManualFreeze,
                            clearPreviousOverrides: command.ManualFreeze == null,
                            notifyOfUpdates: Config.TimeFlowChangeNotifications,
                            notifyOfMultiplayerUpdates: Config.TimeFlowChangeNotificationsMultiplayer
                        );

                        // We ignore the case where the client clock is slightly behind (timeOfDay is equal and
                        // tick progess behind) so that the clock waits until the next world state update instead
                        // of jumping backwards unnecessarily (better UX for when Config.DisplayMinutes is set).
                        if (TimeSpeed.TimeHelper.TickProgress < command.TickProgress)
                        {
                            TimeSpeed.TimeHelper.SetTime(command.TickProgress);
                        }

                        break;

                    case Messages.SetLockMonstersMode:
                        client_side_state.ShouldLockMonsters = e.ReadAs<bool>();
                        break;

                    case Messages.SetVoteState:
                        client_side_state.IsVoteForPauseAffirmative = e.ReadAs<bool>();
                        break;

                    case Messages.UpdatePauseRequestState when Context.IsMainPlayer:
                        bool requesting_pause = e.ReadAs<bool>();
                        _playerStates.UpdatePauseState(e.FromPlayerID, requesting_pause);
                        break;

                    case Messages.UpdateVoteForPause when Context.IsMainPlayer:
                        bool new_vote = e.ReadAs<bool>();
                        _playerStates.UpdateVote(e.FromPlayerID, new_vote);
                        break;

                    case Messages.UpdateEventState when Context.IsMainPlayer:
                        _playerStates.UpdateEventActivity(e.FromPlayerID, e.ReadAs<bool>());
                        break;

                    default:
                        Monitor.LogOnce(
                            $"Unable to handle message ({e.Type}). Sender: PlayerID={e.FromPlayerID}. Receiver IsMainPlayer={Context.IsMainPlayer}",
                            LogLevel.Error);
                        break;
                }
            }
            catch (Exception ex)
            {
                Monitor.LogOnce(
                    $"Unable to handle message ({e.Type}). Sender: PlayerID={e.FromPlayerID}. Receiver IsMainPlayer={Context.IsMainPlayer}. Technical details:\n{ex}",
                    LogLevel.Error);
            }
        }

        private void GameLoop_UpdateTicking(object? sender, StardewModdingAPI.Events.UpdateTickingEventArgs e)
        {
            if (!e.IsMultipleOf(7u))
            {
                return;
            }

            if (!Context.IsWorldReady)
            {
                return;
            }

            var client_side_state = _clientSideState.Value;
            if (client_side_state.LastEventState != Game1.eventUp)
            {
                client_side_state.LastEventState = Game1.eventUp;
                if (Context.IsMainPlayer)
                {
                    _playerStates.UpdateEventActivity(Game1.player.UniqueMultiplayerID, Game1.eventUp);
                }
                else
                {
                    _helper.Multiplayer.SendMessage(Game1.eventUp, Messages.UpdateEventState,
                        new string[1] { _modManifest.UniqueID }, new long[1] { Game1.MasterPlayer.UniqueMultiplayerID });
                }
            }

            bool client_requests_pause = ClientShouldRequestPause();
            if (client_side_state.PauseIsRequested != client_requests_pause)
            {
                client_side_state.PauseIsRequested = client_requests_pause;
                if (Context.IsMainPlayer)
                {
                    _playerStates.UpdatePauseState(Game1.player.UniqueMultiplayerID, client_requests_pause);
                }
                else
                {
                    _helper.Multiplayer.SendMessage(client_requests_pause, Messages.UpdatePauseRequestState,
                        new string[1] { _modManifest.UniqueID }, new long[1] { Game1.MasterPlayer.UniqueMultiplayerID });
                }
            }

            bool is_time_frozen = TimeSpeed.IsTimeFrozen;
            bool has_frozen_state_changed = is_time_frozen != client_side_state.WasTimeFrozen;
            client_side_state.WasTimeFrozen = is_time_frozen;
            if (!has_frozen_state_changed && !is_time_frozen)
            {
                return;
            }

            if (has_frozen_state_changed && !is_time_frozen)
            {
                if (Context.IsMainPlayer)
                {
                    _monsterLocks.Clear();
                }

                client_side_state.FoodDuration = -100;
                client_side_state.DrinkDuration = -100;
                client_side_state.OtherBuffsDurations.Clear();
                client_side_state.HealthLock = -100;
                Game1.player.temporaryInvincibilityTimer = 0;
                Game1.player.currentTemporaryInvincibilityDuration = 0;
                Game1.player.temporarilyInvincible = false;
            }

            if (is_time_frozen)
            {
                if (Context.IsMainPlayer)
                {
                    // Periodically while paused in Fair mode, need to evalute paused state to make
                    // sure the currently paused player remains the one that requested the least pause time.
                    if (e.IsMultipleOf(10u * 7u) && Config.PauseMode == PauseMode.Fair)
                    {
                        _playerStates.ForceRecompute();
                        _playerStates.UpdateAllClients();
                    }

                    foreach (GameLocation location in Game1.locations)
                    {
                        if (location == null)
                        {
                            continue;
                        }

                        foreach (NPC character in location.characters)
                        {
                            character.movementPause = 250;
                        }
                    }

                    if (client_side_state.ShouldLockMonsters)
                    {
                        HashSet<GameLocation> farmer_locations = new();
                        foreach (Farmer f in Game1.getOnlineFarmers())
                        {
                            if (f.currentLocation != null)
                            {
                                farmer_locations.Add(f.currentLocation);
                            }
                        }

                        foreach (GameLocation location in farmer_locations)
                        {
                            if (location is Farm farm)
                            {
                                foreach (FarmAnimal animal2 in farm.getAllFarmAnimals())
                                {
                                    animal2.pauseTimer = 250;
                                }
                            }
                            else if (location is AnimalHouse animal_house)
                            {
                                foreach (FarmAnimal animal in animal_house.animals.Values)
                                {
                                    animal.pauseTimer = 250;
                                }
                            }

                            foreach (NPC c in location.characters)
                            {
                                if (c is Monster monster)
                                {
                                    monster.invincibleCountdown = 250;
                                    monster.movementPause = 250;
                                    if (_monsterLocks.TryGetValue(monster, out var locked_position))
                                    {
                                        if (monster.Position != locked_position)
                                        {
                                            monster.Position = locked_position;
                                        }
                                    }
                                    else
                                    {
                                        _monsterLocks[monster] = monster.Position;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (client_side_state.ShouldLockMonsters && Game1.currentLocation != null)
                    {
                        if (Game1.currentLocation is Farm farm)
                        {
                            foreach (FarmAnimal animal2 in farm.getAllFarmAnimals())
                            {
                                animal2.pauseTimer = 250;
                            }
                        }
                        else if (Game1.currentLocation is AnimalHouse animal_house)
                        {
                            foreach (FarmAnimal animal in animal_house.animals.Values)
                            {
                                animal.pauseTimer = 250;
                            }
                        }

                        foreach (NPC c in Game1.currentLocation.characters)
                        {
                            if (c is Monster monster)
                            {
                                monster.invincibleCountdown = 250;
                                monster.movementPause = 250;
                            }
                        }
                    }
                }

                var buffs = Game1.buffsDisplay.GetSortedBuffs();
                // find food buff
                Buff? food_buff = buffs.FirstOrDefault(b => b.source == "food");
                // find drink buff
                Buff? drink_buff = buffs.FirstOrDefault(b => b.source == "drink");
                // find other buffs
                List<Buff> other_buffs = buffs.Where(b => b.source != "food" && b.source != "drink").ToList();

                if (food_buff != null)
                {
                    if (food_buff.millisecondsDuration > client_side_state.FoodDuration)
                    {
                        client_side_state.FoodDuration = food_buff.millisecondsDuration;
                    }
                    else
                    {
                        food_buff.millisecondsDuration = client_side_state.FoodDuration;
                    }
                }

                if (drink_buff != null)
                {
                    if (drink_buff.millisecondsDuration > client_side_state.DrinkDuration)
                    {
                        client_side_state.DrinkDuration = drink_buff.millisecondsDuration;
                    }
                    else
                    {
                        drink_buff.millisecondsDuration = client_side_state.DrinkDuration;
                    }
                }

                if (other_buffs.Count > 0)
                {
                    if (client_side_state.OtherBuffsDurations.Count == 0)
                    {
                        foreach (Buff buff in other_buffs)
                        {
                            if (buff.millisecondsDuration > 0)
                            {
                                client_side_state.OtherBuffsDurations[buff] = buff.millisecondsDuration;
                            }
                        }
                    }
                    else
                    {
                        foreach (Buff buff in other_buffs)
                        {
                            if (client_side_state.OtherBuffsDurations.TryGetValue(buff, out int duration))
                            {
                                buff.millisecondsDuration = duration;
                            }
                        }
                    }
                }

                if (client_side_state.ShouldLockMonsters)
                {
                    if (client_side_state.HealthLock == -100)
                    {
                        client_side_state.HealthLock = Game1.player.health;
                    }

                    if (Game1.player.health > client_side_state.HealthLock)
                    {
                        client_side_state.HealthLock = Game1.player.health;
                    }

                    Game1.player.health = client_side_state.HealthLock;
                    Game1.player.temporarilyInvincible = true;
                    Game1.player.temporaryInvincibilityTimer = -1000000000;
                }
            }
        }

        private void VotePauseToggle()
        {
            var client_side_state = _clientSideState.Value;
            client_side_state.IsVoteForPauseAffirmative = !client_side_state.IsVoteForPauseAffirmative;
            if (Context.IsMainPlayer)
            {
                _playerStates.UpdateVote(Game1.player.UniqueMultiplayerID, client_side_state.IsVoteForPauseAffirmative);
            }
            else
            {
                _helper.Multiplayer.SendMessage(client_side_state.IsVoteForPauseAffirmative, Messages.UpdateVoteForPause,
                    modIDs: new[] { _modManifest.UniqueID },
                    playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
            }
        }

        private static bool ClientShouldRequestPause()
        {
            if (!Game1.shouldTimePass(true))
            {
                return true;
            }

            if (Game1.IsMultiplayer && Game1.netWorldState.Value.IsTimePaused)
            {
                return true;
            }

            if (Game1.activeClickableMenu is BobberBar)
            {
                return true;
            }

            if (Game1.currentMinigame != null)
            {
                return true;
            }

            if (Game1.eventUp)
            {
                return true;
            }

            return false;
        }

        private int GetTickInterval(GameLocation location)
        {
            if (_shouldAdjustTimeSpeed)
            {
                return Config.GetMillisecondsPerTenMinuteInterval(location);
            }

            return (int)TimeHelper.CurrentDefaultTickInterval;
        }

        internal void UpdateHealthLock(int newHealthLock)
        {
            var client_side_state = _clientSideState.Value;
            if (client_side_state.HealthLock != -100)
            {
                client_side_state.HealthLock = newHealthLock;
            }
        }
    }
}