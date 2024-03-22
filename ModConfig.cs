using System.Reflection;
using System.Runtime.Serialization;
using ItsStardewTime.Framework;
using StardewModdingAPI;
using StardewValley;

namespace ItsStardewTime
{
    internal class ModConfig
    {
        // Speed of Time Options

        /// <summary>Whether to change tick length on festival days.</summary>
        public bool EnableOnFestivalDays = true;

        /// <summary>Whether to show a notification about the time flow changes when you enter a location or pause.</summary>
        public bool TimeFlowChangeNotifications = false;

        /// <summary>The time speed for in-game locations, measured in seconds per in-game minute.</summary>
        public ModSecondsPerMinuteConfig SecondsPerMinute = new();


        // Freeze Time Options

        /// <summary>Whether objects should pass time when game time is frozen.</summary>
        public bool ObjectsPassTimeWhenTimeIsFrozen = false;

        /// <summary>The mod configuration for where time should be frozen.</summary>
        public ModFreezeTimeConfig FreezeTime = new();


        // Clock Display
        public bool DisplayMinutes = true;
        public bool ShowPauseX = false;
        public bool Use24HourFormat = false;


        // Multiplayer Options
        public bool DisplayVotePauseMessages = true;
        public bool TimeFlowChangeNotificationsMultiplayer = false;


        // Multiplayer Host Options
        public PauseMode PauseMode = PauseMode.Fair;
        public bool AnyCutscenePauses = true;
        public bool LockMonsters = true;
        public bool EnableVotePause = true;
        public TimeSpeedMode TimeSpeedMode = TimeSpeedMode.Average;
        public bool RelativeTimeSpeed = true;


        // Controls

        /// <summary>The keyboard bindings used to control the flow of time. See available keys at <a href="https://msdn.microsoft.com/en-us/library/microsoft.xna.framework.input.keys.aspx" />.</summary>
        public ModControlsConfig Keys = new();


        // Internal config

        public bool ShouldMergePauseInMultiplayerConfigOnNextRun = true;
        public bool ShouldMergeTimeSlowConfigOnNextRun = true;

        [Obsolete] public SButton VotePauseHotkey = SButton.None;

        /*********
         ** Public methods
         *********/
        /// <summary>Get whether time should be frozen at a given location.</summary>
        /// <param name="location">The game location.</param>
        private bool ShouldFreeze(GameLocation location)
        {
            return FreezeTime.ShouldFreeze(location);
        }

        /// <summary>Get whether the time should be frozen at a given time of day.</summary>
        /// <param name="time">The time of day in 24-hour military format (e.g. 1600 for 8pm).</param>
        private bool ShouldFreeze(int time)
        {
            return time >= FreezeTime.AnywhereAtTime;
        }

        /// <summary>Get whether the time should be frozen.</summary>
        /// <param name="time">The time of day in 24-hour military format (e.g. 1600 for 8pm).</param>
        /// <param name="location">The game location.</param>
        public AutoFreezeReason ShouldFreeze(int time, GameLocation location)
        {
            if (ShouldFreeze(time))
                return AutoFreezeReason.FrozenAtTime;

            if (ShouldFreeze(location))
                return AutoFreezeReason.FrozenForLocation;

            return AutoFreezeReason.None;
        }

        /// <summary>Get whether time settings should be applied on a given day.</summary>
        /// <param name="season">The season to check.</param>
        /// <param name="dayOfMonth">The day of month to check.</param>
        public bool ShouldScale(string season, int dayOfMonth)
        {
            Season seasonNumber = (Season)Utility.getSeasonNumber(season);
            return EnableOnFestivalDays || !Utility.isFestivalDay(dayOfMonth, seasonNumber);
        }

        /// <summary>Get the number of milliseconds per 10 minutes to apply for a location.</summary>
        /// <param name="location">The game location.</param>
        public int GetMillisecondsPerTenMinuteInterval(GameLocation location)
        {
            return (int)(SecondsPerMinute.GetSecondsPerMinute(location) * 1000 * 10);
        }

        public void Update(ModConfig other)
        {
            foreach (FieldInfo field in typeof(ModConfig).GetFields())
            {
                field.SetValue(this, field.GetValue(other));
            }
        }

        /*********
         ** Private methods
         *********/
        /// <summary>The method called after the config file is deserialized.</summary>
        /// <param name="context">The deserialization context.</param>
        [OnDeserialized]
        private void OnDeserializedMethod(StreamingContext context)
        {
            SecondsPerMinute ??= new();
            FreezeTime ??= new();
            Keys ??= new();

            if (PauseMode.IsDeprecated())
            {
                PauseMode = PauseMode.Fair;
            }

            if (TimeSpeedMode.IsDeprecated())
            {
                TimeSpeedMode = TimeSpeedMode.Average;
            }
        }
    }
}