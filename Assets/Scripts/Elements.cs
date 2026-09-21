using System.Collections.Generic;
using UnityEngine;

/// <summary>Вся таблица Менделеева — 118 элементов, данными, а не префабами.
///
/// Строка данных: символ|название|масса|группа|период|валентность|электроотрицательность|цвет|класс
/// Группа 0 означает f-блок (лантаноиды и актиноиды): они рисуются двумя отдельными рядами
/// под основной таблицей, как в учебнике.
///
/// 🔴 ЧЕСТНО О ЧИСЛАХ: масса, группа, период, электроотрицательность (шкала Полинга) и цвет
/// (палитра CPK, ей красят атомы во всех химических редакторах) — настоящие. А вот
/// ВАЛЕНТНОСТЬ здесь одна на элемент — «самая обычная». У железа их две (II и III), у серы
/// три (II, IV, VI). Это игра-конструктор, а не справочник: одно число даёт понятные правила
/// сборки. Радиус шарика тоже схематический — считается из периода и группы (вниз крупнее,
/// вправо мельче), а не берётся из измерений.</summary>
public static class Elements
{
    public enum Cls { Nonmetal, Noble, Alkali, AlkEarth, Metalloid, Halogen, Transition, PostMetal, Lanth, Actin }

    /// <summary>Крупная раскраска таблицы (🔴 21.09, владелец): красный металлы, синий
    /// неметаллы, зелёный радиоактивные, жёлтый — те, о которых толком ничего не известно.
    /// Порядок проверки важен: сверхтяжёлые элементы и радиоактивны, и неизучены, поэтому
    /// «неизвестный» перебивает «радиацию», иначе жёлтого в таблице не было бы совсем.</summary>
    public enum Paint { Metal, Nonmetal, Radioactive, Unknown, Synthetic, Assembled }

    /// <summary>Высшая валентность — только там, где она выше обычной. Числа — настоящие
    /// высшие степени окисления/ковалентности: S 6 (H2SO4, SF6), Cl 7 (HClO4), Mn 7 (KMnO4),
    /// Xe 8 (XeO4), N 4 (HNO3: у азота четыре связи, пятой в жизни нет).</summary>
    public static readonly System.Collections.Generic.Dictionary<string, int> HighValence =
        new System.Collections.Generic.Dictionary<string, int>
    {
        {"B",4},{"N",4},{"P",5},{"S",6},{"Cl",7},{"As",5},{"Se",6},{"Br",7},{"Sb",5},{"Te",6},{"I",7},{"Bi",5},
        {"Kr",2},{"Xe",8},{"Rn",6},{"Si",4},{"Ge",4},{"Sn",4},{"Pb",4},{"Tl",3},{"Ga",3},{"In",3},
        {"Ti",4},{"V",5},{"Cr",6},{"Mn",7},{"Co",3},{"Ni",3},{"Zn",2},   // 21.09: Fe 6, Cu 3 убраны — это экзотика (ферраты), в школьной химии железо до трёх, медь до двух
        {"Zr",4},{"Nb",5},{"Mo",6},{"Tc",7},{"Ru",8},{"Rh",6},{"Pd",4},{"Cd",2},
        {"Hf",4},{"Ta",5},{"W",6},{"Re",7},{"Os",8},{"Ir",6},{"Pt",4},{"Au",3},{"Hg",2},
        {"Ce",4},{"U",6},{"Np",7},{"Pu",7},{"Am",6},{"Th",4},{"Pa",5},
    };

    public class El
    {
        public int Z;               // порядковый номер = число протонов
        public string Sym, Name;
        public string NameEn;       // английское название; пусто -> показываем русское
        public float Mass;
        public int Group, Period;
        public int Valence;         // обычная валентность: на ней считаются реакции и соли

        /// <summary>🔴 21.09, владелец: «убери лимит подключения у атомов, а то некоторые
        /// соединения нельзя собрать». Причина была в том, что у серы, азота, фосфора, хлора,
        /// марганца одна валентность, а в жизни их несколько: H2SO4, SO3, HNO3, PCl5, KMnO4
        /// руками не собирались.
        ///
        /// Лечим ПРЕДЕЛОМ СВЯЗЕЙ, а не валентностью. Valence трогать нельзя: по ней движок
        /// реакций считает, сколько хлора идёт на натрий. Поднять её — и соль стала бы NaCl7.
        /// Поэтому предел — отдельное число: высшая из настоящих валентностей элемента.
        /// Водород, кислород, фтор, натрий остаются при своих — у них другой валентности нет,
        /// и H с пятью соседями был бы уже не химией. Совсем без предела — это «Режим бога».
        ///
        /// У собранного иона (оранжевая клетка) предел — его заряд: там число задано руками.</summary>
        /// <summary>Символ для ХИМИИ (21.09, ревью Саула). У собранного атома Sym — подпись
        /// («O-18», «Fe-56 3+»), и по ней вода из тяжёлого кислорода не узнавалась водой, а
        /// ион железа выпадал из ряда активности. Изотопы химически одинаковы — это главное,
        /// чему учит сборка атома. Для счёта состава берём символ элемента по номеру.</summary>
        public string ChemSym
        {
            get
            {
                if (!Assembled && !Synthetic) return Sym;
                var b = ByZ(Z);
                return (b != null && !b.Assembled && !b.Synthetic) ? b.Sym : Sym;
            }
        }

        public int MaxBonds
        {
            get
            {
                if (Assembled) return Valence;
                int m;
                return HighValence.TryGetValue(Sym, out m) ? Mathf.Max(m, Valence) : Valence;
            }
        }
        public float EN;            // электроотрицательность, 0 = не определена
        public Color Color;
        public Cls Class;

        /// <summary>Добыт в ускорителе, а не выдан таблицей. Такие клетки голубые и стоят
        /// отдельным рядом под таблицей.</summary>
        public bool Synthetic;

        /// <summary>Собран вручную из протонов, нейтронов и электронов. Такие клетки
        /// оранжевые. У них, в отличие от обычных клеток, записано ЧИСЛО НЕЙТРОНОВ и ЗАРЯД —
        /// то есть это конкретный изотоп или ион, а не элемент вообще.</summary>
        public bool Assembled;
        public int Neutrons;
        public int Charge;

        /// <summary>Массовое число: протоны плюс нейтроны. У обычной клетки таблицы его нет —
        /// там стоит средняя масса всех изотопов, дробная.</summary>
        public int MassNumber { get { return Z + Neutrons; } }

        /// <summary>Радиус шарика — от НАСТОЯЩЕГО ковалентного радиуса элемента (таблица
        /// Кордеро, пикометры), а не от номера строки.
        ///
        /// 🔴 21.09, владелец: «пресет кривой». Он был прав: по старой формуле водород выходил
        /// почти с углерод (0.385 против 0.415), и молекула получалась комом из одинаковых
        /// шаров. На деле водород вдвое меньше. Общая добавка 0.14 оставлена нарочно — с
        /// чистой пропорцией водород выходит меньше двух десятых и в него не попасть мышью.</summary>
        public float Radius { get { return 0.14f + CovalentPm / 400f; } }

        /// <summary>Ковалентный радиус в пикометрах. Величина настоящая, измеренная.</summary>
        public float CovalentPm
        {
            get
            {
                if (Z >= 1 && Z <= COV.Length) return COV[Z - 1];
                // Для добытых в ускорителе радиуса не существует: элемент выдуман. Берём
                // продолжение ряда сверхтяжёлых и честно называем это оценкой.
                return 160f + (Z - COV.Length) * 1.5f;
            }
        }

        /// <summary>Радиоактивны: технеций (43), прометий (61) и всё от полония (84) и дальше —
        /// у этих элементов нет ни одного стабильного изотопа. Это не приблизительно, это
        /// ровно тот список, который учат в школе как «дальше стабильных нет».</summary>
        public Paint Paint
        {
            get
            {
                if (Assembled) return Elements.Paint.Assembled;
                if (Synthetic) return Elements.Paint.Synthetic;
                if (Z >= 104) return Elements.Paint.Unknown;
                if (Z == 43 || Z == 61 || Z >= 84) return Elements.Paint.Radioactive;
                switch (Class)
                {
                    case Cls.Alkali:
                    case Cls.AlkEarth:
                    case Cls.Transition:
                    case Cls.PostMetal:
                    case Cls.Lanth:
                    case Cls.Actin: return Elements.Paint.Metal;
                    default: return Elements.Paint.Nonmetal;
                }
            }
        }

        public Color PaintColor
        {
            get
            {
                switch (Paint)
                {
                    case Elements.Paint.Metal: return new Color(0.82f, 0.20f, 0.22f);
                    case Elements.Paint.Nonmetal: return new Color(0.20f, 0.45f, 0.88f);
                    case Elements.Paint.Radioactive: return new Color(0.20f, 0.72f, 0.32f);
                    case Elements.Paint.Synthetic: return new Color(0.35f, 0.80f, 1.00f);
                    case Elements.Paint.Assembled: return new Color(1.00f, 0.55f, 0.10f);
                    default: return new Color(0.92f, 0.80f, 0.18f);
                }
            }
        }

        public string PaintName
        {
            get
            {
                switch (Paint)
                {
                    case Elements.Paint.Metal: return "металл";
                    case Elements.Paint.Nonmetal: return "неметалл";
                    case Elements.Paint.Radioactive: return "радиоактивный";
                    case Elements.Paint.Synthetic: return "синтезирован в ускорителе";
                    case Elements.Paint.Assembled: return "собран из частиц";
                    default: return "неизученный";
                }
            }
        }

        public string ClassName
        {
            get
            {
                switch (Class)
                {
                    case Cls.Nonmetal: return "неметалл";
                    case Cls.Noble: return "благородный газ";
                    case Cls.Alkali: return "щелочной металл";
                    case Cls.AlkEarth: return "щёлочноземельный";
                    case Cls.Metalloid: return "полуметалл";
                    case Cls.Halogen: return "галоген";
                    case Cls.Transition: return "переходный металл";
                    case Cls.PostMetal: return "металл";
                    case Cls.Lanth: return "лантаноид";
                    default: return "актиноид";
                }
            }
        }
    }


    /// <summary>Ковалентные радиусы, пикометры, по порядку от водорода до оганесона.
    /// Для сверхтяжёлых элементов это расчётные оценки — измерять там нечего, их получают
    /// поштучно.</summary>
    static readonly int[] COV =
    {
        31, 28, 128, 96, 84, 76, 71, 66, 57, 58,
        166, 141, 121, 111, 107, 105, 102, 106,
        203, 176, 170, 160, 153, 139, 139, 132, 126, 124, 132, 122, 122, 120, 119, 120, 120, 116,
        220, 195, 190, 175, 164, 154, 147, 146, 142, 139, 145, 144, 142, 139, 139, 138, 139, 140,
        244, 215, 207, 204, 203, 201, 199, 198, 198, 196, 194, 192, 192, 189, 190, 187, 187,
        175, 170, 162, 151, 144, 141, 136, 136, 132, 145, 146, 148, 140, 150, 150,
        260, 221, 215, 206, 200, 196, 190, 187, 180, 169, 168, 168, 165, 167, 173, 176, 161,
        157, 149, 143, 141, 134, 129, 128, 121, 122, 136, 143, 162, 175, 165, 157
    };

    static readonly string[] RAW =
    {
        "H|Водород|1.008|1|1|1|2.20|FFFFFF|N",
        "He|Гелий|4.003|18|1|0|0|D9FFFF|G",
        "Li|Литий|6.94|1|2|1|0.98|CC80FF|A",
        "Be|Бериллий|9.012|2|2|2|1.57|C2FF00|E",
        "B|Бор|10.81|13|2|3|2.04|FFB5B5|M",
        "C|Углерод|12.011|14|2|4|2.55|505050|N",
        "N|Азот|14.007|15|2|3|3.04|3050F8|N",
        "O|Кислород|15.999|16|2|2|3.44|FF0D0D|N",
        "F|Фтор|18.998|17|2|1|3.98|90E050|H",
        "Ne|Неон|20.180|18|2|0|0|B3E3F5|G",
        "Na|Натрий|22.990|1|3|1|0.93|AB5CF2|A",
        "Mg|Магний|24.305|2|3|2|1.31|8AFF00|E",
        "Al|Алюминий|26.982|13|3|3|1.61|BFA6A6|P",
        "Si|Кремний|28.085|14|3|4|1.90|F0C8A0|M",
        "P|Фосфор|30.974|15|3|3|2.19|FF8000|N",
        "S|Сера|32.06|16|3|2|2.58|FFFF30|N",
        "Cl|Хлор|35.45|17|3|1|3.16|1FF01F|H",
        "Ar|Аргон|39.948|18|3|0|0|80D1E3|G",
        "K|Калий|39.098|1|4|1|0.82|8F40D4|A",
        "Ca|Кальций|40.078|2|4|2|1.00|3DFF00|E",
        "Sc|Скандий|44.956|3|4|3|1.36|E6E6E6|T",
        "Ti|Титан|47.867|4|4|4|1.54|BFC2C7|T",
        "V|Ванадий|50.942|5|4|5|1.63|A6A6AB|T",
        "Cr|Хром|51.996|6|4|3|1.66|8A99C7|T",
        "Mn|Марганец|54.938|7|4|2|1.55|9C7AC7|T",
        "Fe|Железо|55.845|8|4|3|1.83|E06633|T",
        "Co|Кобальт|58.933|9|4|2|1.88|F090A0|T",
        "Ni|Никель|58.693|10|4|2|1.91|50D050|T",
        "Cu|Медь|63.546|11|4|2|1.90|C88033|T",
        "Zn|Цинк|65.38|12|4|2|1.65|7D80B0|T",
        "Ga|Галлий|69.723|13|4|3|1.81|C28F8F|P",
        "Ge|Германий|72.630|14|4|4|2.01|668F8F|M",
        "As|Мышьяк|74.922|15|4|3|2.18|BD80E3|M",
        "Se|Селен|78.971|16|4|2|2.55|FFA100|N",
        "Br|Бром|79.904|17|4|1|2.96|A62929|H",
        "Kr|Криптон|83.798|18|4|0|3.00|5CB8D1|G",
        "Rb|Рубидий|85.468|1|5|1|0.82|702EB0|A",
        "Sr|Стронций|87.62|2|5|2|0.95|00FF00|E",
        "Y|Иттрий|88.906|3|5|3|1.22|94FFFF|T",
        "Zr|Цирконий|91.224|4|5|4|1.33|94E0E0|T",
        "Nb|Ниобий|92.906|5|5|5|1.60|73C2C9|T",
        "Mo|Молибден|95.95|6|5|6|2.16|54B5B5|T",
        "Tc|Технеций|98|7|5|7|1.90|3B9E9E|T",
        "Ru|Рутений|101.07|8|5|4|2.20|248F8F|T",
        "Rh|Родий|102.91|9|5|3|2.28|0A7D8C|T",
        "Pd|Палладий|106.42|10|5|2|2.20|006985|T",
        "Ag|Серебро|107.87|11|5|1|1.93|C0C0C0|T",
        "Cd|Кадмий|112.41|12|5|2|1.69|FFD98F|T",
        "In|Индий|114.82|13|5|3|1.78|A67573|P",
        "Sn|Олово|118.71|14|5|4|1.96|668080|P",
        "Sb|Сурьма|121.76|15|5|3|2.05|9E63B5|M",
        "Te|Теллур|127.60|16|5|2|2.10|D47A00|M",
        "I|Иод|126.90|17|5|1|2.66|940094|H",
        "Xe|Ксенон|131.29|18|5|0|2.60|429EB0|G",
        "Cs|Цезий|132.91|1|6|1|0.79|57178F|A",
        "Ba|Барий|137.33|2|6|2|0.89|00C900|E",
        "La|Лантан|138.91|0|6|3|1.10|70D4FF|L",
        "Ce|Церий|140.12|0|6|3|1.12|FFFFC7|L",
        "Pr|Празеодим|140.91|0|6|3|1.13|D9FFC7|L",
        "Nd|Неодим|144.24|0|6|3|1.14|C7FFC7|L",
        "Pm|Прометий|145|0|6|3|1.13|A3FFC7|L",
        "Sm|Самарий|150.36|0|6|3|1.17|8FFFC7|L",
        "Eu|Европий|151.96|0|6|3|1.20|61FFC7|L",
        "Gd|Гадолиний|157.25|0|6|3|1.20|45FFC7|L",
        "Tb|Тербий|158.93|0|6|3|1.10|30FFC7|L",
        "Dy|Диспрозий|162.50|0|6|3|1.22|1FFFC7|L",
        "Ho|Гольмий|164.93|0|6|3|1.23|00FF9C|L",
        "Er|Эрбий|167.26|0|6|3|1.24|00E675|L",
        "Tm|Тулий|168.93|0|6|3|1.25|00D452|L",
        "Yb|Иттербий|173.05|0|6|3|1.10|00BF38|L",
        "Lu|Лютеций|174.97|0|6|3|1.27|00AB24|L",
        "Hf|Гафний|178.49|4|6|4|1.30|4DC2FF|T",
        "Ta|Тантал|180.95|5|6|5|1.50|4DA6FF|T",
        "W|Вольфрам|183.84|6|6|6|2.36|2194D6|T",
        "Re|Рений|186.21|7|6|7|1.90|267DAB|T",
        "Os|Осмий|190.23|8|6|4|2.20|266696|T",
        "Ir|Иридий|192.22|9|6|4|2.20|175487|T",
        "Pt|Платина|195.08|10|6|4|2.28|D0D0E0|T",
        "Au|Золото|196.97|11|6|3|2.54|FFD123|T",
        "Hg|Ртуть|200.59|12|6|2|2.00|B8B8D0|T",
        "Tl|Таллий|204.38|13|6|1|1.62|A6544D|P",
        "Pb|Свинец|207.2|14|6|2|2.33|575961|P",
        "Bi|Висмут|208.98|15|6|3|2.02|9E4FB5|P",
        "Po|Полоний|209|16|6|2|2.00|AB5C00|M",
        "At|Астат|210|17|6|1|2.20|754F45|H",
        "Rn|Радон|222|18|6|0|2.20|428296|G",
        "Fr|Франций|223|1|7|1|0.70|420066|A",
        "Ra|Радий|226|2|7|2|0.90|007D00|E",
        "Ac|Актиний|227|0|7|3|1.10|70ABFA|C",
        "Th|Торий|232.04|0|7|4|1.30|00BAFF|C",
        "Pa|Протактиний|231.04|0|7|5|1.50|00A1FF|C",
        "U|Уран|238.03|0|7|6|1.38|008FFF|C",
        "Np|Нептуний|237|0|7|5|1.36|0080FF|C",
        "Pu|Плутоний|244|0|7|4|1.28|006BFF|C",
        "Am|Америций|243|0|7|3|1.13|545CF2|C",
        "Cm|Кюрий|247|0|7|3|1.28|785CE3|C",
        "Bk|Берклий|247|0|7|3|1.30|8A4FE3|C",
        "Cf|Калифорний|251|0|7|3|1.30|A136D4|C",
        "Es|Эйнштейний|252|0|7|3|1.30|B31FD4|C",
        "Fm|Фермий|257|0|7|3|1.30|B31FBA|C",
        "Md|Менделевий|258|0|7|3|1.30|B30DA6|C",
        "No|Нобелий|259|0|7|3|1.30|BD0D87|C",
        "Lr|Лоуренсий|266|0|7|3|1.30|C70066|C",
        "Rf|Резерфордий|267|4|7|4|0|CC0059|T",
        "Db|Дубний|268|5|7|5|0|D1004F|T",
        "Sg|Сиборгий|269|6|7|6|0|D90045|T",
        "Bh|Борий|270|7|7|7|0|E00038|T",
        "Hs|Хассий|269|8|7|4|0|E6002E|T",
        "Mt|Мейтнерий|278|9|7|3|0|EB0026|T",
        "Ds|Дармштадтий|281|10|7|2|0|EB0026|T",
        "Rg|Рентгений|282|11|7|3|0|D9004F|T",
        "Cn|Коперниций|285|12|7|2|0|C2007A|T",
        "Nh|Нихоний|286|13|7|1|0|B0008F|P",
        "Fl|Флеровий|289|14|7|4|0|9E00A6|P",
        "Mc|Московий|290|15|7|3|0|8C00BA|P",
        "Lv|Ливерморий|293|16|7|2|0|7A00CC|P",
        "Ts|Теннессин|294|17|7|1|0|6B00DB|H",
        "Og|Оганесон|294|18|7|0|0|5C00EB|G",
    };

    static El[] _all;
    static Dictionary<string, El> _bySym;

    public static El[] All
    {
        get { if (_all == null) Parse(); return _all; }
    }

    public static El BySymbol(string sym)
    {
        if (_bySym == null) Parse();
        El e; return _bySym.TryGetValue(sym, out e) ? e : null;
    }

    /// <summary>Элемент по порядковому номеру.
    ///
    /// 🔴 21.09. Раньше здесь стояло _all[z - 1] — номер брался как МЕСТО В МАССИВЕ. Пока в
    /// таблице были только настоящие 118 элементов, место и номер совпадали. Как только снизу
    /// прирос свой ряд (добытые в ускорителе и собранные из частиц), совпадение кончилось, и
    /// «элемент 119» оказывался литием-34. Поймала это проверка синтеза: Og + H дали Z=3.
    ///
    /// Теперь ищем ПО НОМЕРУ. Собранные вручную пропускаем: у изотопа кислорода-18 номер
    /// тоже восемь, но «элемент номер 8» — это кислород, а не одна из его записей.</summary>
    public static El ByZ(int z)
    {
        if (_all == null) Parse();
        for (int i = 0; i < _all.Length; i++)
            if (_all[i].Z == z && !_all[i].Assembled) return _all[i];
        return null;
    }

    /// <summary>Записать в таблицу элемент, склеенный в ускорителе. 🔴 21.09, владелец:
    /// «ускоритель склеивает частицы и сохраняет их в таблицу Менделеева под голубым цветом».
    ///
    /// Имя и символ строятся по НАСТОЯЩЕМУ правилу ИЮПАК для ещё не названных элементов: имя
    /// читается по цифрам номера (ун-ун-энний для 119), символ — первые буквы этих корней.
    /// Так в учебниках и стоят элементы, которым имя ещё не дали.</summary>
    public static El AddSynthetic(int z, float mass)
    {
        if (_all == null) Parse();
        var exist = ByZ(z);
        if (exist != null) return exist;

        string name = SystematicName(z);
        string sym = SystematicSymbol(z);
        while (_bySym.ContainsKey(sym)) sym += "x";

        var el = new El
        {
            Z = z,
            Sym = sym,
            Name = name,
            Mass = mass,
            Group = 0,
            Period = 8,
            Valence = 4,              // неизвестна: берём четыре, чтобы элемент был на что-то годен
            EN = 0f,
            Color = new Color(0.35f, 0.80f, 1.00f),
            Class = Cls.Actin,
            Synthetic = true,
        };

        // Не сортируем: порядок в массиве больше ничего не значит, а сортировка мешала бы
        // рядам «добытых» и «собранных» стоять в порядке появления.
        var list = new List<El>(_all);
        list.Add(el);
        _all = list.ToArray();
        _bySym[sym] = el;
        return el;
    }

    /// <summary>Записать в таблицу собранный вручную атом: изотоп (столько-то нейтронов)
    /// или ион (электронов не поровну с протонами). 🔴 21.09, владелец: режим «сборка атома»,
    /// результат сохраняется в таблицу оранжевым.
    ///
    /// Обозначения настоящие: кислород с десятью нейтронами — это кислород-18 (8 протонов
    /// плюс 10 нейтронов), а железо, потерявшее три электрона, — Fe3+.</summary>
    public static El AddAssembled(int z, int neutrons, int charge)
    {
        if (_all == null) Parse();

        var baseEl = ByZ(z);
        string bSym = baseEl != null ? baseEl.Sym : SystematicSymbol(z);
        string bName = baseEl != null ? baseEl.Name : SystematicName(z);

        int a = z + neutrons;
        string sym = bSym + "-" + a;
        string name = bName + "-" + a;
        if (charge != 0)
        {
            string sign = charge > 0 ? "+" : "-";
            string mag = Mathf.Abs(charge) > 1 ? Mathf.Abs(charge).ToString() : "";
            sym += " " + mag + sign;
            name += " (ион " + mag + sign + ")";
        }

        var found = BySymbol(sym);
        if (found != null) return found;

        var el = new El
        {
            Z = z,
            Sym = sym,
            Name = name,
            Mass = a,
            Group = baseEl != null ? baseEl.Group : 0,
            Period = baseEl != null ? baseEl.Period : 8,
            // Ион уже отдал или взял электроны — связей у него ровно столько, сколько он
            // отдал/взял. Нейтральный собранный атом ведёт себя как обычный элемент.
            Valence = charge != 0 ? Mathf.Abs(charge) : (baseEl != null ? baseEl.Valence : 4),
            EN = baseEl != null ? baseEl.EN : 0f,
            Color = new Color(1.00f, 0.55f, 0.10f),
            Class = baseEl != null ? baseEl.Class : Cls.Actin,
            Assembled = true,
            Neutrons = neutrons,
            Charge = charge,
        };

        var list = new List<El>(_all);
        list.Add(el);
        _all = list.ToArray();
        _bySym[sym] = el;
        return el;
    }

    /// <summary>Устойчив ли такой изотоп. Считаем по числу нейтронов: у лёгких элементов их
    /// примерно поровну с протонами, у тяжёлых — в полтора раза больше. Ожидаемое число
    /// берём из настоящей средней массы элемента, допуск растёт с номером.
    ///
    /// 🔴 Это прикидка, а не таблица нуклидов: настоящая устойчивость — штука пятнистая,
    /// у технеция стабильных изотопов нет вовсе, хотя по этой прикидке они «должны» быть.</summary>
    public static bool IsStableIsotope(int z, int neutrons, out int expected)
    {
        var baseEl = ByZ(z);
        expected = baseEl != null ? Mathf.RoundToInt(baseEl.Mass) - z : Mathf.RoundToInt(z * 1.5f);
        if (z == 43 || z == 61 || z >= 84) return false;      // здесь стабильных нет по-настоящему
        float tol = 2f + z * 0.06f;
        return Mathf.Abs(neutrons - expected) <= tol;
    }

    static readonly string[] ROOT_RU = { "нил", "ун", "би", "три", "квад", "пент", "гекс", "септ", "окт", "энн" };
    static readonly string[] ROOT_LAT = { "n", "u", "b", "t", "q", "p", "h", "s", "o", "e" };

    public static string SystematicName(int z)
    {
        string digits = z.ToString();
        var sb = new System.Text.StringBuilder();
        foreach (char c in digits) sb.Append(ROOT_RU[c - '0']);
        sb.Append("ий");
        string s0 = sb.ToString();
        return char.ToUpper(s0[0]) + s0.Substring(1);
    }

    public static string SystematicSymbol(int z)
    {
        string digits = z.ToString();
        var sb = new System.Text.StringBuilder();
        foreach (char c in digits) sb.Append(ROOT_LAT[c - '0']);
        string s0 = sb.ToString();
        return char.ToUpper(s0[0]) + s0.Substring(1);
    }

    static void Parse()
    {
        if (COV.Length != RAW.Length)
            Debug.LogError("Радиусов " + COV.Length + ", а элементов " + RAW.Length + " — таблицы разошлись.");
        _all = new El[RAW.Length];
        _bySym = new Dictionary<string, El>(RAW.Length);
        for (int i = 0; i < RAW.Length; i++)
        {
            string[] p = RAW[i].Split('|');
            var e = new El
            {
                Z = i + 1,
                Sym = p[0],
                Name = p[1],
                Mass = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
                Group = int.Parse(p[3]),
                Period = int.Parse(p[4]),
                Valence = int.Parse(p[5]),
                EN = float.Parse(p[6], System.Globalization.CultureInfo.InvariantCulture),
                Color = Hex(p[7]),
                Class = ClsOf(p[8]),
            };
            _all[i] = e;
            _bySym[e.Sym] = e;
        }
    }

    static Cls ClsOf(string c)
    {
        switch (c)
        {
            case "N": return Cls.Nonmetal;
            case "G": return Cls.Noble;
            case "A": return Cls.Alkali;
            case "E": return Cls.AlkEarth;
            case "M": return Cls.Metalloid;
            case "H": return Cls.Halogen;
            case "T": return Cls.Transition;
            case "P": return Cls.PostMetal;
            case "L": return Cls.Lanth;
            default: return Cls.Actin;
        }
    }

    static Color Hex(string h)
    {
        int r = System.Convert.ToInt32(h.Substring(0, 2), 16);
        int g = System.Convert.ToInt32(h.Substring(2, 2), 16);
        int b = System.Convert.ToInt32(h.Substring(4, 2), 16);
        return new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
