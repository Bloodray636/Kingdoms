using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public class BuildVersionIncrementor : IPreprocessBuildWithReport
{
    public int callbackOrder => 1;

    public void OnPreprocessBuild(BuildReport report)
    {
        // Формат версии: Major.Minor.Build (например, 0.1.0, 0.1.1 и т.д.)
        string currentVersion = PlayerSettings.bundleVersion;
        string[] parts = currentVersion.Split('.');

        if (parts.Length == 3 && int.TryParse(parts[2], out int buildNumber))
        {
            buildNumber++;
            PlayerSettings.bundleVersion = $"{parts[0]}.{parts[1]}.{buildNumber}";
            Debug.Log($"[Build] Version bumped to: {PlayerSettings.bundleVersion}");
        }
        else
        {
            Debug.LogWarning($"[Build] Could not parse version '{currentVersion}'. Expected format: X.Y.Z");
            // Устанавливаем дефолтную версию, если формат неверный
            PlayerSettings.bundleVersion = "0.1.0"; 
        }
    }
}