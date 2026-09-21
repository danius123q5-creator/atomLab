using System.Collections.Generic;
using UnityEngine;

/// <summary>Имя веществу, которого нет в справочнике, — по правилам химической номенклатуры.
///
/// 🔴 21.09, владелец: «игра не даёт создать некоторые соединения» — на снимке FeO4S и надпись
/// «такого вещества в справочнике нет — в природе такая связка не живёт». Вторая половина была
/// НЕПРАВДОЙ: FeSO4 — железный купорос, его продают в садовом магазине. Справочник игры — 92
/// вещества, а настоящих соединений миллионы. Отсутствие в моём списке доказывало только
/// отсутствие в моём списке.
///
/// Теперь игра сама разбирает собранное: если это соль, оксид, кислота или щёлочь, заряды
/// которых сходятся, — называет по правилам: «сульфат железа(II)», «хлорид алюминия». Римская
/// цифра ставится там, где у металла больше одной степени окисления: у железа их две, и без
/// цифры не понять, о каком из двух сульфатов речь.
///
/// Если заряды не сходятся — игра говорит «в справочнике нет, и заряды не сходятся», а не «в
/// природе не бывает». Первое она проверила. Второго она знать не может.</summary>
public static class Naming
{
    /// <summary>Обычные степени окисления металлов. У большинства одна — берём игровую
    /// валентность; здесь только те, у кого их несколько.</summary>
    static readonly Dictionary<string, int[]> STATES = new Dictionary<string, int[]>
    {
        { "Fe", new[] { 2, 3 } }, { "Cu", new[] { 1, 2 } }, { "Cr", new[] { 2, 3, 6 } },
        { "Mn", new[] { 2, 4, 7 } }, { "Co", new[] { 2, 3 } }, { "Sn", new[] { 2, 4 } },
        { "Pb", new[] { 2, 4 } }, { "Hg", new[] { 1, 2 } }, { "Au", new[] { 1, 3 } },
        { "Ti", new[] { 2, 3, 4 } }, { "V", new[] { 2, 3, 4, 5 } }, { "Pt", new[] { 2, 4 } },
        { "Ni", new[] { 2, 3 } }, { "Tl", new[] { 1, 3 } }, { "Ce", new[] { 3, 4 } },
    };

    public static int[] StatesOf(string metal)
    {
        int[] s;
        if (STATES.TryGetValue(metal, out s)) return s;
        var el = Elements.BySymbol(metal);
        return new[] { Mathf.Max(1, el != null ? el.Valence : 1) };
    }

    public static bool HasManyStates(string metal) { return STATES.ContainsKey(metal); }

    static readonly Dictionary<string, string[]> ANION_NAMES = new Dictionary<string, string[]>
    {
        { "SO4", new[] { "сульфат", "sulfate" } },
        { "CO3", new[] { "карбонат", "carbonate" } },
        { "PO4", new[] { "фосфат", "phosphate" } },
        { "NO3", new[] { "нитрат", "nitrate" } },
        { "OH",  new[] { "гидроксид", "hydroxide" } },
        { "Cl",  new[] { "хлорид", "chloride" } },
        { "Br",  new[] { "бромид", "bromide" } },
        { "I",   new[] { "иодид", "iodide" } },
        { "F",   new[] { "фторид", "fluoride" } },
        { "S",   new[] { "сульфид", "sulfide" } },
        { "O",   new[] { "оксид", "oxide" } },
    };

    static readonly Dictionary<string, string[]> ACID_NAMES = new Dictionary<string, string[]>
    {
        { "SO4", new[] { "серная кислота", "sulfuric acid" } },
        { "CO3", new[] { "угольная кислота", "carbonic acid" } },
        { "PO4", new[] { "фосфорная кислота", "phosphoric acid" } },
        { "NO3", new[] { "азотная кислота", "nitric acid" } },
        { "Cl",  new[] { "соляная кислота", "hydrochloric acid" } },
        { "Br",  new[] { "бромоводородная кислота", "hydrobromic acid" } },
        { "I",   new[] { "иодоводородная кислота", "hydroiodic acid" } },
        { "F",   new[] { "плавиковая кислота", "hydrofluoric acid" } },
        { "S",   new[] { "сероводород", "hydrogen sulfide" } },
    };

    /// <summary>Родительный падеж названия металла: «железо» → «железа», «натрий» → «натрия».
    /// Правило по окончанию закрывает почти все элементы; неправильные — списком.</summary>
    static string Genitive(string name)
    {
        string n = name.ToLower();
        switch (n)
        {
            case "медь": return "меди";
            case "ртуть": return "ртути";
            case "свинец": return "свинца";
            case "марганец": return "марганца";
        }
        if (n.EndsWith("ий")) return n.Substring(0, n.Length - 2) + "ия";
        if (n.EndsWith("о")) return n.Substring(0, n.Length - 1) + "а";
        if (n.EndsWith("ь")) return n.Substring(0, n.Length - 1) + "я";
        if (n.EndsWith("а")) return n.Substring(0, n.Length - 1) + "ы";
        return n + "а";
    }

    static string Roman(int n)
    {
        switch (n) { case 1: return "I"; case 2: return "II"; case 3: return "III"; case 4: return "IV";
                     case 5: return "V"; case 6: return "VI"; case 7: return "VII"; }
        return n.ToString();
    }

    public class Guess
    {
        public string Ru, En, Why;
        public bool Plausible;     // заряды сошлись — такое вещество по правилам возможно
    }

    /// <summary>Разобрать состав и назвать. null — если это вообще не похоже на ионное или
    /// кислотно-основное соединение (например, органика без справочника).</summary>
    public static Guess Describe(Dictionary<string, int> comp)
    {
        if (comp == null || comp.Count < 2) return null;

        // Металл в составе.
        string metal = null;
        foreach (var kv in comp)
        {
            var el = Elements.BySymbol(kv.Key);
            if (el != null && IsMetal(el)) { metal = kv.Key; break; }
        }

        if (metal != null)
        {
            var rest = new Dictionary<string, int>(comp);
            int m = rest[metal]; rest.Remove(metal);
            foreach (var an in AnionList())
            {
                int times;
                if (!Divides(an.Comp, rest, out times)) continue;
                int totalMinus = times * an.Charge;
                if (totalMinus % m != 0) continue;
                int state = totalMinus / m;
                bool ok = System.Array.IndexOf(StatesOf(metal), state) >= 0;

                var el = Elements.BySymbol(metal);
                string[] an2 = ANION_NAMES[an.Name];
                string suffix = HasManyStates(metal) ? "(" + Roman(state) + ")" : "";
                return new Guess
                {
                    Ru = an2[0] + " " + Genitive(el.Name) + suffix,
                    En = (el.NameEn ?? el.Name).ToLower() + suffix + " " + an2[1],
                    Plausible = ok,
                    Why = ok
                        ? Lang.T("заряды сходятся: " + metal + " +" + state + ", остаток " + an.Name + " −" + an.Charge,
                                 "charges balance: " + metal + " +" + state + ", " + an.Name + " −" + an.Charge)
                        : Lang.T("заряды не сходятся: " + metal + " пришлось бы стать +" + state + ", а так он не умеет",
                                 "charges do not balance: " + metal + " would need to be +" + state + ", which it cannot"),
                };
            }
        }
        else if (comp.ContainsKey("H"))
        {
            // Кислота: водород плюс остаток.
            var rest = new Dictionary<string, int>(comp);
            int h = rest["H"]; rest.Remove("H");
            foreach (var an in AnionList())
            {
                if (an.Name == "OH" || an.Name == "O") continue;
                string[] nm;
                if (!ACID_NAMES.TryGetValue(an.Name, out nm)) continue;
                int times;
                if (Divides(an.Comp, rest, out times) && times == 1 && h == an.Charge)
                    return new Guess { Ru = nm[0], En = nm[1], Plausible = true,
                        Why = Lang.T("водород +1 столько раз, сколько заряд остатка", "one hydrogen per unit of residue charge") };
            }
        }

        // Оксид неметалла: называем по числу атомов кислорода.
        if (metal == null && comp.ContainsKey("O") && comp.Count == 2)
        {
            string other = null;
            foreach (var kv in comp) if (kv.Key != "O") other = kv.Key;
            var el = Elements.BySymbol(other);
            if (el != null)
            {
                string[] pre = { "", "моно", "ди", "три", "тетра", "пента", "гекса", "гепта" };
                string[] preEn = { "", "mono", "di", "tri", "tetra", "penta", "hexa", "hepta" };
                int o = comp["O"];
                if (o < pre.Length)
                    return new Guess { Ru = pre[o] + "оксид " + Genitive(el.Name), En = (el.NameEn ?? el.Name).ToLower() + " " + preEn[o] + "oxide",
                                       Plausible = true, Why = Lang.T("оксид неметалла", "nonmetal oxide") };
            }
        }
        return null;
    }

    static bool IsMetal(Elements.El el)
    {
        return el.Class == Elements.Cls.Alkali || el.Class == Elements.Cls.AlkEarth ||
               el.Class == Elements.Cls.Transition || el.Class == Elements.Cls.PostMetal ||
               el.Class == Elements.Cls.Lanth || el.Class == Elements.Cls.Actin;
    }

    static IEnumerable<Reactions.Anion> AnionList()
    {
        foreach (var n in new[] { "SO4", "CO3", "PO4", "NO3", "OH", "Cl", "Br", "I", "F", "S", "O" })
        {
            var a = Reactions.AnionByName(n);
            if (a != null) yield return a;
        }
    }

    static bool Divides(Dictionary<string, int> part, Dictionary<string, int> whole, out int times)
    {
        times = 0;
        if (part.Count != whole.Count) return false;
        int t = -1;
        foreach (var kv in part)
        {
            int n;
            if (!whole.TryGetValue(kv.Key, out n) || n % kv.Value != 0) return false;
            int k = n / kv.Value;
            if (t < 0) t = k; else if (t != k) return false;
        }
        times = t;
        return t > 0;
    }
}
