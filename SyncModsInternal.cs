using Bindito.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Timberborn.GameSaveRepositorySystem;
using Timberborn.Modding;
using Timberborn.SaveMetadataSystem;
using UnityEngine;
using static UnityEngine.Scripting.GarbageCollector;

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
            Log.Info($" SyncModsinternal service initialized.");
        }

        public void SyncFromInternalMetadata(SaveReference saveRef)
        {
            Log.Info($" --- INTERNAL SYNC START: {saveRef.SaveName} ---");

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

                // 2. GLOBAL RE-PRIORITIZE: Sort all installed mods by mod name Descending (Z to A)
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
                Log.Info($" Global baseline priorities set (1 to {priorityCounter - 1}) based on Z-A sort.");

                // 3. READ INTERNAL METADATA
                SaveMetadata metadata = _gameSaveDeserializer.ReadFromSaveFile<SaveMetadata>(saveRef, _saveMetadataSerializer);
                if (metadata == null || metadata.Mods == null)
                {
                    Log.Info("No internal mod metadata found in save.");
                    return;
                }

                // 4. ENABLE MODS FROM SAVE & ASSIGN HIGH STEPPED PRIORITIES SO TEHY ARE AT THE TOP OF THE LIST
                int enabledCount = 0;
                int highPriority = 2000000;
                foreach (var modRef in metadata.Mods)
                {
                    Mod installedMod = SelectPreferredMod(modRef.Id);

                    if (installedMod != null)
                    {
                        string modKey = GetModKey(installedMod);

                        if (UseModPlayerPrefs)
                        {
                            ModPlayerPrefsHelper.ToggleMod(true, installedMod);
                            ModPlayerPrefsHelper.SetModPriority(installedMod, highPriority);
                        }
                        else
                        {
                            PlayerPrefs.SetInt($"ModEnabled.{modKey}", 1);
                            PlayerPrefs.SetInt($"ModPriority.{modKey}", highPriority);
                        }

                        Log.Info($"Enabled: {installedMod.Manifest.Name} | Custom Priority: {highPriority}");

                        highPriority -= 10;
                        enabledCount++;
                    }
                }
                // 5. ENABLE AND MOVE ESSENTIAL MODS TO THE TOP (HARMONY + SYNCMODS)
                EnsureEssentialMod("Harmony", 200000000);
                EnsureEssentialMod("calloatti.syncmods", 200000000 - 1);


                // 6. GROUP LOCAL MOD WITH WORKSHOP MOD

                // 1. Group all discovered mods by their Unique ID from the manifest
                var modGroups = _modRepository.Mods.GroupBy(m => m.Manifest.Id);

                foreach (var group in modGroups)
                {
                    // Only process if we have at least two versions of the same mod ID
                    if (group.Count() > 1)
                    {
                        Mod localVersion = null;
                        Mod workshopVersion = null;

                        foreach (var mod in group)
                        {
                            // Check the directory path to distinguish between local files and Steam Workshop files
                            if (mod.ModDirectory.DisplaySource == "Local")
                            {
                                localVersion = mod;
                            }
                            else
                            {
                                workshopVersion = mod;
                            }
                        }

                        // 2. If both versions exist, set the Workshop priority lower than the Local priority
                        if (localVersion != null && workshopVersion != null)
                        {
                            // Retrieve the current local priority using the game's internal preference helper

                            string key = GetModKey(localVersion);


                            int localPriority = PlayerPrefs.GetInt($"ModPriority.{key}", 0);

                            key = GetModKey(workshopVersion);

                            // Set the workshop priority to (localPriority - 1) so it lists after the local version in the UI
                            PlayerPrefs.SetInt($"ModPriority.{key}", localPriority - 1);

                            Log.Info($"Workshop mod {workshopVersion.Manifest.Id} priority set to {localPriority - 1}.");
                        }
                    }
                }


                PlayerPrefs.Save();

                List<Mod> sortedMods;

                if (UseModPlayerPrefs)
                {
                    sortedMods = _modRepository.Mods
                        .OrderByDescending(mod => ModPlayerPrefsHelper.GetModPriority(mod))
                        .ToList();
                }
                else
                {
                    sortedMods = _modRepository.Mods
                        .OrderByDescending(mod => PlayerPrefs.GetInt($"ModPriority.{GetModKey(mod)}", -1))
                        .ToList();
                }

                // Refresh the UI with specific order and trigger notifications
                ModUIStateController.Refresh(sortedMods);

                Log.Info($" --- INTERNAL SYNC COMPLETE: {enabledCount} mods enabled. ---");
            }
            catch (Exception ex)
            {
                Log.Info($"Internal Sync Critical Error: {ex.Message}");
            }
        }

        private void EnsureEssentialMod(string modId, int Priority)
        {
            var SingleMod = SelectPreferredMod(modId);

            if (SingleMod != null)
            {
                string key = GetModKey(SingleMod);

                if (UseModPlayerPrefs)
                {
                    ModPlayerPrefsHelper.ToggleMod(true, SingleMod); //
                    ModPlayerPrefsHelper.SetModPriority(SingleMod, Priority); //
                }
                else
                {
                    PlayerPrefs.SetInt($"ModEnabled.{key}", 1);
                    PlayerPrefs.SetInt($"ModPriority.{key}", Priority);
                }
            }

        }

        private string GetModKey(Mod mod) => $"{mod.ModDirectory.DisplaySource}.{mod.ModDirectory.OriginName}.{mod.Manifest.Id}";

        private Mod SelectPreferredMod(string modId)
        {
            // 1. Find all installed mods with this ID
            var matchingMods = _modRepository.Mods
                .Where(m => string.Equals(m.Manifest.Id, modId, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (matchingMods.Count == 0) return null;

            // 2. If we have duplicates, explicitly look for the Local version
            if (matchingMods.Count > 1)
            {
                var localVersion = matchingMods.Find(m => m.ModDirectory.DisplaySource == "Local");

                if (localVersion != null)
                {
                    Log.Info($"[SelectPreferredMod] Duplicate found for {modId}. Prioritizing Local version.");
                    return localVersion;
                }
            }

            // 3. Otherwise, just return the first one found 
            return matchingMods[0];
        }
    }

    [Context("MainMenu")]
    internal class MainMenuSyncConfigurator : Configurator
    {
        protected override void Configure() => Bind<SyncModsinternal>().AsSingleton();
    }
}