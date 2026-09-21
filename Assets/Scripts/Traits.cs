using UnityEngine;

/// <summary>Свойства элементов в действии: атомы ведут себя по-разному просто потому, что они
/// разные. Именно это и делает собранную конструкцию «живой» — она сама что-то творит.
///
/// 🔴 21.09, владелец: «добавь реакции атомов с учётом их свойств, чтоб собрать франкенштейна
/// и посмотреть как он работает».
///
/// Что работает и почему это правда:
///   • РАДИОАКТИВНЫЕ (технеций, прометий, полоний и всё тяжелее) светятся и распадаются: раз
///     в несколько секунд выбрасывают частицу, а изредка превращаются в элемент на две клетки
///     левее — это и есть альфа-распад, атом теряет два протона.
///   • ЖЕЛЕЗО, КОБАЛЬТ, НИКЕЛЬ притягиваются друг к другу. Втроём они и есть все
///     ферромагнетики при комнатной температуре.
///   • ГАЛОГЕНЫ (фтор, хлор, бром, иод) подтягивают к себе всё, у чего есть свободная связь:
///     они самые жадные до электронов.
///   • БЛАГОРОДНЫЕ ГАЗЫ мягко расталкивают соседей — ни с кем не связываются и никого не
///     держат.
///   • МЕТАЛЛЫ проводят ток: по связям между металлами пробегает искра.
///
/// 🔴 Чего тут нет: температуры, растворителя, давления и скоростей настоящих реакций.
/// Это характеры, а не расчёт.</summary>
public class Traits : MonoBehaviour
{
    float decayTimer, sparkTimer;

    void Update()
    {
        float dt = Time.deltaTime;
        decayTimer -= dt;
        sparkTimer -= dt;

        // ——— радиоактивное свечение и распад ———
        if (decayTimer <= 0f)
        {
            decayTimer = 1.2f;
            foreach (var a in Atom.All)
            {
                if (a.El.Paint != Elements.Paint.Radioactive && a.El.Paint != Elements.Paint.Unknown) continue;
                Fx.Sparks(a.transform.position, new Color(0.45f, 1f, 0.5f), 6, 1.6f, 0.05f);

                // Изредка — настоящий распад: минус два протона, элемент меняется.
                if (Random.value < 0.06f && a.El.Z > 2)
                {
                    var next = Elements.ByZ(a.El.Z - 2);
                    if (next != null)
                    {
                        Fx.Flash(a.transform.position, new Color(0.5f, 1f, 0.6f), 5f, 7f, 0.4f);
                        Fx.Sparks(a.transform.position, new Color(0.6f, 1f, 0.7f), 40, 5f);
                        Fx.Pop(1.6f);
                        string was = a.El.Name;
                        a.Become(next);
                        if (Lab.I != null)
                            Lab.I.Say(was + Lang.T(" распался: минус два протона — теперь это ", " decayed: minus two protons — now it is ") + next.Name + ".",
                                new Color(0.6f, 1f, 0.7f));
                    }
                }
            }
        }

        // ——— искра по металлическим связям ———
        if (sparkTimer <= 0f && Bond.All.Count > 0)
        {
            sparkTimer = 0.35f;
            var b = Bond.All[Random.Range(0, Bond.All.Count)];
            if (b != null && b.A != null && b.B != null &&
                b.A.El.Paint == Elements.Paint.Metal && b.B.El.Paint == Elements.Paint.Metal)
            {
                Vector3 p = Vector3.Lerp(b.A.transform.position, b.B.transform.position, Random.value);
                Fx.Sparks(p, new Color(1f, 0.95f, 0.6f), 5, 1.2f, 0.05f);
            }
        }
    }

    void FixedUpdate()
    {
        var list = Atom.All;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            for (int j = i + 1; j < list.Count; j++)
            {
                var b = list[j];
                Vector3 d = b.transform.position - a.transform.position;
                float dist = d.magnitude;
                if (dist < 0.05f || dist > 6f) continue;
                Vector3 dir = d / dist;
                float pull = 0f;

                // Магнетизм: железо, кобальт, никель тянутся друг к другу.
                if (IsMagnet(a) && IsMagnet(b)) pull += 3.2f / (dist * dist);

                // Галоген тянет к себе соседа, которому есть чем связаться.
                if (a.El.Class == Elements.Cls.Halogen && b.FreeValence > 0) pull += 1.6f / (dist * dist);
                if (b.El.Class == Elements.Cls.Halogen && a.FreeValence > 0) pull += 1.6f / (dist * dist);

                // Благородный газ расталкивает: ни с кем не дружит.
                if (a.El.Class == Elements.Cls.Noble || b.El.Class == Elements.Cls.Noble) pull -= 2.2f / (dist * dist);

                if (Mathf.Abs(pull) < 0.001f) continue;
                Vector3 f = dir * Mathf.Clamp(pull, -6f, 6f);
                a.Body.AddForce(f, ForceMode.Acceleration);
                b.Body.AddForce(-f, ForceMode.Acceleration);
            }
        }
    }

    static bool IsMagnet(Atom a) { return a.El.Sym == "Fe" || a.El.Sym == "Co" || a.El.Sym == "Ni"; }
}
