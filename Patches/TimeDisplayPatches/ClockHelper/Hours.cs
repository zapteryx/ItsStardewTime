using ItsStardewTime.Framework;
using StardewValley;

namespace ItsStardewTime.Patches.ClockHelper;

public class Hours
{
    public static void Handle(int time, ref string timeString)
    {
        if (!TimeController.Config.Use24HourFormat) return;
        // No hours needed for french
        if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.fr)
        {
            return;
        }
        
        // Hours as seen on a 24 hour clock
        int hours = time / 100 % 24;
        // Hours as seen on a 12 hour clock
        int hours_12hour = hours % 12;

        bool has_leading_zero = true;
        switch (LocalizedContentManager.CurrentLanguageCode)
        {
            
            case LocalizedContentManager.LanguageCode.ja:
            case LocalizedContentManager.LanguageCode.zh:
                has_leading_zero = false;
                break;
            case LocalizedContentManager.LanguageCode.ru:
            case LocalizedContentManager.LanguageCode.pt:
            case LocalizedContentManager.LanguageCode.es:
            case LocalizedContentManager.LanguageCode.de:
            case LocalizedContentManager.LanguageCode.th:
            case LocalizedContentManager.LanguageCode.tr:
            case LocalizedContentManager.LanguageCode.hu:
            default:
                break;
        }

        // Determine if the 12-hour formatted hours are less than 10, excluding 0 (midnight and noon)
        // We are removing the original 12-hour hours digits from the string, and replacing them with
        // the 24-hour hours digits.
        var is_single_digit = (!has_leading_zero) && hours_12hour < 10 && hours_12hour != 0;

        // Find the start index of the hours portion of the time string
        // Subtract 2 from the position of the colon, or 1 if it's a single digit hour
        var start_index = timeString.IndexOf(':') - 2;
        if (is_single_digit)
        {
            start_index++;
        }

        has_leading_zero = has_leading_zero && is_single_digit;
        // Remove the original hours digits and insert the new ones
        timeString = timeString.Remove(start_index, is_single_digit ? 1 : 2);
        timeString = timeString.Insert(start_index, $"""{(has_leading_zero ? "0": "")}{hours}""");
    }
}