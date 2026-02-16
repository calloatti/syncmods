using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;

namespace Calloatti.SyncMods
{
    public class SyncModsinternal
    {
        // Toggle this to switch between ModPlayerPrefsHelper and standard PlayerPrefs
        public bool UseModPlayerPrefs = false;

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

                    if (UseModPlayerPrefs)
                    {
                        ModPlayerPrefsHelper.ToggleMod(false, mod); //
                        ModPlayerPrefsHelper.SetModPriority(mod, -1); //
                    }
                    else
                    {
                        PlayerPrefs.SetInt($"ModEnabled.{key}", 0);
                        PlayerPrefs.SetInt($"ModPriority.{key}", -1);
                    }
                }

                // 2. GLOBAL RE-PRIORITIZE: Sort all installed mods Descending (Z to A)
                List<Mod> allMods = _modRepository.Mods.ToList();
                var sortedList = allMods.OrderByDescending(m => m.Manifest.Name).ToList();

                int priorityCounter = 1;
                foreach (var mod in sortedList)
                {
                    string key = GetModKey(mod);
                    if (UseModPlayerPrefs)
                    {
                        ModPlayerPrefsHelper.SetModPriority(mod, priorityCounter); //
                    }
                    else
                    {
                        PlayerPrefs.SetInt($"ModPriority.{key}", priorityCounter);
                    }
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
                    var installedMod = _modRepository.Mods.FirstOrDefault(m =>
                        string.Equals(m.Manifest.Id, modRef.Id, StringComparison.OrdinalIgnoreCase));

                    if (installedMod != null)
                    {
                        string modKey = GetModKey(installedMod);
                        if (UseModPlayerPrefs)
                        {
                            ModPlayerPrefsHelper.ToggleMod(true, installedMod); //
                            ModPlayerPrefsHelper.SetModPriority(installedMod, highPriority); //
                        }
                        else
                        {
                            PlayerPrefs.SetInt($"ModEnabled.{modKey}", 1);
                            PlayerPrefs.SetInt($"ModPriority.{modKey}", highPriority);
                        }

                        Debug.Log($"[SyncMods] Enabled: {installedMod.Manifest.Name} | Custom Priority: {highPriority}");

                        highPriority -= 10;
                        enabledCount++;
                    }
                }

                // 5. FORCE ESSENTIALS
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
            var harmonyMod = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == "Harmony");
            if (harmonyMod != null)
            {
                string key = GetModKey(harmonyMod);
                if (UseModPlayerPrefs)
                {
                    ModPlayerPrefsHelper.ToggleMod(true, harmonyMod); //
                    ModPlayerPrefsHelper.SetModPriority(harmonyMod, topPriority); //
                }
                else
                {
                    PlayerPrefs.SetInt($"ModEnabled.{key}", 1);
                    PlayerPrefs.SetInt($"ModPriority.{key}", topPriority);
                }
            }

            var syncMod = _modRepository.Mods.FirstOrDefault(m => m.Manifest.Id == "calloatti.syncmods");
            if (syncMod != null)
            {
                string key = GetModKey(syncMod);
                if (UseModPlayerPrefs)
                {
                    ModPlayerPrefsHelper.ToggleMod(true, syncMod); //
                    ModPlayerPrefsHelper.SetModPriority(syncMod, topPriority - 1); //
                }
                else
                {
                    PlayerPrefs.SetInt($"ModEnabled.{key}", 1);
                    PlayerPrefs.SetInt($"ModPriority.{key}", topPriority - 1);
                }
            }

            // Order by Priority ascending to prevent UI inversion
            var sortedMods = _modRepository.Mods
                .OrderByDescending(modi =>
                {
                    if (UseModPlayerPrefs)
                    {
                        return ModPlayerPrefsHelper.GetModPriority(modi); //
                    }
                    return PlayerPrefs.GetInt($"ModPriority.{GetModKey(modi)}", -1);
                })
                .ToList();

            // Refresh the UI with specific order and trigger notifications
            ModUIStateController.Refresh(sortedMods);
        }

        private string GetModKey(Mod mod) => $"{mod.ModDirectory.DisplaySource}.{mod.ModDirectory.OriginName}.{mod.Manifest.Id}";
    }

    [Context("MainMenu")]
    internal class MainMenuSyncConfigurator : Configurator
    {
        protected override void Configure() => Bind<SyncModsinternal>().AsSingleton();
    }
}