using System.Collections.Generic;
using UnityEngine;

/// <summary>НАСТОЯЩИЕ ПРАВИЛА РЕАКЦИЙ. 🔴 21.09, владелец: «добавь реалистичные реакции химии,
/// чтобы свойства всех элементов в цепи учитывались».
///
/// До этого «реакция» была арифметикой состава: хватило атомов на вещество — оно и собралось.
/// Теперь реакция идёт по СВОЙСТВАМ, и вещество может не получиться, даже когда атомов вдоволь.
///
/// Что именно решает:
///   • РЯД АКТИВНОСТИ МЕТАЛЛОВ (Li K Ba Ca Na Mg Al Mn Zn Cr Fe Ni Sn Pb — H — Cu Hg Ag Pt Au).
///     Цинк вытесняет водород из кислоты, а медь — нет, потому что стоит ПОСЛЕ водорода. Это
///     не выдумка игры: на том же ряду стоит вся школьная химия вытеснения.
///   • КЛАСС ВЕЩЕСТВА: кислота, щёлочь, соль, оксид, углеводород — правило выбирается по нему.
///   • ЭЛЕКТРООТРИЦАТЕЛЬНОСТЬ: чем больше разница у пары металл–неметалл, тем злее прямой
///     синтез (натрий с хлором — вспышка, олово с серой — вяло).
///   • ЗАРЯДЫ ИОНОВ: формула соли не выдумывается, а считается из зарядов. Цинк +2 и хлор −1
///     дают ZnCl2, алюминий +3 и кислотный остаток SO4 −2 дают Al2(SO4)3 — три на два.
///   • НЕДОСТАТОК РЕАГЕНТА: если кислоты не хватило на весь металл, прореагирует столько,
///     на сколько хватило, остальное останется лежать. Так и в пробирке.
///
/// 🔴 Чего здесь нет: температуры, концентрации, растворителя, скорости и обратимых реакций.
/// Пассивацию (алюминий в холодной концентрированной азотной) тоже не считаем. Это школьный
/// уровень «что с чем реагирует», а не расчёт опыта.</summary>
public static class Reactions
{
    // ==================== свойства ====================

    /// <summary>Ряд активности. Чем меньше число, тем активнее металл. Водород — рубеж: всё,
    /// что левее, вытесняет его из кислот.</summary>
    static readonly string[] ACTIVITY =
    {
        "Li", "K", "Ba", "Ca", "Na", "Mg", "Al", "Mn", "Zn", "Cr", "Fe", "Ni", "Sn", "Pb",
        "H",
        "Cu", "Hg", "Ag", "Pt", "Au"
    };

    public static int Activity(string sym)
    {
        for (int i = 0; i < ACTIVITY.Length; i++) if (ACTIVITY[i] == sym) return i;
        return -1;                        // металла нет в ряду — правило вытеснения к нему не применяем
    }

    public static int HydrogenRank { get { return Activity("H"); } }

    /// <summary>Активность галогенов: фтор вытесняет хлор, хлор — бром, бром — иод.</summary>
    static int HalogenRank(string sym)
    {
        switch (sym) { case "F": return 0; case "Cl": return 1; case "Br": return 2; case "I": return 3; }
        return -1;
    }

    static bool IsMetal(Elements.El el)
    {
        return el != null && (el.Class == Elements.Cls.Alkali || el.Class == Elements.Cls.AlkEarth ||
                              el.Class == Elements.Cls.Transition || el.Class == Elements.Cls.PostMetal ||
                              el.Class == Elements.Cls.Lanth || el.Class == Elements.Cls.Actin);
    }

    // ==================== ионы ====================

    /// <summary>Кислотный остаток: состав и заряд. Из них и собирается формула соли.</summary>
    public class Anion
    {
        public string Name;
        public Dictionary<string, int> Comp;
        public int Charge;                 // по модулю; знак минус подразумевается
    }

    static readonly List<Anion> ANIONS = new List<Anion>
    {
        New("SO4", 2, "S", 1, "O", 4),
        New("CO3", 2, "C", 1, "O", 3),
        // 21.09: гидрокарбонат (пищевая сода NaHCO3) и остатки органических кислот — уксусной
        // и муравьиной. Без них сода с уксусом, самый известный опыт на кухне, не шёл.
        New("HCO3", 1, "H", 1, "C", 1, "O", 3),
        New("CH3COO", 1, "C", 2, "H", 3, "O", 2),
        New("HCOO", 1, "C", 1, "H", 1, "O", 2),
        New("PO4", 3, "P", 1, "O", 4),
        New("NO3", 1, "N", 1, "O", 3),
        New("OH",  1, "O", 1, "H", 1),
        New("Cl",  1, "Cl", 1),
        New("Br",  1, "Br", 1),
        New("I",   1, "I", 1),
        New("F",   1, "F", 1),
        New("S",   2, "S", 1),
        New("O",   2, "O", 1),
    };

    static Anion New(string name, int charge, params object[] pairs)
    {
        var c = new Dictionary<string, int>();
        for (int i = 0; i < pairs.Length; i += 2) c[(string)pairs[i]] = (int)pairs[i + 1];
        return new Anion { Name = name, Charge = charge, Comp = c };
    }

    /// <summary>Растворима ли соль в воде — по школьной таблице растворимости, упрощённо:
    /// соли натрия, калия, аммония и все нитраты растворимы; хлориды — кроме серебра и
    /// свинца; сульфаты — кроме бария, кальция и свинца; карбонаты и фосфаты — только у
    /// натрия и калия; гидрокарбонаты и ацетаты растворимы.</summary>
    public static bool Soluble(string metal, string residue)
    {
        if (metal == "Na" || metal == "K" || metal == "Li") return true;
        switch (residue)
        {
            case "NO3": case "HCO3": case "CH3COO": case "HCOO": return true;
            case "Cl": case "Br": case "I": return metal != "Ag" && metal != "Pb";
            case "SO4": return metal != "Ba" && metal != "Ca" && metal != "Pb" && metal != "Sr";
            case "F": return metal != "Ca" && metal != "Mg";
            default: return false;           // CO3, PO4, S — у остальных металлов нерастворимы
        }
    }

    public static Anion AnionByName(string n)
    {
        foreach (var a in ANIONS) if (a.Name == n) return a;
        return null;
    }

    // ==================== разбор вещества ====================

    public enum Kind { Element, Acid, Base, Salt, Oxide, Water, Hydrocarbon, Other }

    public class Species
    {
        public Lab.Mol Mol;
        public Dictionary<string, int> Comp;
        public Kind Kind;
        public string Metal;               // для соли, щёлочи, оксида металла
        public Anion Residue;              // кислотный остаток
        public string Formula;
    }

    static Dictionary<string, int> CompOf(Lab.Mol m)
    {
        var c = new Dictionary<string, int>();
        foreach (var a in m.Atoms)
        {
            int n; c.TryGetValue(a.El.Sym, out n);
            c[a.El.Sym] = n + 1;
        }
        return c;
    }

    /// <summary>Что это за вещество. Порядок проверок важен: вода — прежде оксида, щёлочь —
    /// прежде соли, иначе NaOH опознался бы как соль с остатком O.</summary>
    public static Species Classify(Lab.Mol m)
    {
        var comp = CompOf(m);
        var sp = new Species { Mol = m, Comp = comp, Formula = m.Formula, Kind = Kind.Other };

        if (comp.Count == 1) { sp.Kind = Kind.Element; return sp; }

        if (Same(comp, "H", 2, "O", 1)) { sp.Kind = Kind.Water; return sp; }

        // Углеводород: только углерод и водород.
        if (comp.Count == 2 && comp.ContainsKey("C") && comp.ContainsKey("H")) { sp.Kind = Kind.Hydrocarbon; return sp; }

        string metal = null;
        foreach (var kv in comp)
        {
            var el = Elements.BySymbol(kv.Key);
            if (IsMetal(el)) { metal = kv.Key; break; }
        }

        // Щёлочь: металл плюс целое число групп OH.
        if (metal != null && comp.ContainsKey("O") && comp.ContainsKey("H") && comp.Count == 3 &&
            comp["O"] == comp["H"])
        {
            sp.Kind = Kind.Base; sp.Metal = metal; sp.Residue = AnionByName("OH"); return sp;
        }

        // Кислота: водород плюс известный остаток, металла нет.
        if (metal == null && comp.ContainsKey("H"))
        {
            var rest = new Dictionary<string, int>(comp);
            int hCount = rest["H"]; rest.Remove("H");
            foreach (var an in ANIONS)
            {
                if (an.Name == "OH") continue;
                if (hCount == an.Charge && SameComp(rest, an.Comp))
                {
                    sp.Kind = Kind.Acid; sp.Residue = an; return sp;
                }
                // Остаток сам содержит водород (уксусная: CH3COO + H). Тогда отнимать надо не
                // весь водород, а ровно столько, сколько у остатка заряд.
                if (an.Comp.ContainsKey("H") && an.Name != "HCO3")
                {
                    var need = new Dictionary<string, int>(an.Comp);
                    need["H"] += an.Charge;
                    if (SameComp(comp, need)) { sp.Kind = Kind.Acid; sp.Residue = an; return sp; }
                }
            }
        }

        // Соль: металл плюс известный остаток в целом числе.
        if (metal != null)
        {
            var rest = new Dictionary<string, int>(comp);
            int mCount = rest[metal]; rest.Remove(metal);
            foreach (var an in ANIONS)
            {
                int times;
                if (Divides(an.Comp, rest, out times) && times > 0)
                {
                    var el = Elements.BySymbol(metal);
                    if (el != null && mCount * el.Valence == times * an.Charge)
                    {
                        sp.Metal = metal;
                        sp.Residue = an;
                        sp.Kind = (an.Name == "O") ? Kind.Oxide : Kind.Salt;
                        return sp;
                    }
                }
            }
        }

        // Оксид неметалла: элемент плюс кислород.
        if (metal == null && comp.ContainsKey("O") && comp.Count == 2) { sp.Kind = Kind.Oxide; return sp; }

        return sp;
    }

    static bool Same(Dictionary<string, int> c, params object[] pairs)
    {
        if (c.Count != pairs.Length / 2) return false;
        for (int i = 0; i < pairs.Length; i += 2)
        {
            int n;
            if (!c.TryGetValue((string)pairs[i], out n) || n != (int)pairs[i + 1]) return false;
        }
        return true;
    }

    static bool SameComp(Dictionary<string, int> a, Dictionary<string, int> b)
    {
        if (a.Count != b.Count) return false;
        foreach (var kv in a) { int n; if (!b.TryGetValue(kv.Key, out n) || n != kv.Value) return false; }
        return true;
    }

    /// <summary>Сколько раз состав «часть» укладывается в состав «целое» без остатка.</summary>
    static bool Divides(Dictionary<string, int> part, Dictionary<string, int> whole, out int times)
    {
        times = 0;
        if (part.Count != whole.Count) return false;
        int t = -1;
        foreach (var kv in part)
        {
            int n;
            if (!whole.TryGetValue(kv.Key, out n)) return false;
            if (n % kv.Value != 0) return false;
            int k = n / kv.Value;
            if (t < 0) t = k; else if (t != k) return false;
        }
        times = t;
        return t > 0;
    }

    // ==================== формула соли по зарядам ====================

    /// <summary>Состав соли из металла и остатка: заряды сводятся наименьшим общим кратным.
    /// Zn(+2) и Cl(−1) дают ZnCl2, Al(+3) и SO4(−2) — Al2(SO4)3.</summary>
    public static Dictionary<string, int> SaltComp(string metal, Anion an, int units)
    {
        var el = Elements.BySymbol(metal);
        int v = Mathf.Max(1, el != null ? el.Valence : 1);
        int lcm = Lcm(v, an.Charge);
        int nMetal = lcm / v, nAn = lcm / an.Charge;

        var res = new Dictionary<string, int>();
        Add(res, metal, nMetal * units);
        foreach (var kv in an.Comp) Add(res, kv.Key, kv.Value * nAn * units);
        return res;
    }

    static int Lcm(int a, int b) { return a / Gcd(a, b) * b; }
    static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return a; }

    public static void Add(Dictionary<string, int> d, string k, int n)
    {
        int prev; d.TryGetValue(k, out prev);
        d[k] = prev + n;
    }
}
