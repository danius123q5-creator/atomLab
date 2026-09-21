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

        Debug.Log("SELFTEST water=" + water + " neonAlone=" + neonAlone + " accel=" + accOk + " synth=" + synthOk);
        if (!DemoOnly) Application.Quit((water && neonAlone && bad == 0 && accOk && synthOk) ? 0 : 2);
    }
}
