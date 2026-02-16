using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Timberborn.Modding;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.SaveMetadataSystem;
using Bindito.Core;

namespace Calloatti.SyncMods
{
    public class SyncModsinternal
    {
        public static SyncModsinternal Instance { get; private set; }
        private readonly ModRepository _modRepository;
        private readonly GameSaveDeserializer _gameSaveDeserializer;
        private readonly SaveMetadataSerializer _saveMetadataSerializer;

        public SyncModsinternal(ModRepository modRepository, GameSaveDeserializer gameSaveDeserializer, SaveMetadataSerializer saveMetadataSerializer)
        {
            _modRepository = modRepository;
            _gameSaveDeserializer = gameSaveDeserializer;
            _saveMetadataSerializer = saveMetadataSerializer;
            Instance = this;
            Debug.Log("[SyncMods] SyncModsinternal service initialized.");
        }

        public void SyncFromInternalMetadata(SaveReference saveRef)
        {
            Debug.Log($"[SyncMods] --- INTERNAL SYNC START: {saveRef.SaveName} ---");

            try
            {
                // 1. GLOBAL RESET: Disable all mods and clear priorities to -1
                foreach (var mod in _modRepository.Mods)
                {
                    string key = GetModKey(mod);
                    PlayerPrefs.SetInt($"ModEnabled.{key}", 0);
                    PlayerPrefs.SetInt($"ModPriority.{key}", -1);
                }

                // 2. GLOBAL RE-PRIORITIZE: Sort all installed mods Descending (Z to A)
                List<Mod> allMods = _modRepository.Mods.ToList();
                var sortedList = allMods.OrderByDescending(m => m.Manifest.Name).ToList();

                int priorityCounter = 1;
                foreach (var mod in sortedList)
                {
                    string key = GetModKey(mod);
                    PlayerPrefs.SetInt($"ModPriority.{key}", priorityCounter);
                    priorityCounter++;
                }
                Debug.Log($"[SyncMods] Global baseline priorities set (1 to {priorityCounter - 1}) based on Z-A sort.");

                // 3. READ INTERNAL METADATA
                SaveMetadata metadata = _gameSaveDeserializer.ReadFromSaveFile<SaveMetadata>(saveRef, _saveMetadataSerializer);
                if (metadata == null || metadata.Mods == null)
                {
                    Debug.LogWarning("[SyncMods] No internal mod metadata found in save.");
                    return;
                }

                // 4. ENABLE MODS FROM SAVE & ASSIGN HIGH STEPPED PRIORITIES
                int enabledCount = 0;
                int highPriority = 2000000;
                foreach (var modRef in metadata.Mods)
                {
                    // Find matching installed mod by ID (Case-Insensitive)
                    var installedMod = _modRepository.Mods.FirstOrDefault(m =>
                        string.Equals(m.Manifest.Id, modRef.Id, StringComparison.OrdinalIgnoreCase));

                    if (installedMod != null)
                    {
                        string modKey = GetModKey(installedMod);
                        PlayerPrefs.SetInt($"ModEnabled.{modKey}", 1);

                        // Set priority starting from 2,000,000 and decreasing by 10 for each mod
                        PlayerPrefs.SetInt($"ModPriority.{modKey}", highPriority);

                        Debug.Log($"[SyncMods] Enabled: {installedMod.Manifest.Name} | Custom Priority: {highPriority}");

                        highPriority -= 10;
                        enabledCount++;
                    }
                }

                // 5. FORCE ESSENTIALS (Harmony/Self at absolute top)
                EnsureEssentialMods(200000000);

                PlayerPrefs.Save();
                Debug.Log($"[SyncMods] --- INTERNAL SYNC COMPLETE: {enabledCount} mods enabled. ---");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SyncMods] Internal Sync Critical Error: {ex.Message}");
            }
        }

        private void EnsureEssentialMods(int topPriority)
        {
            var harmony = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == "Harmony");
            if (harmony != null)
            {
                string key = GetModKey(harmony);
                PlayerPrefs.SetInt($"ModEnabled.{key}", 1);
                PlayerPrefs.SetInt($"ModPriority.{key}", topPriority);
            }

            var self = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == "calloatti.syncmods");
            if (self != null)
            {
                string key = GetModKey(self);
                PlayerPrefs.SetInt($"ModEnabled.{key}", 1);
                PlayerPrefs.SetInt($"ModPriority.{key}", topPriority - 1);
            }
        }

        private string GetModKey(Mod mod) => $"{mod.ModDirectory.DisplaySource}.{mod.ModDirectory.OriginName}.{mod.Manifest.Id}";
    }

    [Context("MainMenu")]
    internal class MainMenuSyncConfigurator : Configurator
    {
        protected override void Configure() => Bind<SyncModsinternal>().AsSingleton();
    }
}