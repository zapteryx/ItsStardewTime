using System.Reflection;
using System.Text;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Sickhead.Engine.Util;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace ItsStardewTime.Patches
{
    internal class TimeDisplayPatches
    {
#nullable disable
        private static IMonitor Monitor;

        private static ModConfig Config;
#nullable enable

        internal static void Initialize(IMonitor monitor, ModConfig config)
        {
            Monitor = monitor;
            Config = config;
        }

        internal static bool GetTimeOfDayString_Prefix(int time, ref string __result)
        {
            try
            {
                if (!Config.Use24HourFormat)
                {
                    return true;
                }

                __result = $"{time / 100 % 24}:{time % 100:00}";
                return false;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to run patch:\n{ex}", LogLevel.Error);
                return true;
            }
        }

        internal static bool Draw_Prefix(DayTimeMoneyBox __instance, SpriteBatch b)
        {
            try
            {
                if (!Config.Use24HourFormat && !Config.DisplayMinutes && !Config.ShowPauseX)
                {
                    return true;
                }

                SpriteFont font =
                    ((LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko)
                        ? Game1.smallFont
                        : Game1.dialogueFont);
                typeof(DayTimeMoneyBox).GetMethod
                (
                    "updatePosition",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )?.Invoke(__instance, new object[0]);
                if (__instance.timeShakeTimer > 0)
                {
                    __instance.timeShakeTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                }

                if (__instance.questPulseTimer > 0)
                {
                    __instance.questPulseTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                }

                if (__instance.whenToPulseTimer >= 0)
                {
                    __instance.whenToPulseTimer -= Game1.currentGameTime.ElapsedGameTime.Milliseconds;
                    if (__instance.whenToPulseTimer <= 0)
                    {
                        __instance.whenToPulseTimer = 3000;
                        if (Game1.player.hasNewQuestActivity())
                        {
                            __instance.questPulseTimer = 1000;
                        }
                    }
                }

                // get source rect via reflection
                Rectangle sourceRect = (Rectangle)typeof(DayTimeMoneyBox).GetField
                (
                    "sourceRect",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )?.GetValue(__instance)!;
                b.Draw(Game1.mouseCursors, __instance.position, sourceRect, Color.White, 0f, Vector2.Zero,
                    4f, SpriteEffects.None, 0.9f);
                var timeSpeed = TimeMaster.TimeSpeed;
                if (Config.ShowPauseX && timeSpeed.IsTimeFrozen)
                {
                    b.Draw(Game1.mouseCursors, __instance.position + new Vector2(23f, 55f),
                        new Rectangle(269, 471, 15, 15), new Color(0, 0, 0, 64), 0f, Vector2.Zero, 4f,
                        SpriteEffects.None, 0.9f);
                }

                // get last day of month via reflection
                var lastDayOfMonth = typeof(DayTimeMoneyBox).GetField
                (
                    "_lastDayOfMonth",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );
                // get last day of month string via reflection
                if (Game1.dayOfMonth != (int)lastDayOfMonth!.GetValue(__instance)!)
                {
                    // set last day of month via reflection
                    lastDayOfMonth.SetValue(__instance, Game1.dayOfMonth);
                    // set last day of month string via reflection
                    typeof(DayTimeMoneyBox).GetField
                    (
                        "_lastDayOfMonthString",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )?.SetValue
                    (
                        __instance,
                        Game1.shortDayDisplayNameFromDayOfSeason
                        (
                            (int)lastDayOfMonth!.GetValue(__instance)!
                        )
                    );
                }

                // get date text via reflection
                StringBuilder dateText = (StringBuilder)(typeof(DayTimeMoneyBox).GetField
                (
                    "_dateText",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )!.GetValue(__instance))!;
                dateText.Clear();

                var lastDayOfMonthString = (string)typeof(DayTimeMoneyBox).GetField
                (
                    "_lastDayOfMonthString",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )!.GetValue(__instance)!;
                if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ja)
                {
                    dateText.AppendEx(Game1.dayOfMonth);
                    dateText.Append("日 (");
                    dateText.Append(lastDayOfMonthString);
                    dateText.Append(')');
                }
                else if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh)
                {
                    dateText.Append(lastDayOfMonthString);
                    dateText.Append(' ');
                    dateText.AppendEx(Game1.dayOfMonth);
                    dateText.Append('日');
                }
                else if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.mod)
                {
                    dateText.Append(LocalizedContentManager.CurrentModLanguage.ClockDateFormat
                        .Replace("[DAY_OF_WEEK]", lastDayOfMonthString)
                        .Replace("[DAY_OF_MONTH]", Game1.dayOfMonth.ToString()));
                }
                else
                {
                    dateText.Append(lastDayOfMonthString);
                    dateText.Append(". ");
                    dateText.AppendEx(Game1.dayOfMonth);
                }

                Vector2 daySize = font.MeasureString(dateText);
                Vector2 dayPosition = new((float)sourceRect.X * 0.55f - daySize.X / 2f,
                    (float)sourceRect.Y * (LocalizedContentManager.CurrentLanguageLatin ? 0.1f : 0.1f) -
                    daySize.Y / 2f);
                Utility.drawTextWithShadow(b, dateText, font, __instance.position + dayPosition,
                    Game1.textColor);
                b.Draw(Game1.mouseCursors, __instance.position + new Vector2(212f, 68f),
                    new Rectangle(406, 441 + Utility.getSeasonNumber(Game1.currentSeason) * 8, 12, 8), Color.White, 0f,
                    Vector2.Zero, 4f, SpriteEffects.None, 0.9f);
                b.Draw(Game1.mouseCursors, __instance.position + new Vector2(116f, 68f),
                    new Rectangle(317 + 12 * Game1.weatherIcon, 421, 12, 8), Color.White, 0f, Vector2.Zero, 4f,
                    SpriteEffects.None, 0.9f);
                int timeOfDay = Game1.timeOfDay;
                if (Config.DisplayMinutes)
                {
                    timeOfDay += Math.Min(9, (int)(10d * timeSpeed.TimeHelper.TickProgress));
                }

                StringBuilder hours = (StringBuilder)typeof(DayTimeMoneyBox).GetField
                (
                    "_hours",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )!.GetValue(__instance)!;
                hours.Clear();
                if (Config.Use24HourFormat)
                {
                   hours.AppendEx(timeOfDay / 100 % 24);
                }
                else
                {
                    var temp = (StringBuilder)typeof(DayTimeMoneyBox).GetField
                    (
                        "_temp",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )!.GetValue(__instance)!;
                    
                    switch (LocalizedContentManager.CurrentLanguageCode)
                    {
                        case LocalizedContentManager.LanguageCode.zh:
                            if (timeOfDay / 100 % 24 == 0)
                            {
                               hours.Append("00");
                            }
                            else if (timeOfDay / 100 % 12 == 0)
                            {
                               hours.Append("12");
                            }
                            else
                            {
                               hours.AppendEx(timeOfDay / 100 % 12);
                            }

                            break;
                        case LocalizedContentManager.LanguageCode.ru:
                        case LocalizedContentManager.LanguageCode.pt:
                        case LocalizedContentManager.LanguageCode.es:
                        case LocalizedContentManager.LanguageCode.de:
                        case LocalizedContentManager.LanguageCode.th:
                        case LocalizedContentManager.LanguageCode.fr:
                        case LocalizedContentManager.LanguageCode.tr:
                        case LocalizedContentManager.LanguageCode.hu:

                            temp.Clear();
                            temp.AppendEx(timeOfDay / 100 % 24);
                            if (timeOfDay / 100 % 24 <= 9)
                            {
                               hours.Append('0');
                            }
                            hours.AppendEx(temp);
                            break;
                        default:
                            if (timeOfDay / 100 % 12 == 0)
                            {
                                if (LocalizedContentManager.CurrentLanguageCode ==
                                    LocalizedContentManager.LanguageCode.ja)
                                {
                                   hours.Append('0');
                                }
                                else
                                {
                                   hours.Append("12");
                                }
                            }
                            else
                            {
                               hours.AppendEx(timeOfDay / 100 % 12);
                            }

                            break;
                    }
                }
                
                var timeText = (StringBuilder)typeof(DayTimeMoneyBox).GetField
                (
                    "_timeText",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )!.GetValue(__instance)!;
                timeText.Clear();
                timeText.AppendEx(hours);
                timeText.Append(':');
                timeText.Append($"{timeOfDay % 100:00}");
                if (!Config.Use24HourFormat)
                {
                    var amString = (string)typeof(DayTimeMoneyBox).GetField
                    (
                        "_amString",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )!.GetValue(__instance)!;
                    var pmString = (string)typeof(DayTimeMoneyBox).GetField
                    (
                        "_pmString",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    )!.GetValue(__instance)!;
                    if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.en ||
                        LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.it)
                    {
                        timeText.Append(' ');
                        if (timeOfDay < 1200 || timeOfDay >= 2400)
                        {
                            timeText.Append(amString);
                        }
                        else
                        {
                            timeText.Append(pmString);
                        }
                    }
                    else if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ko)
                    {
                        if (timeOfDay < 1200 || timeOfDay >= 2400)
                        {
                            timeText.Append(amString);
                        }
                        else
                        {
                            timeText.Append(amString);
                        }
                    }
                    else if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.ja)
                    {
                        var temp = (StringBuilder)typeof(DayTimeMoneyBox).GetField
                        (
                            "_temp",
                            BindingFlags.NonPublic | BindingFlags.Instance
                        )!.GetValue(__instance)!;
                        
                        temp.Clear();
                        temp.AppendEx(timeText);
                        timeText.Clear();
                        if (timeOfDay < 1200 || timeOfDay >= 2400)
                        {
                            timeText.Append(amString);
                            timeText.Append(' ');
                            timeText.AppendEx(temp);
                        }
                        else
                        {
                            timeText.Append(amString);
                            timeText.Append(' ');
                            timeText.AppendEx(temp);
                        }
                    }
                    else if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.zh)
                    {
                        var temp = (StringBuilder)typeof(DayTimeMoneyBox).GetField
                        (
                            "_temp",
                            BindingFlags.NonPublic | BindingFlags.Instance
                        )!.GetValue(__instance)!;
                        temp.Clear();
                        temp.AppendEx(timeText);
                        timeText.Clear();
                        if (timeOfDay < 600 || timeOfDay >= 2400)
                        {
                            timeText.Append("凌晨 ");
                            timeText.AppendEx(temp);
                        }
                        else if (timeOfDay < 1200)
                        {
                            timeText.Append(amString);
                            timeText.Append(' ');
                            timeText.AppendEx(temp);
                        }
                        else if (timeOfDay < 1300)
                        {
                            timeText.Append("中午  ");
                            timeText.AppendEx(temp);
                        }
                        else if (timeOfDay < 1900)
                        {
                            timeText.Append(pmString);
                            timeText.Append(' ');
                            timeText.AppendEx(temp);
                        }
                        else
                        {
                            timeText.Append("晚上  ");
                            timeText.AppendEx(temp);
                        }
                    }
                    else if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.mod)
                    {
                        timeText.Clear();
                        timeText.Append(LocalizedContentManager.FormatTimeString(timeOfDay,
                            LocalizedContentManager.CurrentModLanguage.ClockTimeFormat));
                    }
                }

                Vector2 txtSize = font.MeasureString(timeText);
                Vector2 timePosition = new(
                    (float)sourceRect.X * 0.55f - txtSize.X / 2f +
                    (float)((__instance.timeShakeTimer > 0) ? Game1.random.Next(-2, 3) : 0),
                    (float)sourceRect.Y * (LocalizedContentManager.CurrentLanguageLatin ? 0.31f : 0.31f) -
                    txtSize.Y / 2f + (float)((__instance.timeShakeTimer > 0) ? Game1.random.Next(-2, 3) : 0));
                bool nofade = (!timeSpeed.IsTimeFrozen && Game1.shouldTimePass()) || Game1.fadeToBlack ||
                              Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 2000.0 > 1000.0;
                Utility.drawTextWithShadow(b, timeText, font, __instance.position + timePosition,
                    (timeOfDay >= 2400) ? Color.Red : (Game1.textColor * (nofade ? 1f : 0.5f)));
                int adjustedTime = (int)((float)(timeOfDay - timeOfDay % 100) + (float)(timeOfDay % 100 / 10) * 16.66f);
                if (Game1.player.hasVisibleQuests)
                {
                    __instance.questButton.draw(b);
                    if (__instance.questPulseTimer > 0)
                    {
                        float scaleMult =
                            1f / (Math.Max(300f, Math.Abs(__instance.questPulseTimer % 1000 - 500)) / 500f);
                        b.Draw(Game1.mouseCursors,
                            new Vector2(__instance.questButton.bounds.X + 24, __instance.questButton.bounds.Y + 32) +
                            ((scaleMult > 1f)
                                ? new Vector2(Game1.random.Next(-1, 2), Game1.random.Next(-1, 2))
                                : Vector2.Zero), new Rectangle(395, 497, 3, 8), Color.White, 0f, new Vector2(2f, 4f),
                            4f * scaleMult, SpriteEffects.None, 0.99f);
                    }

                    if (__instance.questPingTimer > 0)
                    {
                        b.Draw(Game1.mouseCursors,
                            new Vector2(Game1.dayTimeMoneyBox.questButton.bounds.Left - 16,
                                Game1.dayTimeMoneyBox.questButton.bounds.Bottom + 8),
                            new Rectangle(128 + ((__instance.questPingTimer / 200 % 2 != 0) ? 16 : 0), 208, 16, 16),
                            Color.White, 0f, Vector2.Zero, 4f, SpriteEffects.None, 0.9f);
                    }
                }

                if (Game1.options.zoomButtons)
                {
                    __instance.zoomInButton.draw(b,
                        Color.White * ((Game1.options.desiredBaseZoomLevel >= 2f) ? 0.5f : 1f), 1f);
                    __instance.zoomOutButton.draw(b,
                        Color.White * ((Game1.options.desiredBaseZoomLevel <= 0.75f) ? 0.5f : 1f), 1f);
                }

                __instance.drawMoneyBox(b);
                // get hoverText via reflection
                var hoverText = typeof(DayTimeMoneyBox).GetField
                (
                    "_hoverText",
                    BindingFlags.NonPublic | BindingFlags.Instance
                )!.GetValue(__instance)!.ToString();
                if (hoverText.Length > 0 &&
                    __instance.isWithinBounds(Game1.getOldMouseX(), Game1.getOldMouseY()))
                {
                    IClickableMenu.drawHoverText(b, hoverText, Game1.dialogueFont);
                }

                b.Draw(Game1.mouseCursors, __instance.position + new Vector2(88f, 88f), new Rectangle(324, 477, 7, 19),
                    Color.White,
                    (float)(Math.PI + Math.Min(Math.PI,
                        (double)(((float)adjustedTime + (float)Game1.gameTimeInterval / 7000f * 16.6f - 600f) / 2000f) *
                        Math.PI)), new Vector2(3f, 17f), 4f, SpriteEffects.None, 0.9f);
                return false;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Failed to run patch:\n{ex}", LogLevel.Error);
                return true;
            }
        }
    }
}