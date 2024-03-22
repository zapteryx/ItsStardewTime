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
        private bool shouldAdjustTimeSpeed = true;
        private readonly PlayerStates playerStates;
        private readonly Dictionary<Monster, Vector2> monsterLocks = new(20);

        // client specific
        private readonly PerScreen<ScreenState> ClientSideState = new(() => new());

        internal readonly IMonitor Monitor;
        private readonly IManifest ModManifest;
        private readonly IModHelper Helper;
        private readonly ModConfig Config;

        private static TimeSpeed TimeSpeed => TimeMaster.TimeSpeed;

        internal TimeController(IModHelper helper, IMonitor monitor, IManifest manifest, ModConfig config)
        {
            Helper = helper;
            Monitor = monitor;
            ModManifest = manifest;
            Config = config;
            playerStates = new(config, monitor, helper, manifest);

            helper.Events.GameLoop.TimeChanged += (s, e) =>
            {
                if (!Context.IsWorldReady || !Context.IsMainPlayer)
                {
                    return;
                }

                AutoFreezeReason autoFreeze = Config.ShouldFreeze(Game1.timeOfDay, Game1.currentLocation);
                playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, autoFreeze: autoFreeze);
            };

            helper.Events.GameLoop.DayStarted += (s, e) =>
            {
                if (!Context.IsWorldReady || !Context.IsMainPlayer)
                {
                    return;
                }

                playerStates.DayStarted();

                shouldAdjustTimeSpeed = Config.ShouldScale(Game1.currentSeason, Game1.dayOfMonth);
                int tickInterval = GetTickInterval(Game1.currentLocation);
                AutoFreezeReason autoFreeze = Config.ShouldFreeze(Game1.timeOfDay, Game1.currentLocation);
                playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, tickInterval: tickInterval,
                    autoFreeze: autoFreeze);
            };

            helper.Events.Player.Warped += (s, e) =>
            {
                if (!Context.IsWorldReady || !Context.IsMainPlayer || !e.IsLocalPlayer)
                {
                    return;
                }

                int tickInterval = GetTickInterval(e.NewLocation);
                AutoFreezeReason autoFreeze = Config.ShouldFreeze(Game1.timeOfDay, e.NewLocation);
                playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, tickInterval: tickInterval,
                    autoFreeze: autoFreeze, skipUpdate: true);
                if (Config.TimeFlowChangeNotifications)
                {
                    Monitor.Log(
                        $"Time speed settings for location '{e.NewLocation.Name}': {tickInterval / 1000f:N} seconds per 10 minutes",
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
                    ClientSideState.Value.ShouldLockMonsters = Config.LockMonsters;
                    playerStates.Clear();
                    playerStates.Add(Game1.player.UniqueMultiplayerID);
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

            ClientSideState.Value.ShouldLockMonsters = Config.LockMonsters;
            Helper.Multiplayer.SendMessage(Config.LockMonsters, Messages.SetLockMonstersMode,
                new string[1] { ModManifest.UniqueID });

            shouldAdjustTimeSpeed = Config.ShouldScale(Game1.currentSeason, Game1.dayOfMonth);
            int tickInterval = GetTickInterval(Game1.currentLocation);
            AutoFreezeReason autoFreeze = Config.ShouldFreeze(Game1.timeOfDay, Game1.currentLocation);
            playerStates.ForceRecompute();
            playerStates.UpdateTimeSpeedSettings(Game1.player.UniqueMultiplayerID, tickInterval: tickInterval,
                autoFreeze: autoFreeze);
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
            playerStates.PollForLocationUpdates();
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
                bool freezeOverride = !TimeSpeed.IsTimeFrozen;
                playerStates.SetFreezeOverride(freezeOverride);
                if (freezeOverride)
                {
                    Monitor.Log("Time is manually frozen.", LogLevel.Info);
                    Helper.Multiplayer.SendMessage("The host has paused.", Messages.VoteUpdateMessage,
                        new[] { ModManifest.UniqueID });
                    if (Config.TimeFlowChangeNotifications)
                    {
                        Notifier.QuickNotify(I18n.Message_TimeStopped());
                    }
                }
                else
                {
                    Monitor.Log($"Time is manually unfrozen at \"{Game1.currentLocation?.Name}\".", LogLevel.Info);
                    Helper.Multiplayer.SendMessage("The host has unpaused.", Messages.VoteUpdateMessage,
                        new[] { ModManifest.UniqueID });
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

                playerStates.AdjustTickInterval(Game1.MasterPlayer.UniqueMultiplayerID, increase ? change : -change);
            }
        }

        private void Multiplayer_PeerDisconnected(object? sender, StardewModdingAPI.Events.PeerDisconnectedEventArgs e)
        {
            if (Context.IsMainPlayer)
            {
                playerStates.Remove(e.Peer.PlayerID);
            }
        }

        private void Multiplayer_PeerConnected(object? sender, StardewModdingAPI.Events.PeerConnectedEventArgs e)
        {
            if (!Context.IsMainPlayer)
            {
                return;
            }

            IMultiplayerPeerMod? timeMasterMod = e.Peer.GetMod(ModManifest.UniqueID);
            if (timeMasterMod == null)
            {
                Notifier.NotifyErrorInChatBox(
                    I18n.Message_PlayerDoesNotHaveMod(Game1.getFarmer(e.Peer.PlayerID).Name, ModManifest.Name));
                Monitor.Log(I18n.Message_PlayerDoesNotHaveMod(Game1.getFarmer(e.Peer.PlayerID).Name, ModManifest.Name),
                    LogLevel.Warn);
            }
            else if (!timeMasterMod.Version.Equals(ModManifest.Version))
            {
                Notifier.NotifyErrorInChatBox(
                    I18n.Message_PlayerDoesNotHaveCorrectVersion(Game1.getFarmer(e.Peer.PlayerID).Name,
                        ModManifest.Version));
                Monitor.Log(
                    I18n.Message_PlayerDoesNotHaveCorrectVersion(Game1.getFarmer(e.Peer.PlayerID).Name,
                        ModManifest.Version), LogLevel.Warn);
                Notifier.NotifyErrorInChatBox(I18n.Message_PlayerVersionComparison(ModManifest.Version,
                    Game1.getFarmer(e.Peer.PlayerID).Name, timeMasterMod.Version));
                Monitor.Log(
                    I18n.Message_PlayerVersionComparison(ModManifest.Version, Game1.getFarmer(e.Peer.PlayerID).Name,
                        timeMasterMod.Version), LogLevel.Warn);
            }

            playerStates.Add(e.Peer.PlayerID);
        }

        private void Multiplayer_ModMessageReceived(object? sender,
            StardewModdingAPI.Events.ModMessageReceivedEventArgs e)
        {
            if (e.FromModID != ModManifest.UniqueID)
            {
                return;
            }

            var clientSideState = ClientSideState.Value;

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
                        TimeSpeed.UpdateTimeSpeed(
                            tickInterval: command.TickInterval,
                            autoFreeze: command.AutoFreeze,
                            manualOverride: command.ManualFreeze,
                            clearPreviousOverrides: command.ManualFreeze == null,
                            notifyOfUpdates: Config.TimeFlowChangeNotifications,
                            notifyOfMultiplayerUpdates: Config.TimeFlowChangeNotificationsMultiplayer);

                        // We ignore the case where the client clock is slightly behind (timeOfDay is equal and
                        // tick progess behind) so that the clock waits until the next world state update instead
                        // of jumping backwards unnecessarily (better UX for when Config.DisplayMinutes is set).
                        if (TimeSpeed.TimeHelper.TickProgress < command.TickProgress)
                        {
                            TimeSpeed.TimeHelper.SetTime(command.TickProgress);
                        }

                        break;

                    case Messages.SetLockMonstersMode:
                        clientSideState.ShouldLockMonsters = e.ReadAs<bool>();
                        break;

                    case Messages.SetVoteState:
                        clientSideState.IsVoteForPauseAffirmative = e.ReadAs<bool>();
                        break;

                    case Messages.UpdatePauseRequestState when Context.IsMainPlayer:
                        bool requestingPause = e.ReadAs<bool>();
                        playerStates.UpdatePauseState(e.FromPlayerID, requestingPause);
                        break;

                    case Messages.UpdateVoteForPause when Context.IsMainPlayer:
                        bool newVote = e.ReadAs<bool>();
                        playerStates.UpdateVote(e.FromPlayerID, newVote);
                        break;

                    case Messages.UpdateEventState when Context.IsMainPlayer:
                        playerStates.UpdateEventActivity(e.FromPlayerID, e.ReadAs<bool>());
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

            var clientSideState = ClientSideState.Value;
            if (clientSideState.LastEventState != Game1.eventUp)
            {
                clientSideState.LastEventState = Game1.eventUp;
                if (Context.IsMainPlayer)
                {
                    playerStates.UpdateEventActivity(Game1.player.UniqueMultiplayerID, Game1.eventUp);
                }
                else
                {
                    Helper.Multiplayer.SendMessage(Game1.eventUp, Messages.UpdateEventState,
                        new string[1] { ModManifest.UniqueID }, new long[1] { Game1.MasterPlayer.UniqueMultiplayerID });
                }
            }

            bool clientRequestsPause = ClientShouldRequestPause();
            if (clientSideState.PauseIsRequested != clientRequestsPause)
            {
                clientSideState.PauseIsRequested = clientRequestsPause;
                if (Context.IsMainPlayer)
                {
                    playerStates.UpdatePauseState(Game1.player.UniqueMultiplayerID, clientRequestsPause);
                }
                else
                {
                    Helper.Multiplayer.SendMessage(clientRequestsPause, Messages.UpdatePauseRequestState,
                        new string[1] { ModManifest.UniqueID }, new long[1] { Game1.MasterPlayer.UniqueMultiplayerID });
                }
            }

            bool isTimeFrozen = TimeSpeed.IsTimeFrozen;
            bool hasFrozenStateChanged = isTimeFrozen != clientSideState.WasTimeFrozen;
            clientSideState.WasTimeFrozen = isTimeFrozen;
            if (!hasFrozenStateChanged && !isTimeFrozen)
            {
                return;
            }

            if (hasFrozenStateChanged && !isTimeFrozen)
            {
                if (Context.IsMainPlayer)
                {
                    monsterLocks.Clear();
                }

                clientSideState.FoodDuration = -100;
                clientSideState.DrinkDuration = -100;
                clientSideState.OtherBuffsDurations.Clear();
                clientSideState.HealthLock = -100;
                Game1.player.temporaryInvincibilityTimer = 0;
                Game1.player.currentTemporaryInvincibilityDuration = 0;
                Game1.player.temporarilyInvincible = false;
            }

            if (isTimeFrozen)
            {
                if (Context.IsMainPlayer)
                {
                    // Periodically while paused in Fair mode, need to evalute paused state to make
                    // sure the currently paused player remains the one that requested the least pause time.
                    if (e.IsMultipleOf(10u * 7u) && Config.PauseMode == PauseMode.Fair)
                    {
                        playerStates.ForceRecompute();
                        playerStates.UpdateAllClients();
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

                    if (clientSideState.ShouldLockMonsters)
                    {
                        HashSet<GameLocation> farmerLocations = new();
                        foreach (Farmer f in Game1.getOnlineFarmers())
                        {
                            if (f.currentLocation != null)
                            {
                                farmerLocations.Add(f.currentLocation);
                            }
                        }

                        foreach (GameLocation location in farmerLocations)
                        {
                            if (location is Farm farm)
                            {
                                foreach (FarmAnimal animal2 in farm.getAllFarmAnimals())
                                {
                                    animal2.pauseTimer = 250;
                                }
                            }
                            else if (location is AnimalHouse animalHouse)
                            {
                                foreach (FarmAnimal animal in animalHouse.animals.Values)
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
                                    if (monsterLocks.TryGetValue(monster, out var lockedPosition))
                                    {
                                        if (monster.Position != lockedPosition)
                                        {
                                            monster.Position = lockedPosition;
                                        }
                                    }
                                    else
                                    {
                                        monsterLocks[monster] = monster.Position;
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (clientSideState.ShouldLockMonsters && Game1.currentLocation != null)
                    {
                        if (Game1.currentLocation is Farm farm)
                        {
                            foreach (FarmAnimal animal2 in farm.getAllFarmAnimals())
                            {
                                animal2.pauseTimer = 250;
                            }
                        }
                        else if (Game1.currentLocation is AnimalHouse animalHouse)
                        {
                            foreach (FarmAnimal animal in animalHouse.animals.Values)
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
                Buff? foodBuff = buffs.FirstOrDefault(b => b.source == "food");
                // find drink buff
                Buff? drinkBuff = buffs.FirstOrDefault(b => b.source == "drink");
                // find other buffs
                List<Buff> otherBuffs = buffs.Where(b => b.source != "food" && b.source != "drink").ToList();

                if (foodBuff != null)
                {
                    if (foodBuff.millisecondsDuration > clientSideState.FoodDuration)
                    {
                        clientSideState.FoodDuration = foodBuff.millisecondsDuration;
                    }
                    else
                    {
                        foodBuff.millisecondsDuration = clientSideState.FoodDuration;
                    }
                }

                if (drinkBuff != null)
                {
                    if (drinkBuff.millisecondsDuration > clientSideState.DrinkDuration)
                    {
                        clientSideState.DrinkDuration = drinkBuff.millisecondsDuration;
                    }
                    else
                    {
                        drinkBuff.millisecondsDuration = clientSideState.DrinkDuration;
                    }
                }
                
                if (otherBuffs.Count > 0)
                {
                    if (clientSideState.OtherBuffsDurations.Count == 0)
                    {
                        foreach (Buff buff in otherBuffs)
                        {
                            if (buff.millisecondsDuration > 0)
                            {
                                clientSideState.OtherBuffsDurations[buff] = buff.millisecondsDuration;
                            }
                        }
                    }
                    else
                    {
                        foreach (Buff buff in otherBuffs)
                        {
                            if (clientSideState.OtherBuffsDurations.TryGetValue(buff, out int duration))
                            {
                                buff.millisecondsDuration = duration;
                            }
                        }
                    }

                }

                if (clientSideState.ShouldLockMonsters)
                {
                    if (clientSideState.HealthLock == -100)
                    {
                        clientSideState.HealthLock = Game1.player.health;
                    }

                    if (Game1.player.health > clientSideState.HealthLock)
                    {
                        clientSideState.HealthLock = Game1.player.health;
                    }

                    Game1.player.health = clientSideState.HealthLock;
                    Game1.player.temporarilyInvincible = true;
                    Game1.player.temporaryInvincibilityTimer = -1000000000;
                }
            }
        }

        private void VotePauseToggle()
        {
            var clientSideState = ClientSideState.Value;
            clientSideState.IsVoteForPauseAffirmative = !clientSideState.IsVoteForPauseAffirmative;
            if (Context.IsMainPlayer)
            {
                playerStates.UpdateVote(Game1.player.UniqueMultiplayerID, clientSideState.IsVoteForPauseAffirmative);
            }
            else
            {
                Helper.Multiplayer.SendMessage(clientSideState.IsVoteForPauseAffirmative, Messages.UpdateVoteForPause,
                    modIDs: new[] { ModManifest.UniqueID },
                    playerIDs: new[] { Game1.MasterPlayer.UniqueMultiplayerID });
            }
        }

        private static bool ClientShouldRequestPause()
        {
            if (!Game1.shouldTimePass(true))
            {
                return true;
            }
            else if (Game1.IsMultiplayer && Game1.netWorldState.Value.IsTimePaused)
            {
                return true;
            }
            else if (Game1.activeClickableMenu is BobberBar)
            {
                return true;
            }
            else if (Game1.currentMinigame != null)
            {
                return true;
            }
            else if (Game1.eventUp)
            {
                return true;
            }

            return false;
        }

        private int GetTickInterval(GameLocation location)
        {
            if (shouldAdjustTimeSpeed)
            {
                return Config.GetMillisecondsPerTenMinuteInterval(location);
            }
            else
            {
                return (int)TimeHelper.CurrentDefaultTickInterval;
            }
        }

        internal void UpdateHealthLock(int newHealthLock)
        {
            var clientSideState = ClientSideState.Value;
            if (clientSideState.HealthLock != -100)
            {
                clientSideState.HealthLock = newHealthLock;
            }
        }
    }
}