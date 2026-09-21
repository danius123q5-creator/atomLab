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
