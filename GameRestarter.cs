using UnityEngine;
using System.Diagnostics;
using System.IO;
using System;

namespace Calloatti.SyncMods
{
  public static class GameRestarter
  {
    private static readonly string LogFilePath = Path.Combine(Application.persistentDataPath, "SyncMods.log");

    private static void LogDirect(string message)
    {
      try { File.AppendAllText(LogFilePath, $"{Log.Prefix} {message}{Environment.NewLine}"); } catch { }
    }

    /// <summary>
    /// Overload to allow calling Restart() without any arguments.
    /// </summary>
    public static void Restart()
    {
      Restart(new string[0]);
    }

    // Minimal Change: Changed parameter to string[] args
    public static void Restart(string[] args)
    {
      try { if (File.Exists(LogFilePath)) File.Delete(LogFilePath); } catch { }

      LogDirect("Restart sequence initiated.");

      string rootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
      string exePath;
      int currentPid = Process.GetCurrentProcess().Id;
      LogDirect($"Current Process ID: {currentPid}");

      ProcessStartInfo psi = new ProcessStartInfo
      {
        CreateNoWindow = false, // Keep visible for verification
        UseShellExecute = true
      };

      if (Application.platform == RuntimePlatform.WindowsPlayer)
      {
        exePath = Path.Combine(rootPath, "Timberborn.exe");

        // We keep the exact logic that worked: 
        // wrapping each argument in literal quotes for the shell.
        string argString = string.Join(" ", Array.ConvertAll(args, a => $"\"{a}\""));

        // This is the literal string we feed into the pipe.
        string psCommand = $"Wait-Process -Id {currentPid} -ErrorAction SilentlyContinue; & '{exePath}' {argString}";

        LogDirect($"Piping to hidden process: {psCommand}");

        psi.FileName = "powershell.exe";
        // -WindowStyle Hidden helps prevent that initial flash.
        psi.Arguments = "-NoProfile -WindowStyle Hidden";

        // Core settings to make it invisible and pipe-able
        psi.RedirectStandardInput = true;
        psi.UseShellExecute = false;
        psi.CreateNoWindow = true; // This kills the console window entirely

        try
        {
          Process p = Process.Start(psi);
          using (StreamWriter sw = p.StandardInput)
          {
            if (sw.BaseStream.CanWrite)
            {
              // We "type" the command into the hidden background process.
              sw.WriteLine(psCommand);
            }
          }
        }
        catch (Exception ex)
        {
          LogDirect($"Pipe Error: {ex.Message}");
        }
      }
      else // Linux or macOS
      {
        if (Application.platform == RuntimePlatform.OSXPlayer)
          exePath = Path.Combine(rootPath, "Timberborn.app/Contents/MacOS/Timberborn");
        else
          exePath = Path.Combine(rootPath, "Timberborn.x86_64");

        // Minimal Change: Join array with spaces and quotes for Bash
        string unixArgs = string.Join(" ", Array.ConvertAll(args, a => $"\"{a}\""));
        string shCommand = $"while kill -0 {currentPid} 2>/dev/null; do sleep 1; done; nohup \"{exePath}\" {unixArgs} > /dev/null 2>&1 &";

        psi.FileName = "/bin/bash";
        psi.Arguments = $"-c \"{shCommand}\"";

        // LOG THE FULL COMMAND
        LogDirect($"Full Bash Command: {shCommand}");
      }

      try
      {
        LogDirect("Launching background restarter...");
        Process.Start(psi);
        LogDirect("Restarter launched.");
      }
      catch (Exception ex)
      {
        LogDirect($"CRITICAL ERROR launching restarter: {ex.Message}");
      }

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