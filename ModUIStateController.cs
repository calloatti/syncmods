using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Timberborn.Modding;
using Timberborn.ModdingUI; //
using UnityEngine;
using UnityEngine.UIElements;

namespace Calloatti.SyncMods
{
    public static class ModUIStateController
    {
        public static ModListView CurrentListView { get; private set; }

        private static readonly FieldInfo ModItemsField = AccessTools.Field(typeof(ModListView), "_modItems");
        private static readonly FieldInfo ScrollViewField = AccessTools.Field(typeof(ModListView), "_scrollView");
        private static readonly FieldInfo WarningUpdaterField = AccessTools.Field(typeof(ModListView), "_modWarningUpdater");
        private static readonly FieldInfo ListChangedField = AccessTools.Field(typeof(ModListView), "ListChanged");
        private static readonly MethodInfo BuildListMethod = AccessTools.Method(typeof(ModListView), "BuildList", new[] { typeof(IEnumerable<Mod>) });

        [HarmonyPatch(typeof(ModListView), "Initialize")]
        static class CaptureInstancePatch
        {
            static void Postfix(ModListView __instance) => CurrentListView = __instance;
        }

        public static void Refresh(IEnumerable<Mod> mods)
        {
            if (CurrentListView == null) return;

            var scrollView = (ScrollView)ScrollViewField.GetValue(CurrentListView);
            var modItems = (IDictionary)ModItemsField.GetValue(CurrentListView);
            var warningUpdater = WarningUpdaterField.GetValue(CurrentListView);

            if (scrollView == null || modItems == null || warningUpdater == null) return;

            // Clear internal tracking and visual elements to prevent duplication
            modItems.Clear();
            scrollView.Clear();

            // Build the list in the exact order provided in the 'mods' parameter
            // This reads the Enabled/Disabled toggle state from prefs
            BuildListMethod.Invoke(CurrentListView, new object[] { mods });

            // Manually trigger mod-specific warnings (red icons/text)
            var updateMethod = AccessTools.Method(warningUpdater.GetType(), "Update");
            updateMethod.Invoke(warningUpdater, new object[] { modItems });

            // Signal the 'ListChanged' event to show the "Restart Required" warning at the bottom
            var eventDelegate = (MulticastDelegate)ListChangedField.GetValue(CurrentListView);
            if (eventDelegate != null)
            {
                foreach (var handler in eventDelegate.GetInvocationList())
                {
                    handler.Method.Invoke(handler.Target, new object[] { CurrentListView, EventArgs.Empty });
                }
            }
        }
    }
}