using System.Text;
using HarmonyLib;
using ItsStardewTime.Framework;
using ItsStardewTime.Patches;
using ItsStardewTime.Patches.TimeDisplayPatches;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Newtonsoft.Json.Linq;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;

namespace ItsStardewTime
{
    internal sealed class TimeMaster : Mod
    {
#nullable disable
        internal static TimeController TimeController;
        private static PerScreen<TimeSpeed> _timeSpeeds;
#nullable enable

        internal static TimeSpeed TimeSpeed => _timeSpeeds.Value;

        public override void Entry(IModHelper helper)
        {
            try
            {
                I18n.Init(helper.Translation);
                _timeSpeeds = new(() => new(helper, Monitor));
                TimeController = new
                (
                    helper,
                    Monitor,
                    ModManifest,
                    helper.ReadConfig<ModConfig>()
                );

                TimeSpeed.FrozenTick += (s, e) =>
                {
                    if (!TimeController.Config.ObjectsPassTimeWhenTimeIsFrozen ||
                        !Context.IsWorldReady ||
                        !Context.IsMainPlayer)
                    {
                        return;
                    }

                    foreach (var location in Game1.locations)
                    {
                        location?.passTimeForObjects(10);
                        if (location is null or { IsFarm: false })
                        {
                            continue;
                        }

                        foreach (var building in location.buildings)
                        {
                            if (building.daysOfConstructionLeft.Value > 0)
                            {
                                continue;
                            }

                            var indoors = building?.indoors.Value;
                            if (indoors != null && !Game1.locations.Contains(indoors))
                            {
                                indoors.passTimeForObjects(10);
                            }
                        }
                    }
                };

                helper.Events.GameLoop.GameLaunched += OnGameLaunched;

                var harmony = new Harmony(ModManifest.UniqueID);

                HarmonyInitializer.Initialize(harmony);


                if (helper.ModRegistry.Get("jorgamun.PauseInMultiplayer") is IModInfo pim_mod)
                {
                    Monitor.Log
                    (
                        $"Disabling the {pim_mod.Manifest.Name} mod. It can be uninstalled as it is no longer necessary with {ModManifest.Name}. {ModManifest.Name} provides its features.",
                        LogLevel.Warn
                    );
                    if
                    (
                        FancyModIntegration.RemoveModEventHandlers
                        (
                            helper,
                            pim_mod,
                            Monitor
                        ) is var num_removed and < 10
                    )
                    {
                        Monitor.Log
                        (
                            $"Removed only {num_removed} event handlers from {pim_mod.Manifest.Name}. Please report this as a bug in the {ModManifest.Name} mod (with an SMAPI log) as it may be a bug causing incompatibility with {pim_mod.Manifest.Name}.",
                            LogLevel.Warn
                        );
                    }
                }

                if (helper.ModRegistry.Get("cantorsdust.TimeSpeed") is IModInfo ts_mod)
                {
                    Monitor.Log
                    (
                        $"Disabling the {ts_mod.Manifest.Name} mod. It can be uninstalled as it is no longer necessary with {ModManifest.Name}. {ModManifest.Name} provides its features.",
                        LogLevel.Warn
                    );
                    if (FancyModIntegration.RemoveModEventHandlers
                        (
                            helper,
                            ts_mod,
                            Monitor
                        ) is var num_removed &&
                        num_removed < 9)
                    {
                        Monitor.Log
                        (
                            $"Removed only {num_removed} event handlers from {ts_mod.Manifest.Name}. Please report this as a bug in the {ModManifest.Name} mod (with an SMAPI log) as it may be a bug causing incompatibility with {ts_mod.Manifest.Name}.",
                            LogLevel.Warn
                        );
                    }
                }
            }
            catch (Exception e)
            {
                Monitor.Log
                (
                    $"Failed to apply patches; some features may not work correctly. Technical details:\n{e}",
                    LogLevel.Error
                );
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var config_menu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (config_menu != null)
            {
                config_menu.Register
                (
                    mod: ModManifest,
                    reset: () => TimeController.Config.Update(new ModConfig()),
                    save: () =>
                    {
                        Helper.WriteConfig(TimeController.Config);
                        TimeController.ReloadConfig();
                    }
                );

                // Speed of Time
                const float minSpeedOfTime = 0f;
                const float maxSpeedOfTime = 3f;
                const float unsetSpeedOfTime = 0f;
                const float vanillaSecondsPerMinute = 0.7f;
                const float vanillaSecondsPerMinuteSkull = 0.9f;
                const float speedOfTimeInterval = 0.01f;
                const string additionalLocationsSpeedPageId = "speed-additional-locations";

                config_menu.AddSectionTitle(ModManifest, I18n.Config_SpeedOfTime);
                config_menu.AddParagraph(ModManifest, I18n.Config_SpeedOfTime_Paragraph);
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_IndoorsSpeed_Name,
                    tooltip: I18n.Config_IndoorsSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.Indoors.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.Indoors
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.Indoors = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FarmHouseSpeed_Name,
                    tooltip: I18n.Config_FarmHouseSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.FarmHouse.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.FarmHouse
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.FarmHouse = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_BathHouseSpeed_Name,
                    tooltip: I18n.Config_BathHouseSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.BathHouse.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.BathHouse
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.BathHouse = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_OutdoorsSpeed_Name,
                    tooltip: I18n.Config_OutdoorsSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.Outdoors.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.Outdoors
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.Outdoors = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FarmSpeed_Name,
                    tooltip: I18n.Config_FarmSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.Farm.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.Farm
                            : unsetSpeedOfTime,
                    setValue: value =>
                        TimeController.Config.SecondsPerMinute.Farm = (value == unsetSpeedOfTime
                            ? null
                            : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_TownExteriorsSpeed_Name,
                    tooltip: I18n.Config_TownExteriorsSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.TownExteriors.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.TownExteriors
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.TownExteriors = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_MineSpeed_Name,
                    tooltip: I18n.Config_MineSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.Mines.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.Mines
                            : unsetSpeedOfTime,
                    setValue: value =>
                        TimeController.Config.SecondsPerMinute.Mines = (value == unsetSpeedOfTime
                            ? null
                            : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_SkullCavernSpeed_Name,
                    tooltip: I18n.Config_SkullCavernSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.SkullCavern.HasValue
                            ? vanillaSecondsPerMinuteSkull / (float)TimeController.Config.SecondsPerMinute.SkullCavern
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.SkullCavern = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinuteSkull / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_VolcanoDungeonSpeed_Name,
                    tooltip: I18n.Config_VolcanoDungeonSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.VolcanoDungeon.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.VolcanoDungeon
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.VolcanoDungeon =
                        (value == unsetSpeedOfTime
                            ? null
                            : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                if (Helper.ModRegistry.IsLoaded("maxvollmer.deepwoodsmod"))
                {
                    config_menu.AddNumberOption
                    (
                        mod: ModManifest,
                        name: I18n.Config_DeepWoodsSpeed_Name,
                        tooltip: I18n.Config_DeepWoodsSpeed_Desc,
                        getValue: () =>
                            TimeController.Config.SecondsPerMinute.DeepWoods.HasValue
                                ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.DeepWoods
                                : unsetSpeedOfTime,
                        setValue: value => TimeController.Config.SecondsPerMinute.DeepWoods = (value == unsetSpeedOfTime
                            ? null
                            : Math.Round(vanillaSecondsPerMinute / value, 4)),
                        formatValue: value =>
                            value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                        min: minSpeedOfTime,
                        max: maxSpeedOfTime,
                        interval: speedOfTimeInterval
                    );
                }

                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_NightMarketSpeed_Name,
                    tooltip: I18n.Config_NightMarketSpeed_Desc,
                    getValue: () =>
                        TimeController.Config.SecondsPerMinute.NightMarket.HasValue
                            ? vanillaSecondsPerMinute / (float)TimeController.Config.SecondsPerMinute.NightMarket
                            : unsetSpeedOfTime,
                    setValue: value => TimeController.Config.SecondsPerMinute.NightMarket = (value == unsetSpeedOfTime
                        ? null
                        : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value =>
                        value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                config_menu.AddTextOption
                (
                    mod: ModManifest,
                    name: I18n.Config_AdditionalLocationsSpeed_Names_Name,
                    tooltip: I18n.Config_AdditionalLocationsSpeed_Names_Desc,
                    getValue: () => string.Join(", ", TimeController.Config.SecondsPerMinute.ByLocationName.Keys),
                    setValue: value =>
                    {
                        config_menu.AddPage
                        (
                            mod: ModManifest,
                            pageId: additionalLocationsSpeedPageId,
                            pageTitle: I18n.Config_AdditionalLocationsSpeed_Page_Title
                        );
                        Dictionary<string, double> previous_by_location_name =
                            new(TimeController.Config.SecondsPerMinute.ByLocationName);
                        TimeController.Config.SecondsPerMinute.ByLocationName.Clear();
                        foreach (var location_name in value.Split
                                         (",").
                                     Select(p => p.Trim()).
                                     Where(p => p != string.Empty))
                        {
                            if (previous_by_location_name.TryGetValue(location_name, out double old_value))
                            {
                                TimeController.Config.SecondsPerMinute.ByLocationName.Add(location_name, old_value);
                            }
                            else
                            {
                                TimeController.Config.SecondsPerMinute.ByLocationName.Add
                                    (location_name, vanillaSecondsPerMinute);
                                AddAdditionalLocationSpeedOptions
                                (
                                    config_menu,
                                    minSpeedOfTime,
                                    maxSpeedOfTime,
                                    vanillaSecondsPerMinute,
                                    speedOfTimeInterval,
                                    location_name
                                );
                            }
                        }

                        config_menu.AddPage
                        (
                            mod: ModManifest,
                            pageId: "",
                            pageTitle: null
                        );
                    }
                );
                config_menu.AddPage
                (
                    mod: ModManifest,
                    pageId: additionalLocationsSpeedPageId,
                    pageTitle: I18n.Config_AdditionalLocationsSpeed_Page_Title
                );
                foreach (var location_name in TimeController.Config.SecondsPerMinute.ByLocationName.Keys)
                {
                    AddAdditionalLocationSpeedOptions
                    (
                        config_menu,
                        minSpeedOfTime,
                        maxSpeedOfTime,
                        vanillaSecondsPerMinute,
                        speedOfTimeInterval,
                        location_name
                    );
                }

                config_menu.AddPage
                (
                    mod: ModManifest,
                    pageId: "",
                    pageTitle: null
                );
                config_menu.AddPageLink
                (
                    mod: ModManifest,
                    pageId: additionalLocationsSpeedPageId,
                    text: I18n.Config_AdditionalLocationsSpeed_Link_Name,
                    tooltip: I18n.Config_AdditionalLocationsSpeed_Link_Desc
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_EnableOnFestivalDays_Name,
                    tooltip: I18n.Config_EnableOnFestivalDays_Desc,
                    getValue: () => TimeController.Config.EnableOnFestivalDays,
                    setValue: value => TimeController.Config.EnableOnFestivalDays = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_TimeFlowChangeNotifications_Name,
                    tooltip: I18n.Config_TimeFlowChangeNotifications_Desc,
                    getValue: () => TimeController.Config.TimeFlowChangeNotifications,
                    setValue: value => TimeController.Config.TimeFlowChangeNotifications = value
                );

                // Freeze time
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddSectionTitle(ModManifest, I18n.Config_FreezeTime);
                config_menu.AddParagraph(ModManifest, I18n.Config_FreezeTime_Paragraph);
                config_menu.AddNumberOption
                (
                    mod: ModManifest,
                    name: I18n.Config_AnywhereAtTime_Name,
                    tooltip: I18n.Config_AnywhereAtTime_Desc,
                    getValue: () => TimeController.Config.FreezeTime.AnywhereAtTime ?? 2600,
                    setValue: value => TimeController.Config.FreezeTime.AnywhereAtTime = (value == 2600 ? null : value),
                    formatValue: value =>
                        value == 2600 ? I18n.Config_OptionDisabled() : Game1.getTimeOfDayString(value),
                    min: 600,
                    max: 2600,
                    interval: 100
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeIndoors_Name,
                    tooltip: I18n.Config_FreezeTimeIndoors_Desc,
                    getValue: () => TimeController.Config.FreezeTime.Indoors,
                    setValue: value => TimeController.Config.FreezeTime.Indoors = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeFarmHouse_Name,
                    tooltip: I18n.Config_FreezeTimeFarmHouse_Desc,
                    getValue: () => TimeController.Config.FreezeTime.FarmHouse,
                    setValue: value => TimeController.Config.FreezeTime.FarmHouse = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeBathHouse_Name,
                    tooltip: I18n.Config_FreezeTimeBathHouse_Desc,
                    getValue: () => TimeController.Config.FreezeTime.BathHouse,
                    setValue: value => TimeController.Config.FreezeTime.BathHouse = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeOutdoors_Name,
                    tooltip: I18n.Config_FreezeTimeOutdoors_Desc,
                    getValue: () => TimeController.Config.FreezeTime.Outdoors,
                    setValue: value => TimeController.Config.FreezeTime.Outdoors = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeFarm_Name,
                    tooltip: I18n.Config_FreezeTimeFarm_Desc,
                    getValue: () => TimeController.Config.FreezeTime.Farm,
                    setValue: value => TimeController.Config.FreezeTime.Farm = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeTownExteriors_Name,
                    tooltip: I18n.Config_FreezeTimeTownExteriors_Desc,
                    getValue: () => TimeController.Config.FreezeTime.TownExteriors,
                    setValue: value => TimeController.Config.FreezeTime.TownExteriors = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeMine_Name,
                    tooltip: I18n.Config_FreezeTimeMine_Desc,
                    getValue: () => TimeController.Config.FreezeTime.Mines,
                    setValue: value => TimeController.Config.FreezeTime.Mines = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeSkullCavern_Name,
                    tooltip: I18n.Config_FreezeTimeSkullCavern_Desc,
                    getValue: () => TimeController.Config.FreezeTime.SkullCavern,
                    setValue: value => TimeController.Config.FreezeTime.SkullCavern = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeVolcanoDungeon_Name,
                    tooltip: I18n.Config_FreezeTimeVolcanoDungeon_Desc,
                    getValue: () => TimeController.Config.FreezeTime.VolcanoDungeon,
                    setValue: value => TimeController.Config.FreezeTime.VolcanoDungeon = value
                );
                if (Helper.ModRegistry.IsLoaded("maxvollmer.deepwoodsmod"))
                {
                    config_menu.AddBoolOption
                    (
                        mod: ModManifest,
                        name: I18n.Config_FreezeTimeDeepWoods_Name,
                        tooltip: I18n.Config_FreezeTimeDeepWoods_Desc,
                        getValue: () => TimeController.Config.FreezeTime.DeepWoods,
                        setValue: value => TimeController.Config.FreezeTime.DeepWoods = value
                    );
                }

                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeNightMarket_Name,
                    tooltip: I18n.Config_FreezeTimeNightMarket_Desc,
                    getValue: () => TimeController.Config.FreezeTime.NightMarket,
                    setValue: value => TimeController.Config.FreezeTime.NightMarket = value
                );
                config_menu.AddTextOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeFreezeNames_Name,
                    tooltip: I18n.Config_FreezeTimeFreezeNames_Desc,
                    getValue: () => string.Join(", ", TimeController.Config.FreezeTime.ByLocationName),
                    setValue: value => TimeController.Config.FreezeTime.ByLocationName = new
                    (
                        value.Split(",").Select(p => p.Trim()).Where(p => p != string.Empty)
                    )
                );
                config_menu.AddTextOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeDontFreezeNames_Name,
                    tooltip: I18n.Config_FreezeTimeDontFreezeNames_Desc,
                    getValue: () => string.Join(", ", TimeController.Config.FreezeTime.ExceptLocationNames),
                    setValue: value => TimeController.Config.FreezeTime.ExceptLocationNames = new
                    (
                        value.Split(",").Select(p => p.Trim()).Where(p => p != string.Empty)
                    )
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeObjectsPassTime_Name,
                    tooltip: I18n.Config_FreezeTimeObjectsPassTime_Desc,
                    getValue: () => TimeController.Config.ObjectsPassTimeWhenTimeIsFrozen,
                    setValue: value => TimeController.Config.ObjectsPassTimeWhenTimeIsFrozen = value
                );

                // Clock Display
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddSectionTitle(mod: base.ModManifest, text: I18n.Config_ClockDisplay);
                config_menu.AddParagraph(ModManifest, I18n.Config_ClockDisplay_Paragraph);
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_ClockDisplayDisplayMinutes_Name,
                    tooltip: I18n.Config_ClockDisplayDisplayMinutes_Desc,
                    getValue: () => TimeController.Config.DisplayMinutes,
                    setValue: value => TimeController.Config.DisplayMinutes = value
                );
                config_menu.AddBoolOption
                (
                    mod: base.ModManifest,
                    name: I18n.Config_ClockDisplayShowPauseX_Name,
                    tooltip: I18n.Config_ClockDisplayShowPauseX_Desc,
                    getValue: () => TimeController.Config.ShowPauseX,
                    setValue: value => TimeController.Config.ShowPauseX = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_ClockDisplayUse24HourFormat_Name,
                    tooltip: I18n.Config_ClockDisplayUse24HourFormat_Desc,
                    getValue: () => TimeController.Config.Use24HourFormat,
                    setValue: value => TimeController.Config.Use24HourFormat = value
                );

                // Multiplayer Options
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddSectionTitle(mod: base.ModManifest, text: I18n.Config_Multiplayer);
                config_menu.AddBoolOption
                (
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerDisplayVotePauseMessages_Name,
                    tooltip: I18n.Config_MultiplayerDisplayVotePauseMessages_Desc,
                    getValue: () => TimeController.Config.DisplayVotePauseMessages,
                    setValue: value => TimeController.Config.DisplayVotePauseMessages = value
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_TimeFlowChangeNotificationsMultiplayer_Name,
                    tooltip: I18n.Config_TimeFlowChangeNotificationsMultiplayer_Desc,
                    getValue: () => TimeController.Config.TimeFlowChangeNotificationsMultiplayer,
                    setValue: value => TimeController.Config.TimeFlowChangeNotificationsMultiplayer = value
                );

                // Multiplayer Host-only Options
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddSectionTitle(mod: base.ModManifest, text: I18n.Config_MultiplayerHost);
                config_menu.AddParagraph(ModManifest, I18n.Config_MultiplayerHost_Paragraph);
                config_menu.AddTextOption
                (
                    mod: ModManifest,
                    name: I18n.Config_MultiplayerHostPauseMode_Name,
                    tooltip: I18n.Config_MultiplayerHostPauseMode_Desc,
                    getValue: () => Enum.TryParse
                    (
                        TimeController.Config.PauseMode.ToString(),
                        true,
                        out PauseMode _
                    )
                        ? TimeController.Config.PauseMode.ToString()
                        : PauseMode.Fair.ToString(),
                    setValue: value => TimeController.Config.PauseMode = Enum.TryParse
                    (
                        value,
                        true,
                        out PauseMode result
                    )
                        ? result
                        : PauseMode.Fair,
                    allowedValues: Enum.GetNames<PauseMode>().
                        Where(mode => !Enum.Parse<PauseMode>(mode).IsDeprecated()).
                        ToArray(),
                    formatAllowedValue: (s) => Helper.Translation.Get
                        ($"TimeController.Config.multiplayer-host-pause-mode.{s}")
                );
                config_menu.AddBoolOption
                (
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerHostAnyCutscenePauses_Name,
                    tooltip: I18n.Config_MultiplayerHostAnyCutscenePauses_Desc,
                    getValue: () => TimeController.Config.AnyCutscenePauses,
                    setValue: value => TimeController.Config.AnyCutscenePauses = value
                );
                config_menu.AddBoolOption
                (
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerHostLockMonsters_Name,
                    tooltip: I18n.Config_MultiplayerHostLockMonsters_Desc,
                    getValue: () => TimeController.Config.LockMonsters,
                    setValue: value => TimeController.Config.LockMonsters = value
                );
                config_menu.AddBoolOption
                (
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerHostEnableVotePause_Name,
                    tooltip: I18n.Config_MultiplayerHostEnableVotePause_Desc,
                    getValue: () => TimeController.Config.EnableVotePause,
                    setValue: value => TimeController.Config.EnableVotePause = value
                );
                config_menu.AddTextOption
                (
                    mod: ModManifest,
                    name: I18n.Config_MultiplayerHostTimeSpeedMode_Name,
                    tooltip: I18n.Config_MultiplayerHostTimeSpeedMode_Desc,
                    getValue: () => Enum.TryParse
                    (
                        TimeController.Config.TimeSpeedMode.ToString(),
                        true,
                        out TimeSpeedMode _
                    )
                        ? TimeController.Config.TimeSpeedMode.ToString()
                        : TimeSpeedMode.Average.ToString(),
                    setValue: value => TimeController.Config.TimeSpeedMode = Enum.TryParse
                    (
                        value,
                        true,
                        out TimeSpeedMode result
                    )
                        ? result
                        : TimeSpeedMode.Average,
                    allowedValues: Enum.GetNames<TimeSpeedMode>().
                        Where(mode => !Enum.Parse<TimeSpeedMode>(mode).IsDeprecated()).
                        ToArray(),
                    formatAllowedValue: (s) => Helper.Translation.Get
                        ($"TimeController.Config.multiplayer-host-time-speed-mode.{s}")
                );
                config_menu.AddBoolOption
                (
                    mod: ModManifest,
                    name: I18n.Config_MultiplayerHostRelativeTimeSpeed_Name,
                    tooltip: I18n.Config_MultiplayerHostRelativeTimeSpeed_Desc,
                    getValue: () => TimeController.Config.RelativeTimeSpeed,
                    setValue: value => TimeController.Config.RelativeTimeSpeed = value
                );

                // Controls
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddParagraph(ModManifest, () => "");
                config_menu.AddSectionTitle(ModManifest, I18n.Config_Controls);
                config_menu.AddKeybindList
                (
                    mod: base.ModManifest,
                    name: I18n.Config_VoteForPauseKey_Name,
                    tooltip: I18n.Config_VoteForPauseKey_Desc,
                    getValue: () => TimeController.Config.Keys.VoteForPause,
                    setValue: value => TimeController.Config.Keys.VoteForPause = value
                );
                config_menu.AddKeybindList
                (
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeKey_Name,
                    tooltip: I18n.Config_FreezeTimeKey_Desc,
                    getValue: () => TimeController.Config.Keys.FreezeTime,
                    setValue: value => TimeController.Config.Keys.FreezeTime = value
                );
                config_menu.AddKeybindList
                (
                    mod: ModManifest,
                    name: I18n.Config_SlowTimeKey_Name,
                    tooltip: I18n.Config_SlowTimeKey_Desc,
                    getValue: () => TimeController.Config.Keys.IncreaseTickInterval,
                    setValue: value => TimeController.Config.Keys.IncreaseTickInterval = value
                );
                config_menu.AddKeybindList
                (
                    mod: ModManifest,
                    name: I18n.Config_SpeedUpTimeKey_Name,
                    tooltip: I18n.Config_SpeedUpTimeKey_Desc,
                    getValue: () => TimeController.Config.Keys.DecreaseTickInterval,
                    setValue: value => TimeController.Config.Keys.DecreaseTickInterval = value
                );
            }

            RunCompatibilityChecksForMod
            (
                config_menu,
                "jorgamun.PauseInMultiplayer",
                "config",
                TimeController.Config.ShouldMergePauseInMultiplayerConfigOnNextRun,
                c => c.ShouldMergePauseInMultiplayerConfigOnNextRun = false
            );

            RunCompatibilityChecksForMod
            (
                config_menu,
                "cantorsdust.TimeSpeed",
                "Config",
                TimeController.Config.ShouldMergeTimeSlowConfigOnNextRun,
                c => c.ShouldMergeTimeSlowConfigOnNextRun = false
            );

            void AddAdditionalLocationSpeedOptions
            (
                IGenericModConfigMenuApi configMenu,
                float minSpeedOfTime,
                float maxSpeedOfTime,
                float vanillaSecondsPerMinute,
                float speedOfTimeInterval,
                string locationName
            )
            {
                configMenu.AddNumberOption
                (
                    mod: ModManifest,
                    name: () => I18n.Config_AdditionalLocationSpeed_Name(locationName),
                    tooltip: () => I18n.Config_AdditionalLocationSpeed_Desc(locationName),
                    getValue: () =>
                    {
                        if (TimeController.Config.SecondsPerMinute.ByLocationName.ContainsKey(locationName))
                        {
                            return vanillaSecondsPerMinute /
                                   (float)TimeController.Config.SecondsPerMinute.ByLocationName.GetValueOrDefault
                                       (locationName, vanillaSecondsPerMinute);
                        }
                        else
                        {
                            return 0f;
                        }
                    },
                    setValue: value =>
                    {
                        if (TimeController.Config.SecondsPerMinute.ByLocationName.ContainsKey(locationName))
                        {
                            TimeController.Config.SecondsPerMinute.ByLocationName[locationName] = Math.Round
                                (vanillaSecondsPerMinute / value, 4);
                        }
                    },
                    formatValue: value => value == 0f ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: Math.Max(0.01f, minSpeedOfTime),
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddBoolOption
                (
                    mod: ModManifest,
                    name: () => I18n.Config_AdditionalLocationSpeedDelete_Name(locationName),
                    tooltip: I18n.Config_AdditionalLocationSpeedDelete_Desc,
                    getValue: () => !TimeController.Config.SecondsPerMinute.ByLocationName.ContainsKey(locationName),
                    setValue: value =>
                    {
                        if (value)
                        {
                            TimeController.Config.SecondsPerMinute.ByLocationName.Remove(locationName);
                        }
                    }
                );
            }
        }

        private void RunCompatibilityChecksForMod
        (
            IGenericModConfigMenuApi? configMenu,
            string uniqueId,
            string configFieldTypeColonName,
            bool shouldMergeConfigs,
            Action<ModConfig> recordMergeCompletion
        )
        {
            try
            {
                if (Helper.ModRegistry.Get(uniqueId) is IModInfo mod_info)
                {
                    configMenu?.Unregister(mod_info.Manifest);
                    if (shouldMergeConfigs && MergeConfig(mod_info, configFieldTypeColonName) is ModConfig new_config)
                    {
                        TimeController.Config.Update(new_config);
                        Monitor.Log
                        (
                            $"Successfully merged {mod_info.Manifest.Name} config into {ModManifest.Name} TimeController.Config.",
                            LogLevel.Info
                        );
                    }
                }

                if (shouldMergeConfigs)
                {
                    recordMergeCompletion(TimeController.Config);
                    Helper.WriteConfig(TimeController.Config);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Exception while merging {uniqueId} TimeController.Config.\n{ex}", LogLevel.Error);
            }
        }

        private ModConfig? MergeConfig(IModInfo modInfo, string configFieldName)
        {
            if (!FancyModIntegration.TryGetModEntry
                (
                    modInfo,
                    Monitor,
                    out var mod_entry
                ))
            {
                Monitor.Log
                (
                    $"Failed to merge {modInfo.Manifest.Name} TimeController.Config. Could not find its ModEntry.",
                    LogLevel.Error
                );
                return null;
            }

            if (AccessTools.Field(mod_entry.GetType(), configFieldName)?.GetValue(mod_entry) is not object mod_config)
            {
                Monitor.Log
                (
                    $"Failed to merge {modInfo.Manifest.Name} TimeController.Config. Could not find its ModConfig at {mod_entry.GetType().FullName}:{configFieldName}.",
                    LogLevel.Error
                );
                return null;
            }

            var current_config_j_object = JObject.FromObject(TimeController.Config);
            var to_be_merged_config_j_object = JObject.FromObject(mod_config);
            current_config_j_object.Merge
            (
                to_be_merged_config_j_object,
                new JsonMergeSettings
                {
                    MergeArrayHandling = MergeArrayHandling.Replace
                }
            );

            if (current_config_j_object.ToObject<ModConfig>() is not ModConfig merged_config)
            {
                Monitor.Log($"Failed to serialize merged config for {modInfo.Manifest.Name}.", LogLevel.Error);
                return null;
            }

#pragma warning disable CS0612 // Type or member is obsolete
            merged_config.Keys.VoteForPause = new(merged_config.VotePauseHotkey);
#pragma warning restore CS0612 // Type or member is obsolete

            return merged_config;
        }
    }
}