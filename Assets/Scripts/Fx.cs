using UnityEngine;

/// <summary>Отклик на событие: вспышка, искры, звук. Ничего не подгружается с диска —
/// и частицы, и звуки делаются кодом при первом обращении.
///
/// 🔴 21.09, владелец: «хотелось бы реакцию какую-то». До этого связь возникала молча, и
/// игрок не понимал, случилось что-то или нет. Теперь у каждого события свой отклик:
/// склейка — щелчок и белые искры, узнанное вещество — аккорд и зелёный сноп,
/// бурная реакция — грохот, оранжевая вспышка и разлёт.</summary>
public static class Fx
{
    static AudioSource src;
    static AudioClip popClip, chimeClip, boomClip;

    static AudioSource Src()
    {
        if (src != null) return src;
        var go = new GameObject("Sfx");
        Object.DontDestroyOnLoad(go);
        src = go.AddComponent<AudioSource>();
        src.spatialBlend = 0f;       // звук интерфейсный, без панорамы: иначе он «уезжает» за камерой
        src.volume = 0.45f;
        return src;
    }

    // ==================== звук ====================

    static AudioClip Make(string name, float seconds, System.Func<float, float> wave)
    {
        int sr = 44100;
        int n = Mathf.Max(16, Mathf.RoundToInt(sr * seconds));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(wave((float)i / sr), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, sr, false);
        c.SetData(data, 0);
        return c;
    }

    static float Noise(float t) { return Random.value * 2f - 1f; }

    /// <summary>Щелчок склейки: короткий тон вниз плюс шорох.</summary>
    public static void Pop(float pitch = 1f)
    {
        if (popClip == null)
            popClip = Make("pop", 0.12f, t =>
                (Mathf.Sin(2f * Mathf.PI * (740f - 500f * t / 0.12f) * t) * 0.7f + Noise(t) * 0.25f)
                * Mathf.Exp(-28f * t));
        var s = Src();
        s.pitch = pitch;
        s.PlayOneShot(popClip, 0.7f);
        s.pitch = 1f;
    }

    /// <summary>Аккорд открытия: до-ми-соль, затухающий.</summary>
    public static void Chime()
    {
        if (chimeClip == null)
            chimeClip = Make("chime", 0.9f, t =>
                (Mathf.Sin(2f * Mathf.PI * 523.25f * t) +
                 Mathf.Sin(2f * Mathf.PI * 659.25f * t) * 0.8f +
                 Mathf.Sin(2f * Mathf.PI * 783.99f * t) * 0.6f) * 0.28f * Mathf.Exp(-3.2f * t));
        Src().PlayOneShot(chimeClip, 0.9f);
    }

    /// <summary>Грохот бурной реакции: низкий гул и шум.</summary>
    public static void Boom()
    {
        if (boomClip == null)
            boomClip = Make("boom", 0.75f, t =>
                (Mathf.Sin(2f * Mathf.PI * 62f * t) * 0.8f + Noise(t) * 0.6f) * Mathf.Exp(-5.5f * t));
        Src().PlayOneShot(boomClip, 1f);
    }

    // ==================== свет и искры ====================

    /// <summary>Вспышка: точечный свет, который гаснет сам.</summary>
    public static void Flash(Vector3 pos, Color color, float intensity, float range, float life = 0.35f)
    {
        var go = new GameObject("Flash");
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        go.AddComponent<FadeLight>().Life = life;
    }

    /// <summary>Сноп искр. Система частиц создаётся кодом и сама себя убирает.</summary>
    public static void Sparks(Vector3 pos, Color color, int count, float speed, float size = 0.09f)
    {
        var go = new GameObject("Sparks");
        go.transform.position = pos;
        var ps = go.AddComponent<ParticleSystem>();

        var main = ps.main;
        main.startLifetime = 0.55f;
        main.startSpeed = speed;
        main.startSize = size;
        main.startColor = color;
        main.gravityModifier = 0.25f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        main.duration = 0.3f;
        main.loop = false;

        var em = ps.emission;
        em.enabled = true;
        em.rateOverTime = 0f;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius = 0.12f;

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        var r = go.GetComponent<ParticleSystemRenderer>();
        r.material = SparkMat();
        r.renderMode = ParticleSystemRenderMode.Billboard;

        ps.Play();
        Object.Destroy(go, 2f);
    }

    static Material sparkMat;

    static Material SparkMat()
    {
        if (sparkMat != null) return sparkMat;
        var sh = Shader.Find("Particles/Standard Unlit");
        if (sh == null) sh = Shader.Find("Legacy Shaders/Particles/Additive");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh == null) sh = Shader.Find("Unlit/Color");
        sparkMat = new Material(sh);
        sparkMat.color = Color.white;
        return sparkMat;
    }
}

/// <summary>Гасит свет вспышки и убирает его.</summary>
public class FadeLight : MonoBehaviour
{
    public float Life = 0.35f;
    float t, start;
    Light l;

    void Start() { l = GetComponent<Light>(); start = l.intensity; }

    void Update()
    {
        t += Time.deltaTime;
        float k = 1f - Mathf.Clamp01(t / Life);
        if (l != null) l.intensity = start * k * k;
        if (t >= Life) Destroy(gameObject);
    }
}
