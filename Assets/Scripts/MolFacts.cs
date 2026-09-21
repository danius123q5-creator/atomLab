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
        if (m == null || m.Info != null || m.Atoms.Count < 3) return null;

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
        if (metals.Count < 2 && !metalMetal) return null;

        var why = new List<string>();
        if (metals.Count >= 2)
            why.Add(Lang.T("в одной частице ", "one particle holds ") + metals.Count + (metals.Count < 5 ? Lang.T(" разных металла (", " different metals (") : Lang.T(" разных металлов (", " different metals (")) +
                    string.Join(", ", metals.ToArray()) + Lang.T(") — металлы не собираются в одну молекулу, они отдают электроны и становятся ионами",
                                                              ") — metals do not build one molecule, they give away electrons and become ions"));
        if (metalMetal)
            why.Add(Lang.T("связь металл–металл — это сплав, а не молекула", "a metal–metal bond is an alloy, not a molecule"));

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
}
