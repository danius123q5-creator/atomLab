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

    GUIStyle sCell, sTitle, sSmall, sTab, sToast, sWorld, sNote, sBig;

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
        sBig = new GUIStyle(GUI.skin.label) { fontSize = 28, fontStyle = FontStyle.Bold };
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
        DrawFormulaCard();
        DrawZoneButtons();
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

        // 🔴 21.09, владелец: вместо вкладок «Открытия / Задания / Как играть» — один тумблер.
        // Журнал и задания никуда не делись, они считаются как раньше; счётчик открытий виден
        // в правом верхнем углу. Просто перестали занимать половину панели.
        GUI.color = Lab.GodMode ? new Color(1f, 0.85f, 0.35f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 74f, W - 28f, 28f),
            (Lab.GodMode ? "РЕЖИМ БОГА: ВКЛ" : "Режим бога: выкл") + "  —  все атомы могут соединиться", sTab))
        {
            Lab.GodMode = !Lab.GodMode;
            Lab.I.Say(Lab.GodMode
                ? "Режим бога: валентность больше не считается, склеивается всё со всем — даже гелий."
                : "Обычный режим: работают валентность и правило благородных газов.",
                Lab.GodMode ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.9f, 1f));
        }
        GUI.color = Color.white;

        DrawTable(cw, ch);
    }


    /// <summary>Легенда раскраски таблицы. Цвет КЛЕТКИ теперь говорит про класс элемента,
    /// а шарик в зоне остаётся своего цвета по палитре CPK — это разные вещи, и легенда
    /// об этом прямо говорит, иначе несовпадение выглядело бы как ошибка.</summary>
    void DrawLegend(Rect r)
    {
        string[] names = { "металлы", "неметаллы", "радиация", "неизученные" };
        Color[] cols =
        {
            new Color(0.82f, 0.20f, 0.22f), new Color(0.20f, 0.45f, 0.88f),
            new Color(0.20f, 0.72f, 0.32f), new Color(0.92f, 0.80f, 0.18f)
        };
        float x = r.x;
        for (int i = 0; i < 4; i++)
        {
            GUI.color = cols[i];
            GUI.DrawTexture(new Rect(x, r.y + 3f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var sz = sSmall.CalcSize(new GUIContent(names[i]));
            GUI.Label(new Rect(x + 15f, r.y, sz.x + 4f, 18f), names[i], sSmall);
            x += 19f + sz.x;
        }
        GUI.Label(new Rect(r.x, r.y + 19f, r.width, 30f),
            "Цвет клетки — класс элемента, цвет шарика в зоне — его собственный (палитра CPK).", sSmall);
    }

    void DrawTable(float cw, float ch)
    {
        // Подпись про выбранный элемент — над таблицей, чтобы не прыгала.
        var h = hover ?? carrying;
        if (h != null)
        {
            GUI.Label(new Rect(panelX + 14f, 104f, W - 28f, 44f),
                h.Z + ". " + h.Name + " (" + h.Sym + ")   масса " + h.Mass.ToString("0.###") +
                "   связей: " + h.Valence + (h.EN > 0f ? "   ЭО " + h.EN.ToString("0.00") : "") +
                "\n" + h.ClassName + "  ·  " + h.PaintName, sSmall);
        }
        else
        {
            DrawLegend(new Rect(panelX + 14f, 104f, W - 28f, 44f));
        }

        foreach (var el in Elements.All)
        {
            Rect r = CellRect(el);
            if (r.yMax < tableTop - 4f || r.y > Screen.height) continue;    // вне видимой части — не рисуем
            Color c = el.PaintColor;
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


    /// <summary>Карточка собранного — в левом нижнем углу (🔴 21.09, просьба владельца).
    /// Показывает САМУЮ КРУПНУЮ молекулу в зоне: формулу, название и строчку про неё.
    /// Прижата не к краю экрана, а к краю СВОБОДНОЙ части: под открытой панелью её было бы
    /// просто не видно, панель непрозрачная.</summary>
    float cardHeight;           // высота карточки формулы: по ней кнопки находят свой ряд

    void DrawFormulaCard()
    {
        var lab = Lab.I;
        if (lab == null) return;

        cardHeight = 0f;
        Lab.Mol best = null;
        foreach (var m in lab.Mols)
            if (m.Atoms.Count > 1 && (best == null || m.Atoms.Count > best.Atoms.Count)) best = m;
        if (best == null) return;

        float x = PanelRightPx + 20f;
        float w = Mathf.Min(430f, Screen.width - x - 20f);
        if (w < 160f) return;
        float h = best.Info != null ? 104f : 78f;
        cardHeight = h;
        var r = new Rect(x, Screen.height - h - 20f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = best.Info != null ? new Color(0.4f, 0.9f, 0.5f, 0.9f) : new Color(0.5f, 0.6f, 0.8f, 0.7f);
        GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), Texture2D.whiteTexture);   // цветная полоска слева
        GUI.color = Color.white;

        sBig.normal.textColor = best.Info != null ? new Color(0.65f, 1f, 0.7f) : Color.white;
        GUI.Label(new Rect(r.x + 14f, r.y + 6f, r.width - 24f, 32f), best.Formula, sBig);

        if (best.Info != null)
        {
            GUI.Label(new Rect(r.x + 14f, r.y + 38f, r.width - 24f, 22f), best.Info.Name, sTitle);
            GUI.Label(new Rect(r.x + 14f, r.y + 60f, r.width - 24f, 38f), best.Info.Note, sSmall);
        }
        else
        {
            GUI.Label(new Rect(r.x + 14f, r.y + 38f, r.width - 24f, 34f),
                "Такого вещества в справочнике нет — слепить можно, а в природе такая связка не живёт.\nАтомов: " + best.Atoms.Count +
                ", свободных связей: " + best.FreeLeft + ".", sSmall);
        }
    }


    /// <summary>Кнопки зоны — слева внизу, НАД карточкой формулы (🔴 21.09, просьба
    /// владельца). Держатся над карточкой, а не на месте: карточка растёт, когда вещество
    /// узнано, и кнопки уезжали бы под неё.</summary>
    void DrawZoneButtons()
    {
        var lab = Lab.I;
        if (lab == null) return;

        float x = PanelRightPx + 20f;
        if (Screen.width - x < 200f) return;
        float y = Screen.height - cardHeight - 20f - 34f;

        if (GUI.Button(new Rect(x, y, 140f, 28f), "Убрать атомы", sTab)) lab.ClearZone();
        if (GUI.Button(new Rect(x + 148f, y, 140f, 28f), "Перезапуск", sTab)) lab.RestartLab();
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
