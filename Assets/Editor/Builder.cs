using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public static class Builder
{
    private const string BuildPath = "Builds/Windows/KingdomsClone.exe";
    private const string ItchUser = "Bloodray636"; // Замени на свой ник
    private const string ItchGame = "Kingdom";     // Замени на название игры на itch.io

    [MenuItem("Build/Build Windows (Test Build)")]
    public static void BuildWindows()
    {
        // 1. Получаем список сцен из Build Settings
        string[] scenes = new string[EditorBuildSettings.scenes.Length];
        for (int i = 0; i < scenes.Length; i++)
        {
            scenes[i] = EditorBuildSettings.scenes[i].path;
        }

        if (scenes.Length == 0)
        {
            Debug.LogError("[Builder] No scenes in Build Settings! Add scenes to build first.");
            return;
        }

        // 2. Настраиваем опции сборки
        BuildPlayerOptions buildPlayerOptions = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = BuildPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development // Development билд для тестов (с профайлером и дебаг-меню)
        };

        // 3. Запускаем сборку
        Debug.Log($"[Builder] Starting build to: {BuildPath}");
        BuildReport report = BuildPipeline.BuildPlayer(buildPlayerOptions);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
        {
            Debug.Log($"[Builder] Build succeeded: {summary.totalSize / 1024 / 1024} MB");
            
            // 4. Опционально: Загрузка на itch.io
            if (ItchUser != "your_itch_username")
            {
                UploadToItch();
            }
            else
            {
                Debug.Log("[Builder] Itch.io upload skipped (credentials not set).");
            }
        }
        else
        {
            Debug.LogError($"[Builder] Build failed: {summary.result}");
        }
    }

    private static void UploadToItch()
    {
        // Путь к butler.exe. Предполагаем, что он лежит в корне проекта или в PATH.
        string butlerPath = Path.Combine(Directory.GetCurrentDirectory(), "butler.exe");
        if (!File.Exists(butlerPath))
        {
            Debug.LogError($"[Builder] Butler not found at: {butlerPath}. Please download it from https://itchio.itch.io/butler");
            return;
        }

        string buildDir = Path.GetDirectoryName(BuildPath);
        string channel = "windows"; // Или "test", "dev" и т.д.
        string target = $"{ItchUser}/{ItchGame}:{channel}";

        Debug.Log($"[Builder] Uploading to itch.io: {target}");

        ProcessStartInfo startInfo = new ProcessStartInfo
        {
            FileName = butlerPath,
            Arguments = $"push \"{buildDir}\" {target}",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(output)) Debug.Log($"[Butler] {output}");
            if (!string.IsNullOrEmpty(error)) Debug.LogError($"[Butler] {error}");

            if (process.ExitCode == 0)
                Debug.Log("[Builder] Upload to itch.io completed successfully!");
            else
                Debug.LogError($"[Builder] Upload failed with exit code: {process.ExitCode}");
        }
    }
}