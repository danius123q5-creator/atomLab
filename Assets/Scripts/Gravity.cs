using UnityEngine;

/// <summary>Притяжение масс (21.09, владелец: «сделай, чтоб тяжёлые частицы притягивали к себе
/// более мелкие» и «карта гравитационного притяжения: глубиной воронки показан вес, при
/// движении частиц воронки движутся»).
///
/// Каждый атом тянет каждый с ускорением G·M/(r² + s²) к себе, где M — масса ТЯНУЩЕГО атома
/// в атомных единицах. Поэтому уран (238) тащит водород (1) к себе быстро, а сам почти не
/// сдвигается: лёгкого тянет масса тяжёлого, тяжёлого — масса лёгкого.
/// Честно: в жизни тяготение между атомами примерно в 10³⁶ раз слабее электрических сил, его
/// не видно вовсе. Здесь оно усилено, чтобы было видно, как масса собирает вещество — так же
/// собираются звёзды и планеты.
///
/// Потенциал (глубина воронки) для карты считает Depth(): это та же сумма масс по расстоянию.</summary>
public class Gravity : MonoBehaviour
{
    public static Gravity I;
    public bool On = true;
    public float Strength = 0.5f;           // 0..1, ползунок
    const float Soft2 = 0.35f;              // смягчение: без него у самого атома сила уходит в бесконечность

    void Awake() { I = this; }

    float G { get { return 0.1f * Strength; } }

    void FixedUpdate()
    {
        if (!On || Strength <= 0f) return;
        var list = Atom.All;
        int n = list.Count;
        if (n < 2) return;
        float g = G;
        for (int i = 0; i < n; i++)
        {
            var a = list[i];
            if (a == null || a.Body == null) continue;
            for (int j = i + 1; j < n; j++)
            {
                var b = list[j];
                if (b == null || b.Body == null) continue;
                if (a.BondWith(b) != null) continue;          // связанные и так держатся вместе
                Vector3 d = b.transform.position - a.transform.position;
                if (Lab.Mode2D) d.z = 0f;
                float r2 = d.sqrMagnitude;
                if (r2 < 1e-6f) continue;
                // 🔴 2.5.5, фото владельца «воду не могу взять»: в игре атомы связываются, как
                // только сблизятся, и притяжение само сводило O с O раньше, чем он подносил
                // второй водород, — выходило H–O–O. Теперь притяжение подводит только до
                // порога связи (в Lab он ~1.9 радиуса) с запасом и дальше не тянет: связь
                // делает игрок, а не гравитация.
                float stop = (a.El.Radius + b.El.Radius) * 1.9f * 1.4f;
                if (r2 < stop * stop) continue;
                Vector3 dir = d / Mathf.Sqrt(r2);
                float inv = g / (r2 + Soft2);
                // ускорение каждого — от массы ДРУГОГО: лёгкий летит к тяжёлому, тяжёлый почти стоит
                a.Body.AddForce(dir * inv * b.El.Mass, ForceMode.Acceleration);
                b.Body.AddForce(-dir * inv * a.El.Mass, ForceMode.Acceleration);
            }
        }
    }

    /// <summary>Глубина «воронки» в точке плоскости карты (u — по ширине зоны, v — вглубь или
    /// вверх в 2D). Сумма масс, делённых на квадрат расстояния, и мягкое насыщение, чтобы уран не
    /// проваливал карту в бездну, а водород был виден.</summary>
    public static float Depth(float u, float v)
    {
        float phi = 0f;
        foreach (var a in Atom.All)
        {
            if (a == null) continue;
            Vector2 p = MapPos(a.transform.position);
            float du = p.x - u, dv = p.y - v;
            // Для картинки берём спад 1/r², а не 1/r: с 1/r уран наклонял всю сетку целиком, и
            // отдельных воронок было не различить (кадр 21.09). Сила в FixedUpdate — та же 1/r².
            phi += a.El.Mass / (du * du + dv * dv + Soft2);
        }
        return 1f - Mathf.Exp(-phi / 60f);
    }

    /// <summary>Координаты атома на карте: в 3D — вид сверху (x, z), в 2D — сама плоскость (x, y).</summary>
    public static Vector2 MapPos(Vector3 w)
    {
        Vector3 c = Lab.ZoneCenter;
        return Lab.Mode2D ? new Vector2(w.x - c.x, w.y - c.y) : new Vector2(w.x - c.x, w.z - c.z);
    }

    public static Vector2 MapHalf { get { return Lab.Mode2D ? new Vector2(Lab.ZoneHalf.x, Lab.ZoneHalf.y) : new Vector2(Lab.ZoneHalf.x, Lab.ZoneHalf.z); } }
}
