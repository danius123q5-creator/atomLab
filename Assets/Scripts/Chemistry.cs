using System.Collections.Generic;
using UnityEngine;

/// <summary>«Посмотреть реакцию»: всё, что лежит в зоне, распадается и пересобирается в
/// настоящие вещества из справочника.
///
/// 🔴 21.09, владелец: «посмотреть реакцию запускает химию в действие, и атомы что-то делают».
///
/// Как считается: берём все атомы зоны в общий котёл, рвём все связи и жадно набираем из
/// котла самые крупные вещества, какие можно собрать из наличного состава. Что не вошло —
/// остаётся свободными атомами.
///
/// 🔴 ЧЕСТНО, ЧЕМ ЭТО НЕ ЯВЛЯЕТСЯ. Настоящая реакция идёт по энергии: одни вещества
/// получаются, другие нет, и уравнение надо уравнивать. Здесь считается только СОСТАВ: если
/// атомов хватает на серную кислоту — она и соберётся, хотя в стакане так бы не вышло.
/// Это «что из этих атомов вообще можно собрать», а не предсказание опыта.</summary>
public static class Chemistry
{
    /// <summary>Строение собранного здесь приблизительное: центром берём атом с наибольшей
    /// валентностью и вешаем на него остальных, а лишних цепляем к тем, у кого ещё есть
    /// место. У готовых веществ из списка пресетов строение настоящее — это разные вещи.</summary>
    static void Assemble(List<Atom> atoms, Vector3 center)
    {
        atoms.Sort((x, y) => y.El.Valence.CompareTo(x.El.Valence));
        var core = atoms[0];
        core.transform.position = center;
        core.Body.linearVelocity = Vector3.zero;

        for (int i = 1; i < atoms.Count; i++)
        {
            var a = atoms[i];
            // Точки по сфере Фибоначчи — так соседи не лезут друг в друга.
            float k = (i - 0.5f) / (atoms.Count - 1);
            float phi = Mathf.Acos(1f - 2f * k);
            float theta = Mathf.PI * (1f + Mathf.Sqrt(5f)) * i;
            Vector3 dir = new Vector3(Mathf.Cos(theta) * Mathf.Sin(phi), Mathf.Sin(theta) * Mathf.Sin(phi), Mathf.Cos(phi));
            a.transform.position = center + dir * ((core.El.Radius + a.El.Radius) * 1.45f);
            a.Body.linearVelocity = Vector3.zero;

            Atom host = core;
            if (host.FreeValence < 1)
            {
                host = null;
                for (int j = 0; j < i; j++) if (atoms[j].FreeValence > 0) { host = atoms[j]; break; }
            }
            if (host != null) Bond.Create(host, a);
        }
    }

    static bool CanTake(Dictionary<string, List<Atom>> pool, Dictionary<string, int> need)
    {
        foreach (var kv in need)
        {
            List<Atom> have;
            if (!pool.TryGetValue(kv.Key, out have) || have.Count < kv.Value) return false;
        }
        return true;
    }

    public static void React()
    {
        var lab = Lab.I;
        if (lab == null) return;
        if (Atom.All.Count < 2)
        {
            lab.Say("Реагировать нечему: в зоне меньше двух атомов.", new Color(1f, 0.9f, 0.6f));
            return;
        }

        Vector3 boom = Vector3.zero;
        foreach (var a in Atom.All) boom += a.transform.position;
        boom /= Atom.All.Count;

        // Котёл: разрываем всё и складываем атомы по символам.
        for (int i = Bond.All.Count - 1; i >= 0; i--) Bond.All[i].Break();
        var pool = new Dictionary<string, List<Atom>>();
        foreach (var a in Atom.All)
        {
            List<Atom> l;
            if (!pool.TryGetValue(a.El.Sym, out l)) { l = new List<Atom>(); pool[a.El.Sym] = l; }
            l.Add(a);
        }

        // Вещества перебираем от крупных к мелким: иначе весь углерод уйдёт на угарный газ
        // и ни одна большая молекула уже не соберётся.
        var order = new List<Molecules.Info>(Molecules.DB.Values);
        var size = new Dictionary<Molecules.Info, int>();
        foreach (var inf in order)
        {
            int n = 0;
            foreach (var kv in Molecules.ParseFormula(inf.Formula)) n += kv.Value;
            size[inf] = n;
        }
        order.Sort((x, y) => size[y].CompareTo(size[x]));

        var made = new List<string>();
        int spot = 0;
        bool progress = true;
        while (progress && made.Count < 12)
        {
            progress = false;
            foreach (var inf in order)
            {
                if (size[inf] < 2) continue;
                var need = Molecules.ParseFormula(inf.Formula);
                if (!CanTake(pool, need)) continue;

                var taken = new List<Atom>();
                foreach (var kv in need)
                    for (int i = 0; i < kv.Value; i++)
                    {
                        var list = pool[kv.Key];
                        taken.Add(list[list.Count - 1]);
                        list.RemoveAt(list.Count - 1);
                    }

                float ang = spot * 2.4f;
                Vector3 c = Lab.ZoneCenter + new Vector3(Mathf.Cos(ang) * (1.2f + spot * 0.5f), Mathf.Sin(ang * 1.7f) * 0.9f, Mathf.Sin(ang) * (1.0f + spot * 0.4f));
                c.x = Mathf.Clamp(c.x, Lab.ZoneCenter.x - 4f, Lab.ZoneCenter.x + 4f);
                c.z = Mathf.Clamp(c.z, Lab.ZoneCenter.z - 3f, Lab.ZoneCenter.z + 3f);
                Assemble(taken, c);
                Fx.Sparks(c, new Color(0.6f, 1f, 0.8f), 30, 3f);
                made.Add(inf.Name + " (" + inf.Formula + ")");
                spot++;
                progress = true;
                break;
            }
        }

        // 🔴 21.09, владелец: «надо хотя бы что-то!». Раньше, если из набора не выходило ни
        // одного вещества из справочника, реакция честно писала «впустую» — и не делала
        // НИЧЕГО. Это правда, но бесполезная. Теперь остаток всё равно идёт в дело: атомы
        // слипаются в то, что позволяет валентность. Вещества с именем из этого не выйдет,
        // зато выйдет соединение — и его формулу игра покажет.
        var leftovers = new List<Atom>();
        foreach (var kv in pool) leftovers.AddRange(kv.Value);
        pool.Clear();

        int glued = 0;
        while (leftovers.Count >= 2)
        {
            // Кладём в кучку, пока у неё есть чем связываться: связей у собранного должно
            // хватать, иначе половина атомов повиснет рядом без связи.
            var clump = new List<Atom>();
            int capacity = 0;
            while (leftovers.Count > 0 && clump.Count < 9)
            {
                var a = leftovers[leftovers.Count - 1];
                if (a.El.Valence == 0 && clump.Count > 0) break;       // благородный газ не липнет
                leftovers.RemoveAt(leftovers.Count - 1);
                if (a.El.Valence == 0) { continue; }                   // и в кучку его не берём
                clump.Add(a);
                capacity += a.El.Valence;
                if (clump.Count >= 2 && capacity >= clump.Count * 2) break;
            }
            if (clump.Count < 2) break;

            float ang = spot * 2.4f;
            Vector3 c2 = Lab.ZoneCenter + new Vector3(Mathf.Cos(ang) * (1.2f + spot * 0.5f), Mathf.Sin(ang * 1.7f) * 0.9f, Mathf.Sin(ang) * (1.0f + spot * 0.4f));
            c2.x = Mathf.Clamp(c2.x, Lab.ZoneCenter.x - 4f, Lab.ZoneCenter.x + 4f);
            c2.z = Mathf.Clamp(c2.z, Lab.ZoneCenter.z - 3f, Lab.ZoneCenter.z + 3f);
            Assemble(clump, c2);
            Fx.Sparks(c2, new Color(0.8f, 0.85f, 1f), 24, 2.6f);
            glued += clump.Count;
            spot++;
        }

        int left = leftovers.Count;

        Fx.Boom();
        Fx.Flash(boom, new Color(1f, 0.9f, 0.6f), 12f, 14f, 0.8f);
        Fx.Sparks(boom, new Color(1f, 0.85f, 0.5f), 120, 8f, 0.14f);

        lab.Recompute();

        // Что вышло из остатка — берём формулами прямо из зоны, а не придумываем.
        var unknown = new List<string>();
        foreach (var m in lab.Mols)
            if (m.Atoms.Count > 1 && m.Info == null) unknown.Add(m.Formula);

        if (made.Count > 0 && unknown.Count > 0)
            lab.Say("Реакция: " + string.Join(", ", made.ToArray()) +
                    ".  Из остатка слиплось: " + string.Join(", ", unknown.ToArray()) +
                    " — таких веществ в справочнике нет.", new Color(0.7f, 1f, 0.75f));
        else if (made.Count > 0)
            lab.Say("Реакция: получилось " + string.Join(", ", made.ToArray()) +
                    (left > 0 ? ".  Осталось свободных атомов: " + left : "."), new Color(0.7f, 1f, 0.75f));
        else if (unknown.Count > 0)
            lab.Say("Знакомого вещества из этого набора не выходит — слепилось то, что позволила валентность: " +
                    string.Join(", ", unknown.ToArray()) + ".", new Color(0.9f, 0.95f, 1f));
        else
            lab.Say("Связываться нечему: тут " + (Atom.All.Count == 1 ? "один атом" : "только одиночки — благородные газы") +
                    ". Добавь в зону что-нибудь с валентностью.", new Color(1f, 0.85f, 0.6f));
    }
}
