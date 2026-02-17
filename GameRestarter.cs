using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;

namespace Calloatti.SyncMods
{
    public static class GameRestarter
    {
        // Log file located in the same folder as Player.log (Application.persistentDataPath)
        private static readonly string LogFilePath = Path.Combine(Application.persistentDataPath, "SyncMods.log");

        /// <summary>
        /// Writes directly to disk to ensure logs are saved before the process terminates.
        /// </summary>
        private static void LogDirect(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[SyncMods] {message}{Environment.NewLine}");
            }
            catch
            {
                // Silently ignore logging errors to prevent recursive crashes 
            }
        }

        /// <summary>
        /// Restarts the game, optionally passing command-line arguments.
        /// Uses a PID monitoring loop to ensure the old process is fully gone before starting the new one.
        /// </summary>
        /// <param name="extraArgs">The arguments to pass to the new game process.</param>
        public static void Restart(string extraArgs = "")
        {
            // Reset the log file for this run
            try { if (File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch { }

            LogDirect("Restart sequence initiated.");

            string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string exePath;
            string shell;
            string args;

            // Get the current process ID to monitor its exit
            int currentPid = Process.GetCurrentProcess().Id;
            LogDirect($"Current Process ID: {currentPid}");

            // Cross-Platform Path and Command Setup
            if (Application.platform == RuntimePlatform.WindowsPlayer)
            {
                exePath = Path.Combine(rootPath, "Timberborn.exe");
                shell = "cmd.exe";

                // Use escaped quotes for the exe path and the arguments string.
                // The "" before the exepath is there representing the Window Title, which is required when using start with a quoted path.
                // The complete string after /C must be enclose in quotes to ensure it's treated as a single command, especially if extraArgs contains spaces.

                // Windows: Loop checks for PID existence. Once gone (||), it starts the exe.
                // Wrapped in outer quotes to preserve internal quotes in extraArgs.
                args = $"/C \"for /L %i in (1,0,2) do (tasklist /FI \"PID eq {currentPid}\" 2>nul | find \"{currentPid}\" >nul || (start \"\" \"{exePath}\" {extraArgs} & exit)) & timeout /t 1 /nobreak >nul\"";
            }
            else // Linux or macOS
            {
                if (Application.platform == RuntimePlatform.OSXPlayer)
                    exePath = Path.Combine(rootPath, "Timberborn.app/Contents/MacOS/Timberborn");
                else
                    exePath = Path.Combine(rootPath, "Timberborn.x86_64");

                shell = "/bin/bash";

                // Unix: Loop with kill -0 checks for PID. Once gone, nohup launches.
                args = $"-c \"while kill -0 {currentPid} 2>/dev/null; do sleep 1; done; nohup \\\"{exePath}\\\" {extraArgs} > /dev/null 2>&1 &\"";
            }

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = shell,
                Arguments = args,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            LogDirect($"Shell: '{shell}'");
            LogDirect($"ExePath: '{exePath}'");
            LogDirect($"ExtraArgs: '{extraArgs}'");
            LogDirect($"Full Args: '{args}'");

            try
            {
                LogDirect("Launching shell process...");
                Process.Start(psi);
                LogDirect("Shell process launched.");
            }
            catch (Exception ex)
            {
                LogDirect($"CRITICAL ERROR launching process: {ex.Message}");
                LogDirect($"Failed to restart process: {ex.Message}");
            }

            // Attempt to quit the application
            try
            {
                LogDirect("Calling Application.Quit()...");
                Application.Quit();
            }
            catch (Exception ex)
            {
                LogDirect($"ERROR during Application.Quit(): {ex.Message}");
            }
        }
    }
}