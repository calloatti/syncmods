using HarmonyLib;
using System;
using System.Reflection;
using Timberborn.CoreUI;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.Localization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

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
                syncButton.text = LocHelper.T("calloatti.syncmods.ButtonSync");

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

                            var SaveName = selectedSave.SaveName;
                            var SettlementName = selectedSave.SettlementReference.SettlementName;

                            // This works
                            var extraArgs = $" -skipModManager -settlementName \"{SettlementName}\" -saveName \"{SaveName}\"";

                            // This crashes
                            //var extraArgs = $" -settlementName \"{SettlementName}\" -saveName \"{SaveName}\"";

                            
                            // --- DIALOG CONFIGURATION ---
                            var builder = dialogBoxShower?.Create()
                                .SetMessage(LocHelper.T("calloatti.syncmods.SyncDialogboxText"))

                               // 1. CANCEL - Red/Standard Cancel Button
                               .SetCancelButton(() => { }, LocHelper.T("calloatti.syncmods.ButtonCancel")) // Closes the dialog silently

                                // 2. RESTART + LOAD - Blue Info Button
                                .SetInfoButton(() => { GameRestarter.Restart(extraArgs); }, LocHelper.T("calloatti.syncmods.ButtonRestartLoad"))

                                // 3. RESTART (Safe) - Green Confirm Button
                                .SetConfirmButton(() => { GameRestarter.Restart(""); }, LocHelper.T("calloatti.syncmods.ButtonRestart"));

                            // SHOW THE DIALOG
                            builder.Show();

                        }
                    }
                });

                VisualElement container = loadButton.parent;
                container.Insert(container.IndexOf(loadButton), syncButton);
            }

            // ---------------------------------------------------------
            // ENABLE/DISABLE LOGIC
            // ---------------------------------------------------------
            if (root != null)
            {
                Button existingSyncButton = root.Q<Button>(SyncButtonName);

                if (existingSyncButton != null)
                {
                    //Log.Info($" SceneManager.GetActiveScene().name: {SceneManager.GetActiveScene().name}");

                    bool isMainMenu = string.Equals(SceneManager.GetActiveScene().name, "1-MainMenuScene", StringComparison.OrdinalIgnoreCase);

                    existingSyncButton.SetEnabled(isMainMenu);

                }
            }
        }
    }
}