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
    public enum Paint { Metal, Nonmetal, Radioactive, Unknown }

    public class El
    {
        public int Z;               // порядковый номер = число протонов
        public string Sym, Name;
        public float Mass;
        public int Group, Period;
        public int Valence;         // сколько связей тянет в этой игре
        public float EN;            // электроотрицательность, 0 = не определена
        public Color Color;
        public Cls Class;

        /// <summary>Схематический радиус шарика: вниз по таблице крупнее, вправо — мельче.</summary>
        public float Radius
        {
            get
            {
                int g = Group == 0 ? 3 : Group;         // f-блок считаем по третьей группе
                float r = 0.30f + 0.085f * Period - 0.0045f * g;
                return Mathf.Clamp(r, 0.30f, 0.95f);
            }
        }

        /// <summary>Радиоактивны: технеций (43), прометий (61) и всё от полония (84) и дальше —
        /// у этих элементов нет ни одного стабильного изотопа. Это не приблизительно, это
        /// ровно тот список, который учат в школе как «дальше стабильных нет».</summary>
        public Paint Paint
        {
            get
            {
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

    /// <summary>Элемент по порядковому номеру, 1..118.</summary>
    public static El ByZ(int z)
    {
        if (_all == null) Parse();
        return (z >= 1 && z <= _all.Length) ? _all[z - 1] : null;
    }

    static void Parse()
    {
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
