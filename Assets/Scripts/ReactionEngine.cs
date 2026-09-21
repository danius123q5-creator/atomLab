using System.Collections.Generic;
using UnityEngine;

/// <summary>Движок реакций: смотрит, ЧТО лежит в зоне, выбирает подходящее правило и проводит
/// реакцию по-настоящему — с коэффициентами, недостатком реагента и отказом, когда свойства
/// не позволяют. 🔴 21.09, владелец: «чтобы свойства всех элементов в цепи учитывались».
///
/// Цепь работает так: после каждой реакции состав зоны пересчитывается, и движок ищет
/// следующее правило уже для НОВЫХ веществ. Натрий с водой даёт щёлочь, щёлочь встречает
/// кислоту и даёт соль, соль встречает более активный металл — и так до упора. До восьми
/// шагов за нажатие, каждый со своим объяснением.</summary>
public static class ReactionEngine
{
    public class Step
    {
        public string Text;            // «Zn + 2HCl -> ZnCl2 + H2 (цинк активнее водорода)»
        public float Violence;         // 0..1 — насколько бурно
        public Vector3 Where;
    }

    /// <summary>Несостоявшиеся реакции: почему свойства не позволили. Это ровно то, ради чего
    /// всё затевалось — «медь с соляной кислотой не реагирует» и есть знание.</summary>
    public static readonly List<string> Refusals = new List<string>();

    class Group
    {
        public Reactions.Species Sample;
        public List<Lab.Mol> Mols = new List<Lab.Mol>();
        public int Count { get { return Mols.Count; } }
    }

    static List<Group> Survey(Lab lab)
    {
        lab.Recompute();
        var groups = new List<Group>();
        foreach (var m in lab.Mols)
        {
            var sp = Reactions.Classify(m);
            Group g = null;
            foreach (var x in groups)
                if (x.Sample.Formula == sp.Formula && x.Sample.Kind == sp.Kind) { g = x; break; }
            if (g == null) { g = new Group { Sample = sp }; groups.Add(g); }
            g.Mols.Add(m);
        }
        return groups;
    }

    // ==================== одна попытка ====================

    /// <summary>Провести одну реакцию, если есть подходящая пара. Возвращает описание или null.</summary>
    public static Step TryOne(Lab lab)
    {
        var groups = Survey(lab);

        for (int i = 0; i < groups.Count; i++)
            for (int j = 0; j < groups.Count; j++)
            {
                if (i == j) continue;
                var s = Apply(lab, groups[i], groups[j]);
                if (s != null) return s;
            }
        return null;
    }

    static Step Apply(Lab lab, Group A, Group B)
    {
        var a = A.Sample;
        var b = B.Sample;

        // ---------- кислота + щёлочь -> соль + вода ----------
        if (a.Kind == Reactions.Kind.Acid && b.Kind == Reactions.Kind.Base)
        {
            var metal = Elements.BySymbol(b.Metal);
            int v = Mathf.Max(1, metal.Valence), ch = a.Residue.Charge;
            int g = Gcd(v, ch);
            int acidUnits = v / g, baseUnits = ch / g;

            var salt = new Dictionary<string, int>();
            Reactions.Add(salt, b.Metal, ch / g);
            foreach (var kv in a.Residue.Comp) Reactions.Add(salt, kv.Key, kv.Value * v / g);
            var water = new Dictionary<string, int> { { "H", 2 }, { "O", 1 } };

            return Run(lab, A, acidUnits, B, baseUnits,
                new[] { salt, water }, new[] { 1, ch * v / g },
                Lang.T("нейтрализация: кислота отдаёт H, щёлочь — OH, из них выходит вода, остальное — соль", "neutralisation: the acid gives H, the base gives OH, they make water, the rest is salt"),
                0.45f);
        }

        // ---------- металл + кислота -> соль + водород ----------
        if (a.Kind == Reactions.Kind.Element && b.Kind == Reactions.Kind.Acid)
        {
            string sym = null;
            foreach (var kv in a.Comp) sym = kv.Key;
            var el = Elements.BySymbol(sym);
            int rank = Reactions.Activity(sym);

            if (el != null && rank >= 0)
            {
                if (rank > Reactions.HydrogenRank)
                {
                    Refuse(sym + Lang.T(" стоит ПОСЛЕ водорода в ряду активности — водород из кислоты он не вытесняет. ", " is AFTER hydrogen in the activity series — it cannot push hydrogen out of an acid. ") +
                           Lang.T("Поэтому медной ложкой и можно мешать соляную кислоту.", "That is why you can stir hydrochloric acid with a copper spoon."));
                    return null;
                }

                int v = Mathf.Max(1, el.Valence), ch = b.Residue.Charge;
                int mult = ((ch * v) % 2 == 0) ? 1 : 2;      // водород выходит парами: H2
                int metalUnits = ch * mult, acidUnits = v * mult;

                var salt = new Dictionary<string, int>();
                Reactions.Add(salt, sym, ch * mult);
                foreach (var kv in b.Residue.Comp) Reactions.Add(salt, kv.Key, kv.Value * v * mult);
                var h2 = new Dictionary<string, int> { { "H", 2 } };

                return Run(lab, A, metalUnits, B, acidUnits,
                    new[] { salt, h2 }, new[] { 1, ch * v * mult / 2 },
                    sym + Lang.T(" активнее водорода (ряд активности) — вытесняет его из кислоты", " is more active than hydrogen (activity series) — it pushes it out of the acid"),
                    0.75f);
            }
        }

        // ---------- активный металл + вода -> щёлочь + водород ----------
        if (a.Kind == Reactions.Kind.Element && b.Kind == Reactions.Kind.Water)
        {
            string sym = null;
            foreach (var kv in a.Comp) sym = kv.Key;
            var el = Elements.BySymbol(sym);
            int rank = Reactions.Activity(sym);

            if (el != null && rank >= 0 && rank <= Reactions.Activity("Na"))
            {
                int v = Mathf.Max(1, el.Valence);
                int mult = (v % 2 == 0) ? 1 : 2;
                int metalUnits = mult, waterUnits = v * mult;

                var baseComp = new Dictionary<string, int>();
                Reactions.Add(baseComp, sym, 1);
                Reactions.Add(baseComp, "O", v);
                Reactions.Add(baseComp, "H", v);
                var h2 = new Dictionary<string, int> { { "H", 2 } };

                return Run(lab, A, metalUnits, B, waterUnits,
                    new[] { baseComp, h2 }, new[] { mult, v * mult / 2 },
                    sym + Lang.T(" — активный металл, он рвёт воду: забирает OH и отпускает водород", " is an active metal, it tears water apart: takes OH, releases hydrogen"),
                    1f);
            }
            if (el != null && rank > Reactions.Activity("Mg"))
                Refuse(sym + Lang.T(" с холодной водой не реагирует: в ряду активности он слишком правый.", " does not react with cold water: it sits too far right in the activity series."));
        }

        // ---------- углеводород + кислород -> углекислый газ + вода ----------
        if (a.Kind == Reactions.Kind.Hydrocarbon && b.Kind == Reactions.Kind.Element && b.Comp.ContainsKey("O"))
        {
            int x = a.Comp["C"], y = a.Comp["H"];
            int o2 = b.Comp["O"] / 2;
            if (o2 >= 1)
            {
                // 4 CxHy + (4x + y) O2 -> 4x CO2 + 2y H2O — коэффициенты без дробей.
                int fuelUnits = 4, oxyUnits = 4 * x + y;
                int g = Gcd(Gcd(fuelUnits, oxyUnits), Gcd(4 * x, 2 * y));
                var co2 = new Dictionary<string, int> { { "C", 1 }, { "O", 2 } };
                var water = new Dictionary<string, int> { { "H", 2 }, { "O", 1 } };
                return Run(lab, A, fuelUnits / g, B, oxyUnits / g,
                    new[] { co2, water }, new[] { 4 * x / g, 2 * y / g },
                    Lang.T("горение: углерод уходит в углекислый газ, водород — в воду", "combustion: carbon goes into carbon dioxide, hydrogen into water"),
                    1f);
            }
        }

        // ---------- оксид + вода ----------
        if (a.Kind == Reactions.Kind.Oxide && b.Kind == Reactions.Kind.Water)
        {
            // Оксид активного металла даёт щёлочь.
            if (a.Metal != null)
            {
                int rank = Reactions.Activity(a.Metal);
                if (rank >= 0 && rank <= Reactions.Activity("Ca"))
                {
                    var el = Elements.BySymbol(a.Metal);
                    int v = Mathf.Max(1, el.Valence);
                    int nMetal = a.Comp[a.Metal];
                    var baseComp = new Dictionary<string, int>();
                    Reactions.Add(baseComp, a.Metal, 1);
                    Reactions.Add(baseComp, "O", v);
                    Reactions.Add(baseComp, "H", v);
                    return Run(lab, A, 1, B, v * nMetal / 2 > 0 ? v * nMetal / 2 : 1,
                        new[] { baseComp }, new[] { nMetal },
                        Lang.T("оксид активного металла с водой даёт щёлочь — известь так и гасят", "an active metal oxide with water gives a base — that is how lime is slaked"),
                        0.55f);
                }
                Refuse(Lang.T("оксид ", "oxide of ") + a.Metal + Lang.T(" с водой не реагирует: этот металл недостаточно активен.", " does not react with water: the metal is not active enough."));
            }
            else
            {
                // Оксид неметалла даёт кислоту: SO3 -> H2SO4, CO2 -> H2CO3.
                string other = null;
                foreach (var kv in a.Comp) if (kv.Key != "O") other = kv.Key;
                int nO = a.Comp["O"], nX = a.Comp[other];
                var acid = new Dictionary<string, int>();
                Reactions.Add(acid, "H", 2);
                Reactions.Add(acid, other, nX);
                Reactions.Add(acid, "O", nO + 1);
                var probe = Molecules.LookupByComposition(acid);
                if (probe != null)
                    return Run(lab, A, 1, B, 1, new[] { acid }, new[] { 1 },
                        Lang.T("оксид неметалла с водой даёт кислоту — так и рождаются кислотные дожди", "a nonmetal oxide with water gives an acid — this is how acid rain forms"),
                        0.5f);
            }
        }

        // ---------- вытеснение металла из соли ----------
        if (a.Kind == Reactions.Kind.Element && b.Kind == Reactions.Kind.Salt)
        {
            string sym = null;
            foreach (var kv in a.Comp) sym = kv.Key;
            int r1 = Reactions.Activity(sym), r2 = Reactions.Activity(b.Metal);
            if (r1 >= 0 && r2 >= 0 && sym != b.Metal)
            {
                if (r1 < r2)
                {
                    var el1 = Elements.BySymbol(sym);
                    var el2 = Elements.BySymbol(b.Metal);
                    int v1 = Mathf.Max(1, el1.Valence), v2 = Mathf.Max(1, el2.Valence);
                    int g = Gcd(v1, v2);
                    int newSaltUnits = v2 / g, oldSaltUnits = v1 / g;

                    var newSalt = Reactions.SaltComp(sym, b.Residue, 1);
                    var freed = new Dictionary<string, int> { { b.Metal, 1 } };
                    return Run(lab, A, b.Residue.Charge * v2 / g / Mathf.Max(1, v1) > 0 ? v2 / g : 1,
                               B, oldSaltUnits * b.Comp[b.Metal] > 0 ? oldSaltUnits : 1,
                        new[] { newSalt, freed }, new[] { newSaltUnits, oldSaltUnits * b.Comp[b.Metal] },
                        sym + Lang.T(" активнее, чем ", " is more active than ") + b.Metal + Lang.T(" — и выбивает его из соли", " — and knocks it out of the salt"),
                        0.6f);
                }
                Refuse(sym + Lang.T(" менее активен, чем ", " is less active than ") + b.Metal + Lang.T(" — из соли его не вытеснит. ", " — it cannot push it out of the salt. ") +
                       Lang.T("Серебро медь из раствора не выгонит, а медь серебро — выгонит.", "Silver will not push copper out of a solution, but copper will push silver out."));
            }
        }

        // ---------- прямой синтез: металл + неметалл ----------
        if (a.Kind == Reactions.Kind.Element && b.Kind == Reactions.Kind.Element)
        {
            string s1 = null, s2 = null;
            foreach (var kv in a.Comp) s1 = kv.Key;
            foreach (var kv in b.Comp) s2 = kv.Key;
            var e1 = Elements.BySymbol(s1);
            var e2 = Elements.BySymbol(s2);
            if (e1 != null && e2 != null && e1.Valence > 0 && e2.Valence > 0)
            {
                bool m1 = Reactions.Activity(s1) >= 0;
                bool m2 = Reactions.Activity(s2) >= 0;
                var an = Reactions.AnionByName(s2);
                if (m1 && !m2 && an != null)
                {
                    float dEN = Mathf.Abs(e1.EN - e2.EN);
                    var salt = Reactions.SaltComp(s1, an, 1);
                    int nMetal = salt[s1];
                    int nOther = salt[s2];
                    return Run(lab, A, nMetal, B, nOther, new[] { salt }, new[] { 1 },
                        Lang.T("прямой синтез: разница электроотрицательностей ", "direct synthesis: electronegativity difference ") + dEN.ToString("0.0") +
                        (dEN >= 1.7f ? Lang.T(" — связь ионная, реакция бурная", " — ionic bond, violent reaction") : Lang.T(" — связь ковалентная, реакция спокойная", " — covalent bond, calm reaction")),
                        Mathf.Clamp01(dEN / 3f));
                }
            }
        }

        return null;
    }

    static void Refuse(string why)
    {
        foreach (var r in Refusals) if (r == why) return;
        Refusals.Add(why);
    }

    static int Gcd(int a, int b) { a = Mathf.Abs(a); b = Mathf.Abs(b); while (b != 0) { int t = a % b; a = b; b = t; } return Mathf.Max(1, a); }

    // ==================== проведение ====================

    /// <summary>Провести реакцию: забрать нужное число молекул каждого реагента, разобрать их
    /// на атомы и собрать продукты. Сколько хватило — столько и прореагирует: недостаток
    /// реагента здесь настоящий, остаток просто останется лежать.</summary>
    static Step Run(Lab lab, Group A, int coefA, Group B, int coefB,
                    Dictionary<string, int>[] products, int[] productCounts,
                    string why, float violence)
    {
        if (coefA < 1 || coefB < 1) return null;
        int k = Mathf.Min(A.Count / coefA, B.Count / coefB);
        if (k < 1) return null;

        var taken = new List<Lab.Mol>();
        for (int i = 0; i < coefA * k; i++) taken.Add(A.Mols[i]);
        for (int i = 0; i < coefB * k; i++) taken.Add(B.Mols[i]);

        // Разбираем взятое на атомы.
        var pool = new Dictionary<string, List<Atom>>();
        Vector3 where = Vector3.zero;
        int n = 0;
        foreach (var m in taken)
            foreach (var at in m.Atoms)
            {
                for (int i = at.Bonds.Count - 1; i >= 0; i--) at.Bonds[i].Break();
                List<Atom> l;
                if (!pool.TryGetValue(at.El.Sym, out l)) { l = new List<Atom>(); pool[at.El.Sym] = l; }
                l.Add(at);
                where += at.transform.position; n++;
            }
        if (n == 0) return null;
        where /= n;

        // Собираем продукты. Если на какой-то не хватило атомов — останавливаемся честно.
        var text = new System.Text.StringBuilder();
        text.Append(Formula(A.Sample.Comp, coefA * k)).Append(" + ").Append(Formula(B.Sample.Comp, coefB * k)).Append(" → ");

        int spot = 0;
        bool first = true;
        for (int p = 0; p < products.Length; p++)
        {
            int count = productCounts[p] * k;
            for (int c = 0; c < count; c++)
            {
                var need = products[p];
                var atoms = new List<Atom>();
                bool ok = true;
                foreach (var kv in need)
                {
                    List<Atom> have;
                    if (!pool.TryGetValue(kv.Key, out have) || have.Count < kv.Value) { ok = false; break; }
                    for (int i = 0; i < kv.Value; i++)
                    {
                        atoms.Add(have[have.Count - 1]);
                        have.RemoveAt(have.Count - 1);
                    }
                }
                if (!ok)
                {
                    foreach (var at in atoms) pool[at.El.Sym].Add(at);   // вернули, что взяли
                    break;
                }

                float ang = spot * 1.9f;
                // Разносим продукты подальше: рядом стоящие молекулы расталкивают друг друга
                // и рвут собственные связи.
                Vector3 c2 = where + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang * 1.4f) * 0.5f, Mathf.Sin(ang)) * (1.9f + spot * 0.8f);
                Chemistry.Assemble(atoms, ClampToZone(c2));
                Fx.Sparks(c2, new Color(0.7f, 1f, 0.85f), 22, 2.6f);
                spot++;
            }
            if (!first) text.Append(" + ");
            text.Append(Formula(products[p], count));
            first = false;
        }

        // Что не вошло — разлетается свободными атомами.
        foreach (var kv in pool)
            foreach (var at in kv.Value)
            {
                at.Body.linearVelocity = Random.onUnitSphere * 2f;
            }

        Fx.Boom();
        Fx.Flash(where, Color.Lerp(new Color(0.6f, 1f, 0.8f), new Color(1f, 0.7f, 0.3f), violence),
                 4f + 10f * violence, 8f + 8f * violence, 0.5f + 0.4f * violence);
        Fx.Sparks(where, new Color(1f, 0.9f, 0.6f), Mathf.RoundToInt(30 + 110 * violence), 4f + 6f * violence);

        lab.Recompute();
        return new Step { Text = text.ToString() + "   (" + why + ")", Violence = violence, Where = where };
    }

    static Vector3 ClampToZone(Vector3 p)
    {
        return new Vector3(
            Mathf.Clamp(p.x, Lab.ZoneCenter.x - 4f, Lab.ZoneCenter.x + 4f),
            Mathf.Clamp(p.y, Lab.ZoneCenter.y - 2f, Lab.ZoneCenter.y + 2f),
            Mathf.Clamp(p.z, Lab.ZoneCenter.z - 3f, Lab.ZoneCenter.z + 3f));
    }

    static string Formula(Dictionary<string, int> comp, int times)
    {
        var info = Molecules.LookupByComposition(comp);
        string f = info != null ? info.Formula : Molecules.Formula(comp);
        return (times > 1 ? times.ToString() : "") + f;
    }
}
