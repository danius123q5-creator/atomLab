using System.Collections.Generic;
using UnityEngine;

/// <summary>РЕЖИМ СБОРКИ ИЗ КВАРКОВ — этаж ниже атома. 🔴 21.09, владелец.
///
/// Протон и нейтрон не неделимы: каждый состоит из трёх кварков. Здесь их и складывают.
///
///   • ВЕРХНИЙ кварк (u) несёт заряд +2/3, НИЖНИЙ (d) — минус 1/3.
///   • Протон — это uud: 2/3 + 2/3 − 1/3 = +1. Нейтрон — udd: 2/3 − 1/3 − 1/3 = 0.
///     Это не присказка, а ровно та арифметика, по которой заряды и сходятся.
///   • uuu даёт заряд +2 — это дельта-барион Δ++, он существует по-настоящему и живёт
///     около 10⁻²³ секунды. ddd — это Δ−.
///   • Кварки поодиночке не живут: их держит сильное взаимодействие, и растащить пару
///     нельзя — раньше родится новая пара. Поэтому здесь нельзя «собрать» один кварк,
///     только тройку.
///
/// Собранные протоны и нейтроны идут прямо в режим сборки атома — этажом выше.
///
/// 🔴 Чего тут нет: цвета (у настоящих кварков есть «цветной заряд», и в тройке он обязан
/// гаситься), глюонов, спина и массы, которая на 99% берётся не от кварков, а от энергии
/// их связи.</summary>
public class QuarkLab : MonoBehaviour
{
    public static QuarkLab I;

    public static readonly Vector3 Rig = new Vector3(-16f, 1.6f, 0f);

    public bool Active;

    /// <summary>true — верхний кварк (u), false — нижний (d). Больше трёх не кладём.</summary>
    public readonly List<bool> Slots = new List<bool>();

    public int MadeProtons, MadeNeutrons;

    GameObject rigRoot;
    readonly List<Transform> vis = new List<Transform>();
    int shown = -1;

    void Awake() { I = this; }

    void Start()
    {
        rigRoot = new GameObject("QuarkRig");
        rigRoot.transform.position = Rig;

        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pad.GetComponent<Collider>());
        pad.transform.SetParent(rigRoot.transform, false);
        pad.transform.localPosition = new Vector3(0f, -2f, 0f);
        pad.transform.localScale = new Vector3(5f, 0.08f, 5f);
        pad.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(new Color(0.2f, 0.16f, 0.3f));
    }

    public float Charge
    {
        get
        {
            float q = 0f;
            foreach (bool up in Slots) q += up ? (2f / 3f) : (-1f / 3f);
            return q;
        }
    }

    public string Composition
    {
        get
        {
            var sb = new System.Text.StringBuilder();
            foreach (bool up in Slots) sb.Append(up ? "u" : "d");
            return sb.Length == 0 ? Lang.T("пусто", "empty") : sb.ToString();
        }
    }

    /// <summary>Что получится из того, что лежит в тройке.</summary>
    public string Verdict
    {
        get
        {
            if (Slots.Count == 0) return Lang.T("Положи три кварка. Верхний даёт +2/3, нижний −1/3.", "Put in three quarks. Up gives +2/3, down −1/3.");
            if (Slots.Count < 3)
                return Lang.T("Кварков ", "Quarks ") + Slots.Count + Lang.T(" из трёх (", " of three (") + Composition + Lang.T("), заряд пока ", "), charge so far ") + Q(Charge) +
                       Lang.T(". Поодиночке кварки не живут — нужна тройка.", ". Quarks do not live alone — you need a triple.");

            int up = 0;
            foreach (bool u in Slots) if (u) up++;
            if (up == 2) return Lang.T("uud — это ПРОТОН, заряд +1. Он и задаёт номер элемента.", "uud is a PROTON, charge +1. It sets the element number.");
            if (up == 1) return Lang.T("udd — это НЕЙТРОН, заряд 0. Он задаёт изотоп.", "udd is a NEUTRON, charge 0. It sets the isotope.");
            if (up == 3) return Lang.T("uuu — дельта-барион Δ++, заряд +2. Существует, но живёт около 10⁻²³ секунды.", "uuu is the delta baryon Δ++, charge +2. It exists, but lives about 10⁻²³ seconds.");
            return Lang.T("ddd — дельта-барион Δ−, заряд −1. Тоже настоящий и тоже почти мгновенный.", "ddd is the delta baryon Δ−, charge −1. Also real, also almost instant.");
        }
    }

    static string Q(float q)
    {
        int third = Mathf.RoundToInt(q * 3f);
        if (third % 3 == 0) return (third / 3).ToString();
        return third + "/3";
    }

    public void Add(bool up)
    {
        if (Slots.Count >= 3)
        {
            if (Lab.I != null) Lab.I.Say(Lang.T("Больше трёх в барион не влезает: убери лишнее или собери.", "A baryon holds only three: remove one or build."), new Color(1f, 0.9f, 0.6f));
            return;
        }
        Slots.Add(up);
        Fx.Pop(up ? 1.5f : 1.2f);
        Fx.Sparks(Rig, up ? new Color(1f, 0.7f, 0.3f) : new Color(0.5f, 0.7f, 1f), 12, 2f);
    }

    public void Clear() { Slots.Clear(); }

    /// <summary>Собрать барион. Протон и нейтрон уходят наверх, в сборку атома.</summary>
    public void Assemble()
    {
        var lab = Lab.I;
        if (Slots.Count < 3)
        {
            if (lab != null) lab.Say(Lang.T("Нужны ровно три кварка: барион меньше чем из трёх не выходит.", "Exactly three quarks are needed: a baryon cannot be made from fewer."), new Color(1f, 0.9f, 0.6f));
            return;
        }

        int up = 0;
        foreach (bool u in Slots) if (u) up++;

        Fx.Boom();
        Fx.Flash(Rig, new Color(0.8f, 0.5f, 1f), 12f, 14f, 0.7f);
        Fx.Sparks(Rig, new Color(0.85f, 0.6f, 1f), 90, 7f);

        var bld = AtomBuilder.I;
        if (up == 2)
        {
            MadeProtons++;
            if (bld != null) bld.Protons++;
            if (lab != null) lab.Say(Lang.T("Собран ПРОТОН (uud). Он ушёл в сборку атома: протонов там теперь ", "PROTON built (uud). Sent to the atom builder: protons there now ") +
                (bld != null ? bld.Protons.ToString() : "?") + ".", new Color(1f, 0.6f, 0.5f));
        }
        else if (up == 1)
        {
            MadeNeutrons++;
            if (bld != null) bld.Neutrons++;
            if (lab != null) lab.Say(Lang.T("Собран НЕЙТРОН (udd). Он ушёл в сборку атома: нейтронов там теперь ", "NEUTRON built (udd). Sent to the atom builder: neutrons there now ") +
                (bld != null ? bld.Neutrons.ToString() : "?") + ".", new Color(0.8f, 0.8f, 0.85f));
        }
        else
        {
            if (lab != null) lab.Say((up == 3 ? "Δ++ (uuu)" : "Δ− (ddd)") +
                Lang.T(" собран — и тут же распался бы: такие барионы живут около 10⁻²³ секунды. В атом его не положить.", " built — and would decay at once: such baryons live about 10⁻²³ seconds. It cannot go into an atom."),
                new Color(1f, 0.8f, 0.5f));
        }

        Slots.Clear();
    }

    void Update()
    {
        if (rigRoot != null) rigRoot.SetActive(Active);
        if (!Active) return;

        if (shown != Slots.Count) Rebuild();

        // Кварки крутятся вокруг общего центра — связь их не отпускает.
        for (int i = 0; i < vis.Count; i++)
        {
            if (vis[i] == null) continue;
            float a = Time.time * 1.4f + i * Mathf.PI * 2f / Mathf.Max(1, vis.Count);
            vis[i].localPosition = new Vector3(Mathf.Cos(a), Mathf.Sin(a * 1.3f) * 0.4f, Mathf.Sin(a)) * 0.9f;
        }
    }

    void Rebuild()
    {
        shown = Slots.Count;
        foreach (var t in vis) if (t) Destroy(t.gameObject);
        vis.Clear();
        for (int i = 0; i < Slots.Count; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(rigRoot.transform, false);
            go.transform.localScale = Vector3.one * 0.45f;
            go.GetComponent<Renderer>().sharedMaterial =
                LabMaterials.Atom(Slots[i] ? new Color(1f, 0.65f, 0.25f) : new Color(0.45f, 0.65f, 1f));
            vis.Add(go.transform);
        }
    }
}
