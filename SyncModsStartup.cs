using Bindito.Core;
using HarmonyLib;
using System;
using Timberborn.ModManagerScene;
using UnityEngine;

namespace Calloatti.SyncMods
{
    public class Log
    {
        public static readonly string Prefix = "[SyncMods]";

        public static void Info(string message) => Debug.Log($"{Prefix} {message}");
    }

    [Context("MainMenu")] // Define DÓNDE se ejecuta Configure
    [Context("Game")]     // Define DÓNDE se ejecuta Configure
    public class SyncModsStartup : IModStarter, IConfigurator
    {
        private const string HarmonyId = "calloatti.SyncMods";

        public void StartMod(IModEnvironment environment)
        {
            LogInitialization();
            ApplyHarmonyPatches();
        }

        // AÑADIDO: Método obligatorio de IConfigurator para inicializar LocHelper
        public void Configure(IContainerDefinition containerDefinition)
        {
            LocHelper.Register(containerDefinition);
        }

        private void ApplyHarmonyPatches()
        {
            var harmonyInstance = new Harmony(HarmonyId);
            try
            {
                harmonyInstance.PatchAll();
                Log.Info($" Harmony patches applied successfully.");
            }
            catch (Exception ex)
            {
                Log.Info($" Failed to apply harmony patches: {ex.Message}");
            }
        }

        private void LogInitialization()
        {
            Log.Info($" Mod initialized");
        }
    }
}
