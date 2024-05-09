using ItsStardewTime.Framework;
using StardewValley;

namespace ItsStardewTime.Patches.ClockHelper;

public static class Minutes
{
    public static void Handle(int time, ref string timeString)
    {
        // Handle minutes display
        if (!TimeController.Config.DisplayMinutes) return;
        
        // Check the time, if it's less than 10 on the hour, we need to add a 0 to the front
        // This just handles the first 10 minutes which read (01, 02, 03...) 
        // exclude 0 (the start of the hour, 00).
        // e.g. without this logic the original game code would display 7:05am as 7:5am 
        var time_mod = time % 100;
        if (time_mod < 10 && time_mod != 0)
        {
            if (LocalizedContentManager.CurrentLanguageCode == LocalizedContentManager.LanguageCode.fr)
            {
                // Insert a 0 after the h
                timeString = timeString.Insert(timeString.Length - 1, "0");
                return;
            }
            int colon_position = timeString.IndexOf(':');
            // Add a 0 after the colon 
            if (colon_position != -1 && colon_position < (timeString.Length - 1))
            {
                timeString = timeString.Insert(colon_position + 1, "0");
            }
        }
    }
}