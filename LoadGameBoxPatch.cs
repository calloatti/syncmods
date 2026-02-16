using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using System.Reflection;
using Timberborn.Localization;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.CoreUI;
using System;
using System.IO;

namespace Calloatti.SyncMods
{
    [HarmonyPatch]
    public static class LoadGameBoxPatch
    {
        private const string SyncButtonName = "SyncButtonPermanent";

        [HarmonyPatch("Timberborn.GameSaveRepositorySystemUI.LoadGameBox", "Open")]
        [HarmonyPostfix]
        public static void OpenPostfix(object __instance)
        {
            var getPanelMethod = __instance.GetType().GetMethod("GetPanel", BindingFlags.Public | BindingFlags.Instance);
            VisualElement root = (VisualElement)getPanelMethod?.Invoke(__instance, null);

            if (root != null && root.Q(SyncButtonName) == null)
            {
                Button loadButton = root.Q<Button>("LoadButton");
                if (loadButton == null) return;

                var dialogShowerField = __instance.GetType().GetField("_dialogBoxShower", BindingFlags.NonPublic | BindingFlags.Instance);
                DialogBoxShower dialogBoxShower = (DialogBoxShower)dialogShowerField?.GetValue(__instance);

                Button syncButton = (Button)Activator.CreateInstance(loadButton.GetType());
                syncButton.name = SyncButtonName;
                syncButton.text = "Sync Mods";

                foreach (var className in loadButton.GetClasses()) syncButton.AddToClassList(className);
                syncButton.style.width = loadButton.style.width;
                syncButton.style.height = loadButton.style.height;
                //syncButton.style.marginRight = 10;

                syncButton.RegisterCallback<ClickEvent>(evt =>
                {
                    FieldInfo saveListField = __instance.GetType().GetField("_saveList", BindingFlags.NonPublic | BindingFlags.Instance);
                    object saveList = saveListField?.GetValue(__instance);

                    if (saveList != null)
                    {
                        MethodInfo tryGetMethod = saveList.GetType().GetMethod("TryGetSelectedSave");
                        object[] args = new object[] { null };
                        bool found = (bool)tryGetMethod.Invoke(saveList, args);

                        if (found && args[0] != null)
                        {
                            object gameSaveItem = args[0];
                            PropertyInfo saveRefProp = gameSaveItem.GetType().GetProperty("SaveReference");
                            SaveReference selectedSave = (SaveReference)saveRefProp.GetValue(gameSaveItem);

                            if (SyncModsinternal.Instance != null)
                            {
                                SyncModsinternal.Instance.SyncFromInternalMetadata(selectedSave);
                            }

                            // --- DIALOG CONFIGURATION ---
                            var builder = dialogBoxShower?.Create()
                                .SetMessage("Mods synced successfully from internal save data.")

                                // 1. RESTART (Safe) - Green Confirm Button
                                .SetConfirmButton(() => {GameRestarter.Restart("");}, "Restart")

                                // 2. CANCEL - Red/Standard Cancel Button
                                .SetCancelButton(() => {
                                    UnityEngine.Debug.Log("[SyncMods] User chose to stay on Main Menu.");
                                }, "Cancel")

                                // 3. RESTART + LOAD - Blue Info Button (To be Disabled)
                                .SetInfoButton(() => { GameRestarter.Restart("");}, "Restart + Load");

                            // SHOW THE DIALOG
                            builder.Show();

                            // --- THE FIX: NATIVE DISABLE ---
                            // We search the UI tree for the button we just created and strictly disable it.
                            try
                            {
                                // Access the main UI panel (root of the screen)
                                var visualRoot = root.panel?.visualTree;
                                if (visualRoot != null)
                                {
                                    // Find the button by its text content
                                    visualRoot.Query<Button>().ForEach(btn =>
                                    {
                                        if (btn.text == "Restart + Load")
                                        {
                                            // This applies the native Unity/Timberborn disabled state (grayed out + unclickable)
                                            //btn.SetEnabled(false);
                                        }
                                    });
                                }
                            }
                            catch (Exception ex)
                            {
                                UnityEngine.Debug.LogWarning($"[SyncMods] Failed to set button state: {ex.Message}");
                            }
                        }
                    }
                });

                VisualElement container = loadButton.parent;
                container.Insert(container.IndexOf(loadButton), syncButton);
            }
        }
    }
}