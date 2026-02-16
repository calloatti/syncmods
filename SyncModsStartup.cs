using HarmonyLib;
using System;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.SyncMods
{
    public class SyncModsStartup : IModStarter
    {
        private const string HarmonyId = "calloatti.SyncMods";

        public void StartMod(IModEnvironment environment)
        {
            LogInitialization();
            ApplyHarmonyPatches();
        }

        private void ApplyHarmonyPatches()
        {
            var harmonyInstance = new Harmony(HarmonyId);
            try
            {
                harmonyInstance.PatchAll();
                Debug.Log($"[SyncMods] Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Debug.Log($"[SyncMods] Failed to apply harmony patches: {ex.Message}");
            }
        }

        private void LogInitialization()
        {
            Debug.Log("[SyncMods] Mod initialized");
        }
    }
}
