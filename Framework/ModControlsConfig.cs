using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace ItsStardewTime.Framework
{
    /// <summary>The keyboard bindings used to control the flow of time. See available keys at <a href="https://msdn.microsoft.com/en-us/library/microsoft.xna.framework.input.keys.aspx" />. Set a key to null to disable it.</summary>
    internal class ModControlsConfig
    {
        /// <summary> Toggle vote for whether or not to pause the game. </summary>
        public KeybindList VoteForPause = new(SButton.Pause);

        /// <summary>Freeze or unfreeze time. Freezing time will stay in effect until you unfreeze it; unfreezing time will stay in effect until you enter a new location with time settings.</summary>
        public KeybindList FreezeTime = new(SButton.None);

        /// <summary>Slow down time by one second per 10-game-minutes. Combine with Control to increase by 100 seconds, Shift to increase by 10 seconds, or Alt to increase by 0.1 seconds.</summary>
        public KeybindList IncreaseTickInterval = new(SButton.None);

        /// <summary>Speed up time by one second per 10-game-minutes. Combine with Control to decrease by 100 seconds, Shift to decrease by 10 seconds, or Alt to decrease by 0.1 seconds.</summary>
        public KeybindList DecreaseTickInterval = new(SButton.None);
    }
}
