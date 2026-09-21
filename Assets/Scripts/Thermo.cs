using System.Collections.Generic;
using UnityEngine;

/// <summary>Нагрев и заморозка. 🔴 21.09, владелец: «добавь левый сайдбар, где можно нагреть и
/// заморозить атомы, есть настройка радиуса и силы охлаждения; есть переключатель, который
/// всю зону охлаждает и даёт ей синий оттенок».
///
/// Температура здесь — это движение атомов, как и в жизни:
///   • НАГРЕВ добавляет атомам в круге беспорядочные толчки. Сильный и долгий нагрев рвёт
///     связи — самые слабые первыми (по сумме ковалентных радиусов: длинная связь слабее).
///     Это и есть термическое разложение.
///   • ХОЛОД гасит движение атомов в круге; на полной силе они замирают на месте.
///   • «Охладить всю зону» — холод везде сразу и синий оттенок зоны.
/// Честно: температура тут не в градусах и без теплоёмкости — это наглядность «горячее —
/// значит быстрее и рвётся, холоднее — значит тише».</summary>
public class Thermo : MonoBehaviour
{
    public static Thermo I;

    public enum Tool { None, Heat, Cool }
    public Tool Current = Tool.None;
    public float Radius = 1.6f;          // в единицах зоны
    public float Strength = 0.5f;        // 0..1
    public bool ZoneCold;                // вся зона охлаждена

    public bool Applying;                // сейчас держат кнопку над зоной
    public bool Scripted;                // управляет самопроверка, а не мышь
    public Vector3 Point;                // куда направлен инструмент (в мире)

    readonly Dictionary<Atom, float> heat = new Dictionary<Atom, float>();   // накопленный нагрев атома
    float fxTimer;

    void Awake() { I = this; }

    /// <summary>Вызывается из Lab.Update, когда инструмент выбран: забирает мышь себе.</summary>
    public bool HandleInput(bool overPanel)
    {
        if (Scripted) return true;       // самопроверка управляет инструментом сама, без мыши
        if (Current == Tool.None || Lab.I == null || Lab.I.Cam == null) { Applying = false; return false; }
        Point = Lab.I.DropPoint(Input.mousePosition);
        Applying = !overPanel && Input.GetMouseButton(0);
        return !overPanel;
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        if (ZoneCold)
            foreach (var a in Atom.All) if (a != null) Cool(a, 0.6f, dt);

        if (!Applying) return;
        float r2 = Radius * Radius;
        var near = new List<Atom>();
        foreach (var a in Atom.All)
            if (a != null && (a.transform.position - Point).sqrMagnitude <= r2) near.Add(a);

        foreach (var a in near)
        {
            if (Current == Tool.Cool) { Cool(a, Strength, dt); continue; }
            // НАГРЕВ: беспорядочные толчки, сильнее к центру круга
            float k = 1f - Mathf.Clamp01((a.transform.position - Point).magnitude / Mathf.Max(0.01f, Radius));
            a.Body.AddForce(Random.onUnitSphere * (4f + 26f * Strength) * (0.4f + 0.6f * k), ForceMode.Acceleration);
            float h; heat.TryGetValue(a, out h);
            h += dt * Strength * (0.5f + k);
            heat[a] = h;
            // Долгий сильный нагрев рвёт самую слабую связь атома.
            if (Strength >= 0.35f && h > 2.2f - 1.5f * Strength && a.Bonds.Count > 0)
            {
                Bond weakest = null; float longest = -1f;
                foreach (var b in a.Bonds)
                {
                    var o = b.Other(a);
                    if (o == null) continue;
                    float len = (a.El.CovalentPm + o.El.CovalentPm) / Mathf.Max(1, b.Order);   // длиннее и реже — слабее
                    if (len > longest) { longest = len; weakest = b; }
                }
                if (weakest != null)
                {
                    var other = weakest.Other(a);
                    string pair = a.El.Sym + "–" + (other != null ? other.El.Sym : "?");
                    Lab.I.BreakByHand(weakest);
                    Lab.I.Recompute();
                    Fx.Flash(a.transform.position, new Color(1f, 0.5f, 0.2f), 3f, 4f, 0.25f);
                    Lab.I.Say(Lang.T("Связь ", "Bond ") + pair + Lang.T(" разорвалась от нагрева — это термическое разложение.", " broke from the heat — thermal decomposition."),
                              new Color(1f, 0.7f, 0.4f));
                }
                heat[a] = 0f;
            }
        }

        fxTimer -= dt;
        if (fxTimer <= 0f)
        {
            fxTimer = 0.12f;
            if (Current == Tool.Heat) Fx.Sparks(Point, new Color(1f, 0.55f, 0.2f), 6, 1.5f + 3f * Strength, 0.06f);
            else Fx.Sparks(Point, new Color(0.6f, 0.85f, 1f), 4, 0.6f, 0.05f);
        }
    }

    /// <summary>Холод: гасим скорость. На полной силе — почти полная остановка за доли секунды.</summary>
    static void Cool(Atom a, float s, float dt)
    {
        float keep = Mathf.Clamp01(1f - s * 12f * dt);
        a.Body.linearVelocity *= keep;
        a.Body.angularVelocity *= keep;
    }

    public string Hint()
    {
        switch (Current)
        {
            case Tool.Heat: return Lang.T("Держи на зоне — атомы в круге греются: быстрее двигаются, а на сильном огне рвут связи.",
                                          "Hold over the zone — atoms in the circle heat up: they move faster and, on strong heat, break bonds.");
            case Tool.Cool: return Lang.T("Держи на зоне — атомы в круге остывают и замирают.",
                                          "Hold over the zone — atoms in the circle cool down and freeze.");
            default: return Lang.T("Выбери нагрев или холод, потом держи на зоне.", "Pick heat or cold, then hold over the zone.");
        }
    }
}
