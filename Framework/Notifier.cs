using StardewValley;

namespace ItsStardewTime.Framework
{
    /// <summary>Displays messages to the user in-game.</summary>
    internal class Notifier
    {
        /*********
        ** Public methods
        *********/
        /// <summary>Display a message for one second.</summary>
        /// <param name="message">The message to display.</param>
        public static void QuickNotify(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type) { timeLeft = 1000 });
        }

        /// <summary>Display a message for two seconds.</summary>
        /// <param name="message">The message to display.</param>
        public static void ShortNotify(string message)
        {
            Game1.addHUDMessage(new HUDMessage(message, HUDMessage.newQuest_type) { timeLeft = 2000 });
        }

        /// <summary>Display a message for ten seconds in the chat box.</summary>
        /// <param name="message">The message to display.</param>
        public static void NotifyInChatBox(string message)
        {
            Game1.chatBox.addInfoMessage(message);
        }

        /// <summary>Display an error message for ten seconds in the chat box.</summary>
        /// <param name="message">The message to display.</param>
        public static void NotifyErrorInChatBox(string message)
        {
            Game1.chatBox.addErrorMessage(message);
        }
    }
}
