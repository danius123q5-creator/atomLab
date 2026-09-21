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
        new P { Name = Lang.T("Вода", "Water"), Formula = "H2O", Build = c => {
            var o = A("O", c);
            // Угол 104.5° — из-за него вода полярная, а лёд легче воды.
            B(o, A("H", c + new Vector3( 0.92f, 0.71f, 0f)));
            B(o, A("H", c + new Vector3(-0.92f, 0.71f, 0f)));
        }},

        new P { Name = Lang.T("Перекись водорода", "Hydrogen peroxide"), Formula = "H2O2", Build = c => {
            var o1 = A("O", c + new Vector3(-0.65f, 0f, 0f));
            var o2 = A("O", c + new Vector3( 0.65f, 0f, 0f));
            B(o1, o2);
            B(o1, A("H", c + new Vector3(-1.15f, 0.9f,  0.45f)));
            B(o2, A("H", c + new Vector3( 1.15f, 0.9f, -0.45f)));   // молекула перекручена, как книжка
        }},

        new P { Name = Lang.T("Углекислый газ", "Carbon dioxide"), Formula = "CO2", Build = c => {
            var cc = A("C", c);
            B(cc, A("O", c + new Vector3( 1.25f, 0f, 0f)), 2);      // линейная: O=C=O
            B(cc, A("O", c + new Vector3(-1.25f, 0f, 0f)), 2);
        }},

        new P { Name = Lang.T("Метан", "Methane"), Formula = "CH4", Build = c => {
            var cc = A("C", c);
            // Тетраэдр: четыре угла куба через один, угол 109.5°.
            Vector3[] t = { new Vector3(1,1,1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(-1,-1,1) };
            foreach (var d in t) B(cc, A("H", c + d.normalized * 1.2f));
        }},

        new P { Name = Lang.T("Этан", "Ethane"), Formula = "C2H6", Build = c => {
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

        new P { Name = Lang.T("Этилен", "Ethylene"), Formula = "C2H4", Build = c => {
            var c1 = A("C", c + new Vector3(-0.6f, 0f, 0f));
            var c2 = A("C", c + new Vector3( 0.6f, 0f, 0f));
            B(c1, c2, 2);                                           // двойная связь держит молекулу плоской
            B(c1, A("H", c + new Vector3(-1.35f,  0.95f, 0f)));
            B(c1, A("H", c + new Vector3(-1.35f, -0.95f, 0f)));
            B(c2, A("H", c + new Vector3( 1.35f,  0.95f, 0f)));
            B(c2, A("H", c + new Vector3( 1.35f, -0.95f, 0f)));
        }},

        new P { Name = Lang.T("Ацетилен", "Acetylene"), Formula = "C2H2", Build = c => {
            var c1 = A("C", c + new Vector3(-0.55f, 0f, 0f));
            var c2 = A("C", c + new Vector3( 0.55f, 0f, 0f));
            B(c1, c2, 3);                                           // тройная: молекула строго прямая
            B(c1, A("H", c + new Vector3(-1.75f, 0f, 0f)));
            B(c2, A("H", c + new Vector3( 1.75f, 0f, 0f)));
        }},

        new P { Name = Lang.T("Формальдегид", "Formaldehyde"), Formula = "CH2O", Build = c => {
            var cc = A("C", c);
            B(cc, A("O", c + new Vector3(1.25f, 0f, 0f)), 2);
            B(cc, A("H", c + new Vector3(-0.7f,  1.0f, 0f)));
            B(cc, A("H", c + new Vector3(-0.7f, -1.0f, 0f)));
        }},

        new P { Name = Lang.T("Этанол", "Ethanol"), Formula = "C2H6O", Build = c => {
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

        new P { Name = Lang.T("Бензол", "Benzene"), Formula = "C6H6", Build = c => {
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

        new P { Name = Lang.T("Аммиак", "Ammonia"), Formula = "NH3", Build = c => {
            var n = A("N", c + new Vector3(0f, 0.35f, 0f));
            // Пирамида, а не плоскость: сверху у азота неподелённая пара электронов.
            for (int i = 0; i < 3; i++)
            {
                float a = i * 120f * Mathf.Deg2Rad;
                B(n, A("H", c + new Vector3(Mathf.Cos(a) * 1.05f, -0.45f, Mathf.Sin(a) * 1.05f)));
            }
        }},

        new P { Name = Lang.T("Азот", "Nitrogen"), Formula = "N2", Build = c => {
            var a1 = A("N", c + new Vector3(-0.6f, 0f, 0f));
            var a2 = A("N", c + new Vector3( 0.6f, 0f, 0f));
            B(a1, a2, 3);                                           // тройная связь — оттого азот и ленив
        }},

        new P { Name = Lang.T("Кислород", "Oxygen"), Formula = "O2", Build = c => {
            var a1 = A("O", c + new Vector3(-0.62f, 0f, 0f));
            var a2 = A("O", c + new Vector3( 0.62f, 0f, 0f));
            B(a1, a2, 2);
        }},

        new P { Name = Lang.T("Поваренная соль", "Table salt"), Formula = "NaCl", Build = c => {
            var na = A("Na", c + new Vector3(-0.95f, 0f, 0f));
            var cl = A("Cl", c + new Vector3( 0.95f, 0f, 0f));
            B(na, cl);                                              // связь будет жёлтой: ионная
        }},

        new P { Name = Lang.T("Серная кислота", "Sulfuric acid"), Formula = "H2SO4", Build = c => {
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

    // ==================== 20 популярных соединений ====================
    //
    // 21.09, владелец: «под таблицу добавь 20 популярных соединений». Сетка под таблицей
    // Менделеева: нажал — вещество появилось в зоне.
    //
    // Все двадцать взяты из справочника игры (Molecules), поэтому карточка их узнаёт и
    // показывает название, заметку и применение. Девять из двадцати есть среди пресетов —
    // они собираются с настоящим строением. Остальные собираются общим сборщиком
    // (Chemistry.Assemble): СОСТАВ точный, а строение приблизительное — центральный атом и
    // соседи вокруг. Это честно написано в сообщении, чтобы глюкоза «ёжиком» не выдавала себя
    // за настоящую.
    public class Pop { public string Formula, Ru, En; }

    public static readonly Pop[] Popular =
    {
        new Pop { Formula = "H2O",       Ru = "Вода",             En = "Water" },
        new Pop { Formula = "CO2",       Ru = "Углекислый газ",   En = "Carbon dioxide" },
        new Pop { Formula = "O2",        Ru = "Кислород",         En = "Oxygen" },
        new Pop { Formula = "NaCl",      Ru = "Соль",             En = "Table salt" },
        new Pop { Formula = "CH4",       Ru = "Метан",            En = "Methane" },
        new Pop { Formula = "NH3",       Ru = "Аммиак",           En = "Ammonia" },
        new Pop { Formula = "C2H6O",     Ru = "Спирт",            En = "Ethanol" },
        new Pop { Formula = "H2SO4",     Ru = "Серная кислота",   En = "Sulfuric acid" },
        new Pop { Formula = "H2O2",      Ru = "Перекись",         En = "Peroxide" },
        new Pop { Formula = "O3",        Ru = "Озон",             En = "Ozone" },
        new Pop { Formula = "CO",        Ru = "Угарный газ",      En = "Carbon monoxide" },
        new Pop { Formula = "HCl",       Ru = "Соляная кислота",  En = "Hydrochloric acid" },
        new Pop { Formula = "NaOH",      Ru = "Едкий натр",       En = "Caustic soda" },
        new Pop { Formula = "NaHCO3",    Ru = "Пищевая сода",     En = "Baking soda" },
        new Pop { Formula = "CaCO3",     Ru = "Мел",              En = "Chalk" },
        new Pop { Formula = "C2H4O2",    Ru = "Уксус",            En = "Vinegar" },
        new Pop { Formula = "C6H12O6",   Ru = "Глюкоза",          En = "Glucose" },
        new Pop { Formula = "C12H22O11", Ru = "Сахар",            En = "Sugar" },
        new Pop { Formula = "C8H10N4O2", Ru = "Кофеин",           En = "Caffeine" },
        new Pop { Formula = "C9H8O4",    Ru = "Аспирин",          En = "Aspirin" },
    };

    /// <summary>21.09, владелец: «заполни элементами всё пространство и ниже тоже сделай».
    /// Сетка под таблицей — это теперь сначала двадцать популярных, а за ними ВЕСЬ справочник
    /// игры, до последнего вещества. Порядок справочника сохранён (он идёт от простого к
    /// сложному), популярные не повторяются.</summary>
    static Pop[] _grid;
    public static Pop[] Grid
    {
        get
        {
            if (_grid != null) return _grid;
            var list = new System.Collections.Generic.List<Pop>(Popular);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (var q in Popular) seen.Add(Molecules.Canon(Molecules.ParseFormula(q.Formula)));
            foreach (var info in Molecules.DB.Values)
            {
                string key = Molecules.Canon(Molecules.ParseFormula(info.Formula));
                if (!seen.Add(key)) continue;
                list.Add(new Pop { Formula = info.Formula, Ru = info.Name,
                                   En = string.IsNullOrEmpty(info.NameEn) ? info.Name : info.NameEn });
            }
            _grid = list.ToArray();
            return _grid;
        }
    }

    public static void SpawnPopular(Pop q)
    {
        foreach (var p in All) if (p.Formula == q.Formula) { Spawn(p); return; }

        Vector3 c = Lab.ZoneCenter + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.6f, 0.6f), Random.Range(-0.8f, 0.8f));
        var atoms = new System.Collections.Generic.List<Atom>();
        int k = 0;
        foreach (var kv in Molecules.ParseFormula(q.Formula))
        {
            var el = Elements.BySymbol(kv.Key);
            if (el == null) { Debug.LogWarning("нет элемента " + kv.Key + " для " + q.Formula); return; }
            for (int i = 0; i < kv.Value; i++, k++)
                atoms.Add(Atom.Spawn(el, c + new Vector3((k % 5) * 0.3f, (k / 5) * 0.3f, 0f)));
        }
        if (q.Formula == "NH4Cl")
        {
            // Нашатырь — соль из ИОНОВ: аммоний NH4+ и хлорид Cl-. Одной ковалентной молекулой
            // его не собрать никак — азоту понадобилось бы пять связей. Кладём как пресет:
            // аммоний-тетраэдр, а к азоту — хлор той же связью, которой игра рисует ионную пару
            // в NaCl. Проверка всего справочника поймала, что общий сборщик его разваливал.
            var n = atoms.Find(t => t.El.Sym == "N");
            var cl = atoms.Find(t => t.El.Sym == "Cl");
            n.transform.position = c;
            Vector3[] tet = { new Vector3(1,1,1), new Vector3(1,-1,-1), new Vector3(-1,1,-1), new Vector3(-1,-1,1) };
            int hi = 0;
            foreach (var h in atoms) if (h.El.Sym == "H") { h.transform.position = c + tet[hi++ % 4].normalized * 1.1f; Bond.Create(n, h); }
            cl.transform.position = c + new Vector3(-1.9f, 0f, 0f);
            Bond.Create(n, cl);
        }
        else Chemistry.Assemble(atoms, c);
        Fx.Chime();
        Fx.Flash(c, new Color(0.6f, 0.9f, 1f), 6f, 9f, 0.6f);
        Fx.Sparks(c, new Color(0.7f, 0.95f, 1f), 40, 3.5f);
        if (Lab.I != null)
        {
            Lab.I.Recompute();
            Lab.I.Say(Lang.T("Собрано: ", "Built: ") + Lang.T(q.Ru, q.En) + " (" + q.Formula +
                      Lang.T(") — состав точный, строение приблизительное.", ") — exact composition, approximate structure."),
                      new Color(0.7f, 0.95f, 1f));
        }
    }

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
            Lab.I.Say(Lang.T("Собрано: ", "Built: ") + p.Name + " (" + p.Formula + Lang.T(") — со своим строением.", ") — with its real structure."), new Color(0.7f, 0.95f, 1f));
        }
    }
}
