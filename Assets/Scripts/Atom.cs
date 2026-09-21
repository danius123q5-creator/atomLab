using System.Collections.Generic;
using UnityEngine;

/// <summary>Один атом в зоне сборки: шарик своего цвета и размера, с запасом свободных связей.
/// Подписи (символ, свободные связи) рисует LabUI поверх экрана — так не нужен шрифт в сцене,
/// а кириллица в интерфейсе уже работает.</summary>
public class Atom : MonoBehaviour
{
    public Elements.El El;
    public readonly List<Bond> Bonds = new List<Bond>();
    public Rigidbody Body;
    public Renderer Rend;

    public static readonly List<Atom> All = new List<Atom>();

    /// <summary>Сколько связей ещё можно повесить. Благородные газы — ноль всегда,
    /// кроме режима бога, где запас бесконечный у всех.</summary>
    public int FreeValence
    {
        get
        {
            if (Lab.GodMode) return 99;
            int used = 0;
            for (int i = 0; i < Bonds.Count; i++) used += Bonds[i].Order;
            return Mathf.Max(0, El.Valence - used);   // точки над атомом и общий сборщик — по обычной
        }
    }

    /// <summary>Предел связей с КОНКРЕТНЫМ соседом.
    ///
    /// 21.09. Первая редакция давала высшую валентность всем и всегда — и проверка тут же
    /// поймала поломку: хлор с пределом 7 стал липким, хлор из HCl хватал медь, и медь
    /// «растворялась» в соляной кислоте вопреки ряду активности.
    ///
    /// Так и в жизни не бывает. Высшая валентность у серы, фосфора, хлора, азота, марганца,
    /// ксенона раскрывается только с соседом ЭЛЕКТРООТРИЦАТЕЛЬНЕЕ их самих — с кислородом,
    /// фтором, хлором: H2SO4, SO3, PCl5, HClO4, KMnO4, XeF4. С водородом или металлом сера
    /// двухвалентна (H2S), хлор одновалентен (HCl, NaCl). Это правило и стоит здесь.</summary>
    public int CapWith(Atom partner)
    {
        if (Lab.Mode == Lab.Level.Fun) return El.MaxBonds;   // Фан: собирается почти всё
        bool high = partner != null && partner.El.EN > 0f && El.EN > 0f && partner.El.EN > El.EN;
        return high ? El.MaxBonds : El.Valence;
    }

    /// <summary>Сколько связей занято сейчас (с учётом кратности).</summary>
    public int UsedBonds
    {
        get { int used = 0; for (int i = 0; i < Bonds.Count; i++) used += Bonds[i].Order; return used; }
    }

    public int FreeBondsWith(Atom partner)
    {
        if (Lab.GodMode) return 99;
        int used = 0;
        for (int i = 0; i < Bonds.Count; i++) used += Bonds[i].Order;
        return Mathf.Max(0, CapWith(partner) - used);
    }

    public bool BondedTo(Atom other)
    {
        for (int i = 0; i < Bonds.Count; i++) if (Bonds[i].Other(this) == other) return true;
        return false;
    }

    public Bond BondWith(Atom other)
    {
        for (int i = 0; i < Bonds.Count; i++) if (Bonds[i].Other(this) == other) return Bonds[i];
        return null;
    }

    public static Atom Spawn(Elements.El el, Vector3 pos)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Atom_" + el.Sym;
        go.transform.position = pos;
        float d = el.Radius * 2f;
        go.transform.localScale = new Vector3(d, d, d);

        var a = go.AddComponent<Atom>();
        a.El = el;
        a.Rend = go.GetComponent<Renderer>();
        a.Rend.sharedMaterial = LabMaterials.Atom(el.Color);

        var rb = go.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 2.2f;          // вязкость: атомы плывут, а не разлетаются
        rb.angularDamping = 4f;
        rb.mass = Mathf.Max(0.2f, el.Mass / 40f);
        a.Body = rb;

        All.Add(a);
        return a;
    }

    /// <summary>Стать другим элементом: размер, цвет и масса меняются на месте. Связи, на
    /// которые нового запаса не хватает, рвутся — сначала самые длинные, они «держатся хуже».
    /// Нужно и для распада, и для пункта «Заменить» в меню атома.</summary>
    public void Become(Elements.El el)
    {
        El = el;
        float d = el.Radius * 2f;
        transform.localScale = new Vector3(d, d, d);
        Rend.sharedMaterial = LabMaterials.Atom(el.Color);
        Body.mass = Mathf.Max(0.2f, el.Mass / 40f);

        while (Bonds.Count > 0)
        {
            int used = 0;
            for (int i = 0; i < Bonds.Count; i++) used += Bonds[i].Order;
            if (used <= el.MaxBonds) break;
            int worst = 0; float far = -1f;
            for (int i = 0; i < Bonds.Count; i++)
            {
                float len = Vector3.Distance(Bonds[i].A.transform.position, Bonds[i].B.transform.position);
                if (len > far) { far = len; worst = i; }
            }
            Bonds[worst].Break();
        }
    }

    public void Despawn()
    {
        for (int i = Bonds.Count - 1; i >= 0; i--) Bonds[i].Break();
        All.Remove(this);
        if (Lab.I != null) Lab.I.Selected.Remove(this);   // иначе в выделении остаются призраки
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        // Мягкие стенки зоны: за границей толкает обратно, а не отскок — чтобы ничего не терялось.
        Vector3 p = transform.position;
        Vector3 c = Lab.ZoneCenter, h = Lab.ZoneHalf;
        Vector3 push = Vector3.zero;
        if (p.x < c.x - h.x) push.x = (c.x - h.x) - p.x;
        if (p.x > c.x + h.x) push.x = (c.x + h.x) - p.x;
        if (p.y < c.y - h.y) push.y = (c.y - h.y) - p.y;
        if (p.y > c.y + h.y) push.y = (c.y + h.y) - p.y;
        if (p.z < c.z - h.z) push.z = (c.z - h.z) - p.z;
        if (p.z > c.z + h.z) push.z = (c.z + h.z) - p.z;
        if (push != Vector3.zero) Body.AddForce(push * 30f, ForceMode.Acceleration);
    }
}

/// <summary>Материалы делаем один раз на цвет: 118 элементов — 118 материалов максимум,
/// а не по одному на каждый шарик.</summary>
public static class LabMaterials
{
    static readonly Dictionary<int, Material> _cache = new Dictionary<int, Material>();
    static Shader _lit;

    static Shader Lit()
    {
        if (_lit != null) return _lit;
        // Проект собран на встроенном конвейере, но если когда-нибудь переедет на URP —
        // ищем его шейдер первым, иначе всё станет розовым.
        _lit = Shader.Find("Universal Render Pipeline/Lit");
        if (_lit == null) _lit = Shader.Find("Standard");
        if (_lit == null) _lit = Shader.Find("Legacy Shaders/Diffuse");
        if (_lit == null) _lit = Shader.Find("Unlit/Color");
        return _lit;
    }

    public static Material Atom(Color c)
    {
        int key = ((int)(c.r * 255) << 16) | ((int)(c.g * 255) << 8) | (int)(c.b * 255);
        Material m;
        if (_cache.TryGetValue(key, out m) && m != null) return m;
        m = new Material(Lit());
        m.color = c;
        if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.65f);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.65f);
        _cache[key] = m;
        return m;
    }

    static Material _bondCov, _bondIon;

    public static Material BondCovalent()
    {
        if (_bondCov == null) { _bondCov = new Material(Lit()); _bondCov.color = new Color(0.85f, 0.85f, 0.9f); }
        return _bondCov;
    }

    /// <summary>Ионная связь — другим цветом: там электрон не общий, а отобран.</summary>
    public static Material BondIonic()
    {
        if (_bondIon == null) { _bondIon = new Material(Lit()); _bondIon.color = new Color(1f, 0.75f, 0.2f); }
        return _bondIon;
    }
}
