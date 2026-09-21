using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Сборка из командной строки:
/// Unity.exe -batchmode -quit -projectPath "C:\Users\PC\Desktop\AtomLab" -executeMethod BuildScript.BuildWindows
///
/// Сцена в проекте пустая нарочно — всё строит Bootstrap при запуске. Но одна сцена в списке
/// сборки быть ОБЯЗАНА, иначе плеер не соберётся; если её нет, делаем здесь.</summary>
public static class BuildScript
{
    const string ScenePath = "Assets/Scenes/Lab.unity";

    static string[] EnsureScene()
    {
        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory("Assets/Scenes");
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(s, ScenePath);
            AssetDatabase.Refresh();
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        return new[] { ScenePath };
    }

    public static void BuildWindows()
    {
        var scenes = EnsureScene();
        PlayerSettings.productName = "AtomLab";
        PlayerSettings.companyName = "Danich";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/Windows/AtomLab.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log("BUILD RESULT: " + s.result + "  size=" + s.totalSize + " bytes  errors=" + s.totalErrors);
        if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    /// <summary>Только проверка компиляции: ничего не собирает, но падает на ошибке в коде.
    /// Быстрее полной сборки, поэтому правки гоняем через неё.</summary>
    public static void CompileOnly()
    {
        var errors = UnityEditor.Compilation.CompilationPipeline
            .GetAssemblies(UnityEditor.Compilation.AssembliesType.Editor).Length;
        Debug.Log("COMPILE OK, assemblies=" + errors);
    }
}
