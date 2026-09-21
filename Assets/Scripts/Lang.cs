using UnityEngine;

/// <summary>Два языка: русский и английский. 🔴 21.09, владелец: «добавь переключалку языков
/// ру/анг».
///
/// Устроено просто: каждая надпись пишется сразу парой — T("Таблица", "Table"). Словаря с
/// ключами нет нарочно: ключ живёт отдельно от текста, и однажды кто-то меняет строку, а
/// перевод остаётся старым, и никто этого не видит. Здесь обе половины стоят рядом, в одной
/// строке кода — рассинхронизировать их можно только нарочно.
///
/// Выбор языка запоминается между запусками.</summary>
public static class Lang
{
    static bool en;
    static bool loaded;

    public static bool EN
    {
        get
        {
            if (!loaded) { en = PlayerPrefs.GetInt("atomlab.en", 0) == 1; loaded = true; }
            return en;
        }
        set
        {
            en = value; loaded = true;
            PlayerPrefs.SetInt("atomlab.en", en ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    public static string T(string ru, string eng) { return EN ? eng : ru; }

    /// <summary>Название элемента на выбранном языке.</summary>
    public static string Name(Elements.El el)
    {
        if (el == null) return "";
        return Pick(el.Name, el.NameEn);
    }

    /// <summary>Название вещества на выбранном языке.</summary>
    public static string Name(Molecules.Info info)
    {
        if (info == null) return "";
        return Pick(info.Name, info.NameEn);
    }

    public static string Note(Molecules.Info info)
    {
        if (info == null) return "";
        return Pick(info.Note, info.NoteEn);
    }

    /// <summary>21.09, владелец: «кол-во атомов надо писать степенью, а не цифрой». Формула
    /// с числами-индексами, как в учебнике: H₂O, а не H2O. Делаем через размер шрифта в
    /// разметке, а не через символы-индексы Юникода: шрифта с такими символами на телефоне
    /// может не оказаться, и вместо цифр вышли бы квадратики. Мелкие цифры у основания строки
    /// читаются именно как индекс.</summary>
    public static string Sub(string formula, int small)
    {
        if (string.IsNullOrEmpty(formula)) return formula;
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i < formula.Length)
        {
            if (char.IsDigit(formula[i]))
            {
                int j = i;
                while (j < formula.Length && char.IsDigit(formula[j])) j++;
                sb.Append("<size=").Append(small).Append('>').Append(formula, i, j - i).Append("</size>");
                i = j;
            }
            else sb.Append(formula[i++]);
        }
        return sb.ToString();
    }

    /// <summary>Расшифровка формулы словами (21.09, владелец: «ниже писать расшифровку
    /// "7 водород 7 кислород рений"»): каждый элемент по порядку, число — если больше одного.</summary>
    public static string Decode(string formula)
    {
        if (string.IsNullOrEmpty(formula)) return "";
        var parts = new System.Collections.Generic.List<string>();
        int i = 0;
        var order = new System.Collections.Generic.List<string>();
        var count = new System.Collections.Generic.Dictionary<string, int>();
        while (i < formula.Length)
        {
            if (!char.IsUpper(formula[i])) { i++; continue; }
            int st = i++;
            while (i < formula.Length && char.IsLower(formula[i])) i++;
            string sym = formula.Substring(st, i - st);
            int ns = i;
            while (i < formula.Length && char.IsDigit(formula[i])) i++;
            int n = i > ns ? int.Parse(formula.Substring(ns, i - ns)) : 1;
            if (!count.ContainsKey(sym)) { count[sym] = 0; order.Add(sym); }
            count[sym] += n;
        }
        foreach (var sym in order)
        {
            var el = Elements.BySymbol(sym);
            string name = el != null ? Name(el).ToLower() : sym;
            parts.Add(count[sym] > 1 ? count[sym] + " " + name : name);
        }
        return string.Join(" · ", parts.ToArray());
    }

    /// <summary>Где вещество применяют — строка под заметкой в карточке.</summary>
    public static string Use(Molecules.Info info)
    {
        if (info == null) return "";
        return Pick(info.Use, info.UseEn);
    }

    /// <summary>Английская половина есть не у всего (перевод справочника ещё не дописан).
    /// Пустая строка вместо названия хуже русского названия — поэтому при пустом переводе
    /// показываем русское, а не дыру.</summary>
    static string Pick(string ru, string eng)
    {
        return EN && !string.IsNullOrEmpty(eng) ? eng : ru;
    }
}
