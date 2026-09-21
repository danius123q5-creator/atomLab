using System.Collections.Generic;
using UnityEngine;

/// <summary>УСКОРИТЕЛЬ ЧАСТИЦ: два гнезда по краям, одно в центре под результат.
///
/// 🔴 21.09, владелец: «новая камера, где по краям 2 слота, в которые ставятся атомы, и в
/// центре один слот для результата. Это ускоритель частиц, который склеивает частицы и
/// сохраняет их в таблицу Менделеева под голубым цветом».
///
/// Что считается: ядра складываются. Протоны обоих ядер суммируются — из них и получается
/// номер нового элемента (Z1 + Z2), масса тоже складывается. Это и есть настоящий способ,
/// которым получили всё тяжелее урана: в Дубне и Дармштадте разгоняют одно ядро и бьют им по
/// мишени из другого.
///
/// 🔴 Чего тут нет: вероятности. В жизни из миллиона столкновений слипается одно ядро, и живёт
/// оно миллисекунды. Здесь склейка выходит всегда и держится вечно.</summary>
public class Accelerator : MonoBehaviour
{
    public static Accelerator I;

    /// <summary>Стенд стоит поодаль от зоны сборки — это отдельное место, а не угол лаборатории.</summary>
    public static readonly Vector3 Rig = new Vector3(0f, 1.6f, -16f);

    public bool Active;
    public Elements.El SlotA, SlotB, Result;

    Transform visA, visB, visR;
    GameObject rigRoot;

    void Awake() { I = this; }

    void Start() { BuildRig(); LoadSynthetic(); }

    void BuildRig()
    {
        rigRoot = new GameObject("AcceleratorRig");
        rigRoot.transform.position = Rig;

        // Кольцо ускорителя — просто обод из кубиков, чтобы место читалось глазом.
        var mat = LabMaterials.Atom(new Color(0.18f, 0.34f, 0.5f));
        for (int i = 0; i < 48; i++)
        {
            float a = i / 48f * Mathf.PI * 2f;
            var seg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(seg.GetComponent<Collider>());
            seg.transform.SetParent(rigRoot.transform, false);
            seg.transform.localPosition = new Vector3(Mathf.Cos(a) * 5.2f, Mathf.Sin(a) * 2.6f, 0f);
            seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
            seg.transform.localScale = new Vector3(0.7f, 0.12f, 0.12f);
            seg.GetComponent<Renderer>().sharedMaterial = mat;
        }

        visA = MakePad(new Vector3(-3.4f, 0f, 0f), new Color(0.8f, 0.3f, 0.3f));
        visB = MakePad(new Vector3( 3.4f, 0f, 0f), new Color(0.3f, 0.5f, 0.9f));
        visR = MakePad(new Vector3( 0f, 0f, 0f), new Color(0.35f, 0.8f, 1f));
    }

    Transform MakePad(Vector3 local, Color c)
    {
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pad.GetComponent<Collider>());
        pad.transform.SetParent(rigRoot.transform, false);
        pad.transform.localPosition = local + new Vector3(0f, -0.9f, 0f);
        pad.transform.localScale = new Vector3(1.6f, 0.08f, 1.6f);
        pad.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(c);

        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(ball.GetComponent<Collider>());
        ball.name = "SlotBall";
        ball.transform.SetParent(rigRoot.transform, false);
        ball.transform.localPosition = local;
        ball.transform.localScale = Vector3.zero;          // пусто, пока в гнездо ничего не положили
        ball.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(c);
        return ball.transform;
    }

    void Update()
    {
        if (rigRoot != null) rigRoot.SetActive(Active);
        Show(visA, SlotA);
        Show(visB, SlotB);
        Show(visR, Result);
        if (visR != null && Result != null)
            visR.Rotate(Vector3.up, 40f * Time.deltaTime, Space.Self);
    }

    static void Show(Transform t, Elements.El el)
    {
        if (t == null) return;
        if (el == null) { t.localScale = Vector3.zero; return; }
        float d = Mathf.Clamp(el.Radius * 2f, 0.5f, 1.6f) * 1.6f;
        t.localScale = new Vector3(d, d, d);
        t.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(el.Color);
    }

    public void Put(int slot, Elements.El el)
    {
        if (slot == 0) SlotA = el; else SlotB = el;
        Fx.Pop(1.1f);
        Fx.Sparks(Rig + new Vector3(slot == 0 ? -3.4f : 3.4f, 0f, 0f), el.Color, 18, 2.2f);
        if (Lab.I != null) Lab.I.Say("В гнездо " + (slot == 0 ? "слева" : "справа") + ": " + el.Name + ".", new Color(0.8f, 0.95f, 1f));
    }

    /// <summary>Склеить: номера ядер складываются, масса тоже. Если такой элемент в таблице
    /// уже есть — получаем его. Если сумма больше 118, элемента в природе нет: он становится
    /// новым, голубым, и остаётся в таблице навсегда.</summary>
    public void Fuse()
    {
        var lab = Lab.I;
        if (SlotA == null || SlotB == null)
        {
            if (lab != null) lab.Say("Нужны оба гнезда: щёлкни по гнезду, потом по элементу в таблице.", new Color(1f, 0.9f, 0.6f));
            return;
        }

        int z = SlotA.Z + SlotB.Z;
        float mass = SlotA.Mass + SlotB.Mass;

        Fx.Boom();
        Fx.Flash(Rig, new Color(0.5f, 0.9f, 1f), 16f, 20f, 0.9f);
        Fx.Sparks(Rig, new Color(0.6f, 0.95f, 1f), 160, 11f, 0.15f);

        var known = Elements.ByZ(z);
        bool isNew = known == null;
        Result = isNew ? Elements.AddSynthetic(z, mass) : known;

        if (lab != null)
        {
            if (isNew)
            {
                lab.Say("СИНТЕЗ: " + SlotA.Sym + " (" + SlotA.Z + ") + " + SlotB.Sym + " (" + SlotB.Z + ") = элемент " + z +
                        " — " + Result.Name + " (" + Result.Sym + "). Такого в природе нет: записан в таблицу голубым.",
                        new Color(0.6f, 0.95f, 1f));
                SaveSynthetic();
            }
            else
            {
                lab.Say("СИНТЕЗ: " + SlotA.Sym + " + " + SlotB.Sym + " = " + Result.Name + " (" + Result.Sym + ", элемент " + z +
                        "). Этот элемент в таблице уже есть — именно так его и получили в ускорителе.",
                        new Color(0.7f, 1f, 0.8f));
            }
        }

        SlotA = SlotB = null;
    }

    /// <summary>Отправить добытое в зону сборки — играть им как обычным атомом.</summary>
    public void TakeResult()
    {
        if (Result == null) return;
        Atom.Spawn(Result, Lab.ZoneCenter + new Vector3(Random.Range(-1f, 1f), 0.5f, Random.Range(-1f, 1f)));
        if (Lab.I != null)
        {
            Lab.I.Recompute();
            Lab.I.Say(Result.Name + " отправлен в зону сборки.", new Color(0.8f, 0.95f, 1f));
        }
        Result = null;
    }

    // ==================== добытое не теряется ====================

    string Path { get { return System.IO.Path.Combine(Application.persistentDataPath, "synthetic.txt"); } }

    public void SaveSynthetic()
    {
        try
        {
            var sb = new System.Text.StringBuilder();
            foreach (var el in Elements.All)
                if (el.Synthetic)
                    sb.Append(el.Z).Append(' ')
                      .Append(el.Mass.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))
                      .Append((char)10);
            System.IO.File.WriteAllText(Path, sb.ToString());
        }
        catch (System.Exception e) { Debug.LogWarning("Не вышло сохранить добытые элементы: " + e.Message); }
    }

    public void LoadSynthetic()
    {
        try
        {
            if (!System.IO.File.Exists(Path)) return;
            int n = 0;
            foreach (var line in System.IO.File.ReadAllLines(Path))
            {
                var p = line.Split(' ');
                if (p.Length < 2) continue;
                Elements.AddSynthetic(int.Parse(p[0]),
                    float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture));
                n++;
            }
            if (n > 0 && Lab.I != null)
                Lab.I.Say("Добытых в ускорителе элементов в таблице: " + n + ".", new Color(0.6f, 0.9f, 1f));
        }
        catch (System.Exception e) { Debug.LogWarning("Не вышло прочитать добытые элементы: " + e.Message); }
    }

    public static List<Elements.El> SyntheticList()
    {
        var res = new List<Elements.El>();
        foreach (var el in Elements.All) if (el.Synthetic) res.Add(el);
        return res;
    }
}
