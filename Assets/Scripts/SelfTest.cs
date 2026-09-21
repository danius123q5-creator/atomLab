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

        Debug.Log("SELFTEST water=" + water + " neonAlone=" + neonAlone);
        if (!DemoOnly) Application.Quit((water && neonAlone) ? 0 : 2);
    }
}
