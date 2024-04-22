using System.Runtime.Serialization;
using StardewValley;
using StardewValley.Locations;

namespace ItsStardewTime.Framework
{
    /// <summary>The time speed for in-game locations, measured in seconds per 10-game-minutes.</summary>
    internal class ModSecondsPerMinuteConfig
    {
        /// <summary>The time speed when indoors.</summary>
        public double? Indoors = 1.4;

        /// <summary>The time speed when in any farm house.</summary>
        public double? FarmHouse;

        /// <summary>The time speed when inside the bath house.</summary>
        public double? BathHouse;

        /// <summary>The time speed when outdoors.</summary>
        public double? Outdoors = 0.875;

        /// <summary>The time speed when on any farm.</summary>
        public double? Farm;

        /// <summary>The time speed when in any town exterior.</summary>
        public double? TownExteriors;

        /// <summary>The time speed in the mines.</summary>
        public double? Mines = 0.7;

        /// <summary>The time speed in the Skull Cavern.</summary>
        public double? SkullCavern = 0.9;

        /// <summary>The time speed in the Volcano Dungeon.</summary>
        public double? VolcanoDungeon = 0.7;

        /// <summary>The time speed in the Deep Woods mod.</summary>
        public double? DeepWoods = 0.7;

        /// <summary>The time speed when visiting the night market.</summary>
        public double? NightMarket = 0.875;

        /// <summary>The time speed for custom location names.</summary>
        /// <remarks>Location names can be seen in-game using the <a href="https://www.nexusmods.com/stardewvalley/mods/679">Debug Mode</a> mod.</remarks>
        public Dictionary<string, double> ByLocationName = new(StringComparer.OrdinalIgnoreCase);

        /*********
        ** Public methods
        *********/
        /// <summary>Get the number of seconds per in-game minute for a given location.</summary>
        /// <param name="location">The location to check.</param>
        public double GetSecondsPerMinute(GameLocation location)
        {
            const double vanillaSecondsPerMinute = 0.7;
            const double vanillaSecondsPerMinuteSkull = 0.9;

            if (location == null)
                return Outdoors ?? vanillaSecondsPerMinute;

            // by location name
            if (ByLocationName.TryGetValue(location.Name, out double tick_length))
                return tick_length;

            // by location name (Deep Woods mod)
            if (DeepWoods.HasValue && location.Name.StartsWith("DeepWoods"))
            {
                if (ByLocationName.TryGetValue("DeepWoods", out tick_length))
                    return tick_length;

                return DeepWoods.Value;
            }

            if (BathHouse.HasValue && location.Name.StartsWith("BathHouse"))
                return BathHouse.Value;

            if (NightMarket.HasValue && (location is BeachNightMarket || location is Submarine || location is MermaidHouse))
                return NightMarket.Value;

            if (FarmHouse.HasValue && (location is FarmHouse || location is IslandFarmHouse || location.Name == "Custom_JungleIslandFarmHouse"))
                return FarmHouse.Value;

            if (Farm.HasValue && (location is Farm || location is IslandWest || location.Name == "Custom_JungleIsland_E" || location.Name == "Custom_NewFarm"))
                return Farm.Value;

            if (TownExteriors.HasValue && (location is Town || location.Name == "Custom_Ridgeside_RidgesideVillage" || location.Name == "Custom_EastScarpe"))
                return TownExteriors.Value;

            if (location is MineShaft shaft)
            {
                if (shaft.mineLevel <= 120)
                {
                    return Mines ?? vanillaSecondsPerMinute;
                }
                else
                {
                    return SkullCavern ?? vanillaSecondsPerMinuteSkull;
                }
            }

            if (VolcanoDungeon.HasValue && location is VolcanoDungeon)
                return VolcanoDungeon.Value;

            if (location.IsOutdoors)
            {
                return Outdoors ?? vanillaSecondsPerMinute;
            }
            else
            {
                return Indoors ?? vanillaSecondsPerMinute;
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
            ByLocationName = new(ByLocationName ?? new(), StringComparer.OrdinalIgnoreCase);
        }
    }
}
