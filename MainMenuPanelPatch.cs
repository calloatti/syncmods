using HarmonyLib;
using UnityEngine;
using UnityEngine.UIElements;
using Timberborn.MainMenuPanels;
using Timberborn.Localization;
using System;
using System.Reflection;
using System.Linq;

namespace Calloatti.SyncMods
{
    [HarmonyPatch(typeof(MainMenuPanel), "GetPanel")]
    public static class MainMenuPanelPatch
    {
        private static void Postfix(MainMenuPanel __instance, VisualElement __result)
        {
            if (__result == null) return;

            // Prevent duplicates
            if (__result.Q("RestartGameButton") != null) return;

            // 1. Find the reference button ("ExitButton" confirmed from logs)
            VisualElement exitButton = __result.Q("ExitButton");
            if (exitButton == null)
            {
                Log.Info($"SyncMods: Could not find 'ExitButton'.");
                return;
            }

            // 2. Create the clone
            Button restartButton = (Button)Activator.CreateInstance(exitButton.GetType());
            restartButton.name = "RestartGameButton";

            // Localization

            restartButton.text = LocHelper.T("calloatti.syncmods.ButtonRestart");

            // 3. Copy Styles
            int sheetCount = exitButton.styleSheets.count;
            for (int i = 0; i < sheetCount; i++)
            {
                restartButton.styleSheets.Add(exitButton.styleSheets[i]);
            }

            foreach (var className in exitButton.GetClasses())
            {
                restartButton.AddToClassList(className);
            }

            // Copy Base Layout
            restartButton.style.width = exitButton.style.width;
            restartButton.style.height = exitButton.style.height;

            // 4. Click Event
            restartButton.RegisterCallback<ClickEvent>(evt => {
                Log.Info($"SyncMods: Restarting...");
                GameRestarter.Restart("");
            });

            // 5. Inject and Compact
            VisualElement container = exitButton.parent;
            if (container != null)
            {
                // Insert above Exit
                int exitIndex = container.IndexOf(exitButton);
                container.Insert(exitIndex, restartButton);

                // --- SPACING ADJUSTMENT ---
                // To fit the new button without expanding the menu too much, 
                // we iterate all children and reduce their vertical margins.
                var menuItems = container.Children().ToList();

                foreach (var element in menuItems)
                {
                    // Only apply to Buttons to avoid squashing labels or dividers if they exist
                    if (element is Button)
                    {
                        // Set margins to 5px (Standard is often 10-15px)
                        // This "subtracts" the added height by reclaiming space from the gaps.
                        element.style.marginBottom = new Length(-1, LengthUnit.Pixel);
                        element.style.marginTop = new Length(-1, LengthUnit.Pixel);
                    }
                }

                Log.Info($" Inserted button and compacted spacing for {menuItems.Count} items.");
            }
            else
            {
                Log.Info($" Container is null, cannot insert button.");
            }
        }
    }
}