using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace ItsStardewTime.Framework
{
    internal class FancyModIntegration
    {
        internal static bool TryGetModEntry(IModInfo modInfo, IMonitor monitor, [NotNullWhen(returnValue: true)] out object? modEntry)
        {
            modEntry = null;
            Type iModMetadataType = AccessTools.TypeByName("StardewModdingAPI.Framework.IModMetadata");
            if (modInfo.GetType().IsAssignableFrom(iModMetadataType))
            {
                monitor.Log($"Failed to get ModEntry for {modInfo.Manifest.Name}. Could not find IModMetadata.", LogLevel.Error);
                return false;
            }

            if (AccessTools.Property(iModMetadataType, "Mod") is not PropertyInfo modPropertyInfo
                || modPropertyInfo.GetValue(modInfo) is not object mod)
            {
                monitor.Log($"Failed to get ModEntry for {modInfo.Manifest.Name}. Could not find its IMod.", LogLevel.Error);
                return false;
            }

            modEntry = mod;
            return true;
        }

        internal static int RemoveModEventHandlers(IModHelper helper, IModInfo modInfo, IMonitor monitor)
        {
            int numRemoved = 0;
            Type modEventsBaseType = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ModEventsBase");
            Type managedEventType = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ManagedEvent`1");

            foreach (PropertyInfo propertyInfo in helper.Events.GetType().GetProperties())
            {
                if (propertyInfo.GetValue(helper.Events) is not object modEventsBase || !modEventsBase.GetType().IsAssignableTo(modEventsBaseType))
                {
                    monitor.Log($"Unable to get ModEventsBase for {propertyInfo.Name}.", LogLevel.Error);
                    continue;
                }

                if (AccessTools.Field(modEventsBase.GetType(), "EventManager") is not FieldInfo eventManagerField
                    || eventManagerField.GetValue(modEventsBase) is not object eventManager)
                {
                    monitor.Log($"Unable to get EventManager for {propertyInfo.Name}.", LogLevel.Error);
                    continue;
                }

                foreach (FieldInfo managedEventFieldInfo in eventManager.GetType().GetFields())
                {
                    if (managedEventFieldInfo.GetValue(eventManager) is not object managedEvent
                        || !managedEvent.GetType().IsGenericType
                        || managedEvent.GetType().GetGenericTypeDefinition() != managedEventType)
                    {
                        monitor.Log($"Unable to get ManagedEvent<> for {managedEventFieldInfo.Name}.", LogLevel.Error);
                        continue;
                    }

                    if (AccessTools.Field(managedEvent.GetType(), "Handlers") is not FieldInfo handlersField
                        || handlersField.GetValue(managedEvent) is not IList handlers)
                    {
                        monitor.Log($"Unable to get IList handlers for {managedEventFieldInfo.Name}.", LogLevel.Error);
                        continue;
                    }

                    ArrayList toBeRemoved = new();
                    foreach (var handler in handlers)
                    {
                        if (AccessTools.Property(handler.GetType(), "SourceMod") is not PropertyInfo modMetadataProperty
                            || modMetadataProperty.GetValue(handler) is not IModInfo modMetadata)
                        {
                            monitor.Log($"Unable to get IModMetadata associated with handler in {managedEventFieldInfo.Name}.", LogLevel.Error);
                            continue;
                        }

                        if (modInfo.Manifest.UniqueID != modMetadata.Manifest.UniqueID)
                        {
                            continue;
                        }

                        if (AccessTools.Property(handler.GetType(), "Handler") is not PropertyInfo handlerMethodProperty
                            || handlerMethodProperty.GetValue(handler) is not object handlerMethod)
                        {
                            monitor.Log($"Unable to get EventHandler<> associated with handler in {managedEventFieldInfo.Name}.", LogLevel.Error);
                            continue;
                        }

                        toBeRemoved.Add(handlerMethod);
                    }

                    if (AccessTools.Method(managedEvent.GetType(), "Remove") is not MethodInfo removeMethod)
                    {
                        monitor.Log($"Unable to get Remove method for {managedEventFieldInfo.Name}.", LogLevel.Error);
                        continue;
                    }

                    foreach (var handler in toBeRemoved)
                    {
                        removeMethod.Invoke(managedEvent, new[] { handler });
                    }

                    numRemoved += toBeRemoved.Count;
                }
            }

            return numRemoved;
        }

        internal static int RemoveModEventHandler(string typeName, string methodName, object modEventsBase, string eventName, IMonitor monitor)
        {
            Type modEventsBaseType = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ModEventsBase");
            Type managedEventType = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ManagedEvent`1");

            if (AccessTools.TypeByName(typeName) is not Type type || AccessTools.Method(type, methodName) is not MethodInfo methodInfo)
            {
                monitor.Log($"Failed to lookup type info for method ({typeName}.{methodName}).", LogLevel.Error);
                return 0;
            }

            if (!modEventsBase.GetType().IsAssignableTo(modEventsBaseType))
            {
                monitor.Log($"Unable to get ModEventsBase for {eventName}.", LogLevel.Error);
                return 0;
            }

            if (AccessTools.Field(modEventsBase.GetType(), "EventManager") is not FieldInfo eventManagerField
                || eventManagerField.GetValue(modEventsBase) is not object eventManager)
            {
                monitor.Log($"Unable to get EventManager for {eventName}.", LogLevel.Error);
                return 0;
            }

            if (AccessTools.Field(eventManager.GetType(), eventName) is not FieldInfo managedEventFieldInfo)
            {
                monitor.Log($"Unable to find field for '{eventName}' event.", LogLevel.Error);
                return 0;
            }

            if (managedEventFieldInfo.GetValue(eventManager) is not object managedEvent
                || !managedEvent.GetType().IsGenericType
                || managedEvent.GetType().GetGenericTypeDefinition() != managedEventType)
            {
                monitor.Log($"Unable to get ManagedEvent<> for {managedEventFieldInfo.Name}.", LogLevel.Error);
                return 0;
            }

            if (AccessTools.Field(managedEvent.GetType(), "Handlers") is not FieldInfo handlersField
                || handlersField.GetValue(managedEvent) is not IList handlers)
            {
                monitor.Log($"Unable to get IList handlers for {managedEventFieldInfo.Name}.", LogLevel.Error);
                return 0;
            }

            ArrayList toBeRemoved = new();
            foreach (var handler in handlers)
            {
                if (AccessTools.Property(handler.GetType(), "Handler") is not PropertyInfo handlerMethodProperty
                    || handlerMethodProperty.GetValue(handler) is not Delegate handlerMethod)
                {
                    monitor.Log($"Unable to get EventHandler<> associated with handler ({handler}) for {eventName}.", LogLevel.Error);
                    continue;
                }

                if (handlerMethod.Method.Equals(methodInfo))
                {
                    toBeRemoved.Add(handlerMethod);
                }
            }

            if (AccessTools.Method(managedEvent.GetType(), "Remove") is not MethodInfo removeMethod)
            {
                monitor.Log($"Unable to get Remove method for {eventName}.", LogLevel.Error);
                return 0;
            }

            foreach (var handler in toBeRemoved)
            {
                removeMethod.Invoke(managedEvent, new[] { handler });
            }

            if (toBeRemoved.Count == 0)
            {
                monitor.Log($"Failed to find event handler {typeName}.{methodName} in handlers ({handlers}).", LogLevel.Error);
            }

            return toBeRemoved.Count;
        }
    }
}
