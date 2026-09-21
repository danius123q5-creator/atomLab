using UnityEngine;

/// <summary>РЕЖИМ «СБОРКА АТОМА»: кладёшь протоны, нейтроны и электроны — и смотришь, что
/// получилось. 🔴 21.09, владелец.
///
/// Что от чего зависит — всё как в жизни:
///   • ПРОТОНЫ решают, ЧТО это за элемент. Восемь протонов — всегда кислород, и никак иначе:
///     номер элемента и есть число протонов.
///   • НЕЙТРОНЫ решают, какой это изотоп. Кислород с восемью нейтронами — обычный кислород-16,
///     с десятью — кислород-18, тот самый, по которому читают температуру древних льдов.
///     Слишком много или слишком мало — изотоп неустойчив и в жизни распался бы.
///   • ЭЛЕКТРОНЫ решают заряд. Поровну с протонами — атом нейтрален. Меньше — положительный
///     ион, больше — отрицательный. Электроны рассаживаются по слоям: 2, 8, 18, 32.
///
/// Собранное можно записать в таблицу — оранжевой клеткой, отдельно от настоящих элементов и
/// от голубых, добытых в ускорителе.</summary>
public class AtomBuilder : MonoBehaviour
{
    public static AtomBuilder I;

    /// <summary>Стенд сборки — своё место, напротив ускорителя.</summary>
    public static readonly Vector3 Rig = new Vector3(0f, 1.6f, 16f);

    public bool Active;
    public int Protons = 1, Neutrons = 0, Electrons = 1;

    GameObject rigRoot;
    Transform nucleus;
    readonly System.Collections.Generic.List<Transform> nucleons = new System.Collections.Generic.List<Transform>();
    readonly System.Collections.Generic.List<Transform> electrons = new System.Collections.Generic.List<Transform>();
    int shownP = -1, shownN = -1, shownE = -1;

    void Awake() { I = this; }

    void Start()
    {
        rigRoot = new GameObject("BuilderRig");
        rigRoot.transform.position = Rig;

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pad.GetComponent<Collider>());
        pad.transform.SetParent(rigRoot.transform, false);
        pad.transform.localPosition = new Vector3(0f, -2.6f, 0f);
        pad.transform.localScale = new Vector3(6f, 0.08f, 6f);
        pad.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(new Color(0.16f, 0.2f, 0.28f));

        nucleus = new GameObject("Nucleus").transform;
        nucleus.SetParent(rigRoot.transform, false);

        LoadAssembled();
    }

    public Elements.El BaseElement { get { return Elements.ByZ(Protons); } }
    public int Charge { get { return Protons - Electrons; } }
    public int MassNumber { get { return Protons + Neutrons; } }

    public string Verdict
    {
        get
        {
            if (Protons < 1) return Lang.T("Без протонов нет элемента: это просто нейтроны.", "No protons, no element: these are just neutrons.");
            var b = BaseElement;
            string who = b != null
                ? b.Name + " (" + b.Sym + Lang.T("), элемент ", "), element ") + Protons
                : Lang.T("элемент ", "element ") + Protons + Lang.T(" — такого в природе нет", " — does not exist in nature");

            int expected;
            bool stable = Elements.IsStableIsotope(Protons, Neutrons, out expected);
            string iso = stable
                ? Lang.T("изотоп устойчивый", "stable isotope")
                : Lang.T("изотоп неустойчивый: обычных нейтронов здесь около ", "unstable isotope: the usual neutron count is about ") + expected + Lang.T(", у тебя ", ", you have ") + Neutrons;

            string ion;
            if (Charge == 0) ion = Lang.T("заряд ноль — нейтральный атом", "zero charge — neutral atom");
            else if (Charge > 0) ion = Lang.T("не хватает ", "missing ") + Charge + Lang.T(" электрон(ов) — положительный ион ", " electron(s) — positive ion ") + Charge + "+";
            else ion = Lang.T("лишних электронов ", "extra electrons ") + (-Charge) + Lang.T(" — отрицательный ион ", " — negative ion ") + (-Charge) + "-";

            return who + Lang.T(".  Массовое число ", ".  Mass number ") + MassNumber + ".  " + iso + ".  " + ion + ".";
        }
    }

    public void Add(int dp, int dn, int de)
    {
        Protons = Mathf.Clamp(Protons + dp, 0, 200);
        Neutrons = Mathf.Clamp(Neutrons + dn, 0, 300);
        Electrons = Mathf.Clamp(Electrons + de, 0, 200);
        Fx.Pop(dp != 0 ? 1.0f : (dn != 0 ? 0.8f : 1.5f));
    }

    /// <summary>Записать собранное в таблицу оранжевой клеткой и оставить навсегда.</summary>
    public Elements.El SaveToTable()
    {
        if (Protons < 1)
        {
            if (Lab.I != null) Lab.I.Say(Lang.T("Нечего записывать: нужен хотя бы один протон.", "Nothing to save: at least one proton is needed."), new Color(1f, 0.9f, 0.6f));
            return null;
        }
        var el = Elements.AddAssembled(Protons, Neutrons, Charge);
        Fx.Chime();
        Fx.Flash(Rig, new Color(1f, 0.6f, 0.2f), 9f, 12f, 0.7f);
        Fx.Sparks(Rig, new Color(1f, 0.65f, 0.25f), 70, 5f);
        SaveAssembled();
        if (Lab.I != null)
            Lab.I.Say(Lang.T("В таблицу записан ", "Added to the table: ") + el.Name + " (" + el.Sym + Lang.T(") — оранжевая клетка внизу.", ") — orange cell at the bottom."),
                new Color(1f, 0.75f, 0.4f));
        return el;
    }

    public void SendToZone()
    {
        var el = SaveToTable();
        if (el == null) return;
        Atom.Spawn(el, Lab.ZoneCenter + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f)));
        if (Lab.I != null) { Lab.I.Recompute(); Lab.I.Say(el.Name + Lang.T(" отправлен в зону сборки.", " sent to the build zone."), new Color(1f, 0.8f, 0.5f)); }
    }

    // ==================== вид ====================

    void Update()
    {
        if (rigRoot != null) rigRoot.SetActive(Active);
        if (!Active) return;

        if (shownP != Protons || shownN != Neutrons || shownE != Electrons) Rebuild();

        // Электроны бегут по своим слоям.
        for (int i = 0; i < electrons.Count; i++)
        {
            var t = electrons[i];
            if (t == null) continue;
            var d = t.GetComponent<ElectronOrbit>();
            if (d != null) d.Tick(Time.deltaTime);
        }
        if (nucleus != null) nucleus.Rotate(Vector3.up, 12f * Time.deltaTime, Space.Self);
    }

    void Rebuild()
    {
        shownP = Protons; shownN = Neutrons; shownE = Electrons;

        foreach (var t in nucleons) if (t) Destroy(t.gameObject);
        foreach (var t in electrons) if (t) Destroy(t.gameObject);
        nucleons.Clear(); electrons.Clear();

        int total = Protons + Neutrons;
        // Ядро укладываем по сфере: чем больше нуклонов, тем крупнее комок — как в жизни,
        // радиус ядра растёт как корень кубический из числа частиц.
        float rad = 0.22f * Mathf.Pow(Mathf.Max(1, total), 1f / 3f);
        for (int i = 0; i < total; i++)
        {
            bool proton = i < Protons;
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(nucleus, false);
            Vector3 dir = Random.onUnitSphere * Random.Range(0.25f, 1f);
            go.transform.localPosition = dir * rad * 1.5f;
            go.transform.localScale = Vector3.one * 0.3f;
            go.GetComponent<Renderer>().sharedMaterial =
                LabMaterials.Atom(proton ? new Color(0.9f, 0.25f, 0.2f) : new Color(0.65f, 0.65f, 0.7f));
            nucleons.Add(go.transform);
        }

        // Электроны по слоям 2, 8, 18, 32.
        int[] shells = { 2, 8, 18, 32, 32, 18, 8 };
        int left = Electrons, shell = 0;
        while (left > 0 && shell < shells.Length)
        {
            int inShell = Mathf.Min(left, shells[shell]);
            for (int i = 0; i < inShell; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(rigRoot.transform, false);
                go.transform.localScale = Vector3.one * 0.16f;
                go.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(new Color(0.35f, 0.8f, 1f));
                var orb = go.AddComponent<ElectronOrbit>();
                orb.Radius = 1.1f + shell * 0.65f;
                orb.Speed = 1.6f / (1f + shell * 0.5f);
                orb.Phase = (i / (float)inShell) * Mathf.PI * 2f;
                orb.Tilt = Quaternion.Euler(shell * 27f, shell * 41f, 0f);
                orb.Home = rigRoot.transform;
                electrons.Add(go.transform);
            }
            left -= inShell;
            shell++;
        }
    }

    // ==================== собранное не теряется ====================

    string Path { get { return System.IO.Path.Combine(SelfTest.DataDir, "assembled.txt"); } }

    public void SaveAssembled()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var el in Elements.All)
                if (el.Assembled)
                    sb.Append(el.Z).Append(' ').Append(el.Neutrons).Append(' ').Append(el.Charge).Append((char)10);
            System.IO.File.WriteAllText(Path, sb.ToString());
        }
        catch (System.Exception e) { Debug.LogWarning(Lang.T("Не вышло сохранить собранные атомы: ", "Could not save assembled atoms: ") + e.Message); }
    }

    public void LoadAssembled()
    {
        try
        {
            if (!System.IO.File.Exists(Path)) return;
            int n = 0;
            foreach (var line in System.IO.File.ReadAllLines(Path))
            {
                var p = line.Split(' ');
                if (p.Length < 3) continue;
                Elements.AddAssembled(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
                n++;
            }
            if (n > 0 && Lab.I != null)
                Lab.I.Say(Lang.T("Собранных вручную атомов в таблице: ", "Hand-assembled atoms in the table: ") + n + ".", new Color(1f, 0.75f, 0.4f));
        }
        catch (System.Exception e) { Debug.LogWarning(Lang.T("Не вышло прочитать собранные атомы: ", "Could not read assembled atoms: ") + e.Message); }
    }
}

/// <summary>Электрон на своём слое: бежит по наклонённому кругу вокруг ядра.</summary>
public class ElectronOrbit : MonoBehaviour
{
    public float Radius = 1.2f, Speed = 1.5f, Phase;
    public Quaternion Tilt = Quaternion.identity;
    public Transform Home;

    public void Tick(float dt)
    {
        Phase += Speed * dt;
        Vector3 p = new Vector3(Mathf.Cos(Phase) * Radius, 0f, Mathf.Sin(Phase) * Radius);
        transform.localPosition = Tilt * p;
    }
}
