using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>ВЕРХНИЙ УРОВЕНЬ — ВЕЩИ ИЗ ВЕЩЕСТВА. 🔴 21.09, владелец: «в 2.3 добавь верхний
/// уровень — физические объекты. чтобы не атомы, а уже объекты из атомов. чтобы поместить
/// натрий в воду», и следом: «колбы/низкие чашки».
///
/// Лестница уровней игры: кварки → атом из частиц → молекулы в зоне → ВЕЩИ на столе.
/// Здесь игрок держит в руках не шарики, а предметы: стакан воды, кусочек натрия, медную
/// проволоку, колбу с кислотой, щепоть соли. Бросил предмет в посуду — пошла реакция.
///
/// Правила — те же, что у молекул, только в словах посуды: ряд активности металлов
/// (Reactions.Activity), растворимость солей (Reactions.Soluble), кислоты, щёлочи, карбонаты.
/// Главная сцена — натрий в воде: кусочек бегает по поверхности, шипит, выходит водород,
/// вода становится щёлочью, и с каплей фенолфталеина краснеет. Медь в той же воде и в кислоте
/// не делает ничего — и игра объясняет почему: это такое же знание, как и реакция.
///
/// 🔴 ЧЕГО ТУТ НЕТ, честно: количеств, концентраций, температуры и скорости. Одна вещь —
/// одна порция, реакция идёт до конца или не идёт вовсе. Это наглядность опыта, а не расчёт.</summary>
public class PhysLab : MonoBehaviour
{
    public static PhysLab I;
    public static readonly Vector3 Rig = new Vector3(16f, 1.6f, 0f);
    const float TableY = -0.9f;                    // верх столешницы относительно Rig

    public bool Active;

    public enum Shape { Beaker, Flask, Dish, Chunk, Powder, Wire, Nail, Bottle }

    public class Def
    {
        public string Id, Ru, En, Formula;         // Formula — то, ЧТО это за вещество
        public Shape Shape;
        public Color Color;
        public string Liquid;                      // для посуды: чем налита (формула) или null — пустая
    }

    /// <summary>Полка: что можно поставить на стол.</summary>
    public static readonly Def[] Shelf =
    {
        new Def { Id = "beaker_water", Ru = "Стакан воды",        En = "Glass of water",       Formula = "H2O",    Shape = Shape.Beaker, Liquid = "H2O" },
        new Def { Id = "flask_empty",  Ru = "Колба (пустая)",     En = "Flask (empty)",        Formula = "",       Shape = Shape.Flask },
        new Def { Id = "dish_empty",   Ru = "Чашка (пустая)",     En = "Dish (empty)",         Formula = "",       Shape = Shape.Dish },
        new Def { Id = "flask_hcl",    Ru = "Соляная кислота",    En = "Hydrochloric acid",    Formula = "HCl",    Shape = Shape.Flask, Liquid = "HCl" },
        new Def { Id = "flask_vinegar",Ru = "Уксус",              En = "Vinegar",              Formula = "C2H4O2", Shape = Shape.Flask, Liquid = "C2H4O2" },
        new Def { Id = "flask_naoh",   Ru = "Раствор щёлочи",     En = "Caustic soda solution",Formula = "NaOH",   Shape = Shape.Flask, Liquid = "NaOH" },
        new Def { Id = "na",  Ru = "Кусочек натрия",  En = "Piece of sodium",    Formula = "Na",  Shape = Shape.Chunk,  Color = new Color(0.82f, 0.83f, 0.86f) },
        new Def { Id = "k",   Ru = "Кусочек калия",   En = "Piece of potassium", Formula = "K",   Shape = Shape.Chunk,  Color = new Color(0.75f, 0.72f, 0.80f) },
        new Def { Id = "li",  Ru = "Кусочек лития",   En = "Piece of lithium",   Formula = "Li",  Shape = Shape.Chunk,  Color = new Color(0.78f, 0.80f, 0.80f) },
        new Def { Id = "ca",  Ru = "Кусочек кальция", En = "Piece of calcium",   Formula = "Ca",  Shape = Shape.Chunk,  Color = new Color(0.88f, 0.88f, 0.84f) },
        new Def { Id = "mg",  Ru = "Магниевая лента", En = "Magnesium ribbon",   Formula = "Mg",  Shape = Shape.Wire,   Color = new Color(0.80f, 0.82f, 0.85f) },
        new Def { Id = "zn",  Ru = "Цинк",            En = "Zinc",               Formula = "Zn",  Shape = Shape.Chunk,  Color = new Color(0.62f, 0.66f, 0.72f) },
        new Def { Id = "fe",  Ru = "Железный гвоздь", En = "Iron nail",          Formula = "Fe",  Shape = Shape.Nail,   Color = new Color(0.45f, 0.46f, 0.50f) },
        new Def { Id = "cu",  Ru = "Медная проволока",En = "Copper wire",        Formula = "Cu",  Shape = Shape.Wire,   Color = new Color(0.85f, 0.50f, 0.25f) },
        new Def { Id = "nacl",  Ru = "Соль",           En = "Salt",              Formula = "NaCl",      Shape = Shape.Powder, Color = new Color(0.96f, 0.96f, 0.96f) },
        new Def { Id = "soda",  Ru = "Пищевая сода",   En = "Baking soda",       Formula = "NaHCO3",    Shape = Shape.Powder, Color = new Color(0.98f, 0.98f, 0.95f) },
        new Def { Id = "sugar", Ru = "Сахар",          En = "Sugar",             Formula = "C12H22O11", Shape = Shape.Powder, Color = new Color(1f, 0.98f, 0.92f) },
        new Def { Id = "chalk", Ru = "Мел",            En = "Chalk",             Formula = "CaCO3",     Shape = Shape.Chunk,  Color = new Color(0.95f, 0.95f, 0.92f) },
        new Def { Id = "cao",   Ru = "Негашёная известь", En = "Quicklime",      Formula = "CaO",       Shape = Shape.Powder, Color = new Color(0.92f, 0.92f, 0.88f) },
        new Def { Id = "cuso4", Ru = "Медный купорос", En = "Copper sulfate",    Formula = "CuSO4",     Shape = Shape.Powder, Color = new Color(0.25f, 0.55f, 0.95f) },
        new Def { Id = "phph",  Ru = "Фенолфталеин",   En = "Phenolphthalein",   Formula = "",          Shape = Shape.Bottle, Color = new Color(0.9f, 0.9f, 0.95f) },
    };

    // ==================== что лежит на столе ====================

    public enum Phase { Solid, Dissolved }

    /// <summary>Порция вещества в посуде.</summary>
    public class Portion
    {
        public string Formula;
        public Phase Phase;
        public int Amount = 1;         // сколько порций реакции хватит (колба кислоты — на три)
        public GameObject Go;          // если это твёрдое тело — его видимая часть на дне
        public override string ToString() { return Formula + (Phase == Phase.Solid ? "(тв)" : "(р-р)"); }
    }

    public class Item
    {
        public Def Def;
        public GameObject Go;
        public bool IsVessel { get { return Def.Shape == Shape.Beaker || Def.Shape == Shape.Flask || Def.Shape == Shape.Dish; } }
        public bool HasWater;                              // в посуде есть вода (растворитель)
        public readonly List<Portion> Contents = new List<Portion>();
        public bool Indicator;                             // капнули фенолфталеин
        public Transform Liquid;
        public string Name { get { return Lang.T(Def.Ru, Def.En); } }
    }

    public readonly List<Item> Items = new List<Item>();
    public Item Selected;

    GameObject rigRoot;
    static Material _glass;

    void Awake() { I = this; }

    void Start()
    {
        rigRoot = new GameObject("PhysRig");
        rigRoot.transform.position = Rig;
        var top = GameObject.CreatePrimitive(PrimitiveType.Cube);
        top.name = "Table";
        Destroy(top.GetComponent<Collider>());
        top.transform.SetParent(rigRoot.transform, false);
        top.transform.localPosition = new Vector3(0f, TableY - 0.1f, 0f);
        top.transform.localScale = new Vector3(10f, 0.2f, 5f);
        top.GetComponent<Renderer>().sharedMaterial = LabMaterials.Atom(new Color(0.42f, 0.30f, 0.20f));
        rigRoot.SetActive(false);
    }

    void Update()
    {
        if (rigRoot != null && rigRoot.activeSelf != Active) rigRoot.SetActive(Active);
        if (!Active) return;
        HandleDrag();
        foreach (var it in Items) if (it.IsVessel) Tint(it);
    }

    // ==================== материалы и формы ====================

    static Material Glass(Color c)
    {
        if (_glass == null)
        {
            // Материал стекла лежит в Resources и создаётся сборщиком (BuildScript): так вариант
            // прозрачного шейдера гарантированно попадает в сборку. Нет файла — рисуем
            // непрозрачным светлым, но не падаем.
            _glass = Resources.Load<Material>("Glass");
            if (_glass == null) return LabMaterials.Atom(new Color(c.r, c.g, c.b));
        }
        var m = new Material(_glass);
        m.color = c;
        return m;
    }

    static GameObject Prim(PrimitiveType t, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(t);
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>Собрать видимую вещь. Корень несёт один коллайдер — за него и берут рукой.</summary>
    GameObject Build(Def d, out Transform liquid)
    {
        liquid = null;
        var root = new GameObject("Phys_" + d.Id);
        root.transform.SetParent(rigRoot.transform, false);
        var box = root.AddComponent<BoxCollider>();
        switch (d.Shape)
        {
            case Shape.Beaker:
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.55f, 0f), new Vector3(0.9f, 0.55f, 0.9f), Glass(new Color(0.8f, 0.9f, 1f, 0.22f)));
                liquid = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.8f, 0.34f, 0.8f), Glass(new Color(0.55f, 0.75f, 1f, 0.6f))).transform;
                box.center = new Vector3(0f, 0.55f, 0f); box.size = new Vector3(0.95f, 1.1f, 0.95f);
                break;
            case Shape.Flask:
                Prim(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.95f, 0.85f, 0.95f), Glass(new Color(0.8f, 0.9f, 1f, 0.22f)));
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 1.05f, 0f), new Vector3(0.28f, 0.3f, 0.28f), Glass(new Color(0.8f, 0.9f, 1f, 0.22f)));
                liquid = Prim(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.36f, 0f), new Vector3(0.8f, 0.55f, 0.8f), Glass(new Color(0.55f, 0.75f, 1f, 0.6f))).transform;
                box.center = new Vector3(0f, 0.65f, 0f); box.size = new Vector3(0.95f, 1.3f, 0.95f);
                break;
            case Shape.Dish:
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.1f, 0f), new Vector3(1.5f, 0.1f, 1.5f), Glass(new Color(0.8f, 0.9f, 1f, 0.22f)));
                liquid = Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.09f, 0f), new Vector3(1.38f, 0.06f, 1.38f), Glass(new Color(0.55f, 0.75f, 1f, 0.6f))).transform;
                box.center = new Vector3(0f, 0.12f, 0f); box.size = new Vector3(1.5f, 0.3f, 1.5f);
                break;
            case Shape.Chunk:
                Prim(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.18f, 0f), new Vector3(0.36f, 0.28f, 0.32f), LabMaterials.Atom(d.Color)).transform.localRotation = Quaternion.Euler(0f, 25f, 8f);
                box.center = new Vector3(0f, 0.2f, 0f); box.size = new Vector3(0.5f, 0.4f, 0.5f);
                break;
            case Shape.Powder:
                Prim(PrimitiveType.Sphere, root.transform, new Vector3(0f, 0.08f, 0f), new Vector3(0.7f, 0.2f, 0.7f), LabMaterials.Atom(d.Color));
                box.center = new Vector3(0f, 0.1f, 0f); box.size = new Vector3(0.75f, 0.25f, 0.75f);
                break;
            case Shape.Wire:
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.45f, 0.05f), LabMaterials.Atom(d.Color)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                box.center = new Vector3(0f, 0.1f, 0f); box.size = new Vector3(0.95f, 0.2f, 0.25f);
                break;
            case Shape.Nail:
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.06f, 0f), new Vector3(0.07f, 0.4f, 0.07f), LabMaterials.Atom(d.Color)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(-0.4f, 0.06f, 0f), new Vector3(0.18f, 0.02f, 0.18f), LabMaterials.Atom(d.Color)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                box.center = new Vector3(0f, 0.1f, 0f); box.size = new Vector3(0.95f, 0.25f, 0.25f);
                break;
            case Shape.Bottle:
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.28f, 0.25f, 0.28f), LabMaterials.Atom(new Color(0.35f, 0.22f, 0.12f)));
                Prim(PrimitiveType.Cylinder, root.transform, new Vector3(0f, 0.56f, 0f), new Vector3(0.14f, 0.07f, 0.14f), LabMaterials.Atom(Color.white));
                box.center = new Vector3(0f, 0.3f, 0f); box.size = new Vector3(0.35f, 0.7f, 0.35f);
                break;
        }
        return root;
    }

    // ==================== полка -> стол ====================

    public Item Put(Def d)
    {
        if (Items.Count >= 12)
        {
            Say(Lang.T("На столе уже двенадцать вещей — места нет. Убери лишнее кнопкой «Очистить стол».",
                       "Twelve things on the table already — no room. Clear the table first."), new Color(1f, 0.85f, 0.6f));
            return null;
        }
        Transform liq;
        var go = Build(d, out liq);
        var it = new Item { Def = d, Go = go, Liquid = liq };
        if (it.IsVessel && d.Liquid != null)
        {
            it.HasWater = true;                                   // и кислота, и щёлочь — водные растворы
            // 21.09, владелец: «не растворилась». Колбы кислоты хватало ровно на одну реакцию:
            // бросил цинк, потом магний — и магний молча лежал. Теперь колба — на три порции.
            if (d.Liquid != "H2O") it.Contents.Add(new Portion { Formula = d.Liquid, Phase = Phase.Dissolved, Amount = 3 });
        }
        go.transform.localPosition = FreeSpot();
        Items.Add(it);
        Selected = it;
        Fx.Pop(1.1f);
        return it;
    }

    public Item PutById(string id)
    {
        foreach (var d in Shelf) if (d.Id == id) return Put(d);
        return null;
    }

    Vector3 FreeSpot()
    {
        // Раскладка рядами по столу: 6 в ряд, два ряда.
        for (int k = 0; k < 12; k++)
        {
            var p = new Vector3(-3.75f + (k % 6) * 1.5f, TableY, (k / 6 == 0) ? -0.9f : 1.0f);
            bool busy = false;
            foreach (var it in Items) if (it.Go != null && (it.Go.transform.localPosition - p).sqrMagnitude < 0.6f) busy = true;
            if (!busy) return p;
        }
        return new Vector3(0f, TableY, 0f);
    }

    public void ClearTable()
    {
        foreach (var it in Items) if (it.Go != null) Destroy(it.Go);
        Items.Clear();
        Selected = null;
    }

    // ==================== рука: взять, перенести, бросить ====================

    Item held;
    Vector3 heldOffset;

    bool OverUI { get { return LabUI.I != null && (LabUI.I.PointerOverUI || LabUI.I.PointerOverPhys); } }

    Item Pick(Vector3 screen)
    {
        var cam = Lab.I != null ? Lab.I.Cam : null;
        if (cam == null) return null;
        RaycastHit hit;
        if (!Physics.Raycast(cam.ScreenPointToRay(screen), out hit, 200f)) return null;
        foreach (var it in Items) if (it.Go != null && hit.collider.gameObject == it.Go) return it;
        return null;
    }

    bool TablePoint(Vector3 screen, out Vector3 local)
    {
        local = Vector3.zero;
        var cam = Lab.I != null ? Lab.I.Cam : null;
        if (cam == null) return false;
        var ray = cam.ScreenPointToRay(screen);
        var plane = new Plane(Vector3.up, Rig + Vector3.up * (TableY + 0.6f));   // рука держит чуть выше стола
        float t;
        if (!plane.Raycast(ray, out t)) return false;
        local = rigRoot.transform.InverseTransformPoint(ray.GetPoint(t));
        local.x = Mathf.Clamp(local.x, -4.6f, 4.6f);
        local.z = Mathf.Clamp(local.z, -2.2f, 2.2f);
        return true;
    }

    void HandleDrag()
    {
        if (Input.touchCount >= 2) { held = null; return; }
        if (Input.GetMouseButtonDown(0) && !OverUI)
        {
            var it = Pick(Input.mousePosition);
            if (it != null)
            {
                Selected = it;
                held = it;
                Vector3 p;
                if (TablePoint(Input.mousePosition, out p)) heldOffset = it.Go.transform.localPosition - p;
                heldOffset.y = 0f;
            }
        }
        if (held != null && held.Go != null && Input.GetMouseButton(0))
        {
            Vector3 p;
            if (TablePoint(Input.mousePosition, out p))
            {
                var target = new Vector3(p.x + heldOffset.x, TableY + 0.35f, p.z + heldOffset.z);   // приподнят в руке
                held.Go.transform.localPosition = Vector3.Lerp(held.Go.transform.localPosition, target, 0.5f);
            }
        }
        if (held != null && Input.GetMouseButtonUp(0))
        {
            var it = held; held = null;
            if (it.Go == null) return;
            var into = VesselUnder(it);
            var lp = it.Go.transform.localPosition; lp.y = TableY; it.Go.transform.localPosition = lp;
            if (into != null) DropInto(it, into);
        }
    }

    Item VesselUnder(Item it)
    {
        Item best = null; float bd = 1.0f;
        var p = it.Go.transform.localPosition;
        foreach (var v in Items)
        {
            if (v == it || !v.IsVessel || v.Go == null) continue;
            var q = v.Go.transform.localPosition;
            float d = new Vector2(p.x - q.x, p.z - q.z).magnitude;
            if (d < bd) { bd = d; best = v; }
        }
        return best;
    }

    // ==================== бросили одно в другое ====================

    /// <summary>Положить вещь в посуду (или перелить посуду в посуду) и провести всё, что пойдёт.</summary>
    public void DropInto(Item thing, Item vessel)
    {
        if (thing == null || vessel == null || thing == vessel) return;

        if (thing.IsVessel)
        {
            // Переливаем: всё содержимое одной посуды — в другую.
            if (!thing.HasWater && thing.Contents.Count == 0)
            {
                Say(Lang.T("Переливать нечего: ", "Nothing to pour: ") + thing.Name + Lang.T(" пустая.", " is empty."), new Color(1f, 0.9f, 0.7f));
                return;
            }
            vessel.HasWater |= thing.HasWater;
            foreach (var p in thing.Contents)
            {
                if (p.Go != null) { p.Go.transform.SetParent(vessel.Go.transform, false); p.Go.transform.localPosition = Bottom(vessel); }
                vessel.Contents.Add(p);
            }
            vessel.Indicator |= thing.Indicator;
            thing.Contents.Clear(); thing.HasWater = false; thing.Indicator = false;
            Say(Lang.T("Перелито: ", "Poured: ") + thing.Name + " → " + vessel.Name + ".", new Color(0.8f, 0.9f, 1f));
        }
        else if (thing.Def.Shape == Shape.Bottle)
        {
            vessel.Indicator = true;
            Say(Lang.T("Капля фенолфталеина. В кислоте и в нейтральной воде он бесцветный, а в щёлочи — малиновый.",
                       "A drop of phenolphthalein. Colourless in acid and neutral water, crimson in alkali."), new Color(1f, 0.6f, 0.85f));
            return;                                                           // пузырёк не расходуется
        }
        else
        {
            // Твёрдое тело — на дно посуды.
            var p = new Portion { Formula = thing.Def.Formula, Phase = Phase.Solid, Go = thing.Go };
            thing.Go.transform.SetParent(vessel.Go.transform, false);
            thing.Go.transform.localPosition = Bottom(vessel);
            thing.Go.transform.localScale = Vector3.one * 0.7f;
            var col = thing.Go.GetComponent<Collider>(); if (col != null) Destroy(col);
            Items.Remove(thing);
            vessel.Contents.Add(p);
            if (Selected == thing) Selected = vessel;
        }
        Selected = vessel;
        React(vessel);
    }

    static Vector3 Bottom(Item v)
    {
        return new Vector3(Random.Range(-0.12f, 0.12f), v.Def.Shape == Shape.Dish ? 0.08f : 0.12f, Random.Range(-0.12f, 0.12f));
    }

    // ==================== правила ====================

    public readonly List<string> Log = new List<string>();     // что произошло в последний раз (для проверки)
    public string LastText = "";                                // то же одной строкой — остаётся на пульте

    static bool IsMetal(string f) { var el = Elements.BySymbol(f); return el != null && Reactions.Activity(f) >= 0; }
    static bool IsAcid(string f) { return f == "HCl" || f == "C2H4O2" || f == "H2SO4" || f == "HNO3"; }
    static bool IsBase(string f) { return f == "NaOH" || f == "KOH" || f == "LiOH" || f == "Ca(OH)2" || f == "Ba(OH)2"; }
    static bool IsCarbonate(string f) { return f == "CaCO3" || f == "NaHCO3" || f == "Na2CO3"; }

    static string Residue(string acid)
    {
        switch (acid) { case "HCl": return "Cl"; case "C2H4O2": return "CH3COO"; case "H2SO4": return "SO4"; case "HNO3": return "NO3"; }
        return null;
    }
    static int ResCharge(string r) { return r == "SO4" ? 2 : 1; }

    static string MetalOf(string f)
    {
        switch (f) { case "NaOH": case "NaHCO3": case "Na2CO3": case "NaCl": return "Na"; case "KOH": return "K"; case "LiOH": return "Li";
                     case "Ca(OH)2": case "CaCO3": case "CaO": return "Ca"; case "Ba(OH)2": return "Ba"; }
        return null;
    }

    static string Hydroxide(string m)
    {
        var el = Elements.BySymbol(m); int v = el != null ? Mathf.Max(1, el.Valence) : 1;
        return v == 1 ? m + "OH" : m + "(OH)" + v;
    }

    /// <summary>Формула соли металла и кислотного остатка по зарядам: Zn + Cl → ZnCl2.</summary>
    static string Salt(string m, string res)
    {
        var el = Elements.BySymbol(m); int v = el != null ? Mathf.Max(1, el.Valence) : 1;
        // Железо с соляной кислотой и с медным купоросом даёт соли ДВУхвалентного железа
        // (FeCl2, FeSO4): водород и медь — слабые окислители, до Fe(III) не доводят.
        if (m == "Fe") v = 2;
        int c = ResCharge(res);
        int g = Gcd(v, c), nm = c / g, nr = v / g;
        string mm = m + (nm > 1 ? nm.ToString() : "");
        bool poly = res.Length > 2 || res == "SO4";
        if (res == "CH3COO") return (nr > 1 ? "(CH3COO)" + nr : "CH3COO") + mm;
        return mm + (nr > 1 ? (poly ? "(" + res + ")" + nr : res + nr) : res);
    }
    static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return Mathf.Max(1, a); }

    Portion Find(Item v, System.Func<Portion, bool> f) { foreach (var p in v.Contents) if (f(p)) return p; return null; }

    void Remove(Item v, Portion p)
    {
        v.Contents.Remove(p);
        if (p.Go != null) Destroy(p.Go);
    }

    /// <summary>Израсходовать одну порцию раствора: кислота в колбе кончается не сразу.</summary>
    void Use(Item v, Portion p)
    {
        p.Amount--;
        if (p.Amount <= 0) Remove(v, p);
    }

    void Add(Item v, string formula, Phase ph)
    {
        // Одинаковое растворённое складываем в одну порцию, чтобы не было «NaCl, NaCl, NaCl».
        if (ph == Phase.Dissolved)
            foreach (var p in v.Contents) if (p.Phase == Phase.Dissolved && p.Formula == formula) { p.Amount++; return; }
        v.Contents.Add(new Portion { Formula = formula, Phase = ph });
    }

    public bool IsAlkaline(Item v)
    {
        bool acid = false, basic = false;
        foreach (var p in v.Contents) { if (p.Phase == Phase.Dissolved && IsAcid(p.Formula)) acid = true; if (p.Phase == Phase.Dissolved && IsBase(p.Formula)) basic = true; }
        return basic && !acid;
    }
    public bool IsAcidic(Item v)
    {
        foreach (var p in v.Contents) if (p.Phase == Phase.Dissolved && IsAcid(p.Formula)) return true;
        return false;
    }

    /// <summary>Проводить правила, пока что-то меняется (не больше 8 шагов — как цепь у молекул).</summary>
    public void React(Item v)
    {
        Log.Clear();
        for (int step = 0; step < 8; step++) if (!OneStep(v)) break;
        if (Log.Count == 0)
        {
            // Ничего не пошло и сказать нечего — это тоже ответ: говорим, что лежит и почему тихо.
            bool metalNoAcid = v.Contents.Exists(p => p.Phase == Phase.Solid && IsMetal(p.Formula)) && !IsAcidic(v);
            Log.Add(metalNoAcid && !v.HasWater
                ? Lang.T("Посуда сухая: металлу не с чем реагировать. Налей воды или кислоты (перетащи колбу на колбу).",
                         "The vessel is dry: the metal has nothing to react with. Pour in water or acid (drag a flask onto it).")
                : Lang.T("Ничего не происходит: с тем, что уже в посуде, это вещество не реагирует.",
                         "Nothing happens: this substance does not react with what is already in the vessel."));
        }
        LastText = string.Join("   ", Log.ToArray());
        Say(LastText, new Color(0.75f, 1f, 0.8f));
    }

    bool OneStep(Item v)
    {
        // 1) нейтрализация: кислота + щёлочь в растворе
        var acid = Find(v, p => p.Phase == Phase.Dissolved && IsAcid(p.Formula));
        var bas = Find(v, p => p.Phase == Phase.Dissolved && IsBase(p.Formula));
        if (acid != null && bas != null)
        {
            string m = MetalOf(bas.Formula) ?? bas.Formula.Replace("OH", "");
            string salt = Salt(m, Residue(acid.Formula));
            Use(v, acid); Use(v, bas);
            Add(v, salt, Phase.Dissolved);
            Note(acid.Formula + " + " + bas.Formula + " → " + salt + " + H2O: " + Lang.T("нейтрализация, раствор теплеет.", "neutralisation, the solution warms up."));
            Heat(v);
            return true;
        }

        // 2) металл
        foreach (var p in v.Contents)
        {
            if (p.Phase != Phase.Solid || !IsMetal(p.Formula)) continue;
            string m = p.Formula;
            int rank = Reactions.Activity(m);
            if (acid != null)
            {
                if (rank > Reactions.HydrogenRank)
                {
                    Refuse(m, Lang.T(" стоит после водорода в ряду активности — из кислоты водород не вытесняет. Медной ложкой можно мешать кислоту.",
                                     " stands after hydrogen in the activity series — it does not push hydrogen out of the acid. You can stir acid with a copper spoon."));
                    continue;
                }
                string salt = Salt(m, Residue(acid.Formula));
                Remove(v, p); Use(v, acid);
                Add(v, salt, Phase.Dissolved);
                Note(m + " + " + acid.Formula + " → " + salt + " + H2↑: " + Lang.T("металл активнее водорода и вытесняет его — пузыри водорода.", "the metal is more active than hydrogen and drives it out — hydrogen bubbles."));
                Bubbles(v, new Color(0.95f, 0.95f, 1f), 3f);
                return true;
            }
            if (v.HasWater)
            {
                // Со ХОЛОДНОЙ водой реагируют самые активные: литий, калий, барий, кальций, натрий.
                if (rank >= 0 && rank <= Reactions.Activity("Na"))
                {
                    string hyd = Hydroxide(m);
                    var go = p.Go; p.Go = null;
                    Remove(v, p);
                    Add(v, hyd, Phase.Dissolved);
                    string how = m == "K" ? Lang.T("вспыхивает фиолетовым пламенем", "bursts into a violet flame")
                               : m == "Na" ? Lang.T("бегает по воде, шипит и плавится в шарик", "runs over the water, hisses and melts into a ball")
                               : m == "Li" ? Lang.T("спокойно шипит — литий самый тихий из щелочных", "fizzes calmly — lithium is the mildest alkali metal")
                               : Lang.T("опускается и выделяет пузыри", "sinks and gives off bubbles");
                    Note(m + " + H2O → " + hyd + " + H2↑: " + m + " " + how + Lang.T(". Вода стала щёлочью.", ". The water turned alkaline."));
                    if (go != null) StartCoroutine(Skim(v, go, m));
                    Bubbles(v, new Color(0.95f, 0.95f, 1f), 3f);
                    return true;
                }
                if (m == "Mg")
                    Refuse(m, Lang.T(" с холодной водой почти не реагирует: нужен кипяток или кислота. Брось ленту в соляную кислоту или уксус.",
                                     " barely reacts with cold water: it needs boiling water or an acid. Drop the ribbon into hydrochloric acid or vinegar."));
                else
                    Refuse(m, Lang.T(" в воде не реагирует: для этого он недостаточно активен (ряд активности).", " does not react with water: it is not active enough (activity series)."));
            }
        }

        // 3) вытеснение меди из купороса: железо и цинк активнее меди
        var cuso4 = Find(v, p => p.Phase == Phase.Dissolved && p.Formula == "CuSO4");
        if (cuso4 != null)
        {
            var metal = Find(v, p => p.Phase == Phase.Solid && (p.Formula == "Fe" || p.Formula == "Zn" || p.Formula == "Mg"));
            if (metal != null)
            {
                string salt = Salt(metal.Formula, "SO4");
                Remove(v, cuso4);
                Add(v, salt, Phase.Dissolved);
                if (metal.Go != null) foreach (var r in metal.Go.GetComponentsInChildren<Renderer>()) r.sharedMaterial = LabMaterials.Atom(new Color(0.8f, 0.42f, 0.22f));
                metal.Formula = "Cu";
                Note(Lang.T("Металл покрылся медью: ", "The metal got a copper coat: ") + salt + Lang.T(" в растворе, синий цвет уходит. Активный металл вытесняет медь из соли.", " in solution, the blue fades. A more active metal drives copper out of its salt."));
                return true;
            }
        }

        // 4) карбонат + кислота: шипит углекислым газом
        if (acid != null)
        {
            var carb = Find(v, p => IsCarbonate(p.Formula));
            if (carb != null)
            {
                string salt = Salt(MetalOf(carb.Formula), Residue(acid.Formula));
                Remove(v, carb); Use(v, acid);
                Add(v, salt, Phase.Dissolved);
                Note(carb.Formula + " + " + acid.Formula + " → " + salt + " + H2O + CO2↑: " + Lang.T("кислота выгоняет углекислый газ — бурное шипение.", "the acid drives out carbon dioxide — violent fizzing."));
                Bubbles(v, new Color(1f, 1f, 1f), 4f);
                return true;
            }
        }

        // 5) с водой: гашение извести, растворение
        if (v.HasWater)
        {
            var cao = Find(v, p => p.Formula == "CaO");
            if (cao != null)
            {
                Remove(v, cao);
                Add(v, "Ca(OH)2", Phase.Dissolved);
                Note("CaO + H2O → Ca(OH)2: " + Lang.T("гашение извести — вода греется до пара. Раствор щелочной.", "slaking lime — the water heats up to steam. The solution is alkaline."));
                Heat(v); Bubbles(v, new Color(1f, 1f, 1f), 1.5f);
                return true;
            }
            foreach (var p in v.Contents)
            {
                if (p.Phase != Phase.Solid || IsMetal(p.Formula)) continue;
                bool soluble = p.Formula == "C12H22O11" || p.Formula == "NaHCO3" ||
                               (MetalOf(p.Formula) != null && p.Formula != "CaCO3") || p.Formula == "CuSO4";
                if (p.Formula == "CaCO3") soluble = false;
                if (soluble)
                {
                    p.Phase = Phase.Dissolved;
                    if (p.Go != null) { StartCoroutine(Melt(p.Go)); p.Go = null; }
                    string extra = p.Formula == "NaCl"
                        ? Lang.T(" Солёная вода замерзает ниже нуля — поэтому солью посыпают дороги.", " Salt water freezes below zero — that is why roads are salted.")
                        : p.Formula == "CuSO4" ? Lang.T(" Раствор стал синим — это цвет ионов меди.", " The solution turned blue — that is the colour of copper ions.") : "";
                    Note(p.Formula + Lang.T(" растворяется — это не реакция: вещество распадается на ионы или молекулы между молекулами воды.",
                                             " dissolves — not a reaction: it splits into ions or molecules among the water molecules.") + extra);
                    return true;
                }
                if (p.Formula == "CaCO3" && !noted.Contains("CaCO3"))
                {
                    noted.Add("CaCO3");
                    Note(Lang.T("Мел в воде не растворяется — лежит на дне. Поэтому мрамор и известняк не тают под дождём.",
                                "Chalk does not dissolve in water — it stays at the bottom. That is why marble and limestone do not melt in the rain."));
                }
            }
        }
        return false;
    }

    readonly HashSet<string> noted = new HashSet<string>();

    void Note(string s) { Log.Add(s); }

    void Refuse(string who, string why)
    {
        string s = who + why;
        if (!Log.Contains(s)) Log.Add(s);
    }

    void Say(string s, Color c) { if (Lab.I != null) Lab.I.Say(s, c); }

    // ==================== цвет раствора и эффекты ====================

    void Tint(Item v)
    {
        if (v.Liquid == null) return;
        bool any = v.HasWater || v.Contents.Exists(p => p.Phase == Phase.Dissolved);
        v.Liquid.gameObject.SetActive(any);
        if (!any) return;
        Color c = new Color(0.62f, 0.8f, 1f, 0.45f);                 // вода
        if (v.Contents.Exists(p => p.Phase == Phase.Dissolved && p.Formula == "CuSO4")) c = new Color(0.15f, 0.45f, 1f, 0.75f);
        else if (v.Contents.Exists(p => p.Phase == Phase.Dissolved && p.Formula == "FeSO4")) c = new Color(0.55f, 0.85f, 0.55f, 0.6f);
        else if (v.Contents.Exists(p => p.Phase == Phase.Dissolved && p.Formula == "C2H4O2")) c = new Color(0.92f, 0.92f, 0.85f, 0.45f);
        if (v.Indicator && IsAlkaline(v)) c = new Color(0.95f, 0.1f, 0.55f, 0.8f);   // фенолфталеин в щёлочи
        var r = v.Liquid.GetComponent<Renderer>();
        if (r != null && r.material.color != c) r.material.color = c;
    }

    void Heat(Item v)
    {
        var w = v.Go.transform.position + Vector3.up * 1.2f;
        Fx.Sparks(w, new Color(1f, 1f, 1f), 30, 1.5f, 0.12f);
        Fx.Flash(w, new Color(1f, 0.6f, 0.3f), 2f, 4f, 0.4f);
    }

    void Bubbles(Item v, Color c, float seconds)
    {
        if (isActiveAndEnabled) StartCoroutine(BubbleRun(v, c, seconds));
    }

    IEnumerator BubbleRun(Item v, Color c, float seconds)
    {
        float end = Time.time + seconds;
        var mat = LabMaterials.Atom(c);
        while (Time.time < end && v.Go != null)
        {
            var b = Prim(PrimitiveType.Sphere, v.Go.transform, new Vector3(Random.Range(-0.25f, 0.25f), 0.12f, Random.Range(-0.25f, 0.25f)),
                         Vector3.one * Random.Range(0.04f, 0.09f), mat);
            StartCoroutine(Rise(b.transform, v.Def.Shape == Shape.Dish ? 0.25f : 0.95f));
            Fx.Pop(Random.Range(1.6f, 2.2f));
            yield return new WaitForSeconds(0.07f);
        }
    }

    IEnumerator Rise(Transform b, float top)
    {
        while (b != null && b.localPosition.y < top)
        {
            b.localPosition += Vector3.up * Time.deltaTime * 1.2f + new Vector3(Mathf.Sin(Time.time * 9f) * 0.004f, 0f, 0f);
            yield return null;
        }
        if (b != null) Destroy(b.gameObject);
    }

    /// <summary>Щелочной металл на воде: бегает по поверхности, тает и исчезает. У калия —
    /// фиолетовая вспышка, у натрия — оранжевая.</summary>
    IEnumerator Skim(Item v, GameObject go, string m)
    {
        if (go == null) yield break;
        float surf = v.Def.Shape == Shape.Dish ? 0.18f : v.Def.Shape == Shape.Flask ? 0.62f : 0.72f;
        float t = 0f, dur = m == "Li" ? 3.5f : 2.5f;
        Vector3 s0 = go.transform.localScale;
        while (t < dur && go != null)
        {
            t += Time.deltaTime;
            float a = t * 7f;
            float r = v.Def.Shape == Shape.Dish ? 0.45f : 0.22f;
            go.transform.localPosition = new Vector3(Mathf.Cos(a) * r, surf, Mathf.Sin(a * 1.3f) * r);
            go.transform.localScale = s0 * Mathf.Lerp(1f, 0.15f, t / dur);
            if (Random.value < 0.08f) Fx.Sparks(go.transform.position, new Color(1f, 1f, 1f), 3, 1f, 0.05f);
            yield return null;
        }
        if (go != null)
        {
            if (m == "K") Fx.Flash(go.transform.position, new Color(0.7f, 0.4f, 1f), 6f, 6f, 0.5f);
            else if (m == "Na") Fx.Flash(go.transform.position, new Color(1f, 0.7f, 0.2f), 4f, 5f, 0.4f);
            Destroy(go);
        }
    }

    IEnumerator Melt(GameObject go)
    {
        Vector3 s0 = go.transform.localScale;
        for (float t = 0f; t < 1.2f && go != null; t += Time.deltaTime)
        {
            go.transform.localScale = s0 * (1f - t / 1.2f);
            yield return null;
        }
        if (go != null) Destroy(go);
    }

    // ==================== мост на уровень ниже ====================

    /// <summary>«Разобрать» вещь: её молекула появляется в зоне молекул. Для посуды — то, что
    /// в ней растворено (или вода).</summary>
    public string SendDown(Item it)
    {
        if (it == null) return null;
        string f = null;
        if (!it.IsVessel) f = it.Def.Formula;
        else
        {
            foreach (var p in it.Contents) if (Molecules.LookupByComposition(Molecules.ParseFormula(p.Formula)) != null) { f = p.Formula; break; }
            if (f == null && it.HasWater) f = "H2O";
        }
        if (string.IsNullOrEmpty(f)) return null;
        var counts = Molecules.ParseFormula(f);
        if (counts.Count == 1)
        {
            // Один элемент (кусочек металла) — это один атом в зоне.
            foreach (var kv in counts) Atom.Spawn(Elements.BySymbol(kv.Key), Lab.ZoneCenter + Random.insideUnitSphere * 0.8f);
            if (Lab.I != null) Lab.I.Recompute();
        }
        else Presets.SpawnPopular(new Presets.Pop { Formula = f, Ru = f, En = f });
        return f;
    }

    /// <summary>Строка состояния выбранной вещи для пульта.</summary>
    public string Describe(Item it)
    {
        if (it == null) return Lang.T("Возьми вещь с полки, потом перетащи её в посуду.", "Take something from the shelf, then drag it into a vessel.");
        var sb = new System.Text.StringBuilder(it.Name);
        if (it.IsVessel)
        {
            var parts = new List<string>();
            if (it.HasWater) parts.Add(Lang.T("вода", "water"));
            foreach (var p in it.Contents) parts.Add(p.Formula + (p.Phase == Phase.Solid ? Lang.T(" (на дне)", " (at the bottom)")
                                                  : (p.Amount > 1 ? Lang.T(" (в растворе, хватит на ", " (dissolved, enough for ") + p.Amount + ")" : Lang.T(" (в растворе)", " (dissolved)"))));
            sb.Append(": ").Append(parts.Count == 0 ? Lang.T("пусто", "empty") : string.Join(", ", parts.ToArray()));
            if (it.HasWater) sb.Append(IsAcidic(it) ? Lang.T(" · кислая", " · acidic") : IsAlkaline(it) ? Lang.T(" · щелочная", " · alkaline") : Lang.T(" · нейтральная", " · neutral"));
            if (it.Indicator) sb.Append(Lang.T(" · с фенолфталеином", " · with phenolphthalein"));
        }
        else if (!string.IsNullOrEmpty(it.Def.Formula)) sb.Append(" (").Append(it.Def.Formula).Append(")");
        return sb.ToString();
    }
}
