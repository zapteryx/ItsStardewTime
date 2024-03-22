

using HarmonyLib;
using ItsStardewTime.Framework;
using ItsStardewTime.Patches;
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
        private static PerScreen<TimeSpeed> TimeSpeeds;
        private ModConfig Config;
#nullable enable

        internal static TimeSpeed TimeSpeed => TimeSpeeds.Value;

        public override void Entry(IModHelper helper)
        {
            try
            {
                I18n.Init(helper.Translation);
                Config = helper.ReadConfig<ModConfig>();
                TimeSpeeds = new(() => new(helper, Monitor));
                TimeController = new(helper, Monitor, ModManifest, Config);

                TimeSpeed.FrozenTick += (s, e) =>
                {
                    if (!Config.ObjectsPassTimeWhenTimeIsFrozen || !Context.IsWorldReady || !Context.IsMainPlayer)
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

                TimeDisplayPatches.Initialize(Monitor, Config);
                harmony.Patch(
                    original: AccessTools.Method(typeof(DayTimeMoneyBox), nameof(DayTimeMoneyBox.draw), new[] { typeof(SpriteBatch) }),
                    prefix: new HarmonyMethod(typeof(TimeDisplayPatches), nameof(TimeDisplayPatches.Draw_Prefix))
                    );
                harmony.Patch(
                    original: AccessTools.Method(typeof(Game1), nameof(Game1.getTimeOfDayString)),
                    prefix: new HarmonyMethod(typeof(TimeDisplayPatches), nameof(TimeDisplayPatches.GetTimeOfDayString_Prefix))
                    );

                SkullCavernJumpPatches.Initialize(Monitor);
                harmony.Patch(
                    original: AccessTools.Method(typeof(MineShaft), nameof(MineShaft.enterMineShaft)),
                    transpiler: new HarmonyMethod(typeof(SkullCavernJumpPatches), nameof(SkullCavernJumpPatches.EnterMineShaft_Transpile))
                    );

                FixWarpToFestivalBugPatches.Initialize(Monitor);
                harmony.Patch(
                    original: AccessTools.Method(typeof(Game1), nameof(Game1.warpFarmer), new[] { typeof(LocationRequest), typeof(int), typeof(int), typeof(int) }),
                    prefix: new HarmonyMethod(typeof(FixWarpToFestivalBugPatches), nameof(FixWarpToFestivalBugPatches.WarpFarmer_Prefix))
                    );

                if (helper.ModRegistry.Get("jorgamun.PauseInMultiplayer") is IModInfo pimMod)
                {
                    Monitor.Log($"Disabling the {pimMod.Manifest.Name} mod. It can be uninstalled as it is no longer necessary with {ModManifest.Name}. {ModManifest.Name} provides its features.", LogLevel.Warn);
                    if (FancyModIntegration.RemoveModEventHandlers(helper, pimMod, Monitor) is var numRemoved && numRemoved < 10)
                    {
                        Monitor.Log($"Removed only {numRemoved} event handlers from {pimMod.Manifest.Name}. Please report this as a bug in the {ModManifest.Name} mod (with an SMAPI log) as it may be a bug causing incompatibility with {pimMod.Manifest.Name}.", LogLevel.Warn);
                    }
                }

                if (helper.ModRegistry.Get("cantorsdust.TimeSpeed") is IModInfo tsMod)
                {
                    Monitor.Log($"Disabling the {tsMod.Manifest.Name} mod. It can be uninstalled as it is no longer necessary with {ModManifest.Name}. {ModManifest.Name} provides its features.", LogLevel.Warn);
                    if (FancyModIntegration.RemoveModEventHandlers(helper, tsMod, Monitor) is var numRemoved && numRemoved < 9)
                    {
                        Monitor.Log($"Removed only {numRemoved} event handlers from {tsMod.Manifest.Name}. Please report this as a bug in the {ModManifest.Name} mod (with an SMAPI log) as it may be a bug causing incompatibility with {tsMod.Manifest.Name}.", LogLevel.Warn);
                    }
                }
            }
            catch (Exception e)
            {
                Monitor.Log($"Failed to apply patches; some features may not work correctly. Technical details:\n{e}", LogLevel.Error);
            }
        }

        private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
        {
            var configMenu = Helper.ModRegistry.GetApi<IGenericModConfigMenuApi>("spacechase0.GenericModConfigMenu");
            if (configMenu != null)
            {
                configMenu.Register(
                    mod: ModManifest,
                    reset: () => Config.Update(new ModConfig()),
                    save: () =>
                    {
                        Helper.WriteConfig(Config);
                        TimeController.ReloadConfig();
                    }
                    );

                // Speed of Time
                const float minSpeedOfTime = 0f;
                const float maxSpeedOfTime = 3f;
                const float unsetSpeedOfTime = 0f;
                const float vanillaSecondsPerMinute = 0.7f;
                const float vanillaSecondsPerMinute_skull = 0.9f;
                const float speedOfTimeInterval = 0.01f;
                const string additionalLocationsSpeedPageID = "speed-additional-locations";

                configMenu.AddSectionTitle(ModManifest, I18n.Config_SpeedOfTime);
                configMenu.AddParagraph(ModManifest, I18n.Config_SpeedOfTime_Paragraph);
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_IndoorsSpeed_Name,
                    tooltip: I18n.Config_IndoorsSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.Indoors.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.Indoors : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.Indoors = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_FarmHouseSpeed_Name,
                    tooltip: I18n.Config_FarmHouseSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.FarmHouse.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.FarmHouse : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.FarmHouse = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_BathHouseSpeed_Name,
                    tooltip: I18n.Config_BathHouseSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.BathHouse.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.BathHouse : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.BathHouse = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_OutdoorsSpeed_Name,
                    tooltip: I18n.Config_OutdoorsSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.Outdoors.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.Outdoors : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.Outdoors = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_FarmSpeed_Name,
                    tooltip: I18n.Config_FarmSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.Farm.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.Farm : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.Farm = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_TownExteriorsSpeed_Name,
                    tooltip: I18n.Config_TownExteriorsSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.TownExteriors.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.TownExteriors : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.TownExteriors = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_MineSpeed_Name,
                    tooltip: I18n.Config_MineSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.Mines.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.Mines : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.Mines = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_SkullCavernSpeed_Name,
                    tooltip: I18n.Config_SkullCavernSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.SkullCavern.HasValue ? vanillaSecondsPerMinute_skull / (float)Config.SecondsPerMinute.SkullCavern : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.SkullCavern = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute_skull / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_VolcanoDungeonSpeed_Name,
                    tooltip: I18n.Config_VolcanoDungeonSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.VolcanoDungeon.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.VolcanoDungeon : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.VolcanoDungeon = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                if (Helper.ModRegistry.IsLoaded("maxvollmer.deepwoodsmod"))
                {
                    configMenu.AddNumberOption(
                        mod: ModManifest,
                        name: I18n.Config_DeepWoodsSpeed_Name,
                        tooltip: I18n.Config_DeepWoodsSpeed_Desc,
                        getValue: () => Config.SecondsPerMinute.DeepWoods.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.DeepWoods : unsetSpeedOfTime,
                        setValue: value => Config.SecondsPerMinute.DeepWoods = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                        formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                        min: minSpeedOfTime,
                        max: maxSpeedOfTime,
                        interval: speedOfTimeInterval
                    );
                }
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_NightMarketSpeed_Name,
                    tooltip: I18n.Config_NightMarketSpeed_Desc,
                    getValue: () => Config.SecondsPerMinute.NightMarket.HasValue ? vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.NightMarket : unsetSpeedOfTime,
                    setValue: value => Config.SecondsPerMinute.NightMarket = (value == unsetSpeedOfTime ? null : Math.Round(vanillaSecondsPerMinute / value, 4)),
                    formatValue: value => value == unsetSpeedOfTime ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: minSpeedOfTime,
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddTextOption(
                    mod: ModManifest,
                    name: I18n.Config_AdditionalLocationsSpeed_Names_Name,
                    tooltip: I18n.Config_AdditionalLocationsSpeed_Names_Desc,
                    getValue: () => string.Join(", ", Config.SecondsPerMinute.ByLocationName.Keys),
                    setValue: value =>
                    {
                        configMenu.AddPage(
                            mod: ModManifest,
                            pageId: additionalLocationsSpeedPageID,
                            pageTitle: I18n.Config_AdditionalLocationsSpeed_Page_Title
                        );
                        Dictionary<string, double> previousByLocationName = new(Config.SecondsPerMinute.ByLocationName);
                        Config.SecondsPerMinute.ByLocationName.Clear();
                        foreach (var locationName in value.Split(",").Select(p => p.Trim()).Where(p => p != string.Empty))
                        {
                            if (previousByLocationName.TryGetValue(locationName, out double oldValue))
                            {
                                Config.SecondsPerMinute.ByLocationName.Add(locationName, oldValue);
                            }
                            else
                            {
                                Config.SecondsPerMinute.ByLocationName.Add(locationName, vanillaSecondsPerMinute);
                                AddAdditionalLocationSpeedOptions(configMenu, minSpeedOfTime, maxSpeedOfTime, vanillaSecondsPerMinute, speedOfTimeInterval, locationName);
                            }
                        }
                        configMenu.AddPage(
                            mod: ModManifest,
                            pageId: "",
                            pageTitle: null
                        );
                    }
                );
                configMenu.AddPage(
                    mod: ModManifest,
                    pageId: additionalLocationsSpeedPageID,
                    pageTitle: I18n.Config_AdditionalLocationsSpeed_Page_Title
                );
                foreach (var locationName in Config.SecondsPerMinute.ByLocationName.Keys)
                {
                    AddAdditionalLocationSpeedOptions(configMenu, minSpeedOfTime, maxSpeedOfTime, vanillaSecondsPerMinute, speedOfTimeInterval, locationName);
                }
                configMenu.AddPage(
                    mod: ModManifest,
                    pageId: "",
                    pageTitle: null
                );
                configMenu.AddPageLink(
                    mod: ModManifest,
                    pageId: additionalLocationsSpeedPageID,
                    text: I18n.Config_AdditionalLocationsSpeed_Link_Name,
                    tooltip: I18n.Config_AdditionalLocationsSpeed_Link_Desc
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_EnableOnFestivalDays_Name,
                    tooltip: I18n.Config_EnableOnFestivalDays_Desc,
                    getValue: () => Config.EnableOnFestivalDays,
                    setValue: value => Config.EnableOnFestivalDays = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_TimeFlowChangeNotifications_Name,
                    tooltip: I18n.Config_TimeFlowChangeNotifications_Desc,
                    getValue: () => Config.TimeFlowChangeNotifications,
                    setValue: value => Config.TimeFlowChangeNotifications = value
                );

                // Freeze time
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddSectionTitle(ModManifest, I18n.Config_FreezeTime);
                configMenu.AddParagraph(ModManifest, I18n.Config_FreezeTime_Paragraph);
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: I18n.Config_AnywhereAtTime_Name,
                    tooltip: I18n.Config_AnywhereAtTime_Desc,
                    getValue: () => Config.FreezeTime.AnywhereAtTime ?? 2600,
                    setValue: value => Config.FreezeTime.AnywhereAtTime = (value == 2600 ? null : value),
                    formatValue: value => value == 2600 ? I18n.Config_OptionDisabled() : Game1.getTimeOfDayString(value),
                    min: 600,
                    max: 2600,
                    interval: 100
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeIndoors_Name,
                    tooltip: I18n.Config_FreezeTimeIndoors_Desc,
                    getValue: () => Config.FreezeTime.Indoors,
                    setValue: value => Config.FreezeTime.Indoors = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeFarmHouse_Name,
                    tooltip: I18n.Config_FreezeTimeFarmHouse_Desc,
                    getValue: () => Config.FreezeTime.FarmHouse,
                    setValue: value => Config.FreezeTime.FarmHouse = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeBathHouse_Name,
                    tooltip: I18n.Config_FreezeTimeBathHouse_Desc,
                    getValue: () => Config.FreezeTime.BathHouse,
                    setValue: value => Config.FreezeTime.BathHouse = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeOutdoors_Name,
                    tooltip: I18n.Config_FreezeTimeOutdoors_Desc,
                    getValue: () => Config.FreezeTime.Outdoors,
                    setValue: value => Config.FreezeTime.Outdoors = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeFarm_Name,
                    tooltip: I18n.Config_FreezeTimeFarm_Desc,
                    getValue: () => Config.FreezeTime.Farm,
                    setValue: value => Config.FreezeTime.Farm = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeTownExteriors_Name,
                    tooltip: I18n.Config_FreezeTimeTownExteriors_Desc,
                    getValue: () => Config.FreezeTime.TownExteriors,
                    setValue: value => Config.FreezeTime.TownExteriors = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeMine_Name,
                    tooltip: I18n.Config_FreezeTimeMine_Desc,
                    getValue: () => Config.FreezeTime.Mines,
                    setValue: value => Config.FreezeTime.Mines = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeSkullCavern_Name,
                    tooltip: I18n.Config_FreezeTimeSkullCavern_Desc,
                    getValue: () => Config.FreezeTime.SkullCavern,
                    setValue: value => Config.FreezeTime.SkullCavern = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeVolcanoDungeon_Name,
                    tooltip: I18n.Config_FreezeTimeVolcanoDungeon_Desc,
                    getValue: () => Config.FreezeTime.VolcanoDungeon,
                    setValue: value => Config.FreezeTime.VolcanoDungeon = value
                );
                if (Helper.ModRegistry.IsLoaded("maxvollmer.deepwoodsmod"))
                {
                    configMenu.AddBoolOption(
                        mod: ModManifest,
                        name: I18n.Config_FreezeTimeDeepWoods_Name,
                        tooltip: I18n.Config_FreezeTimeDeepWoods_Desc,
                        getValue: () => Config.FreezeTime.DeepWoods,
                        setValue: value => Config.FreezeTime.DeepWoods = value
                    );
                }
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeNightMarket_Name,
                    tooltip: I18n.Config_FreezeTimeNightMarket_Desc,
                    getValue: () => Config.FreezeTime.NightMarket,
                    setValue: value => Config.FreezeTime.NightMarket = value
                );
                configMenu.AddTextOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeFreezeNames_Name,
                    tooltip: I18n.Config_FreezeTimeFreezeNames_Desc,
                    getValue: () => string.Join(", ", Config.FreezeTime.ByLocationName),
                    setValue: value => Config.FreezeTime.ByLocationName = new(
                        value
                            .Split(",")
                            .Select(p => p.Trim())
                            .Where(p => p != string.Empty)
                    )
                );
                configMenu.AddTextOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeDontFreezeNames_Name,
                    tooltip: I18n.Config_FreezeTimeDontFreezeNames_Desc,
                    getValue: () => string.Join(", ", Config.FreezeTime.ExceptLocationNames),
                    setValue: value => Config.FreezeTime.ExceptLocationNames = new(
                        value
                            .Split(",")
                            .Select(p => p.Trim())
                            .Where(p => p != string.Empty)
                    )
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeObjectsPassTime_Name,
                    tooltip: I18n.Config_FreezeTimeObjectsPassTime_Desc,
                    getValue: () => Config.ObjectsPassTimeWhenTimeIsFrozen,
                    setValue: value => Config.ObjectsPassTimeWhenTimeIsFrozen = value
                );

                // Clock Display
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddSectionTitle(mod: base.ModManifest, text: I18n.Config_ClockDisplay);
                configMenu.AddParagraph(ModManifest, I18n.Config_ClockDisplay_Paragraph);
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_ClockDisplayDisplayMinutes_Name,
                    tooltip: I18n.Config_ClockDisplayDisplayMinutes_Desc,
                    getValue: () => Config.DisplayMinutes,
                    setValue: value => Config.DisplayMinutes = value
                    );
                configMenu.AddBoolOption(
                    mod: base.ModManifest,
                    name: I18n.Config_ClockDisplayShowPauseX_Name,
                    tooltip: I18n.Config_ClockDisplayShowPauseX_Desc,
                    getValue: () => Config.ShowPauseX,
                    setValue: value => Config.ShowPauseX = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_ClockDisplayUse24HourFormat_Name,
                    tooltip: I18n.Config_ClockDisplayUse24HourFormat_Desc,
                    getValue: () => Config.Use24HourFormat,
                    setValue: value => Config.Use24HourFormat = value
                    );

                // Multiplayer Options
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddSectionTitle(mod: base.ModManifest, text: I18n.Config_Multiplayer);
                configMenu.AddBoolOption(
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerDisplayVotePauseMessages_Name,
                    tooltip: I18n.Config_MultiplayerDisplayVotePauseMessages_Desc,
                    getValue: () => Config.DisplayVotePauseMessages,
                    setValue: value => Config.DisplayVotePauseMessages = value
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_TimeFlowChangeNotificationsMultiplayer_Name,
                    tooltip: I18n.Config_TimeFlowChangeNotificationsMultiplayer_Desc,
                    getValue: () => Config.TimeFlowChangeNotificationsMultiplayer,
                    setValue: value => Config.TimeFlowChangeNotificationsMultiplayer = value
                );

                // Multiplayer Host-only Options
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddSectionTitle(mod: base.ModManifest, text: I18n.Config_MultiplayerHost);
                configMenu.AddParagraph(ModManifest, I18n.Config_MultiplayerHost_Paragraph);
                configMenu.AddTextOption(
                    mod: ModManifest,
                    name: I18n.Config_MultiplayerHostPauseMode_Name,
                    tooltip: I18n.Config_MultiplayerHostPauseMode_Desc,
                    getValue: () => Enum.TryParse(Config.PauseMode.ToString(), true, out PauseMode _) ? Config.PauseMode.ToString() : PauseMode.Fair.ToString(),
                    setValue: value => Config.PauseMode = Enum.TryParse(value, true, out PauseMode result) ? result : PauseMode.Fair,
                    allowedValues: Enum.GetNames<PauseMode>().Where(mode => !Enum.Parse<PauseMode>(mode).IsDeprecated()).ToArray(),
                    formatAllowedValue: (s) => Helper.Translation.Get($"config.multiplayer-host-pause-mode.{s}")
                    );
                configMenu.AddBoolOption(
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerHostAnyCutscenePauses_Name,
                    tooltip: I18n.Config_MultiplayerHostAnyCutscenePauses_Desc,
                    getValue: () => Config.AnyCutscenePauses,
                    setValue: value => Config.AnyCutscenePauses = value
                );
                configMenu.AddBoolOption(
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerHostLockMonsters_Name,
                    tooltip: I18n.Config_MultiplayerHostLockMonsters_Desc,
                    getValue: () => Config.LockMonsters,
                    setValue: value => Config.LockMonsters = value
                );
                configMenu.AddBoolOption(
                    mod: base.ModManifest,
                    name: I18n.Config_MultiplayerHostEnableVotePause_Name,
                    tooltip: I18n.Config_MultiplayerHostEnableVotePause_Desc,
                    getValue: () => Config.EnableVotePause,
                    setValue: value => Config.EnableVotePause = value
                );
                configMenu.AddTextOption(
                    mod: ModManifest,
                    name: I18n.Config_MultiplayerHostTimeSpeedMode_Name,
                    tooltip: I18n.Config_MultiplayerHostTimeSpeedMode_Desc,
                    getValue: () => Enum.TryParse(Config.TimeSpeedMode.ToString(), true, out TimeSpeedMode _) ? Config.TimeSpeedMode.ToString() : TimeSpeedMode.Average.ToString(),
                    setValue: value => Config.TimeSpeedMode = Enum.TryParse(value, true, out TimeSpeedMode result) ? result : TimeSpeedMode.Average,
                    allowedValues: Enum.GetNames<TimeSpeedMode>().Where(mode => !Enum.Parse<TimeSpeedMode>(mode).IsDeprecated()).ToArray(),
                    formatAllowedValue: (s) => Helper.Translation.Get($"config.multiplayer-host-time-speed-mode.{s}")
                    );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: I18n.Config_MultiplayerHostRelativeTimeSpeed_Name,
                    tooltip: I18n.Config_MultiplayerHostRelativeTimeSpeed_Desc,
                    getValue: () => Config.RelativeTimeSpeed,
                    setValue: value => Config.RelativeTimeSpeed = value
                );

                // Controls
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddParagraph(ModManifest, () => "");
                configMenu.AddSectionTitle(ModManifest, I18n.Config_Controls);
                configMenu.AddKeybindList(
                    mod: base.ModManifest,
                    name: I18n.Config_VoteForPauseKey_Name,
                    tooltip: I18n.Config_VoteForPauseKey_Desc,
                    getValue: () => Config.Keys.VoteForPause,
                    setValue: value => Config.Keys.VoteForPause = value
                );
                configMenu.AddKeybindList(
                    mod: ModManifest,
                    name: I18n.Config_FreezeTimeKey_Name,
                    tooltip: I18n.Config_FreezeTimeKey_Desc,
                    getValue: () => Config.Keys.FreezeTime,
                    setValue: value => Config.Keys.FreezeTime = value
                );
                configMenu.AddKeybindList(
                    mod: ModManifest,
                    name: I18n.Config_SlowTimeKey_Name,
                    tooltip: I18n.Config_SlowTimeKey_Desc,
                    getValue: () => Config.Keys.IncreaseTickInterval,
                    setValue: value => Config.Keys.IncreaseTickInterval = value
                );
                configMenu.AddKeybindList(
                    mod: ModManifest,
                    name: I18n.Config_SpeedUpTimeKey_Name,
                    tooltip: I18n.Config_SpeedUpTimeKey_Desc,
                    getValue: () => Config.Keys.DecreaseTickInterval,
                    setValue: value => Config.Keys.DecreaseTickInterval = value
                );
            }

            RunCompatibilityChecksForMod(
                configMenu,
                "jorgamun.PauseInMultiplayer",
                "config",
                Config.ShouldMergePauseInMultiplayerConfigOnNextRun,
                c => c.ShouldMergePauseInMultiplayerConfigOnNextRun = false);

            RunCompatibilityChecksForMod(
                configMenu,
                "cantorsdust.TimeSpeed",
                "Config",
                Config.ShouldMergeTimeSlowConfigOnNextRun,
                c => c.ShouldMergeTimeSlowConfigOnNextRun = false);

            void AddAdditionalLocationSpeedOptions(IGenericModConfigMenuApi configMenu, float minSpeedOfTime, float maxSpeedOfTime, float vanillaSecondsPerMinute, float speedOfTimeInterval, string locationName)
            {
                configMenu.AddNumberOption(
                    mod: ModManifest,
                    name: () => I18n.Config_AdditionalLocationSpeed_Name(locationName),
                    tooltip: () => I18n.Config_AdditionalLocationSpeed_Desc(locationName),
                    getValue: () =>
                    {
                        if (Config.SecondsPerMinute.ByLocationName.ContainsKey(locationName))
                        {
                            return vanillaSecondsPerMinute / (float)Config.SecondsPerMinute.ByLocationName.GetValueOrDefault(locationName, vanillaSecondsPerMinute);
                        }
                        else
                        {
                            return 0f;
                        }
                    },
                    setValue: value =>
                    {
                        if (Config.SecondsPerMinute.ByLocationName.ContainsKey(locationName))
                        {
                            Config.SecondsPerMinute.ByLocationName[locationName] = Math.Round(vanillaSecondsPerMinute / value, 4);
                        }
                    },
                    formatValue: value => value == 0f ? I18n.Config_OptionDisabled() : value.ToString("P0"),
                    min: Math.Max(0.01f, minSpeedOfTime),
                    max: maxSpeedOfTime,
                    interval: speedOfTimeInterval
                );
                configMenu.AddBoolOption(
                    mod: ModManifest,
                    name: () => I18n.Config_AdditionalLocationSpeedDelete_Name(locationName),
                    tooltip: I18n.Config_AdditionalLocationSpeedDelete_Desc,
                    getValue: () => !Config.SecondsPerMinute.ByLocationName.ContainsKey(locationName),
                    setValue: value =>
                    {
                        if (value)
                        {
                            Config.SecondsPerMinute.ByLocationName.Remove(locationName);
                        }
                    }
                );
            }
        }

        private void RunCompatibilityChecksForMod(IGenericModConfigMenuApi? configMenu, string uniqueID, string configFieldTypeColonName, bool shouldMergeConfigs, Action<ModConfig> recordMergeCompletion)
        {
            try
            {
                if (Helper.ModRegistry.Get(uniqueID) is IModInfo modInfo)
                {
                    configMenu?.Unregister(modInfo.Manifest);
                    if (shouldMergeConfigs && MergeConfig(modInfo, configFieldTypeColonName) is ModConfig newConfig)
                    {
                        Config.Update(newConfig);
                        Monitor.Log($"Successfully merged {modInfo.Manifest.Name} config into {ModManifest.Name} config.", LogLevel.Info);
                    }
                }

                if (shouldMergeConfigs)
                {
                    recordMergeCompletion(Config);
                    Helper.WriteConfig(Config);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Exception while merging {uniqueID} config.\n{ex}", LogLevel.Error);
            }
        }

        private ModConfig? MergeConfig(IModInfo modInfo, string configFieldName)
        {
            if (!FancyModIntegration.TryGetModEntry(modInfo, Monitor, out var modEntry))
            {
                Monitor.Log($"Failed to merge {modInfo.Manifest.Name} config. Could not find its ModEntry.", LogLevel.Error);
                return null;
            }

            if (AccessTools.Field(modEntry.GetType(), configFieldName)?.GetValue(modEntry) is not object modConfig)
            {
                Monitor.Log($"Failed to merge {modInfo.Manifest.Name} config. Could not find its ModConfig at {modEntry.GetType().FullName}:{configFieldName}.", LogLevel.Error);
                return null;
            }

            var currentConfigJObject = JObject.FromObject(Config);
            var toBeMergedConfigJObject = JObject.FromObject(modConfig);
            currentConfigJObject.Merge(toBeMergedConfigJObject, new JsonMergeSettings
            {
                MergeArrayHandling = MergeArrayHandling.Replace
            });

            if (currentConfigJObject.ToObject<ModConfig>() is not ModConfig mergedConfig)
            {
                Monitor.Log($"Failed to serialize merged config for {modInfo.Manifest.Name}.", LogLevel.Error);
                return null;
            }

#pragma warning disable CS0612 // Type or member is obsolete
            mergedConfig.Keys.VoteForPause = new(mergedConfig.VotePauseHotkey);
#pragma warning restore CS0612 // Type or member is obsolete

            return mergedConfig;
        }
    }
}