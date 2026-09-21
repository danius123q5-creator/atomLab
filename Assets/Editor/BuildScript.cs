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

    /// <summary>21.09, владелец: «убери это» (заставка «Made with Unity» при запуске).
    /// С Unity 6 заставку можно выключать и на бесплатной лицензии. Выключаем целиком: и
    /// логотип, и сам экран заставки — игра открывается сразу.</summary>
    static void NoSplash()
    {
        EnsureGlass();
        // Номер версии — из одного места (Updater.Version): по нему игра сравнивает себя с
        // последним релизом. Код версии Android растёт вместе с ним (2.5 -> 205), иначе телефон
        // откажется ставить новую поверх старой.
        PlayerSettings.bundleVersion = Updater.Version;
        var vp = Updater.Version.Split('.');
        int vmaj = 0, vmin = 0, vpat = 0; int.TryParse(vp[0], out vmaj); if (vp.Length > 1) int.TryParse(vp[1], out vmin); if (vp.Length > 2) int.TryParse(vp[2], out vpat);
        // 2.5.1 -> 20501: третья цифра тоже двигает код, иначе 2.5.1 и 2.5 получили бы один и тот же.
        PlayerSettings.Android.bundleVersionCode = Mathf.Max(PlayerSettings.Android.bundleVersionCode, vmaj * 10000 + vmin * 100 + vpat);
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.SplashScreen.showUnityLogo = false;
    }

    /// <summary>Материал стекла для посуды верхнего уровня (21.09). Создаём ФАЙЛОМ в Resources:
    /// прозрачный вариант стандартного шейдера попадает в сборку, только если его использует
    /// какой-то материал из проекта. Материал, созданный в коде игры, этого не гарантирует —
    /// вариант выпадает, и стакан рисуется розовым или сплошным.</summary>
    static void EnsureGlass()
    {
        const string path = "Assets/Resources/Glass.mat";
        if (File.Exists(path)) return;
        Directory.CreateDirectory("Assets/Resources");
        var m = new Material(Shader.Find("Standard"));
        m.SetFloat("_Mode", 2f);                                   // Fade
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.DisableKeyword("_ALPHATEST_ON");
        m.EnableKeyword("_ALPHABLEND_ON");
        m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000;
        m.SetFloat("_Glossiness", 0.9f);
        m.color = new Color(0.8f, 0.9f, 1f, 0.25f);
        AssetDatabase.CreateAsset(m, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("GLASS: создан " + path);
    }

    public static void BuildWindows()
    {
        var scenes = EnsureScene();
        NoSplash();
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


    /// <summary>Сборка под Linux (21.09, владелец: «создай билд для линукс»):
    /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod BuildScript.BuildLinux
    /// Нужен модуль «Linux Build Support (Mono)» — поставлен через Unity Hub 21.09.
    /// На выходе папка с исполняемым AtomLab.x86_64: на Linux ему надо дать право запуска
    /// (chmod +x AtomLab.x86_64) — архив zip это право не всегда сохраняет.</summary>
    public static void BuildLinux()
    {
        var scenes = EnsureScene();
        NoSplash();
        PlayerSettings.productName = "AtomLab";
        PlayerSettings.companyName = "Danich";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/Linux/AtomLab.x86_64",
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log("LINUX BUILD RESULT: " + s.result + "  size=" + s.totalSize + " bytes  errors=" + s.totalErrors);
        if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }


    /// <summary>Сборка под macOS (из очереди владельца на 2.2). Модуль MacStandaloneSupport
    /// стоит. На выходе AtomLab.app — папка; для раздачи пакуется в zip. Подписи Apple у нас
    /// нет, поэтому мак при первом запуске скажет «разработчик не проверен»: открывать через
    /// правую кнопку -> «Открыть». Это написано в заметках к релизу.</summary>
    public static void BuildMac()
    {
        var scenes = EnsureScene();
        NoSplash();
        PlayerSettings.productName = "AtomLab";
        PlayerSettings.companyName = "Danich";
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.resizableWindow = true;

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/Mac/AtomLab.app",
            target = BuildTarget.StandaloneOSX,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log("MAC BUILD RESULT: " + s.result + "  size=" + s.totalSize + " bytes  errors=" + s.totalErrors);
        if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }


    /// <summary>Сборка под Android:
    /// Unity.exe -batchmode -quit -projectPath "..." -executeMethod BuildScript.BuildAndroid
    ///
    /// Подписывается отладочным ключом Unity — такой apk ставится сбоку (нужно разрешить
    /// «установку из неизвестных источников»), но в Google Play его не примут: туда нужен
    /// свой ключ, а ключи заводит владелец, не я.</summary>
    public static void BuildAndroid()
    {
        var scenes = EnsureScene();
        NoSplash();
        PlayerSettings.productName = "AtomLab";
        PlayerSettings.companyName = "Danich";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.danich.atomlab");
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;   // 24 больше не поддерживается
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        EditorUserBuildSettings.buildAppBundle = false;      // нужен apk, а не aab: aab не поставить напрямую

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/Android/AtomLab.apk",
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log("ANDROID BUILD RESULT: " + s.result + "  size=" + s.totalSize + " bytes  errors=" + s.totalErrors);
        if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }


    /// <summary>Сборка под WebGL — то, чем играют «яблочники».
    ///
    /// 🔴 ПОЧЕМУ НЕ IPA. Приложение под iPhone собирается ТОЛЬКО на маке: Unity под Windows в
    /// лучшем случае выдаёт проект для Xcode, а подписать и упаковать его может лишь macOS с
    /// Xcode и платным ключом разработчика Apple. Модулей iOS и macOS в этом редакторе нет
    /// вовсе. WebGL — честный обход: страница открывается в Safari на айфоне, айпаде и маке,
    /// ставить ничего не нужно.</summary>
    public static void BuildWebGL()
    {
        var scenes = EnsureScene();
        NoSplash();
        PlayerSettings.productName = "AtomLab";
        PlayerSettings.companyName = "Danich";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.memorySize = 512;
        PlayerSettings.runInBackground = true;

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/WebGL",
            target = BuildTarget.WebGL,
            targetGroup = BuildTargetGroup.WebGL,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log("WEBGL BUILD RESULT: " + s.result + "  size=" + s.totalSize + " bytes  errors=" + s.totalErrors);
        if (s.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }


    /// <summary>Экспорт проекта Xcode под iPhone/iPad.
    ///
    /// 🔴 ЧТО ЭТО ДАЁТ И ЧЕГО НЕ ДАЁТ. Unity под Windows выдаёт ПРОЕКТ для Xcode — папку с
    /// исходниками и ресурсами. Это не приложение: превратить её в файл, который ставится на
    /// айфон, может только мак с Xcode, и только с ключом разработчика Apple (платный, свой у
    /// каждого). Без мака папка бесполезна; с маком — открыл и нажал «Run».
    ///
    /// Поэтому для яблочников по-прежнему проще WebGL: открывается в Safari и на айфоне, и на
    /// маке, ставить ничего не нужно.</summary>
    public static void BuildIOS()
    {
        var scenes = EnsureScene();
        NoSplash();
        PlayerSettings.productName = "AtomLab";
        PlayerSettings.companyName = "Danich";
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, "com.danich.atomlab");
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

        var opts = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = "Build/iOS",
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None,
        };

        var report = BuildPipeline.BuildPlayer(opts);
        var s = report.summary;
        Debug.Log("IOS PROJECT RESULT: " + s.result + "  size=" + s.totalSize + " bytes  errors=" + s.totalErrors);
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
