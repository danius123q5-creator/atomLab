using UnityEngine;

/// <summary>Сцена целиком строится кодом при запуске: камера, свет, стол, рамка зоны, мозг.
/// В проекте нет ни одного префаба и ни одной настроенной сцены — так собранная игра не
/// зависит от того, что кто-то случайно передвинул в редакторе, и правится текстом.</summary>
public static class Bootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Object.FindFirstObjectByType<Lab>() != null) return;   // второй раз не строим

        // ——— камера ———
        var camGo = new GameObject("MainCamera");
        camGo.tag = "MainCamera";
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.04f, 0.05f, 0.08f);
        cam.fieldOfView = 55f;
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();

        // ——— свет: основной сверху и мягкая подсветка снизу, чтобы шарики не были плоскими ———
        var sun = new GameObject("Sun").AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.97f, 0.9f);
        sun.transform.rotation = Quaternion.Euler(48f, 35f, 0f);
        sun.shadows = LightShadows.Soft;

        var fill = new GameObject("Fill").AddComponent<Light>();
        fill.type = LightType.Directional;
        fill.intensity = 0.45f;
        fill.color = new Color(0.55f, 0.7f, 1f);
        fill.transform.rotation = Quaternion.Euler(-25f, -140f, 0f);

        RenderSettings.ambientLight = new Color(0.22f, 0.24f, 0.30f);

        // ——— стол ———
        var table = GameObject.CreatePrimitive(PrimitiveType.Cube);
        table.name = "Table";
        table.transform.position = new Vector3(0f, -1.7f, 0f);
        table.transform.localScale = new Vector3(16f, 0.4f, 12f);
        table.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(new Color(0.13f, 0.15f, 0.19f));

        // ——— рамка зоны сборки: двенадцать тонких рёбер ———
        Vector3 c = Lab.ZoneCenter, h = Lab.ZoneHalf;
        var frameMat = LabMaterials.Atom(new Color(0.25f, 0.45f, 0.65f));
        for (int axis = 0; axis < 3; axis++)
            for (int s1 = -1; s1 <= 1; s1 += 2)
                for (int s2 = -1; s2 <= 1; s2 += 2)
                {
                    var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    Object.Destroy(bar.GetComponent<Collider>());
                    bar.name = "ZoneEdge";
                    bar.GetComponent<Renderer>().sharedMaterial = frameMat;
                    Vector3 pos = c, scale = Vector3.one * 0.05f;
                    if (axis == 0) { pos += new Vector3(0f, h.y * s1, h.z * s2); scale.x = h.x * 2f; }
                    if (axis == 1) { pos += new Vector3(h.x * s1, 0f, h.z * s2); scale.y = h.y * 2f; }
                    if (axis == 2) { pos += new Vector3(h.x * s1, h.y * s2, 0f); scale.z = h.z * 2f; }
                    bar.transform.position = pos;
                    bar.transform.localScale = scale;
                }

        // ——— мозг и интерфейс ———
        var labGo = new GameObject("Lab");
        var lab = labGo.AddComponent<Lab>();
        lab.Cam = cam;
        labGo.AddComponent<LabUI>();
        labGo.AddComponent<Traits>();
        labGo.AddComponent<Accelerator>();
        labGo.AddComponent<AtomBuilder>();
        labGo.AddComponent<Updater>();        // «вышла новая версия — скачать»
        labGo.AddComponent<PhysLab>();        // этаж выше: вещи из вещества (стакан, натрий, колбы)
        labGo.AddComponent<QuarkLab>();       // этаж ниже: кварки    // стенд сборки: протоны, нейтроны, электроны   // стенд синтеза: два гнезда и результат      // характеры элементов: магнетизм, распад, жадность галогенов

        if (SelfTest.Requested) labGo.AddComponent<SelfTest>();
        else lab.LoadZone();               // в проверке начинаем с чистой зоны, иначе она увидит чужое

        lab.Say(Lang.T("Тяни элемент из таблицы слева в зону. Начни с водорода и кислорода.", "Drag an element from the table into the zone. Start with hydrogen and oxygen."), new Color(0.8f, 0.9f, 1f));
    }
}
