using UnityEngine;

/// <summary>Готовые вещества: собираются сразу со СТРОЕНИЕМ — углы и кратности связей как в
/// настоящей молекуле, а не куча слипшихся шариков.
///
/// 🔴 21.09, владелец: «добавь пресеты веществ (15 веществ, со строением атомов)».
///
/// Строение здесь настоящее по ФОРМЕ: вода угловая, метан тетраэдр, аммиак пирамида, бензол
/// правильное кольцо с чередующимися связями, углекислый газ линейный. А вот РАССТОЯНИЯ
/// подогнаны под размеры шариков этой игры, а не под пикометры: наши радиусы схематические,
/// и настоящие длины связей выглядели бы слипшимся комом.</summary>
public static class Presets
{
    public class P
    {
        public string Name, Formula;
        public System.Action<Vector3> Build;
    }

    static Atom A(string sym, Vector3 p) { return Atom.Spawn(Elements.BySymbol(sym), p); }
    static void B(Atom a, Atom b) { Bond.Create(a, b, 1); }
    static void B(Atom a, Atom b, int order) { Bond.Create(a, b, order); }

    public static readonly P[] All =
    {
        new P { Name = "Вода", Formula = "H2O", Build = c => {
            var o = A("O", c);
            // Угол 104.5° — из-за него вода полярная, а лёд легче воды.
            B(o, A("H", c + new Vector3( 0.92f, 0.71f, 0f)));
            B(o, A("H", c + new Vector3(-0.92f, 0.71f, 0f)));
        }},

        new P { Name = "Перекись водорода", Formula = "H2O2", Build = c => {
            var o1 = A("O", c + new Vector3(-0.65f, 0f, 0f));
            var o2 = A("O", c + new Vector3( 0.65f, 0f, 0f));
            B(o1, o2);
            B(o1, A("H", c + new Vector3(-1.15f, 0.9f,  0.45f)));
            B(o2, A("H", c + new Vector3( 1.15f, 0.9f, -0.45f)));   // молекула перекручена, как книжка
        }},

        new P { Name = "Углекислый газ", Formula = "CO2", Build = c => {
            var cc = A("C", c);
            B(cc, A("O", c + new Vector3( 1.25f, 0f, 0f)), 2);      // линейная: O=C=O
            B(cc, A("O", c + new Vector3(-1.25f, 0f, 0f)), 2);
        }},

        new P { Name = "Метан", Formula = "CH4", Build = c => {
            var cc = A("C", c);
            // Тетраэдр: четыре угла куба через один, угол 109.5°.
            Vector3[] t = { new Vector3(1,1,1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(-1,-1,1) };
            foreach (var d in t) B(cc, A("H", c + d.normalized * 1.2f));
        }},

        new P { Name = "Этан", Formula = "C2H6", Build = c => {
            var c1 = A("C", c + new Vector3(-0.65f, 0f, 0f));
            var c2 = A("C", c + new Vector3( 0.65f, 0f, 0f));
            B(c1, c2);
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                B(c1, A("H", c + new Vector3(-1.45f, Mathf.Cos(a) * 1.0f, Mathf.Sin(a) * 1.0f)));
                B(c2, A("H", c + new Vector3( 1.45f, Mathf.Cos(a + 1.05f) * 1.0f, Mathf.Sin(a + 1.05f) * 1.0f)));
            }
        }},

        new P { Name = "Этилен", Formula = "C2H4", Build = c => {
            var c1 = A("C", c + new Vector3(-0.6f, 0f, 0f));
            var c2 = A("C", c + new Vector3( 0.6f, 0f, 0f));
            B(c1, c2, 2);                                           // двойная связь держит молекулу плоской
            B(c1, A("H", c + new Vector3(-1.35f,  0.95f, 0f)));
            B(c1, A("H", c + new Vector3(-1.35f, -0.95f, 0f)));
            B(c2, A("H", c + new Vector3( 1.35f,  0.95f, 0f)));
            B(c2, A("H", c + new Vector3( 1.35f, -0.95f, 0f)));
        }},

        new P { Name = "Ацетилен", Formula = "C2H2", Build = c => {
            var c1 = A("C", c + new Vector3(-0.55f, 0f, 0f));
            var c2 = A("C", c + new Vector3( 0.55f, 0f, 0f));
            B(c1, c2, 3);                                           // тройная: молекула строго прямая
            B(c1, A("H", c + new Vector3(-1.75f, 0f, 0f)));
            B(c2, A("H", c + new Vector3( 1.75f, 0f, 0f)));
        }},

        new P { Name = "Формальдегид", Formula = "CH2O", Build = c => {
            var cc = A("C", c);
            B(cc, A("O", c + new Vector3(1.25f, 0f, 0f)), 2);
            B(cc, A("H", c + new Vector3(-0.7f,  1.0f, 0f)));
            B(cc, A("H", c + new Vector3(-0.7f, -1.0f, 0f)));
        }},

        new P { Name = "Этанол", Formula = "C2H6O", Build = c => {
            var c1 = A("C", c + new Vector3(-1.3f, 0f, 0f));
            var c2 = A("C", c);
            var o  = A("O", c + new Vector3( 1.25f, 0.35f, 0f));
            B(c1, c2); B(c2, o);
            B(o, A("H", c + new Vector3(1.9f, 1.2f, 0f)));          // вот эта O-H и делает спирт спиртом
            B(c1, A("H", c + new Vector3(-2.1f,  0.9f,  0f)));
            B(c1, A("H", c + new Vector3(-1.8f, -0.8f,  0.7f)));
            B(c1, A("H", c + new Vector3(-1.8f, -0.8f, -0.7f)));
            B(c2, A("H", c + new Vector3(-0.2f, -1.1f,  0.7f)));
            B(c2, A("H", c + new Vector3(-0.2f, -1.1f, -0.7f)));
        }},

        new P { Name = "Бензол", Formula = "C6H6", Build = c => {
            var ring = new Atom[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                ring[i] = A("C", c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 1.35f);
            }
            // Связи чередуются: одинарная, двойная, одинарная... На самом деле в бензоле они
            // все одинаковые, полуторные — но нарисовать полуторную палочку нечем, и школьная
            // формула Кекуле рисует ровно так же.
            for (int i = 0; i < 6; i++) B(ring[i], ring[(i + 1) % 6], (i % 2 == 0) ? 2 : 1);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                B(ring[i], A("H", c + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 2.5f));
            }
        }},

        new P { Name = "Аммиак", Formula = "NH3", Build = c => {
            var n = A("N", c + new Vector3(0f, 0.35f, 0f));
            // Пирамида, а не плоскость: сверху у азота неподелённая пара электронов.
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                B(n, A("H", c + new Vector3(Mathf.Cos(a) * 1.05f, -0.45f, Mathf.Sin(a) * 1.05f)));
            }
        }},

        new P { Name = "Азот", Formula = "N2", Build = c => {
            var a1 = A("N", c + new Vector3(-0.6f, 0f, 0f));
            var a2 = A("N", c + new Vector3( 0.6f, 0f, 0f));
            B(a1, a2, 3);                                           // тройная связь — оттого азот и ленив
        }},

        new P { Name = "Кислород", Formula = "O2", Build = c => {
            var a1 = A("O", c + new Vector3(-0.62f, 0f, 0f));
            var a2 = A("O", c + new Vector3( 0.62f, 0f, 0f));
            B(a1, a2, 2);
        }},

        new P { Name = "Поваренная соль", Formula = "NaCl", Build = c => {
            var na = A("Na", c + new Vector3(-0.95f, 0f, 0f));
            var cl = A("Cl", c + new Vector3( 0.95f, 0f, 0f));
            B(na, cl);                                              // связь будет жёлтой: ионная
        }},

        new P { Name = "Серная кислота", Formula = "H2SO4", Build = c => {
            var s = A("S", c);
            B(s, A("O", c + new Vector3(0f,  1.35f, 0f)), 2);
            B(s, A("O", c + new Vector3(0f, -1.35f, 0f)), 2);
            var o1 = A("O", c + new Vector3(-1.35f, 0f,  0.2f));
            var o2 = A("O", c + new Vector3( 1.35f, 0f, -0.2f));
            B(s, o1); B(s, o2);
            B(o1, A("H", c + new Vector3(-2.1f, 0.8f,  0.3f)));     // две O-H и делают её кислотой
            B(o2, A("H", c + new Vector3( 2.1f, 0.8f, -0.3f)));
        }},
    };

    /// <summary>Собрать пресет в зоне. Сера в H2SO4 берёт шесть связей — больше её обычной
    /// двойки, поэтому пресеты кладут связи НАПРЯМУЮ, не спрашивая запас: у настоящей серы
    /// валентность как раз переменная, а в игре она одна.</summary>
    public static void Spawn(P p)
    {
        Vector3 c = Lab.ZoneCenter + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.6f, 0.6f), Random.Range(-0.8f, 0.8f));
        p.Build(c);
        Fx.Chime();
        Fx.Flash(c, new Color(0.6f, 0.9f, 1f), 6f, 9f, 0.6f);
        Fx.Sparks(c, new Color(0.7f, 0.95f, 1f), 40, 3.5f);
        if (Lab.I != null)
        {
            Lab.I.Recompute();
            Lab.I.Say("Собрано: " + p.Name + " (" + p.Formula + ") — со своим строением.", new Color(0.7f, 0.95f, 1f));
        }
    }
}
