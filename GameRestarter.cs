using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;

namespace Calloatti.SyncMods
{
    public static class GameRestarter
    {
        /// <summary>
        /// Restarts the game, optionally passing command-line arguments such as 
        /// -settlementName and -saveName for the AutoStarter.
        /// </summary>
        /// <param name="extraArgs">The arguments to pass to the new game process.</param>
        public static void Restart(string extraArgs = "")
        {
            string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string exePath;
            string shell;
            string args;

            // Cross-Platform Path and Command Setup
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                exePath = Path.Combine(rootPath, "Timberborn.exe");
                shell = "cmd.exe";

                // For Windows: wait 5 seconds, then start the exe with the extra arguments.
                // Use escaped quotes for the exe path and the arguments string.
                // The "" before the exepath is there representing the Window Title, which is required when using start with a quoted path.
                // The complete string after /C must be enclose in quotes to ensure it's treated as a single command, especially if extraArgs contains spaces.

                args = $"/C \"timeout /t 5 /nobreak & start \"\" \"{exePath}\" {extraArgs}\"";
            }
            else // Linux or macOS
            {
                if (Application.platform == RuntimePlatform.OSXPlayer)
                    exePath = Path.Combine(rootPath, "Timberborn.app/Contents/MacOS/Timberborn");
                else
                    exePath = Path.Combine(rootPath, "Timberborn.x86_64");

                shell = "/bin/bash";

                // For Unix: sleep 5, then use nohup to launch the background process with arguments.
                args = $"-c \"sleep 5; nohup \\\"{exePath}\\\" {extraArgs} > /dev/null 2>&1 &\"";
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            UnityEngine.Debug.Log($"[SyncMods] shell: '{shell}'");
            UnityEngine.Debug.Log($"[SyncMods] exePath: '{exePath}'");
            UnityEngine.Debug.Log($"[SyncMods] extraArgs: '{extraArgs}'"); 
            UnityEngine.Debug.Log($"[SyncMods] args: '{args}'");

            try
            {
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[SyncMods] Failed to restart process: {ex.Message}");
            }

            // Kill the current process immediately to free Steam/system resources
            Application.Quit();
        }
    }
}