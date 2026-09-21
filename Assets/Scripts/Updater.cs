using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Проверка обновлений. 🔴 21.09, владелец: «пусть игра говорит об обновлении и
/// качает сама».
///
/// При запуске игра спрашивает у GitHub, какой релиз последний. Если он новее этой сборки —
/// в углу появляется плашка «Вышла версия X» с кнопкой «Скачать».
///   • ПК (Windows, Linux, macOS): архив своей платформы качается прямо из игры в папку
///     «Загрузки», с полосой прогресса, потом папка открывается. Заменить саму себя запущенная
///     игра не может (её файлы заняты), поэтому распаковать архив — одно действие руками.
///   • Телефон: apk открывается в браузере, он качает, а Android сам предлагает установить.
///     Поставить apk изнутри игры без отдельного разрешения Android не даст — и это правильно.
///
/// Нет сети, GitHub недоступен, ответ странный — молчим: игра обязана работать и без этого.
/// Первая версия с проверкой — 2.5, поэтому пользу она начнёт приносить с выхода 2.6.</summary>
public class Updater : MonoBehaviour
{
    /// <summary>Версия ЭТОЙ сборки. Сборщик записывает её же в PlayerSettings.bundleVersion.</summary>
    public const string Version = "2.5.5";
    const string Api = "https://api.github.com/repos/danius123q5-creator/atomLab/releases/latest";

    public static Updater I;
    public string Latest;          // номер новой версии, если она есть
    public string AssetUrl;        // что качать для этой платформы
    public string AssetName;
    public float Progress = -1f;   // -1 — не качаем
    public string Status = "";
    public bool Dismissed;

    [System.Serializable] class Asset { public string name; public string browser_download_url; }
    [System.Serializable] class Release { public string tag_name; public Asset[] assets; }

    void Awake() { I = this; }

    IEnumerator Start()
    {
        if (SelfTest.Requested || Application.isBatchMode) yield break;   // проверка и сборка в сеть не ходят
        yield return new WaitForSeconds(2f);
        using (var rq = UnityWebRequest.Get(Api))
        {
            rq.SetRequestHeader("User-Agent", "AtomLab/" + Version);
            rq.SetRequestHeader("Accept", "application/vnd.github+json");
            rq.timeout = 15;
            yield return rq.SendWebRequest();
            if (rq.result != UnityWebRequest.Result.Success) yield break;
            Release rel = null;
            try { rel = JsonUtility.FromJson<Release>(rq.downloadHandler.text); } catch { }
            if (rel == null || string.IsNullOrEmpty(rel.tag_name) || !Newer(rel.tag_name, Version)) yield break;
            string want = Application.platform == RuntimePlatform.Android ? ".apk"
                        : Application.platform == RuntimePlatform.OSXPlayer ? "_Mac.zip"
                        : Application.platform == RuntimePlatform.LinuxPlayer ? "_Linux.zip" : "_Windows.zip";
            if (rel.assets != null)
                foreach (var a in rel.assets)
                    if (a != null && a.name != null && a.name.EndsWith(want)) { AssetUrl = a.browser_download_url; AssetName = a.name; }
            if (AssetUrl != null) Latest = rel.tag_name;
        }
    }

    /// <summary>2.10 новее 2.9: сравниваем по числам, а не строкой.</summary>
    public static bool Newer(string a, string b)
    {
        var x = a.TrimStart('v', 'V').Split('.'); var y = b.TrimStart('v', 'V').Split('.');
        for (int i = 0; i < Mathf.Max(x.Length, y.Length); i++)
        {
            int p = 0, q = 0;
            if (i < x.Length) int.TryParse(x[i], out p);
            if (i < y.Length) int.TryParse(y[i], out q);
            if (p != q) return p > q;
        }
        return false;
    }

    public void Download()
    {
        if (AssetUrl == null || Progress >= 0f) return;
        if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Application.OpenURL(AssetUrl);
            Status = Lang.T("Качается в браузере. Когда скачается — нажми на файл, Android предложит установить.",
                            "Downloading in the browser. When done, tap the file and Android will offer to install it.");
            return;
        }
        StartCoroutine(Fetch());
    }

    IEnumerator Fetch()
    {
        string dir = DownloadsDir();
        string path = Path.Combine(dir, AssetName);
        Progress = 0f;
        Status = Lang.T("Качаю ", "Downloading ") + AssetName + "...";
        using (var rq = UnityWebRequest.Get(AssetUrl))
        {
            rq.downloadHandler = new DownloadHandlerFile(path) { removeFileOnAbort = true };
            rq.SetRequestHeader("User-Agent", "AtomLab/" + Version);
            var op = rq.SendWebRequest();
            while (!op.isDone) { Progress = rq.downloadProgress; yield return null; }
            if (rq.result != UnityWebRequest.Result.Success)
            {
                Progress = -1f;
                Status = Lang.T("Не скачалось: ", "Download failed: ") + rq.error + Lang.T(". Можно скачать руками со страницы релизов.", ". You can download it by hand from the releases page.");
                yield break;
            }
        }
        Progress = 1f;
        Status = Lang.T("Скачано в «Загрузки»: ", "Saved to Downloads: ") + AssetName + Lang.T(". Распакуй поверх старой папки и запусти.", ". Unzip over the old folder and run.");
        Application.OpenURL("file://" + dir.Replace('\\', '/'));
    }

    static string DownloadsDir()
    {
        string home = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        string d = Path.Combine(home, "Downloads");
        if (!Directory.Exists(d)) d = Application.persistentDataPath;
        return d;
    }
}
