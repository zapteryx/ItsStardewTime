using System.Reflection;
using System.Text;
using HarmonyLib;
using ItsStardewTime.Framework;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;

namespace ItsStardewTime.Patches.TimeDisplayPatches
{
    internal static class DayTimeMoneyBoxPatches
    {
        internal static void Initialize(in Harmony harmony)
        {
            harmony.Patch
            (
                original: AccessTools.Method
                (
                    typeof(DayTimeMoneyBox),
                    nameof(DayTimeMoneyBox.draw),
                    new[] { typeof(SpriteBatch) }
                ),
                postfix: new HarmonyMethod(typeof(DayTimeMoneyBoxPatches), nameof(Draw_Postfix))
            );
        }

        internal static void Draw_Postfix(DayTimeMoneyBox __instance, SpriteBatch b)
        {
            try
            {
                // Get source rect via reflection, do some validation of the object before proceeding with
                // the patch. Most of the logic relies on this existing. It SHOULD always exist
                var source_rect_field = __instance.GetType().
                    GetField
                    (
                        "sourceRect",
                        BindingFlags.NonPublic | BindingFlags.Instance
                    );

                Rectangle source_rect = (Rectangle)
                (
                    source_rect_field?.GetValue(__instance) ??
                    throw new InvalidOperationException("Source Rectangle is on Time Display box is Null!")
                );

                SpriteFont font =
                    LocalizedContentManager.CurrentLanguageCode ==
                    LocalizedContentManager.LanguageCode.ko
                        ? Game1.smallFont
                        : Game1.dialogueFont;

                // Draw the display box over the original, this won't affect the quest display or money box.
                // We're letting the original method run to determine value and display quests/money,
                // but we want to overwrite the time display. This means we have to re-render the time, date,
                // weather, and season portions of the box.
                // But oh isn't it a waste to redraw the box, you ask? O(1) = O(2) in the grand scheme of things.
                b.Draw
                (
                    Game1.mouseCursors,
                    __instance.position,
                    // This is the rectangle position of the display box in its texture sheet (somebody fact check)
                    source_rect,
                    Color.White,
                    0.0f,
                    Vector2.Zero,
                    4f,
                    SpriteEffects.None,
                    0.9f
                );

                // Individually call each of the methods that handle the different parts of the display box.
                HandleDateText(__instance, b, font, in source_rect);
                HandleClock(__instance, b, in font, in source_rect);
                HandleSeasonAndWeather(__instance, b);
                HandleTimeDial(__instance, b);
                HandlePauseX(__instance, b);

            }
            catch (Exception ex)
            {
                TimeController.Monitor.Log($"Failed to run patch:\n{ex}", LogLevel.Error);
            }
        }

        private static void HandleClock
        (
            DayTimeMoneyBox instance,
            SpriteBatch b,
            in SpriteFont font,
            in Rectangle sourceRect
        )
        {

            // N.B. The original game code does not use the timeOfDayString function to calculate the time string
            // for the display box. 
            // It instead uses a separate set of logic to essentially do the same thing. Since we already patch this
            // function with our own logic, we're just reusing it here to get the same string and avoid duplication.
            // If things start misbehaving with the reported time, we may need to revisit this.
            string time_of_day_string = Game1.getTimeOfDayString(TimeMaster.TimeSpeed.TimeHelper.TimeInMinutes);
            Vector2 timestring_dimensions = Game1.dialogueFont.MeasureString(time_of_day_string);
            Vector2 vector2_4 = new Vector2
            (
                (float)
                (
                    sourceRect.X *
                    0.550000011920929 -
                    timestring_dimensions.X /
                    2.0 +
                    (instance.timeShakeTimer > 0 ? Game1.random.Next(-2, 3) : 0.0)
                ),
                (float)
                (
                    sourceRect.Y * 
                    // N.B. The original mode used a ternary to determine this number, but both values were identical
                    0.3100000023841858 -
                    timestring_dimensions.Y /
                    2.0 +
                    (instance.timeShakeTimer > 0 ? Game1.random.Next(-2, 3) : 0.0)
                )
            );

            // This pulses the time of day text color when paused
            // The original used a weird flag to change the color but we just use
            // the float directly until it's determined that was necessary.
            var color_mod = 1f;
            if (TimeMaster.TimeSpeed.IsTimeFrozen || !Game1.shouldTimePass() && !Game1.fadeToBlack)
            {
                if (Game1.currentGameTime.TotalGameTime.TotalMilliseconds % 2000.0 > 1000.0)
                {
                    color_mod = 0.5f;
                }
            }

            Utility.drawTextWithShadow
            (
                b,
                time_of_day_string,
                font,
                instance.position + vector2_4,
                Game1.timeOfDay >= 2400 ? Color.Red : Game1.textColor * color_mod
            );
        }

        /// <summary>
        /// Code from the original Time Master mod to draw an X over the clock when paused.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="b"></param>
        private static void HandlePauseX(DayTimeMoneyBox instance, SpriteBatch b)
        {
            if (TimeController.Config.ShowPauseX && TimeMaster.TimeSpeed.IsTimeFrozen)
            {
                b.Draw
                (
                    Game1.mouseCursors,
                    instance.position + new Vector2(23f, 55f),
                    new Rectangle(269, 471, 15, 15),
                    new Color(0, 0, 0, 64),
                    0f,
                    Vector2.Zero,
                    4f,
                    SpriteEffects.None,
                    0.9f
                );
            }
        }

        /// <summary>
        /// Copy of the original logic to draw the date text on the display box.
        /// Original text is overwritten when we redraw the background texture, but the date text is already calculated.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="b"></param>
        /// <param name="font"></param>
        /// <param name="sourceRect"></param>
        private static void HandleDateText
        (
            DayTimeMoneyBox instance,
            SpriteBatch b,
            in SpriteFont font,
            in Rectangle sourceRect
        )
        {
            // Grab the original date text calculated by the game, we don't need to recalculate it.
            var date_text = instance.
                GetType().
                GetField("_dateText", BindingFlags.NonPublic | BindingFlags.Instance)?.
                GetValue(instance) as StringBuilder;
            // These are values as calculated by the original game code, we're just reusing them.
            // to make sure our text is redrawn in the correct location.
            Vector2 vector2_1 = font.MeasureString(date_text);
            Vector2 vector2_2 = new Vector2
            (
                (float)(sourceRect.X * (9.0 / 16.0) - vector2_1.X / 2.0),
                // N.B. The original mode used a ternary to determine this number, but both values were identical
                (float)(sourceRect.Y * 0.10000000149011612 - vector2_1.Y / 2.0)
            );
            Utility.drawTextWithShadow
            (
                b,
                date_text,
                font,
                instance.position + vector2_2,
                Game1.textColor
            );
        }

        private static void HandleSeasonAndWeather
        (
            DayTimeMoneyBox instance,
            SpriteBatch b
        )
        {
            Texture2D weather_texture_2d = Game1.mouseCursors;
            Rectangle weather_rect;

            b.Draw
            (
                weather_texture_2d,
                instance.position + new Vector2(212f, 68f),
                new Rectangle(406, 441 + Game1.seasonIndex * 8, 12, 8),
                Color.White,
                0.0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.9f
            );

            if (Game1.weatherIcon == 999)
            {
                weather_texture_2d = Game1.mouseCursors_1_6;
                weather_rect = new Rectangle(243, 293, 12, 8);
            }
            else
            {
                weather_texture_2d = Game1.mouseCursors;
                weather_rect = new Rectangle(317 + 12 * Game1.weatherIcon, 421, 12, 8);
            }

            b.Draw
            (
                weather_texture_2d,
                instance.position + new Vector2(116f, 68f),
                weather_rect,
                Color.White,
                0.0f,
                Vector2.Zero,
                4f,
                SpriteEffects.None,
                0.9f
            );
        }

        /// <summary>
        /// Draws the "dial" or clock hand rotates up on the day display indicating how much of the day has passed.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="b"></param>
        private static void HandleTimeDial
        (
            DayTimeMoneyBox instance,
            SpriteBatch b
        )
        {
            var time_of_day = Game1.timeOfDay;
            int adjusted_time = (int)
            (
                time_of_day - time_of_day % 100 + time_of_day % 100 / 10 * 16.66f
            );

            // This math was taken from the original code, I've never looked into it.
            float rotation = (float)
            (
                Math.PI +
                Math.Min
                (
                    Math.PI,
                    (
                        adjusted_time +
                        Game1.gameTimeInterval /
                        (double)Game1.realMilliSecondsPerGameTenMinutes *
                        16.600000381469727 -
                        600.0
                    ) /
                    2000.0 *
                    Math.PI
                )
            );
            b.Draw
            (
                Game1.mouseCursors,
                instance.position + new Vector2(88f, 88f),
                new Rectangle(324, 477, 7, 19),
                Color.White,
                rotation,
                new Vector2(3f, 17f),
                4f,
                SpriteEffects.None,
                0.9f
            );
        }
    }
}