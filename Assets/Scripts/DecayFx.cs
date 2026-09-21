using System.Collections.Generic;
using UnityEngine;

/// <summary>Анимация радиоактивного распада: видно, ЧТО именно уходит из атома.
///
/// 🔴 21.09, владелец: «сделай анимацию потери ионов/протонов при распаде частиц». До этого
/// распад выглядел как вспышка, после которой шарик просто менял цвет, — было непонятно, куда
/// делись два протона и почему элемент стал другим.
///
/// Что показываем (Саул):
///   • АЛЬФА-РАСПАД — из ядра вылетает альфа-частица: два протона (красные) и два нейтрона
///     (серые) одним комком. Это ядро гелия-4. Атом теряет 2 протона → элемент на две клетки
///     левее. Следом с оболочки уходят два электрона: зарядов в ядре стало меньше, лишние
///     электроны атом не удержит.
///   • БЕТА-РАСПАД — из ядра вылетает электрон, а нейтрон внутри становится протоном: элемент
///     сдвигается на клетку ПРАВЕЕ. Так на самом деле распадаются технеций и прометий:
///     технеций-99 становится рутением, а не ниобием, как вышло бы по альфа-правилу.
///   • Сам атом сжимается, а потом резко лопается наружу и скачком меняет цвет — в этот миг
///     и вылетают частицы (см. DecayPulse).
///
/// Всё визуальное, без коллайдеров и без влияния на физику: частицы не липнут к атомам и не
/// толкают их. Подписи («α = 2p + 2n», «2e⁻», «β⁻») рисуются поверх экрана, как остальные.</summary>
public class DecayFx : MonoBehaviour
{
    static DecayFx runner;

    class Flight
    {
        public Transform T;
        public Vector3 Vel;
        public float Age, Life, Drag;
        public string Label;
        public Color LabelColor;
        public float SparkTimer;
        public Color Trail;
        public Vector3 Spin;
        public float StartScale;
        public Transform From;     // из какого атома вылетать: пока он сжимается, он может сдвинуться
        public bool Launched;
    }

    readonly List<Flight> flights = new List<Flight>();
    GUIStyle labelStyle;

    static DecayFx Runner()
    {
        if (runner != null) return runner;
        var go = new GameObject("DecayFx");
        runner = go.AddComponent<DecayFx>();
        return runner;
    }

    static readonly Color ProtonC = new Color(0.92f, 0.28f, 0.22f);
    static readonly Color NeutronC = new Color(0.68f, 0.68f, 0.72f);
    static readonly Color ElectronC = new Color(0.35f, 0.8f, 1f);

    static GameObject Ball(Transform parent, Vector3 local, float size, Color c)
    {
        var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Object.Destroy(s.GetComponent<Collider>());
        s.transform.SetParent(parent, false);
        s.transform.localPosition = local;
        s.transform.localScale = Vector3.one * size;
        s.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(c);
        return s;
    }

    /// <summary>Альфа-распад: из атома уходит ядро гелия (2p + 2n), за ним два электрона.</summary>
    public static void Alpha(Atom atom, Elements.El from, Elements.El to)
    {
        if (atom == null) return;
        var r = Runner();
        Vector3 p = atom.transform.position;
        Vector3 dir = Random.onUnitSphere;
        if (Lab.Mode2D) { dir.z = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.right; }

        // Комок из четырёх нуклонов — тетраэдр, как их и рисуют.
        var alpha = new GameObject("AlphaParticle").transform;
        alpha.position = p;
        float q = 0.075f;
        Ball(alpha, new Vector3( q,  q,  q), 0.13f, ProtonC);
        Ball(alpha, new Vector3(-q, -q,  q), 0.13f, ProtonC);
        Ball(alpha, new Vector3(-q,  q, -q), 0.13f, NeutronC);
        Ball(alpha, new Vector3( q, -q, -q), 0.13f, NeutronC);
        r.flights.Add(new Flight
        {
            T = alpha, Vel = dir * 7.5f, Life = 1.6f, Drag = 1.1f, Age = -DecayPulse.Squeeze,
            Label = Lang.T("α = 2p + 2n", "α = 2p + 2n"), LabelColor = new Color(1f, 0.6f, 0.5f),
            Trail = new Color(1f, 0.55f, 0.4f), Spin = Random.onUnitSphere * 400f, StartScale = 1f, From = atom.transform,
        });

        // Два электрона уходят позже и медленнее, в разные стороны от альфы.
        for (int i = 0; i < 2; i++)
        {
            Vector3 d = (Random.onUnitSphere - dir * 0.8f);
            if (Lab.Mode2D) d.z = 0f;
            d = d.sqrMagnitude > 0.01f ? d.normalized : Vector3.up;
            var e = new GameObject("Electron").transform;
            e.position = p;
            Ball(e, Vector3.zero, 0.08f, ElectronC);
            r.flights.Add(new Flight
            {
                T = e, Vel = d * 3.2f, Life = 1.3f, Drag = 1.6f, Age = -DecayPulse.Squeeze - 0.25f,   // после альфы
                Label = i == 0 ? "2e⁻" : null, LabelColor = new Color(0.6f, 0.9f, 1f),
                Trail = new Color(0.5f, 0.85f, 1f), StartScale = 1f, From = atom.transform,
            });
        }

        Pulse(atom, from, to, new Color(1f, 0.6f, 0.45f), 1.6f);
    }

    /// <summary>Бета-минус-распад: нейтрон в ядре становится протоном, наружу вылетает электрон.</summary>
    public static void BetaMinus(Atom atom, Elements.El from, Elements.El to)
    {
        if (atom == null) return;
        var r = Runner();
        Vector3 p = atom.transform.position;
        Vector3 dir = Random.onUnitSphere;
        if (Lab.Mode2D) { dir.z = 0f; dir = dir.sqrMagnitude > 0.01f ? dir.normalized : Vector3.right; }

        var e = new GameObject("BetaElectron").transform;
        e.position = p;
        Ball(e, Vector3.zero, 0.09f, ElectronC);
        r.flights.Add(new Flight
        {
            // Бета-электрон быстрый и лёгкий: летит дальше и тормозит слабее альфы.
            T = e, Vel = dir * 12f, Life = 1.1f, Drag = 0.5f, Age = -DecayPulse.Squeeze,
            Label = Lang.T("β⁻: n → p + e⁻", "β⁻: n → p + e⁻"), LabelColor = new Color(0.6f, 0.9f, 1f),
            Trail = new Color(0.5f, 0.85f, 1f), StartScale = 1f, From = atom.transform,
        });

        Pulse(atom, from, to, new Color(0.5f, 0.85f, 1f), 1.9f);
    }

    static void Pulse(Atom atom, Elements.El from, Elements.El to, Color flash, float pitch)
    {
        var pulse = atom.gameObject.GetComponent<DecayPulse>();
        if (pulse == null) pulse = atom.gameObject.AddComponent<DecayPulse>();
        pulse.Begin(from.Radius * 2f, to.Radius * 2f, from.Color, to.Color, flash, pitch);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = flights.Count - 1; i >= 0; i--)
        {
            var f = flights[i];
            f.Age += dt;
            if (f.T == null) { flights.RemoveAt(i); continue; }
            if (f.Age < 0f) { f.T.localScale = Vector3.zero; continue; }   // ждёт «взрыва» внутри атома
            if (!f.Launched)
            {
                f.Launched = true;
                if (f.From != null) f.T.position = f.From.position;          // вылет из атома, где он СЕЙЧАС
            }

            f.T.position += f.Vel * dt;
            f.Vel *= Mathf.Exp(-f.Drag * dt);
            if (f.Spin != Vector3.zero) f.T.Rotate(f.Spin * dt, Space.Self);

            // След из мелких искр — чтобы глаз ловил траекторию, а не только точку.
            f.SparkTimer -= dt;
            if (f.SparkTimer <= 0f) { f.SparkTimer = 0.05f; Fx.Sparks(f.T.position, f.Trail, 2, 0.4f, 0.04f); }

            float k = Mathf.Clamp01(f.Age / f.Life);
            f.T.localScale = Vector3.one * f.StartScale * (1f - k * k);
            if (f.Age >= f.Life) { Destroy(f.T.gameObject); flights.RemoveAt(i); }
        }
    }

    void OnGUI()
    {
        var cam = Lab.I != null ? Lab.I.Cam : Camera.main;
        if (cam == null) return;
        if (labelStyle == null)
            labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        foreach (var f in flights)
        {
            if (f.T == null || f.Label == null || f.Age < 0f) continue;
            Vector3 sp = cam.WorldToScreenPoint(f.T.position + Vector3.up * 0.25f);
            if (sp.z <= 0f) continue;
            float a = 1f - Mathf.Clamp01(f.Age / f.Life);
            var c = f.LabelColor; c.a = a;
            labelStyle.normal.textColor = c;
            GUI.Label(new Rect(sp.x - 90f, Screen.height - sp.y - 12f, 180f, 24f), f.Label, labelStyle);
        }
    }
}

/// <summary>Атом при распаде: СЖИМАЕТСЯ, а потом РЕЗКО РАСШИРЯЕТСЯ и меняется.
///
/// 🔴 21.09, владелец: «чтоб частица сжималась, а потом резко расширялась и менялась». Ритм:
///   1) сжатие (Squeeze, 0.6 с) — медленно, почти вдвое, с мелкой дрожью; цвет раскаляется;
///   2) взрыв — за 0.07 с распухает с перехлёстом на 35 % сверх нового размера, и в ЭТОТ
///      миг цвет скачком становится цветом нового элемента, вспышка, звук, вылет частиц;
///   3) оседание — затухающая пружинка до нового размера.
/// Итоговые размер и материал — те, что выставил Atom.Become: компонент только ведёт переход
/// и в конце отдаёт общий материал элемента.</summary>
public class DecayPulse : MonoBehaviour
{
    public const float Squeeze = 0.6f;   // сжатие
    const float Pop = 0.07f;             // резкое расширение
    const float Settle = 0.45f;          // оседание

    float t = -1f;
    float fromD, toD;
    Color fromC, toC, flashC;
    float pitch;
    bool burst;
    Renderer rend;
    Material temp;

    public void Begin(float fromDiameter, float toDiameter, Color from, Color to, Color flash, float popPitch)
    {
        fromD = fromDiameter; toD = toDiameter; fromC = from; toC = to; flashC = flash; pitch = popPitch;
        t = 0f; burst = false;
        rend = GetComponent<Renderer>();
        if (rend != null && temp == null) temp = new Material(rend.sharedMaterial);
    }

    void Update()
    {
        if (t < 0f) return;
        t += Time.deltaTime;

        // Atom.Become, вызванный сразу после Begin, ставит общий материал элемента и затирает
        // наш временный — держим свой до конца перехода, иначе цвета не видно.
        if (temp != null && rend != null && rend.sharedMaterial != temp) rend.sharedMaterial = temp;

        float d;
        if (t < Squeeze)
        {
            float k = t / Squeeze;
            float ease = k * k;                                   // сначала медленно, к концу быстрее
            float tremble = 1f + Mathf.Sin(t * 160f) * 0.04f * k; // дрожь нарастает перед взрывом
            d = Mathf.Lerp(fromD, fromD * 0.55f, ease) * tremble;
            if (temp != null) temp.color = Color.Lerp(fromC, Color.white, k * 0.6f);   // раскаляется
        }
        else
        {
            if (!burst)
            {
                burst = true;
                if (temp != null) temp.color = toC;               // цвет меняется СКАЧКОМ
                Fx.Flash(transform.position, flashC, 6f, 8f, 0.45f);
                Fx.Sparks(transform.position, flashC, 45, 6f);
                Fx.Pop(pitch);
            }
            float u = t - Squeeze;
            if (u < Pop)
                d = Mathf.Lerp(fromD * 0.55f, toD * 1.35f, u / Pop);            // резко наружу
            else
            {
                float v = Mathf.Clamp01((u - Pop) / Settle);
                // Затухающая пружинка: от +35 % к новому размеру с одним-двумя качаниями.
                float spring = 0.35f * Mathf.Exp(-5f * v) * Mathf.Cos(v * 14f);
                d = toD * (1f + spring);
            }
        }
        transform.localScale = Vector3.one * d;

        if (t >= Squeeze + Pop + Settle)
        {
            t = -1f;
            transform.localScale = Vector3.one * toD;
            var atom = GetComponent<Atom>();
            // Отдаём общий материал элемента — иначе у каждого распавшегося атома остался бы
            // свой экземпляр материала.
            if (rend != null && atom != null) rend.sharedMaterial = LabMaterials.Atom(atom.El.Color);
        }
    }

    void OnDestroy() { if (temp != null) Destroy(temp); }
}
