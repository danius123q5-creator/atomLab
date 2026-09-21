using System.Collections.Generic;
using UnityEngine;

/// <summary>Весь интерфейс: выдвижная боковая панель с таблицей Менделеева, перетаскивание
/// элемента из таблицы прямо в зону, подписи над атомами и молекулами, журнал и задания.
///
/// 🔴 20.09, владелец: «боковая панель где свайпом выдвигается таблица веществ, и свайпом
/// перетащить элемент с таблицы на плэй-зону». Сделано ровно так:
///   • панель едет за пальцем/курсором — тянешь от левого края вправо, она выезжает;
///     тянешь влево — уезжает. Отпустил на полпути — доедет сама в ближайшую сторону.
///   • ячейку элемента тянешь ВПРАВО — она отрывается от таблицы и едет за курсором;
///     отпустил над зоной — там и появится атом.
/// Направление первого движения и решает спор: влево — это свайп панели, вправо — это взятие
/// элемента. Иначе два жеста дрались бы за один и тот же палец.</summary>
public class LabUI : MonoBehaviour
{
    public static LabUI I;

    float W;                    // ширина панели
    float panelX;               // смещение панели: -W закрыта, 0 открыта
    bool open = true;
    int tab = 0;                // 0 таблица, 1 открытия, 2 задания, 3 как играть
    float scroll, scrollMax;

    enum DragKind { None, Panel, Element, Scroll }
    DragKind drag;
    Vector2 downPos;
    float downPanelX, downScroll;
    Elements.El candidate;      // на что нажали в таблице
    Elements.El carrying;       // что уже несём к зоне
    Elements.El hover;

    const float HandleW = 30f;  // язычок, торчащий из-за края, когда панель закрыта
    float tableTop;             // верх области таблицы в координатах панели

    GUIStyle sCell, sTitle, sSmall, sTab, sToast, sWorld, sNote;

    public bool PointerOverUI
    {
        get
        {
            if (carrying != null || drag == DragKind.Panel) return true;
            float mx = Input.mousePosition.x;
            return mx <= panelX + W + HandleW;
        }
    }

    /// <summary>Правый край видимой части панели в пикселях — по нему камера решает,
    /// насколько сдвинуть зону вправо.</summary>
    public float PanelRightPx { get { return Mathf.Max(0f, panelX + W); } }

    void Awake() { I = this; }

    public void TogglePanel() { open = !open; }

    void Update()
    {
        W = Mathf.Clamp(Screen.width * 0.42f, 360f, 620f);
        if (drag != DragKind.Panel)
            panelX = Mathf.Lerp(panelX, open ? 0f : -W, Time.unscaledDeltaTime * 12f);
    }

    void Styles()
    {
        if (sCell != null) return;
        sCell = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, fontSize = 13, fontStyle = FontStyle.Bold };
        sTitle = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, wordWrap = true };
        sSmall = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        sNote = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, fontStyle = FontStyle.Italic };
        sTab = new GUIStyle(GUI.skin.button) { fontSize = 13 };
        sToast = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.MiddleCenter };
        sWorld = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
    }

    // ==================== геометрия таблицы ====================

    float CellW { get { return (W - 26f) / 18f; } }

    /// <summary>Клетка элемента в экранных координатах GUI (с учётом выезда панели и прокрутки).</summary>
    Rect CellRect(Elements.El e)
    {
        float cw = CellW, ch = cw * 1.12f;
        int row, col;
        if (e.Class == Elements.Cls.Lanth) { row = 8; col = e.Z - 57 + 2; }
        else if (e.Class == Elements.Cls.Actin) { row = 9; col = e.Z - 89 + 2; }
        else { row = e.Period - 1; col = e.Group - 1; }
        return new Rect(panelX + 13f + col * cw, tableTop + row * ch - scroll, cw - 2f, ch - 2f);
    }

    Rect TableViewRect { get { return new Rect(panelX, tableTop, W, Screen.height - tableTop - 8f); } }

    Elements.El HitTest(Vector2 p)
    {
        if (!TableViewRect.Contains(p)) return null;
        foreach (var e in Elements.All) if (CellRect(e).Contains(p)) return e;
        return null;
    }

    // ==================== рисование ====================

    void OnGUI()
    {
        Styles();
        var e = Event.current;
        float cw = CellW, ch = cw * 1.12f;
        tableTop = 150f;
        scrollMax = Mathf.Max(0f, (10f * ch + 40f) - (Screen.height - tableTop - 8f));

        HandleInput(e);

        DrawWorldLabels();
        DrawPanel(cw, ch);
        DrawHud();
        DrawCarry(e);
    }

    void HandleInput(Event e)
    {
        if (e.type == EventType.MouseDown && e.button == 0)
        {
            downPos = e.mousePosition;
            downPanelX = panelX;
            downScroll = scroll;
            candidate = HitTest(e.mousePosition);
            bool onPanel = e.mousePosition.x <= panelX + W;
            bool onEdge = e.mousePosition.x <= panelX + W + HandleW;
            drag = (onPanel || onEdge) ? DragKind.Scroll : DragKind.None;   // род жеста решим по первому движению
        }
        else if (e.type == EventType.MouseDrag && drag != DragKind.None)
        {
            Vector2 d = e.mousePosition - downPos;
            if (drag == DragKind.Scroll && d.magnitude > 14f)
            {
                if (Mathf.Abs(d.x) > Mathf.Abs(d.y))
                {
                    if (d.x > 0f && candidate != null) { drag = DragKind.Element; carrying = candidate; }
                    else drag = DragKind.Panel;
                }
            }

            if (drag == DragKind.Panel) panelX = Mathf.Clamp(downPanelX + d.x, -W, 0f);
            else if (drag == DragKind.Scroll) scroll = Mathf.Clamp(downScroll - d.y, 0f, scrollMax);
        }
        else if (e.type == EventType.MouseUp && e.button == 0)
        {
            Vector2 d = e.mousePosition - downPos;
            if (drag == DragKind.Panel)
            {
                open = panelX > -W * 0.5f || d.x > 60f;
                if (d.x < -60f) open = false;
            }
            else if (drag == DragKind.Element && carrying != null)
            {
                if (e.mousePosition.x > panelX + W && Lab.I != null)
                {
                    Vector3 sp = new Vector3(e.mousePosition.x, Screen.height - e.mousePosition.y, 0f);
                    Lab.I.SpawnFromTable(carrying, sp);
                }
            }
            else if (drag == DragKind.Scroll && d.magnitude < 10f && candidate != null && Lab.I != null)
            {
                // Простой тык по клетке — атом падает в центр зоны.
                Lab.I.SpawnFromTable(candidate, new Vector3(Screen.width * 0.6f, Screen.height * 0.55f, 0f));
            }
            drag = DragKind.None; carrying = null; candidate = null;
        }
        else if (e.type == EventType.ScrollWheel && e.mousePosition.x <= panelX + W)
        {
            scroll = Mathf.Clamp(scroll + e.delta.y * 14f, 0f, scrollMax);
            e.Use();
        }

        hover = (drag == DragKind.None) ? HitTest(e.mousePosition) : candidate;
    }

    void DrawPanel(float cw, float ch)
    {
        // Фон панели.
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.94f);
        GUI.DrawTexture(new Rect(panelX, 0f, W, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Язычок: тянуть отсюда.
        var handle = new Rect(panelX + W, Screen.height * 0.5f - 70f, HandleW, 140f);
        GUI.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        GUI.DrawTexture(handle, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(handle.x + 4f, handle.y + 46f, HandleW, 60f), open ? "<\n<\n<" : ">\n>\n>", sSmall);

        GUI.Label(new Rect(panelX + 14f, 8f, W - 28f, 26f), "АТОМНАЯ ЛАБОРАТОРИЯ", sTitle);
        GUI.Label(new Rect(panelX + 14f, 32f, W - 28f, 36f),
            "Тяни элемент из таблицы вправо — в зону. Панель двигается свайпом, Esc — спрятать.", sSmall);

        float bw = (W - 40f) / 4f;
        string[] tabs = { "Таблица", "Открытия", "Задания", "Как играть" };
        for (int i = 0; i < 4; i++)
        {
            GUI.color = (tab == i) ? new Color(0.5f, 0.8f, 1f) : Color.white;
            if (GUI.Button(new Rect(panelX + 14f + i * (bw + 4f), 74f, bw, 26f), tabs[i], sTab)) tab = i;
        }
        GUI.color = Color.white;

        if (tab == 0) DrawTable(cw, ch);
        else if (tab == 1) DrawDiscoveries();
        else if (tab == 2) DrawQuests();
        else DrawHelp();
    }

    void DrawTable(float cw, float ch)
    {
        // Подпись про выбранный элемент — над таблицей, чтобы не прыгала.
        var h = hover ?? carrying;
        if (h != null)
        {
            GUI.Label(new Rect(panelX + 14f, 104f, W - 28f, 44f),
                h.Z + ". " + h.Name + " (" + h.Sym + ")   масса " + h.Mass.ToString("0.###") +
                "   связей: " + h.Valence + (h.EN > 0f ? "   ЭО " + h.EN.ToString("0.00") : "") + "\n" + h.ClassName, sSmall);
        }
        else
        {
            GUI.Label(new Rect(panelX + 14f, 104f, W - 28f, 44f),
                "118 элементов. Цвет клетки — цвет атома в зоне (палитра CPK).\nЖёлтая связь — ионная, серая — ковалентная.", sSmall);
        }

        foreach (var el in Elements.All)
        {
            Rect r = CellRect(el);
            if (r.yMax < tableTop - 4f || r.y > Screen.height) continue;    // вне видимой части — не рисуем
            Color c = el.Color;
            bool isHover = (h == el);
            GUI.color = isHover ? Color.Lerp(c, Color.white, 0.45f) : c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Подпись поверх: тёмная на светлой клетке и наоборот.
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            sCell.normal.textColor = lum > 0.5f ? Color.black : Color.white;
            GUI.Label(r, el.Sym, sCell);
        }

        // Полоса прокрутки — тонкая, справа.
        if (scrollMax > 1f)
        {
            float viewH = Screen.height - tableTop - 8f;
            float frac = viewH / (viewH + scrollMax);
            float barH = viewH * frac;
            float barY = tableTop + (viewH - barH) * (scroll / scrollMax);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(panelX + W - 6f, barY, 4f, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }

    void DrawDiscoveries()
    {
        var lab = Lab.I;
        GUI.Label(new Rect(panelX + 14f, 108f, W - 28f, 24f),
            "Открыто: " + lab.Discovered.Count + " из " + Molecules.Total + "   ·   очки: " + lab.Score, sTitle);

        float y = tableTop - 6f;
        float viewH = Screen.height - y - 40f;
        var list = new List<string>(lab.Discovered);
        list.Sort(System.StringComparer.Ordinal);
        scrollMax = Mathf.Max(0f, list.Count * 34f - viewH);
        foreach (var f in list)
        {
            var info = Molecules.Lookup(f);
            float yy = y - scroll;
            if (yy > tableTop - 40f && yy < Screen.height)
            {
                GUI.Label(new Rect(panelX + 14f, yy, W - 28f, 18f), f + " — " + (info != null ? info.Name : "?"), sSmall);
                if (info != null) GUI.Label(new Rect(panelX + 24f, yy + 15f, W - 40f, 18f), info.Note, sNote);
            }
            y += 34f;
        }
        if (GUI.Button(new Rect(panelX + 14f, Screen.height - 34f, 160f, 26f), "Очистить журнал", sTab)) lab.ResetProgress();
    }

    void DrawQuests()
    {
        var lab = Lab.I;
        int done = 0;
        foreach (var q in Quests.All) if (q.Done) done++;
        GUI.Label(new Rect(panelX + 14f, 108f, W - 28f, 24f), "Задания: " + done + " из " + Quests.All.Length, sTitle);

        float y = tableTop - 6f;
        foreach (var q in Quests.All)
        {
            GUI.color = q.Done ? new Color(0.6f, 1f, 0.6f) : Color.white;
            GUI.Label(new Rect(panelX + 14f, y, W - 28f, 20f),
                (q.Done ? "✔ " : "•  ") + q.Title + "   (" + q.Formula + ")   +" + q.Reward, sSmall);
            GUI.color = Color.white;
            y += 24f;
        }
        scrollMax = 0f;
    }

    void DrawHelp()
    {
        GUI.Label(new Rect(panelX + 14f, 108f, W - 28f, Screen.height - 140f),
            "КАК ИГРАТЬ\n\n" +
            "• Тяни элемент из таблицы вправо и отпусти над зоной — появится атом.\n" +
            "• Короткий тык по клетке тоже кидает атом в середину зоны.\n" +
            "• Панель: тяни за край или за язычок. Esc — спрятать и показать.\n" +
            "• Атом тащится левой кнопкой. Поднеси два атома вплотную — склеятся сами.\n" +
            "• Сожми уже склеенную пару сильнее — связь станет двойной, потом тройной.\n" +
            "• Правая кнопка — поворот камеры, колесо — приближение, WASD — сдвиг.\n" +
            "• Shift + правая кнопка по атому — убрать его. Delete — очистить всю зону.\n\n" +
            "ПРАВИЛА СКЛЕЙКИ\n\n" +
            "У каждого элемента свой запас связей (валентность): у водорода одна, у кислорода\n" +
            "две, у углерода четыре. Связь тратит по одной у обоих. Когда запас кончился,\n" +
            "атом больше никого не берёт — и мягко отталкивает лишних.\n\n" +
            "Благородные газы (гелий, неон, аргон...) не соединяются ни с кем: у них внешний\n" +
            "слой уже полон. Это не ограничение игры, это их настоящее свойство.\n\n" +
            "Цвет связи: серая — ковалентная (электроны общие), жёлтая — ионная (электрон\n" +
            "отобран более жадным атомом; считаем по разнице электроотрицательностей от 1.7).\n\n" +
            "ЧЕСТНО О ПРОСТОТЕ\n\n" +
            "Валентность здесь одна на элемент, хотя у железа их две, а у серы три. И вещество\n" +
            "определяется по составу, а не по строению — поэтому этанол и диметиловый эфир для\n" +
            "игры одно и то же. Это конструктор, а не химический пакет.", sSmall);
        scrollMax = 0f;
    }

    // ==================== мир: подписи над атомами и молекулами ====================

    void DrawWorldLabels()
    {
        var cam = Lab.I != null ? Lab.I.Cam : null;
        if (cam == null) return;

        foreach (var a in Atom.All)
        {
            Vector3 sp = cam.WorldToScreenPoint(a.transform.position);
            if (sp.z <= 0f) continue;
            float y = Screen.height - sp.y;
            sWorld.normal.textColor = Color.white;
            GUI.Label(new Rect(sp.x - 30f, y - 10f, 60f, 20f), a.El.Sym, sWorld);
            if (a.FreeValence > 0)
            {
                sWorld.normal.textColor = new Color(0.6f, 0.9f, 1f, 0.9f);
                GUI.Label(new Rect(sp.x - 30f, y + 6f, 60f, 16f), new string('·', a.FreeValence), sWorld);
            }
            else if (a.El.Valence > 0)
            {
                // Пусто под символом читалось как «всё в порядке». Теперь занятый атом виден.
                sWorld.normal.textColor = new Color(1f, 0.55f, 0.4f, 0.95f);
                GUI.Label(new Rect(sp.x - 30f, y + 6f, 60f, 16f), "занят", sWorld);
            }
        }

        foreach (var m in Lab.I.Mols)
        {
            if (m.Atoms.Count < 2) continue;
            Vector3 sp = cam.WorldToScreenPoint(m.Center + Vector3.up * 0.9f);
            if (sp.z <= 0f) continue;
            float y = Screen.height - sp.y;
            string text = m.Formula;
            if (m.Info != null) text += "  —  " + m.Info.Name;
            sWorld.normal.textColor = m.Info != null ? new Color(0.6f, 1f, 0.6f) : new Color(1f, 1f, 1f, 0.65f);
            GUI.Label(new Rect(sp.x - 160f, y - 34f, 320f, 20f), text, sWorld);
        }
    }

    void DrawHud()
    {
        var lab = Lab.I;
        var r = new Rect(Screen.width - 260f, 10f, 250f, 60f);
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 22f), "Очки: " + lab.Score, sTitle);
        GUI.Label(new Rect(r.x + 10f, r.y + 30f, r.width - 20f, 22f),
            "Открыто веществ: " + lab.Discovered.Count + " / " + Molecules.Total + "   ·   атомов: " + Atom.All.Count, sSmall);

        if (Time.time < lab.ToastUntil && !string.IsNullOrEmpty(lab.Toast))
        {
            var tr = new Rect(panelX + W + 40f, Screen.height - 96f, Screen.width - (panelX + W) - 80f, 64f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(tr, Texture2D.whiteTexture);
            GUI.color = Color.white;
            sToast.normal.textColor = lab.ToastColor;
            GUI.Label(tr, lab.Toast, sToast);
        }
    }

    void DrawCarry(Event e)
    {
        if (carrying == null) return;
        float cw = CellW * 1.6f;
        var r = new Rect(e.mousePosition.x - cw * 0.5f, e.mousePosition.y - cw * 0.5f, cw, cw);
        GUI.color = new Color(carrying.Color.r, carrying.Color.g, carrying.Color.b, 0.9f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        float lum = carrying.Color.r * 0.3f + carrying.Color.g * 0.59f + carrying.Color.b * 0.11f;
        sCell.normal.textColor = lum > 0.5f ? Color.black : Color.white;
        GUI.Label(r, carrying.Sym, sCell);
        GUI.Label(new Rect(r.x - 30f, r.yMax + 2f, cw + 60f, 20f), "отпусти в зоне", sSmall);
    }
}
