using System.Collections;
using UnityEngine;

/// <summary>Самопроверка склейки: запуск с ключом -selftest ставит два водорода рядом с
/// кислородом, ждёт и печатает в лог, что получилось.
///
/// 🔴 ЗАЧЕМ ОНА ЕСТЬ. Скриншот показывает, что игра нарисовалась, и ровно ничего не говорит
/// о том, соединяются ли атомы: кликнуть за игрока некому. Без этой проверки «работает» было
/// бы словом, а не фактом. Проверка печатает ФОРМУЛУ, которую собрал сам движок, и падает
/// кодом 2, если вода не вышла.</summary>
public class SelfTest : MonoBehaviour
{
    public static bool Requested
    {
        get
        {
            foreach (var a in System.Environment.GetCommandLineArgs())
                if (a == "-selftest" || a == "-demo") return true;
            return false;
        }
    }

    /// <summary>-demo ставит ту же воду, но игру не закрывает: нужно, чтобы снять кадр
    /// с собранной молекулой и увидеть, не спряталась ли зона под панелью.</summary>
    static bool DemoOnly
    {
        get
        {
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-demo") return true;
            return false;
        }
    }

    /// <summary>🔴 Чистка идёт в Awake, а НЕ в Start. Стенды читают свои файлы в Start, и
    /// удаление из корутины успевало опоздать: прерванный прогон оставлял записи, следующий
    /// находил их уже в таблице и объявлял сломанным синтез. Awake у всех выполняется раньше
    /// любого Start, поэтому здесь опоздать нельзя.</summary>
    void Awake()
    {
        foreach (var f in new[] { "synthetic.txt", "assembled.txt", "zone.txt" })
        {
            try { System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, f)); }
            catch { }
        }
    }


    /// <summary>Собрать молекулу из символов, связав всех с первым атомом. Для проверок этого
    /// хватает: движку важен состав и связность, а не углы.</summary>
    static Atom[] Make(Vector3 at, params string[] syms)
    {
        var made = new Atom[syms.Length];
        for (int i = 0; i < syms.Length; i++)
            made[i] = Atom.Spawn(Elements.BySymbol(syms[i]), at + new Vector3(i * 0.55f, 0f, 0f));
        for (int i = 1; i < made.Length; i++) Bond.Create(made[0], made[i]);
        return made;
    }

    static bool HasFormula(Lab lab, string f)
    {
        foreach (var m in lab.Mols) if (m.Formula == f) return true;
        return false;
    }

    static string ZoneText(Lab lab)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var m in lab.Mols) sb.Append(m.Formula).Append(' ');
        return sb.ToString().Trim();
    }

    IEnumerator Start()
    {
        yield return new WaitForSeconds(0.5f);
        var lab = Lab.I;
        var H = Elements.BySymbol("H");
        var O = Elements.BySymbol("O");
        Vector3 c = Lab.ZoneCenter;

        Atom.Spawn(O, c);
        Atom.Spawn(H, c + new Vector3(-0.85f, 0.1f, 0f));
        Atom.Spawn(H, c + new Vector3(0.85f, -0.1f, 0f));

        yield return new WaitForSeconds(2.0f);
        lab.Recompute();

        string got = "";
        foreach (var m in lab.Mols) got += m.Formula + "(" + m.Atoms.Count + " ат.) ";
        Debug.Log("SELFTEST molecules: " + got.Trim());
        Debug.Log("SELFTEST discovered: " + lab.Discovered.Count + ", score: " + lab.Score);

        bool water = false;
        foreach (var m in lab.Mols) if (m.Formula == "H2O") water = true;

        // Второй случай: благородный газ обязан остаться один.
        Atom.Spawn(Elements.BySymbol("Ne"), c + new Vector3(0f, 1.2f, 0f));
        Atom.Spawn(Elements.BySymbol("Ne"), c + new Vector3(0.3f, 1.2f, 0f));
        yield return new WaitForSeconds(1.5f);
        lab.Recompute();
        bool neonAlone = true;
        foreach (var m in lab.Mols) if (m.Formula.StartsWith("Ne") && m.Atoms.Count > 1) neonAlone = false;

        // Третий случай — жалоба владельца 21.09: лоуренсий и медь не стыкуются. Проверяем
        // ПАРУ В ЧИСТОМ ВИДЕ: если здесь они склеятся, значит в его сцене у одного из них
        // просто кончились свободные связи, а игра об этом промолчала.
        lab.ClearZone();
        yield return new WaitForSeconds(0.3f);
        var lr = Atom.Spawn(Elements.BySymbol("Lr"), c + new Vector3(-0.8f, 0f, 0f));
        var cu = Atom.Spawn(Elements.BySymbol("Cu"), c + new Vector3(0.8f, 0f, 0f));
        yield return new WaitForSeconds(2.0f);
        float dist = Vector3.Distance(lr.transform.position, cu.transform.position);
        Debug.Log("SELFTEST pair Lr+Cu: bonded=" + lr.BondedTo(cu) +
                  " dist=" + dist.ToString("0.00") +
                  " touchAt=" + ((lr.El.Radius + cu.El.Radius) * 1.25f).ToString("0.00") +
                  " freeLr=" + lr.FreeValence + " freeCu=" + cu.FreeValence);

        // Четвёртый случай: пресеты. Собираем КАЖДЫЙ и сверяем формулу, которую посчитал сам
        // движок, с той, что заявлена в списке. Глазами это не проверить: бензол от толуола
        // на картинке отличит не каждый, а формула отличит всегда.
        int bad = 0;
        foreach (var pr in Presets.All)
        {
            lab.ClearZone();
            yield return new WaitForSeconds(0.15f);
            Presets.Spawn(pr);
            yield return new WaitForSeconds(0.25f);
            lab.Recompute();
            string mine = "";
            int biggest = 0;
            foreach (var m in lab.Mols) if (m.Atoms.Count > biggest) { biggest = m.Atoms.Count; mine = m.Formula; }
            bool ok = (mine == pr.Formula) && lab.Mols.Count == 1;
            if (!ok) bad++;
            Debug.Log("SELFTEST preset " + pr.Name + ": want=" + pr.Formula + " got=" + mine +
                      " parts=" + lab.Mols.Count + (ok ? " OK" : " MISMATCH"));
        }
        lab.ClearZone();
        Debug.Log("SELFTEST presets bad=" + bad + " of " + Presets.All.Length);

        // 21.09: двадцать популярных соединений из-под таблицы. Сверяем то же, что у пресетов:
        // формулу, посчитанную движком, с заявленной, и что вышла ОДНА молекула, а не куча.
        // Плюс — что карточка вещество узнаёт (иначе строки «применение» у него не будет).
        int popBad = 0;
        foreach (var q in Presets.Grid)
        {
            lab.ClearZone();
            yield return new WaitForSeconds(0.15f);
            Presets.SpawnPopular(q);
            yield return new WaitForSeconds(0.3f);
            lab.Recompute();
            string mine = ""; int biggest = 0; Lab.Mol big = null;
            foreach (var m in lab.Mols) if (m.Atoms.Count > biggest) { biggest = m.Atoms.Count; mine = m.Formula; big = m; }
            var info = Molecules.LookupByComposition(Molecules.ParseFormula(q.Formula));
            bool known = big != null && big.Info != null && !string.IsNullOrEmpty(big.Info.Use);
            bool ok = Molecules.Canon(Molecules.ParseFormula(mine)) == Molecules.Canon(Molecules.ParseFormula(q.Formula))
                      && lab.Mols.Count == 1 && info != null && known;
            if (!ok) popBad++;
            Debug.Log("SELFTEST popular " + q.Formula + ": got=" + mine + " parts=" + lab.Mols.Count +
                      " card=" + (known ? "да" : "НЕТ") + (ok ? " OK" : " MISMATCH"));
        }
        lab.ClearZone();
        Debug.Log("SELFTEST popular bad=" + popBad + " of " + Presets.Grid.Length);

        // 21.09: «убрать связи в меню не работает» — связь рвалась и тут же склеивалась снова.
        lab.ClearZone(); yield return new WaitForSeconds(0.15f);
        Presets.SpawnPopular(new Presets.Pop { Formula = "H2O", Ru = "вода", En = "water" });
        yield return new WaitForSeconds(0.4f);
        Atom brkO = null; foreach (var at in Atom.All) if (at.El.Sym == "O") brkO = at;
        int brkBefore = brkO != null ? brkO.Bonds.Count : -1;
        if (brkO != null) for (int i = brkO.Bonds.Count - 1; i >= 0; i--) lab.BreakByHand(brkO.Bonds[i]);
        yield return new WaitForSeconds(1.0f);
        int brkAfter = brkO != null ? brkO.Bonds.Count : -1;
        Debug.Log("SELFTEST break by hand: связей было " + brkBefore + ", через секунду " + brkAfter + ((brkBefore == 2 && brkAfter == 0) ? " OK" : " MISMATCH"));

        // 21.09: «образовать связь с...» из меню — связь ставится с дальним атомом; занятый отказывает.
        lab.ClearZone(); yield return new WaitForSeconds(0.15f);
        var lnO = Atom.Spawn(Elements.BySymbol("O"), c + Vector3.left * 3f);
        var lnH1 = Atom.Spawn(Elements.BySymbol("H"), c + Vector3.right * 3f);
        var lnH2 = Atom.Spawn(Elements.BySymbol("H"), c + Vector3.up * 2.5f);
        var lnH3 = Atom.Spawn(Elements.BySymbol("H"), c + Vector3.down * 2.5f);
        yield return new WaitForSeconds(0.2f);
        string r1 = lab.BondByHand(lnO, lnH1), r2 = lab.BondByHand(lnO, lnH2), r3 = lab.BondByHand(lnO, lnH3);
        yield return new WaitForSeconds(0.3f);
        bool linkOk = r1 == null && r2 == null && r3 != null && lnO.Bonds.Count == 2;
        Debug.Log("SELFTEST link by menu: O-H " + (r1 ?? "ok") + " | O-H " + (r2 ?? "ok") + " | третий H: " + (r3 ?? "НЕ отказал") + (linkOk ? " OK" : " MISMATCH"));
        lab.ClearZone();
        lab.ClearZone();

        // 21.09: ВЕРХНИЙ УРОВЕНЬ — вещи на столе. Каждый опыт — одна строка с исходом.
        var ph = PhysLab.I;
        if (ph != null)
        {
            ph.Active = true;
            yield return new WaitForSeconds(0.2f);
            ph.ClearTable();
            var pwater = ph.PutById("beaker_water"); var pdrop = ph.PutById("phph"); var pna = ph.PutById("na");
            ph.DropInto(pdrop, pwater); ph.DropInto(pna, pwater);
            bool naOk = ph.IsAlkaline(pwater) && pwater.Indicator && pwater.Contents.Exists(p => p.Formula == "NaOH");
            string naLog = string.Join(" | ", ph.Log.ToArray());

            var pacid = ph.PutById("flask_hcl"); var pcu = ph.PutById("cu"); ph.DropInto(pcu, pacid);
            bool cuOk = pacid.Contents.Exists(p => p.Formula == "Cu") && ph.IsAcidic(pacid) && ph.Log.Count > 0;

            var acid2 = ph.PutById("flask_hcl"); var pzn = ph.PutById("zn"); ph.DropInto(pzn, acid2);
            bool znOk = !acid2.Contents.Exists(p => p.Formula == "Zn") && acid2.Contents.Exists(p => p.Formula == "ZnCl2");

            var pvin = ph.PutById("flask_vinegar"); var psoda = ph.PutById("soda"); ph.DropInto(psoda, pvin);
            bool sodaOk = pvin.Contents.Exists(p => p.Formula == "CH3COONa");

            var pbas = ph.PutById("flask_naoh"); var acid3 = ph.PutById("flask_hcl"); ph.DropInto(pbas, acid3);
            bool neutOk = acid3.Contents.Exists(p => p.Formula == "NaCl") && !ph.IsAcidic(acid3) && !ph.IsAlkaline(acid3);

            ph.ClearTable();
            var w2 = ph.PutById("beaker_water"); var pcuso = ph.PutById("cuso4"); ph.DropInto(pcuso, w2);
            var pnail = ph.PutById("fe"); ph.DropInto(pnail, w2);
            bool nailOk = w2.Contents.Exists(p => p.Formula == "FeSO4") && w2.Contents.Exists(p => p.Formula == "Cu");

            var w3 = ph.PutById("beaker_water"); var psalt = ph.PutById("nacl"); var pchalk = ph.PutById("chalk");
            ph.DropInto(psalt, w3); ph.DropInto(pchalk, w3);
            bool dissOk = w3.Contents.Exists(p => p.Formula == "NaCl" && p.Phase == PhysLab.Phase.Dissolved) &&
                          w3.Contents.Exists(p => p.Formula == "CaCO3" && p.Phase == PhysLab.Phase.Solid);

            var pdish = ph.PutById("dish_empty"); var pcuWater = ph.PutById("cu");
            var w4 = ph.PutById("beaker_water"); ph.DropInto(w4, pdish); ph.DropInto(pcuWater, pdish);
            bool pourOk = pdish.HasWater && !w4.HasWater && pdish.Contents.Exists(p => p.Formula == "Cu");

            Debug.Log("SELFTEST phys: натрий+вода(щёлочь,малиновый)=" + naOk + " | медь в кислоте стоит=" + cuOk +
                      " | цинк в кислоте=" + znOk + " | сода+уксус=" + sodaOk + " | нейтрализация=" + neutOk +
                      " | гвоздь в купоросе=" + nailOk + " | соль растворилась, мел на дне=" + dissOk + " | переливание=" + pourOk +
                      ((naOk && cuOk && znOk && sodaOk && neutOk && nailOk && dissOk && pourOk) ? " OK" : " MISMATCH"));
            Debug.Log("SELFTEST phys натрий: " + naLog);
            ph.ClearTable();
            var two = ph.PutById("flask_hcl"); var z1 = ph.PutById("zn"); var m1 = ph.PutById("mg");
            ph.DropInto(z1, two); ph.DropInto(m1, two);
            bool twoOk = two.Contents.Exists(p => p.Formula == "ZnCl2") && two.Contents.Exists(p => p.Formula == "MgCl2");
            var wm = ph.PutById("beaker_water"); var m2 = ph.PutById("mg"); ph.DropInto(m2, wm);
            Debug.Log("SELFTEST phys две порции кислоты: цинк и магний=" + twoOk + " | магний в воде: " + ph.LastText + (twoOk ? " OK" : " MISMATCH"));
            yield return new WaitForSeconds(0.5f);
            ph.ClearTable();
            ph.Active = false;
        }

        // 21.09, владелец: «при переходе с 3д на 2д атомы не цепляются». Кладём в 3D кислород
        // и два водорода РАЗНОЙ глубины, включаем 2D и сводим их в плоскости — должна выйти
        // вода. Плюс: элемент из таблицы в 2D обязан появиться (в ручной пробе он пропал).
        lab.ClearZone(); yield return new WaitForSeconds(0.15f);
        Lab.Mode2D = false;
        var o2d = Atom.Spawn(Elements.BySymbol("O"), c + new Vector3(0f, 0f, 1.2f));
        var h2dA = Atom.Spawn(Elements.BySymbol("H"), c + new Vector3(-2.4f, 0f, -1.1f));
        var h2dB = Atom.Spawn(Elements.BySymbol("H"), c + new Vector3(2.4f, 0f, 0.8f));
        yield return new WaitForSeconds(0.3f);
        Lab.Mode2D = true;
        yield return new WaitForSeconds(0.3f);
        float zSpread = Mathf.Abs(o2d.transform.position.z - h2dA.transform.position.z) + Mathf.Abs(o2d.transform.position.z - h2dB.transform.position.z);
        // сводим, как рука: тянем скоростью к кислороду
        for (int k = 0; k < 60; k++)
        {
            if (h2dA != null) h2dA.Body.linearVelocity = (o2d.transform.position + Vector3.left * 0.5f - h2dA.transform.position) * 6f;
            if (h2dB != null) h2dB.Body.linearVelocity = (o2d.transform.position + Vector3.right * 0.5f - h2dB.transform.position) * 6f;
            yield return new WaitForFixedUpdate();
        }
        yield return new WaitForSeconds(0.3f);
        lab.Recompute();
        bool water2d = HasFormula(lab, "H2O");
        var spawned = lab.SpawnFromTable(Elements.BySymbol("C"), new Vector3(Screen.width * 0.6f, Screen.height * 0.55f, 0f));
        yield return new WaitForSeconds(0.5f);
        bool alive = spawned != null && Atom.All.Contains(spawned);
        Debug.Log("SELFTEST 2d: разнос по глубине после включения " + zSpread.ToString("0.000") + " | вода=" + water2d +
                  " | связей у O=" + o2d.Bonds.Count + " | элемент из таблицы жив=" + alive +
                  (spawned != null ? " z=" + spawned.transform.position.z.ToString("0.00") : "") +
                  ((water2d && alive) ? " OK" : " MISMATCH"));
        Lab.Mode2D = false;
        lab.ClearZone();

        // 21.09: реакции ПАР молекул. Сода + уксус обязаны дать углекислый газ. Соль + вода
        // обязаны дать объяснение про растворение и НЕ дать щёлочь с кислотой (так делал
        // запасной «котёл», пока правила не было).
        lab.ClearZone(); yield return new WaitForSeconds(0.15f);
        Presets.SpawnPopular(new Presets.Pop { Formula = "NaHCO3", Ru = "сода", En = "soda" });
        // Разводим: обе молекулы рождаются около центра со случайным сдвигом и слипались в одну
        // (C3H5NaO5) ещё до реакции — проверка мерила слипание, а не правило.
        foreach (var at in Atom.All) { at.transform.position += Vector3.left * 3.2f; at.Body.position = at.transform.position; }
        yield return new WaitForSeconds(0.2f);
        Presets.SpawnPopular(new Presets.Pop { Formula = "C2H4O2", Ru = "уксус", En = "vinegar" });
        foreach (var at in Atom.All) if (at.transform.position.x > c.x - 1.6f) { at.transform.position += Vector3.right * 1.8f; at.Body.position = at.transform.position; }
        yield return new WaitForSeconds(0.4f);
        lab.Recompute();
        foreach (var mm in lab.Mols) { var spc = Reactions.Classify(mm); Debug.Log("SELFTEST classify " + mm.Formula + " -> " + spc.Kind + " остаток=" + (spc.Residue != null ? spc.Residue.Name : "-") + " металл=" + (spc.Metal ?? "-")); }
        ReactionEngine.Refusals.Clear();
        Chemistry.React();
        yield return new WaitForSeconds(0.4f);
        lab.Recompute();
        bool fizz = HasFormula(lab, "CO2") && HasFormula(lab, "H2O");
        Debug.Log("SELFTEST pair soda+vinegar: " + ZoneText(lab) + (fizz ? " OK" : " MISMATCH"));

        lab.ClearZone(); yield return new WaitForSeconds(0.15f);
        Presets.SpawnPopular(new Presets.Pop { Formula = "NaCl", Ru = "соль", En = "salt" });
        yield return new WaitForSeconds(0.2f);
        Presets.SpawnPopular(new Presets.Pop { Formula = "H2O", Ru = "вода", En = "water" });
        yield return new WaitForSeconds(0.4f);
        ReactionEngine.Refusals.Clear();
        Chemistry.React();
        yield return new WaitForSeconds(0.4f);
        lab.Recompute();
        bool dissolve = HasFormula(lab, "NaCl") && HasFormula(lab, "H2O") && !HasFormula(lab, "NaOH") && ReactionEngine.Refusals.Count > 0;
        Debug.Log("SELFTEST pair salt+water: " + ZoneText(lab) + " | " + (ReactionEngine.Refusals.Count > 0 ? ReactionEngine.Refusals[0] : "нет объяснения") + (dissolve ? " OK" : " MISMATCH"));
        lab.ClearZone();

        // 21.09: предел связей. Сера с КИСЛОРОДОМ обязана взять больше двух связей (SO3,
        // H2SO4 руками), а сера с ВОДОРОДОМ — не больше двух (H2S, а не H3S). Кладём атомы
        // вплотную, как это сделала бы рука, и считаем, сколько связей сера взяла сама.
        lab.ClearZone();
        yield return new WaitForSeconds(0.15f);
        var sO = Atom.Spawn(Elements.BySymbol("S"), c);
        float rr = Elements.BySymbol("S").Radius + Elements.BySymbol("O").Radius;
        for (int i = 0; i < 3; i++)
        {
            float ang = i * 120f * Mathf.Deg2Rad;
            Atom.Spawn(Elements.BySymbol("O"), c + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * rr * 1.3f);
        }
        yield return new WaitForSeconds(0.8f);
        int sWithO = sO.Bonds.Count;
        lab.ClearZone();
        yield return new WaitForSeconds(0.15f);
        var sH = Atom.Spawn(Elements.BySymbol("S"), c);
        float rh = Elements.BySymbol("S").Radius + Elements.BySymbol("H").Radius;
        for (int i = 0; i < 3; i++)
        {
            float ang = i * 120f * Mathf.Deg2Rad;
            Atom.Spawn(Elements.BySymbol("H"), c + new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f) * rh * 1.3f);
        }
        yield return new WaitForSeconds(0.8f);
        int sWithH = sH.Bonds.Count;
        lab.ClearZone();
        bool hyperOk = sWithO >= 3 && sWithH <= 2;
        Debug.Log("SELFTEST hyper S+3O: связей " + sWithO + " (нужно >=3) | S+3H: связей " + sWithH +
                  " (нужно <=2) " + (hyperOk ? "OK" : "MISMATCH"));

        // Пятый случай: «посмотреть реакцию». Кладём в зону сырьё на этанол плюс лишнего —
        // и смотрим, ЧТО движок из этого собрал и не завис ли он.
        lab.ClearZone();
        yield return new WaitForSeconds(0.2f);
        for (int i = 0; i < 2; i++) Atom.Spawn(Elements.BySymbol("C"), c + new Vector3(-2.5f + i, 1.5f, 0f));
        for (int i = 0; i < 6; i++) Atom.Spawn(Elements.BySymbol("H"), c + new Vector3(-2f + i * 0.9f, -1.5f, 1f));
        Atom.Spawn(Elements.BySymbol("O"), c + new Vector3(2.5f, 1.5f, -1f));
        Atom.Spawn(Elements.BySymbol("Na"), c + new Vector3(3.2f, -1.5f, 1.2f));
        yield return new WaitForSeconds(0.4f);
        float t0 = Time.realtimeSinceStartup;
        Chemistry.React();
        float ms = (Time.realtimeSinceStartup - t0) * 1000f;
        yield return new WaitForSeconds(0.5f);
        lab.Recompute();
        string res = "";
        foreach (var m in lab.Mols) res += m.Formula + "(" + m.Atoms.Count + ") ";
        Debug.Log("SELFTEST react: " + res.Trim() + "   time=" + ms.ToString("0.0") + " ms");
        lab.ClearZone();

        // Шестой случай: ускоритель. Склейка ядер — это арифметика, и она обязана сходиться:
        // уран плюс кальций дают уже известный коперниций, а оганесон плюс водород — элемент,
        // которого в природе нет, и он должен появиться в таблице голубым.
        var acc = Accelerator.I;
        bool accOk = false, synthOk = false;
        if (acc != null)
        {
            acc.Put(0, Elements.BySymbol("U"));
            acc.Put(1, Elements.BySymbol("Ca"));
            acc.Fuse();
            accOk = acc.Result != null && acc.Result.Z == 112 && acc.Result.Sym == "Cn" && !acc.Result.Synthetic;
            Debug.Log("SELFTEST accel U+Ca: " + (acc.Result != null ? acc.Result.Sym + " Z=" + acc.Result.Z : "нет") +
                      (accOk ? " OK" : " MISMATCH"));

            int before = Elements.All.Length;
            acc.Put(0, Elements.BySymbol("Og"));
            acc.Put(1, Elements.BySymbol("H"));
            acc.Fuse();
            synthOk = acc.Result != null && acc.Result.Z == 119 && acc.Result.Synthetic && Elements.All.Length == before + 1;
            Debug.Log("SELFTEST accel Og+H: " + (acc.Result != null ? acc.Result.Sym + " " + acc.Result.Name + " Z=" + acc.Result.Z : "нет") +
                      " вэтаблице=" + (Elements.All.Length - before) + (synthOk ? " OK" : " MISMATCH"));

            // Проверка не должна оставлять свой мусор в настоящей таблице владельца.
            try { System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, "synthetic.txt")); } catch { }
            acc.Result = null;
        }

        // Седьмой случай: сборка атома. Восемь протонов — всегда кислород, и никакие нейтроны
        // этого не меняют; десять нейтронов делают из него кислород-18. Железо без трёх
        // электронов обязано стать ионом 3+ и получить ровно три связи.
        var bld = AtomBuilder.I;
        bool isoOk = false, ionOk = false;
        if (bld != null)
        {
            bld.Protons = 8; bld.Neutrons = 10; bld.Electrons = 8;
            var iso = bld.SaveToTable();
            isoOk = iso != null && iso.Assembled && iso.Sym == "O-18" && iso.MassNumber == 18 && iso.Charge == 0;
            Debug.Log("SELFTEST build O+10n: " + (iso != null ? iso.Sym + " A=" + iso.MassNumber + " заряд=" + iso.Charge : "нет") +
                      (isoOk ? " OK" : " MISMATCH"));

            bld.Protons = 26; bld.Neutrons = 30; bld.Electrons = 23;
            var ion = bld.SaveToTable();
            ionOk = ion != null && ion.Assembled && ion.Charge == 3 && ion.MassNumber == 56 && ion.Valence == 3;
            Debug.Log("SELFTEST build Fe 3+: " + (ion != null ? ion.Sym + " A=" + ion.MassNumber + " заряд=" + ion.Charge + " связей=" + ion.Valence : "нет") +
                      (ionOk ? " OK" : " MISMATCH"));

            // Проверка не оставляет своих записей в настоящей таблице владельца.
            try { System.IO.File.Delete(System.IO.Path.Combine(Application.persistentDataPath, "assembled.txt")); } catch { }
        }

        // Восьмой случай: НАСТОЯЩИЕ реакции. Свойства должны решать, а не наличие атомов.
        bool neutrOk = false, zincOk = false, copperOk = false;

        // 8.1 Нейтрализация: HCl + NaOH -> NaCl + H2O.
        lab.ClearZone();
        yield return new WaitForSeconds(0.2f);
        Make(c + new Vector3(-2f, 0f, 0f), "H", "Cl");
        Make(c + new Vector3(2f, 0f, 0f), "Na", "O", "H");
        yield return new WaitForSeconds(0.3f);
        Chemistry.React();
        lab.Recompute();
        Debug.Log("SELFTEST сразу после реакции: " + ZoneText(lab) + "  связей=" + Bond.All.Count);
        yield return new WaitForSeconds(0.3f);
        lab.Recompute();
        Debug.Log("SELFTEST через 0.3 с: " + ZoneText(lab) + "  связей=" + Bond.All.Count);
        neutrOk = HasFormula(lab, "NaCl") && HasFormula(lab, "H2O");
        Debug.Log("SELFTEST react acid+base: " + ZoneText(lab) + (neutrOk ? " OK" : " MISMATCH"));
        Debug.Log("SELFTEST chain: " + lab.Toast);

        // 8.2 Цинк активнее водорода: Zn + 2HCl -> ZnCl2 + H2.
        lab.ClearZone();
        yield return new WaitForSeconds(0.2f);
        Atom.Spawn(Elements.BySymbol("Zn"), c);
        Make(c + new Vector3(-2f, 0.5f, 0f), "H", "Cl");
        Make(c + new Vector3(2f, -0.5f, 0f), "H", "Cl");
        yield return new WaitForSeconds(0.3f);
        Chemistry.React();
        yield return new WaitForSeconds(0.3f);
        lab.Recompute();
        zincOk = HasFormula(lab, "H2") && HasFormula(lab, "Cl2Zn");
        Debug.Log("SELFTEST react Zn+HCl: " + ZoneText(lab) + (zincOk ? " OK" : " MISMATCH"));

        // 8.3 Медь стоит ПОСЛЕ водорода — реакции быть не должно, и игра обязана сказать почему.
        lab.ClearZone();
        ReactionEngine.Refusals.Clear();
        yield return new WaitForSeconds(0.2f);
        Atom.Spawn(Elements.BySymbol("Cu"), c);
        Make(c + new Vector3(-2f, 0.5f, 0f), "H", "Cl");
        Make(c + new Vector3(2f, -0.5f, 0f), "H", "Cl");
        yield return new WaitForSeconds(0.3f);
        Chemistry.React();
        yield return new WaitForSeconds(0.3f);
        lab.Recompute();
        copperOk = !HasFormula(lab, "H2") && ReactionEngine.Refusals.Count > 0;
        Debug.Log("SELFTEST react Cu+HCl: " + ZoneText(lab) + " отказов=" + ReactionEngine.Refusals.Count +
                  (copperOk ? " OK" : " MISMATCH"));
        lab.ClearZone();

        Debug.Log("SELFTEST water=" + water + " neonAlone=" + neonAlone + " accel=" + accOk + " synth=" + synthOk + " iso=" + isoOk + " ion=" + ionOk + " neutr=" + neutrOk + " zinc=" + zincOk + " copper=" + copperOk);
        if (!DemoOnly) Application.Quit((water && neonAlone && bad == 0 && accOk && synthOk && isoOk && ionOk && neutrOk && zincOk && copperOk) ? 0 : 2);
    }
}
