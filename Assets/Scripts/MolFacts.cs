using System.Collections.Generic;
using UnityEngine;

/// <summary>Факты о собранной молекуле для карточки: размер, масса и — если так в жизни не
/// бывает — почему не держится и на что распалось бы.
///
/// 🔴 21.09, владелец: «добавь в табло под формулой размер молекулы. и напиши что не держится
/// атомы» (на скриншоте — C6H2F2Fe2Li2N2Ni2O7PPb2Ti2, собранная из пяти разных металлов).</summary>
public static class MolFacts
{
    /// <summary>Размер в нанометрах и масса. Игровые расстояния — не пикометры: радиусы шариков
    /// в игре схематические (0.14 + ковалентный/400). Поэтому переводим по самим связям
    /// молекулы: у каждой связи настоящая длина ≈ сумма ковалентных радиусов пары, отсюда
    /// масштаб «игровая единица → пикометры». Размер — самое большое расстояние между
    /// атомами плюс их радиусы. Это оценка по длинам связей, без облака электронов вокруг.</summary>
    public static string SizeLine(Lab.Mol m)
    {
        if (m == null || m.Atoms.Count == 0) return "";
        float mass = 0f;
        foreach (var a in m.Atoms) mass += a.El.Mass;

        float kSum = 0f; int kN = 0;
        foreach (var a in m.Atoms)
            foreach (var b in a.Bonds)
            {
                var o = b.Other(a);
                if (o == null || o.GetInstanceID() < a.GetInstanceID()) continue;   // каждую связь один раз
                float game = Vector3.Distance(a.transform.position, o.transform.position);
                if (game < 1e-3f) continue;
                kSum += (a.El.CovalentPm + o.El.CovalentPm) / game; kN++;
            }

        string massText = Lang.T("масса ", "mass ") + mass.ToString(mass < 100f ? "0.0" : "0") + Lang.T(" а.е.м.", " u");
        if (kN == 0) return massText;
        float k = kSum / kN;

        float best = 0f; Atom e1 = m.Atoms[0], e2 = m.Atoms[0];
        for (int i = 0; i < m.Atoms.Count; i++)
            for (int j = i + 1; j < m.Atoms.Count; j++)
            {
                float d = Vector3.Distance(m.Atoms[i].transform.position, m.Atoms[j].transform.position);
                if (d > best) { best = d; e1 = m.Atoms[i]; e2 = m.Atoms[j]; }
            }
        float pm = best * k + e1.El.CovalentPm + e2.El.CovalentPm;
        float nm = pm / 1000f;
        return Lang.T("Размер ≈ ", "Size ≈ ") + nm.ToString(nm < 1f ? "0.00" : "0.0") + Lang.T(" нм", " nm") + "  ·  " + massText;
    }

    static bool IsMetal(Elements.El el)
    {
        return el != null && (el.Class == Elements.Cls.Alkali || el.Class == Elements.Cls.AlkEarth ||
                              el.Class == Elements.Cls.Transition || el.Class == Elements.Cls.PostMetal ||
                              el.Class == Elements.Cls.Lanth || el.Class == Elements.Cls.Actin);
    }

    static int Gcd(int a, int b) { while (b != 0) { int t = a % b; a = b; b = t; } return Mathf.Max(1, a); }

    /// <summary>Не держится ли такое одной молекулой. Проверяем только то, что игра НЕ знает
    /// (вещества из справочника настоящие по определению), и только два признака, в которых
    /// химия однозначна:
    ///   • несколько РАЗНЫХ металлов в одной частице — в жизни это смесь солей и оксидов;
    ///   • связь металл–металл посреди молекулы — это уже сплав, а не молекула.
    /// Возвращает null, если сказать нечего.</summary>
    public static string Instability(Lab.Mol m)
    {
        if (m == null || m.Info != null) return null;
        if (Lab.Mode == Lab.Level.Fun) return null;                          // Фан: без занудства
        // ВУЗник и выше: радикал (свободные связи) — нестабилен сам по себе.
        string radical = (Lab.Mode >= Lab.Level.Uni && m.FreeLeft > 0 && m.Atoms.Count >= 2)
            ? Lang.T("свободных связей ", "free bonds ") + m.FreeLeft + Lang.T(" — это радикал: в жизни он живёт доли секунды и вцепляется в первое, что встретит",
                                                                              " — a radical: in reality it lives a fraction of a second and grabs the first thing it meets")
            : null;
        if (m.Atoms.Count < 3 && radical == null) return null;

        var metals = new List<string>();
        var have = new HashSet<string>();
        var cnt = new Dictionary<string, int>();
        foreach (var a in m.Atoms) { int c0; cnt.TryGetValue(a.El.Sym, out c0); cnt[a.El.Sym] = c0 + 1; }
        bool metalMetal = false;
        foreach (var a in m.Atoms)
        {
            have.Add(a.El.Sym);
            if (!IsMetal(a.El)) continue;
            if (!metals.Contains(a.El.Sym)) metals.Add(a.El.Sym);
            foreach (var b in a.Bonds) { var o = b.Other(a); if (o != null && IsMetal(o.El)) metalMetal = true; }
        }
        int metalAtoms = 0; foreach (var a in m.Atoms) if (IsMetal(a.El)) metalAtoms++;
        // 21.09 (скриншот владельца: C15H21Fe4N2O9P — четыре железа в органике, а игра молчала).
        // Несколько атомов металла в одной НЕИЗВЕСТНОЙ молекуле — тоже не держится: металл в
        // органике бывает (ферроцен, гем), но по одному атому и в особом окружении.
        if (metals.Count < 2 && !metalMetal && metalAtoms < 2)
            return radical == null ? null : Lang.T("Так не держится: ", "This would not hold together: ") + radical + ".";

        var why = new List<string>();
        if (radical != null) why.Add(radical);
        if (metals.Count >= 2)
            why.Add(Lang.T("в одной частице ", "one particle holds ") + metals.Count + (metals.Count < 5 ? Lang.T(" разных металла (", " different metals (") : Lang.T(" разных металлов (", " different metals (")) +
                    string.Join(", ", metals.ToArray()) + Lang.T(") — металлы не собираются в одну молекулу, они отдают электроны и становятся ионами",
                                                              ") — metals do not build one molecule, they give away electrons and become ions"));
        if (metalMetal)
            why.Add(Lang.T("связь металл–металл — это сплав, а не молекула", "a metal–metal bond is an alloy, not a molecule"));
        if (metals.Count < 2 && metalAtoms >= 2)
            why.Add(metalAtoms + Lang.T(" атома металла ", " metal atoms ") + "(" + metals[0] + ")" +
                    (have.Contains("C") ? Lang.T(" в одной органической молекуле — металл в органике держится поодиночке и в особом окружении (как железо в гемоглобине)",
                                                 " in one organic molecule — a metal stays in organics only one at a time and in a special pocket (like iron in haemoglobin)")
                                        : Lang.T(" в одной частице — это кусок соли или оксида, а не молекула", " in one particle — a piece of salt or oxide, not a molecule")));

        // На что распалось бы: каждый металл уходит к самому «жадному» неметаллу, который есть.
        string[] partners = { "F", "O", "Cl", "N", "S", "Br", "I" };
        var outList = new List<string>();
        foreach (var sym in metals)
        {
            var el = Elements.BySymbol(sym);
            int v = Mathf.Max(1, el.Valence);
            // Партнёра берём по жадности (F, O, Cl…), но только если его атомов ХВАТАЕТ на все
            // атомы этого металла: два фтора не делают фториды пяти металлов сразу.
            string x = null; int nm = 1, nx = 1;
            foreach (var p in partners)
            {
                int avail; if (!cnt.TryGetValue(p, out avail) || avail <= 0) continue;
                int vx0 = p == "O" || p == "S" ? 2 : p == "N" ? 3 : 1;
                int g0 = Gcd(v, vx0), nm0 = vx0 / g0, nx0 = v / g0;
                int units = Mathf.Max(1, cnt[sym] / nm0);
                if (avail < units * nx0) continue;
                cnt[p] = avail - units * nx0;
                x = p; nm = nm0; nx = nx0; break;
            }
            if (x == null) { outList.Add(sym + Lang.T(" (металл, сплав)", " (metal, alloy)")); continue; }
            string f = sym + (nm > 1 ? nm.ToString() : "") + x + (nx > 1 ? nx.ToString() : "");
            if (!outList.Contains(f)) outList.Add(f);
        }
        if (have.Contains("C")) outList.Add(have.Contains("O") ? Lang.T("CO2 и органика", "CO2 and organics") : Lang.T("органика", "organics"));

        return Lang.T("Так не держится: ", "This would not hold together: ") + string.Join("; ", why.ToArray()) + ". " +
               Lang.T("В жизни распалось бы на: ", "In reality it would fall apart into: ") + string.Join(", ", outList.ToArray()) + ".";
    }

    /// <summary>21.09, владелец: «пусть игра пишет, что нужно добавить для стабильности».
    /// Три подсказки, от простой к полезной:
    ///   • свободные связи — чем их закрыть (радикал с незанятыми местами в жизни не живёт);
    ///   • несколько металлов — какие убрать;
    ///   • самое похожее НАСТОЯЩЕЕ вещество из справочника и что добавить/убрать до него.
    /// Для веществ из справочника молчит — они и так настоящие.</summary>
    public static string Advice(Lab.Mol m)
    {
        if (m == null || m.Info != null || m.Atoms.Count < 2) return null;
        if (Lab.Mode == Lab.Level.Fun) return null;                          // Фан: без советов
        var have = new Dictionary<string, int>();
        var metals = new List<string>();
        foreach (var a in m.Atoms)
        {
            int c0; have.TryGetValue(a.El.Sym, out c0); have[a.El.Sym] = c0 + 1;
            if (IsMetal(a.El) && !metals.Contains(a.El.Sym)) metals.Add(a.El.Sym);
        }
        var tips = new List<string>();

        if (m.FreeLeft > 0)
            tips.Add(Lang.T("закрой свободные связи: добавь ", "close the free bonds: add ") + m.FreeLeft +
                     Lang.T(" водород(а) — атом с незанятыми местами (радикал) в жизни сразу во что-то вцепится",
                            " hydrogen(s) — an atom with empty slots (a radical) grabs something at once in reality"));

        if (metals.Count >= 2)
        {
            // оставляем металл, которого больше всего
            string keep = metals[0];
            foreach (var mm in metals) if (have[mm] > have[keep]) keep = mm;
            var drop = new List<string>();
            foreach (var mm in metals) if (mm != keep) drop.Add(mm);
            tips.Add(Lang.T("оставь один металл — ", "keep one metal — ") + keep + Lang.T(", убери ", ", remove ") + string.Join(", ", drop.ToArray()));
        }

        // Ближайшее настоящее вещество: меньше всего атомов добавить и убрать. Металлы из
        // сравнения убираем — их и так советуем убрать; иначе совет тянет к солям железа.
        // 21.09: «убери 3 C, 2 N, 4 Fe, 1 P — получишь сахарозу» из 52 атомов — это не совет.
        // Показываем, только если до вещества рукой подать: не больше пятой части атомов.
        if (metals.Count > 0) { var noMet = new Dictionary<string, int>(); foreach (var kv in have) if (!metals.Contains(kv.Key)) noMet[kv.Key] = kv.Value; have = noMet; }
        int total = 0; foreach (var kv in have) total += kv.Value;
        Molecules.Info best = null; int bestCost = int.MaxValue; Dictionary<string, int> bestComp = null;
        foreach (var info in Molecules.DB.Values)
        {
            var comp = Molecules.ParseFormula(info.Formula);
            bool shares = false;
            foreach (var k in comp.Keys) if (have.ContainsKey(k)) { shares = true; break; }
            if (!shares) continue;
            int cost = 0;
            foreach (var kv in comp) { int h0; have.TryGetValue(kv.Key, out h0); cost += Mathf.Abs(kv.Value - h0); }
            foreach (var kv in have) if (!comp.ContainsKey(kv.Key)) cost += kv.Value;
            // Ничья решается в пользу варианта БЕЗ новых элементов: CH3 -> «добавь H» (метан),
            // а не «добавь Cl» (хлорметан) — проверка поймала именно такую ничью.
            int newEls = 0; foreach (var k in comp.Keys) if (!have.ContainsKey(k)) newEls++;
            int score = cost * 4 + newEls;
            if (score < bestCost) { bestCost = score; best = info; bestComp = comp; }
        }
        if (best != null && bestCost > 0 && bestCost / 4 <= Mathf.Max(3, total / 5))
        {
            var add = new List<string>(); var rem = new List<string>();
            foreach (var kv in bestComp) { int h0; have.TryGetValue(kv.Key, out h0); if (kv.Value > h0) add.Add((kv.Value - h0) + " " + kv.Key); else if (kv.Value < h0) rem.Add((h0 - kv.Value) + " " + kv.Key); }
            foreach (var kv in have) if (!bestComp.ContainsKey(kv.Key)) rem.Add(kv.Value + " " + kv.Key);
            string how = (add.Count > 0 ? Lang.T("добавь ", "add ") + string.Join(", ", add.ToArray()) : "") +
                         (add.Count > 0 && rem.Count > 0 ? Lang.T(" и ", " and ") : "") +
                         (rem.Count > 0 ? Lang.T("убери ", "remove ") + string.Join(", ", rem.ToArray()) : "");
            tips.Add(Lang.T("ближе всего настоящее вещество — ", "the nearest real substance is ") + Lang.Name(best).ToLower() + " (" + best.Formula + "): " + how);
        }
        if (best == null || bestCost / 4 > Mathf.Max(3, total / 5))
            tips.Add(Lang.T("до настоящего вещества отсюда далеко — такие большие случайные сборки в природе не встречаются; начни с ядра поменьше",
                            "far from any real substance — big random assemblies like this do not occur in nature; start from a smaller core"));
        if (tips.Count == 0) return null;
        return Lang.T("Для стабильности: ", "For stability: ") + string.Join("; ", tips.ToArray()) + ".";
    }

    // ==================== ВУЗник: полярность ====================

    /// <summary>Полярность связей по разности электроотрицательностей: меньше 0.4 —
    /// неполярная, до 1.7 — полярная, от 1.7 — ионная. Плюс самая полярная связь.</summary>
    public static string Polarity(Lab.Mol m)
    {
        if (m == null || Lab.Mode < Lab.Level.Uni) return null;
        int non = 0, pol = 0, ion = 0; float best = -1f; string bestPair = "";
        foreach (var a in m.Atoms)
            foreach (var b in a.Bonds)
            {
                var o = b.Other(a);
                if (o == null || o.GetInstanceID() < a.GetInstanceID()) continue;
                if (a.El.EN <= 0f || o.El.EN <= 0f) continue;
                float d = Mathf.Abs(a.El.EN - o.El.EN);
                if (d < 0.4f) non++; else if (d < 1.7f) pol++; else ion++;
                if (d > best) { best = d; bestPair = a.El.Sym + "–" + o.El.Sym; }
            }
        if (non + pol + ion == 0) return null;
        return Lang.T("Связи: неполярных ", "Bonds: nonpolar ") + non + Lang.T(", полярных ", ", polar ") + pol + Lang.T(", ионных ", ", ionic ") + ion +
               Lang.T(" · самая полярная ", " · most polar ") + bestPair + Lang.T(" (ΔЭО ", " (ΔEN ") + best.ToString("0.00") + ")";
    }

    // ==================== Эйнштейн: энергия связей ====================

    // Средние энергии связей, кДж/моль (справочные, для газовой фазы). Ключ — пара символов
    // по алфавиту и кратность.
    static readonly Dictionary<string, float> BE = new Dictionary<string, float>
    {
        {"H-H1",436},{"C-H1",413},{"H-N1",391},{"H-O1",463},{"F-H1",567},{"Cl-H1",431},{"Br-H1",366},{"H-I1",299},{"H-S1",363},{"H-P1",322},{"H-Si1",318},
        {"C-C1",348},{"C-C2",614},{"C-C3",839},{"C-N1",293},{"C-N2",615},{"C-N3",891},{"C-O1",358},{"C-O2",745},{"C-O3",1072},
        {"C-F1",485},{"C-Cl1",328},{"Br-C1",276},{"C-I1",240},{"C-S1",259},{"C-S2",577},{"C-Si1",301},
        {"N-N1",163},{"N-N2",418},{"N-N3",941},{"N-O1",201},{"N-O2",607},{"F-N1",272},{"Cl-N1",200},
        {"O-O1",146},{"O-O2",495},{"F-O1",190},{"Cl-O1",203},{"O-S1",265},{"O-S2",523},{"S-S1",266},{"O-P1",335},{"O-P2",544},
        {"Cl-P1",326},{"O-Si1",452},{"F-F1",155},{"Cl-Cl1",242},{"Br-Br1",193},{"I-I1",151},{"F-S1",327},{"F-Xe1",133},
    };

    /// <summary>Сумма энергий связей — сколько надо вложить, чтобы разобрать молекулу на атомы.
    /// Считаем по тем связям, для которых есть справочное число, и честно говорим, сколько их.</summary>
    public static string BondEnergy(Lab.Mol m)
    {
        if (m == null || Lab.Mode < Lab.Level.Einstein) return null;
        float sum = 0f; int known = 0, total = 0;
        foreach (var a in m.Atoms)
            foreach (var b in a.Bonds)
            {
                var o = b.Other(a);
                if (o == null || o.GetInstanceID() < a.GetInstanceID()) continue;
                total++;
                string x = a.El.Sym, y = o.El.Sym;
                if (string.CompareOrdinal(x, y) > 0) { var t = x; x = y; y = t; }
                float e;
                if (BE.TryGetValue(x + "-" + y + b.Order, out e)) { sum += e; known++; }
            }
        if (total == 0) return null;
        string s = Lang.T("Энергия связей ≈ ", "Bond energy ≈ ") + sum.ToString("0") + Lang.T(" кДж/моль", " kJ/mol");
        if (known < total) s += Lang.T(" (по ", " (from ") + known + Lang.T(" из ", " of ") + total + Lang.T(" связей — для остальных нет справочного числа)", " bonds — no reference value for the rest)");
        return s + Lang.T(" — столько нужно, чтобы разобрать её на атомы", " — that is what it takes to split it into atoms");
    }

    // ==================== Эйнштейн: ядро ====================

    /// <summary>Кулоновский барьер слияния двух ядер (МэВ) и порядок времени жизни результата.</summary>
    public static string NuclearNote(Elements.El a, Elements.El b, int z)
    {
        if (Lab.Mode < Lab.Level.Einstein || a == null || b == null) return null;
        float a1 = Mathf.Max(1f, Mathf.Round(a.Mass)), a2 = Mathf.Max(1f, Mathf.Round(b.Mass));
        float r = 1.2f * (Mathf.Pow(a1, 1f / 3f) + Mathf.Pow(a2, 1f / 3f));      // фм
        float e = 1.44f * a.Z * b.Z / r;                                           // МэВ
        string life = z <= 83 ? Lang.T("ядро может быть стабильным", "the nucleus can be stable")
                    : z <= 92 ? Lang.T("радиоактивно, живёт от секунд до миллиардов лет", "radioactive, lives from seconds to billions of years")
                    : z <= 103 ? Lang.T("живёт от минут до лет, в природе почти нет", "lives minutes to years, almost absent in nature")
                    : z <= 118 ? Lang.T("распадается за миллисекунды–секунды; получены считанные атомы", "decays in milliseconds to seconds; only a handful of atoms were made")
                    : Lang.T("не получен ни разу; ожидаемая жизнь — микросекунды", "never made; expected lifetime — microseconds");
        return Lang.T("Кулоновский барьер ≈ ", "Coulomb barrier ≈ ") + e.ToString("0") + Lang.T(" МэВ — столько надо, чтобы ядра коснулись. ", " MeV — the energy needed for the nuclei to touch. ") + life + ".";
    }
}
