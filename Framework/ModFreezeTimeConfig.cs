using System.Runtime.Serialization;
using StardewValley;
using StardewValley.Locations;

namespace ItsStardewTime.Framework
{
    /// <summary>The mod configuration for where or when time should be frozen.</summary>
    internal class ModFreezeTimeConfig
    {
        /// <summary>The time at which to freeze time everywhere (or <c>null</c> to disable this). This should be 24-hour military time (e.g. 800 for 8am, 1600 for 8pm, etc).</summary>
        public int? AnywhereAtTime;

        /// <summary>Whether to freeze time indoors.</summary>
        public bool Indoors = false;

        /// <summary>Whether to freeze time in any farm house.</summary>
        public bool FarmHouse = false;

        /// <summary>The time speed when inside the bath house.</summary>
        public bool BathHouse = false;

        /// <summary>Whether to freeze time outdoors.</summary>
        public bool Outdoors = false;

        /// <summary>Whether to freeze time on the farm.</summary>
        public bool Farm = false;

        /// <summary>Whether to freeze time in town exteriors.</summary>
        public bool TownExteriors = false;

        /// <summary>Whether to freeze time in the mines.</summary>
        public bool Mines = false;

        /// <summary>Whether to freeze time in the Skull Cavern.</summary>
        public bool SkullCavern = false;

        /// <summary>Whether to freeze time in the Volcano Dungeon.</summary>
        public bool VolcanoDungeon = false;

        /// <summary>Whether to freeze time in the Deep Woods mod.</summary>
        public bool DeepWoods = false;

        /// <summary>The time speed when visiting the night market.</summary>
        public bool NightMarket = false;

        /// <summary>The names of custom locations in which to freeze time.</summary>
        /// <remarks>Location names can be seen in-game using the <a href="https://www.nexusmods.com/stardewvalley/mods/679">Debug Mode</a> mod.</remarks>
        public HashSet<string> ByLocationName = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>The names of custom locations in which time shouldn't be frozen regardless of the previous settings.</summary>
        /// <remarks>See remarks on <see cref="ByLocationName"/>.</remarks>
        public HashSet<string> ExceptLocationNames = new(StringComparer.OrdinalIgnoreCase);


        /*********
        ** Public methods
        *********/
        /// <summary>Get whether time should be frozen in the given location.</summary>
        /// <param name="location">The location to check.</param>
        public bool ShouldFreeze(GameLocation location)
        {
            if (location == null || ExceptLocationNames.Contains(location.Name))
                return false;

            // by location name
            if (ByLocationName.Contains(location.Name))
                return true;

            // by location name (Deep Woods mod)
            if (location.Name.StartsWith("DeepWoods"))
            {
                if (ByLocationName.Contains("DeepWoods"))
                    return true;

                return DeepWoods;
            }

            if (location.Name.StartsWith("BathHouse"))
                return BathHouse;

            if (location is BeachNightMarket || location is Submarine || location is MermaidHouse)
                return NightMarket;

            if (location is FarmHouse || location is IslandFarmHouse || location.Name == "Custom_JungleIslandFarmHouse")
                return FarmHouse;

            if (location is Farm || location is IslandWest || location.Name == "Custom_JungleIsland_E" || location.Name == "Custom_NewFarm")
                return Farm;

            if (location is Town || location.Name == "Custom_Ridgeside_RidgesideVillage" || location.Name == "Custom_EastScarpe")
                return TownExteriors;

            // mines / Skull Cavern
            if (location is MineShaft shaft)
            {
                return shaft.mineLevel <= 120
                    ? Mines
                    : SkullCavern;
            }

            // volcano dungeon
            if (location is VolcanoDungeon)
                return VolcanoDungeon;

            // indoors or outdoors
            return location.IsOutdoors
                ? Outdoors
                : Indoors;
        }


        /*********
        ** Private methods
        *********/
        /// <summary>The method called after the config file is deserialized.</summary>
        /// <param name="context">The deserialization context.</param>
        [OnDeserialized]
        private void OnDeserializedMethod(StreamingContext context)
        {
            ByLocationName = new(ByLocationName ?? new(), StringComparer.OrdinalIgnoreCase);
            ExceptLocationNames = new(ExceptLocationNames ?? new(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
