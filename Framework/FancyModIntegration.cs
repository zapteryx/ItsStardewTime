using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace ItsStardewTime.Framework
{
    internal class FancyModIntegration
    {
        internal static bool TryGetModEntry
        (
            IModInfo modInfo,
            IMonitor monitor,
            [NotNullWhen(returnValue: true)] out object? modEntry
        )
        {
            modEntry = null;
            Type i_mod_metadata_type = AccessTools.TypeByName("StardewModdingAPI.Framework.IModMetadata");
            if (modInfo.GetType().IsAssignableFrom(i_mod_metadata_type))
            {
                monitor.Log
                (
                    $"Failed to get ModEntry for {modInfo.Manifest.Name}. Could not find IModMetadata.",
                    LogLevel.Error
                );
                return false;
            }

            if (AccessTools.Property(i_mod_metadata_type, "Mod") is not PropertyInfo mod_property_info ||
                mod_property_info.GetValue(modInfo) is not object mod)
            {
                monitor.Log
                    ($"Failed to get ModEntry for {modInfo.Manifest.Name}. Could not find its IMod.", LogLevel.Error);
                return false;
            }

            modEntry = mod;
            return true;
        }

        internal static int RemoveModEventHandlers
        (
            IModHelper helper,
            IModInfo modInfo,
            IMonitor monitor
        )
        {
            int num_removed = 0;
            Type mod_events_base_type = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ModEventsBase");
            Type managed_event_type = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ManagedEvent`1");

            foreach (PropertyInfo property_info in helper.Events.GetType().GetProperties())
            {
                if (property_info.GetValue(helper.Events) is not object mod_events_base ||
                    !mod_events_base.GetType().IsAssignableTo(mod_events_base_type))
                {
                    monitor.Log($"Unable to get ModEventsBase for {property_info.Name}.", LogLevel.Error);
                    continue;
                }

                if (AccessTools.Field(mod_events_base.GetType(), "EventManager") is not FieldInfo event_manager_field ||
                    event_manager_field.GetValue(mod_events_base) is not object event_manager)
                {
                    monitor.Log($"Unable to get EventManager for {property_info.Name}.", LogLevel.Error);
                    continue;
                }

                foreach (FieldInfo managed_event_field_info in event_manager.GetType().GetFields())
                {
                    if (managed_event_field_info.GetValue(event_manager) is not object managed_event ||
                        !managed_event.GetType().IsGenericType ||
                        managed_event.GetType().GetGenericTypeDefinition() != managed_event_type)
                    {
                        monitor.Log($"Unable to get ManagedEvent<> for {managed_event_field_info.Name}.", LogLevel.Error);
                        continue;
                    }

                    if (AccessTools.Field(managed_event.GetType(), "Handlers") is not FieldInfo handlers_field ||
                        handlers_field.GetValue(managed_event) is not IList handlers)
                    {
                        monitor.Log($"Unable to get IList handlers for {managed_event_field_info.Name}.", LogLevel.Error);
                        continue;
                    }

                    ArrayList to_be_removed = new();
                    foreach (var handler in handlers)
                    {
                        if (AccessTools.Property
                                (handler.GetType(), "SourceMod") is not PropertyInfo mod_metadata_property ||
                            mod_metadata_property.GetValue(handler) is not IModInfo mod_metadata)
                        {
                            monitor.Log
                            (
                                $"Unable to get IModMetadata associated with handler in {managed_event_field_info.Name}.",
                                LogLevel.Error
                            );
                            continue;
                        }

                        if (modInfo.Manifest.UniqueID != mod_metadata.Manifest.UniqueID)
                        {
                            continue;
                        }

                        if (AccessTools.Property
                                (handler.GetType(), "Handler") is not PropertyInfo handler_method_property ||
                            handler_method_property.GetValue(handler) is not object handler_method)
                        {
                            monitor.Log
                            (
                                $"Unable to get EventHandler<> associated with handler in {managed_event_field_info.Name}.",
                                LogLevel.Error
                            );
                            continue;
                        }

                        to_be_removed.Add(handler_method);
                    }

                    if (AccessTools.Method(managed_event.GetType(), "Remove") is not MethodInfo remove_method)
                    {
                        monitor.Log($"Unable to get Remove method for {managed_event_field_info.Name}.", LogLevel.Error);
                        continue;
                    }

                    foreach (var handler in to_be_removed)
                    {
                        remove_method.Invoke(managed_event, new[] { handler });
                    }

                    num_removed += to_be_removed.Count;
                }
            }

            return num_removed;
        }

        internal static int RemoveModEventHandler
        (
            string typeName,
            string methodName,
            object modEventsBase,
            string eventName,
            IMonitor monitor
        )
        {
            Type mod_events_base_type = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ModEventsBase");
            Type managed_event_type = AccessTools.TypeByName("StardewModdingAPI.Framework.Events.ManagedEvent`1");

            if (AccessTools.TypeByName(typeName) is not Type type ||
                AccessTools.Method(type, methodName) is not MethodInfo method_info)
            {
                monitor.Log($"Failed to lookup type info for method ({typeName}.{methodName}).", LogLevel.Error);
                return 0;
            }

            if (!modEventsBase.GetType().IsAssignableTo(mod_events_base_type))
            {
                monitor.Log($"Unable to get ModEventsBase for {eventName}.", LogLevel.Error);
                return 0;
            }

            if (AccessTools.Field(modEventsBase.GetType(), "EventManager") is not FieldInfo event_manager_field ||
                event_manager_field.GetValue(modEventsBase) is not object event_manager)
            {
                monitor.Log($"Unable to get EventManager for {eventName}.", LogLevel.Error);
                return 0;
            }

            if (AccessTools.Field(event_manager.GetType(), eventName) is not FieldInfo managed_event_field_info)
            {
                monitor.Log($"Unable to find field for '{eventName}' event.", LogLevel.Error);
                return 0;
            }

            if (managed_event_field_info.GetValue(event_manager) is not object managed_event ||
                !managed_event.GetType().IsGenericType ||
                managed_event.GetType().GetGenericTypeDefinition() != managed_event_type)
            {
                monitor.Log($"Unable to get ManagedEvent<> for {managed_event_field_info.Name}.", LogLevel.Error);
                return 0;
            }

            if (AccessTools.Field(managed_event.GetType(), "Handlers") is not FieldInfo handlers_field ||
                handlers_field.GetValue(managed_event) is not IList handlers)
            {
                monitor.Log($"Unable to get IList handlers for {managed_event_field_info.Name}.", LogLevel.Error);
                return 0;
            }

            ArrayList to_be_removed = new();
            foreach (var handler in handlers)
            {
                if (AccessTools.Property(handler.GetType(), "Handler") is not PropertyInfo handler_method_property ||
                    handler_method_property.GetValue(handler) is not Delegate handler_method)
                {
                    monitor.Log
                    (
                        $"Unable to get EventHandler<> associated with handler ({handler}) for {eventName}.",
                        LogLevel.Error
                    );
                    continue;
                }

                if (handler_method.Method.Equals(method_info))
                {
                    to_be_removed.Add(handler_method);
                }
            }

            if (AccessTools.Method(managed_event.GetType(), "Remove") is not MethodInfo remove_method)
            {
                monitor.Log($"Unable to get Remove method for {eventName}.", LogLevel.Error);
                return 0;
            }

            foreach (var handler in to_be_removed)
            {
                remove_method.Invoke(managed_event, new[] { handler });
            }

            if (to_be_removed.Count == 0)
            {
                monitor.Log
                    ($"Failed to find event handler {typeName}.{methodName} in handlers ({handlers}).", LogLevel.Error);
            }

            return to_be_removed.Count;
        }
    }
}