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
            if (menuAtom != null &&
                MenuRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))) return true;
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
        if (e.Synthetic)
        {
            // Добытые в ускорителе стоят своим рядом внизу, по порядку появления.
            int idx = 0;
            foreach (var x in Elements.All) { if (x == e) break; if (x.Synthetic) idx++; }
            row = 11 + idx / 18; col = idx % 18;
        }
        else if (e.Class == Elements.Cls.Lanth) { row = 8; col = e.Z - 57 + 2; }
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
        tableTop = 210f;
        scrollMax = Mathf.Max(0f, (10f * ch + 40f) - (Screen.height - tableTop - 8f));

        HandleInput(e);

        DrawWorldLabels();
        DrawSelection();
        DrawPanel(cw, ch);
        DrawHud();
        DrawFormulaCard();
        DrawZoneButtons();
        DrawAccelerator();
        DrawMenu();
        DrawCarry(e);
    }

    void HandleInput(Event e)
    {
        if (e.type == EventType.MouseDown && e.button == 0 && menuAtom != null && !MenuRect.Contains(e.mousePosition))
            CloseMenu();

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
                if (pendingSlot >= 0 && Accelerator.I != null)
                {
                    Accelerator.I.Put(pendingSlot, candidate);
                    pendingSlot = -1;
                }
                else if (replaceTarget != null)
                {
                    // Ждали выбора элемента для замены — значит, клетка меняет атом, а не родит новый.
                    string was = replaceTarget.El.Sym;
                    replaceTarget.Become(candidate);
                    Fx.Pop(1.2f);
                    Fx.Sparks(replaceTarget.transform.position, new Color(0.8f, 0.95f, 1f), 25, 3f);
                    Lab.I.Recompute();
                    Lab.I.Say(was + " стал " + candidate.Sym + " (" + candidate.Name + ").", new Color(0.85f, 0.95f, 1f));
                    replaceTarget = null;
                }
                else Lab.I.SpawnFromTable(candidate, new Vector3(Screen.width * 0.6f, Screen.height * 0.55f, 0f));
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

        GUI.color = accelOn ? new Color(0.5f, 0.9f, 1f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 138f, W - 28f, 26f),
            accelOn ? "← выйти из ускорителя" : "Ускоритель частиц (склеить ядра)", sTab))
            ToggleAccelerator();
        GUI.color = Color.white;

        if (GUI.Button(new Rect(panelX + 14f, 106f, W - 28f, 26f),
            showPresets ? "← назад к таблице" : "Готовые вещества (15 штук, со строением)", sTab))
            showPresets = !showPresets;

        if (showPresets) DrawPresets(); else DrawTable(cw, ch);
    }

    bool showPresets;

    /// <summary>Список готовых веществ. Каждое собирается со своим строением — углами и
    /// кратностями связей, а не просто нужным набором атомов.</summary>
    void DrawPresets()
    {
        float y = tableTop - 8f;
        GUI.Label(new Rect(panelX + 14f, y, W - 28f, 20f),
            "Нажми — и вещество появится в зоне собранным.", sSmall);
        y += 24f;
        for (int i = 0; i < Presets.All.Length; i++)
        {
            var p = Presets.All[i];
            if (GUI.Button(new Rect(panelX + 14f, y, W - 28f, 26f), p.Name + "   ·   " + p.Formula, sTab))
                Presets.Spawn(p);
            y += 29f;
        }
        scrollMax = 0f;
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
            GUI.Label(new Rect(panelX + 14f, 168f, W - 28f, 44f),
                h.Z + ". " + h.Name + " (" + h.Sym + ")   масса " + h.Mass.ToString("0.###") +
                "   связей: " + h.Valence + (h.EN > 0f ? "   ЭО " + h.EN.ToString("0.00") : "") +
                "\n" + h.ClassName + "  ·  " + h.PaintName, sSmall);
        }
        else
        {
            DrawLegend(new Rect(panelX + 14f, 168f, W - 28f, 44f));
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


    /// <summary>Рамка выделения и уголки на выделенных атомах. Рисуются поверх сцены: своей
    /// подсветки у шарика нет, а красить материал — значит терять его настоящий цвет.</summary>
    void DrawSelection()
    {
        var lab = Lab.I;
        var cam = lab != null ? lab.Cam : null;
        if (cam == null) return;

        foreach (var a in lab.Selected)
        {
            if (a == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(a.transform.position);
            if (sp.z <= 0f) continue;
            float y = Screen.height - sp.y;
            float r = 26f;
            GUI.color = new Color(0.4f, 0.9f, 1f, 0.95f);
            // Четыре уголка, а не рамка целиком: так видно и атом, и то, что он выбран.
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            {
                float cx = sp.x + corner.x * r, cy = y + corner.y * r;
                GUI.DrawTexture(new Rect(cx - (corner.x > 0 ? 9f : 0f), cy - 1f, 9f, 2f), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(cx - 1f, cy - (corner.y > 0 ? 9f : 0f), 2f, 9f), Texture2D.whiteTexture);
            }
            GUI.color = Color.white;
        }

        if (lab.Banding)
        {
            var r = Rect.MinMaxRect(
                Mathf.Min(lab.BandA.x, lab.BandB.x), Screen.height - Mathf.Max(lab.BandA.y, lab.BandB.y),
                Mathf.Max(lab.BandA.x, lab.BandB.x), Screen.height - Mathf.Min(lab.BandA.y, lab.BandB.y));
            GUI.color = new Color(0.4f, 0.8f, 1f, 0.18f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = new Color(0.5f, 0.9f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - 1f, r.width, 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - 1f, r.y, 1f, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
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

        if (accelOn) return;              // в ускорителе внизу стоит его пульт
        float x = PanelRightPx + 20f;
        if (Screen.width - x < 200f) return;
        float y = Screen.height - cardHeight - 20f - 34f;

        if (GUI.Button(new Rect(x, y, 140f, 28f), "Убрать атомы", sTab)) lab.ClearZone();

        GUI.color = new Color(0.75f, 1f, 0.8f);
        if (GUI.Button(new Rect(x + 148f, y, 180f, 28f), "Посмотреть реакцию", sTab)) Chemistry.React();
        GUI.color = new Color(1f, 0.8f, 0.8f);
        if (GUI.Button(new Rect(x + 336f, y, 140f, 28f), "Выход из игры", sTab)) lab.ExitGame();
        GUI.color = Color.white;

        if (lab.Selected.Count > 0 || lab.ClipboardCount > 0)
            GUI.Label(new Rect(x, y - 20f, 520f, 18f),
                "Выделено: " + lab.Selected.Count + "   ·   в буфере: " + lab.ClipboardCount +
                "   ·   Ctrl+C копировать, Ctrl+V (Ctrl+М) вставить, Ctrl+A выделить всё", sSmall);
    }


    // ==================== меню атома (ПКМ) ====================

    Atom menuAtom;                 // на каком атоме открыто меню
    Vector2 menuPos;               // левый верхний угол меню, координаты GUI
    bool menuBondList;             // раскрыт ли список «убрать связь с...»
    Atom replaceTarget;            // кого меняем, когда ждём выбора элемента в таблице

    public void OpenMenu(Atom a, Vector3 screenPos)
    {
        menuAtom = a;
        menuBondList = false;
        menuPos = new Vector2(screenPos.x, Screen.height - screenPos.y);
    }

    public void CloseMenu() { menuAtom = null; menuBondList = false; }

    Rect MenuRect
    {
        get
        {
            if (menuAtom == null) return new Rect();
            int rows = 6 + (menuBondList ? menuAtom.Bonds.Count : 0);
            float w = 260f, h = 26f + rows * 24f;
            float x = Mathf.Min(menuPos.x, Screen.width - w - 6f);
            float y = Mathf.Min(menuPos.y, Screen.height - h - 6f);
            return new Rect(x, y, w, h);
        }
    }

    /// <summary>Меню атома: разорвать одну связь, все связи, удалить, заменить элемент,
    /// провести реакцию. 🔴 21.09, просьба владельца.</summary>
    void DrawMenu()
    {
        if (menuAtom == null) return;
        if (menuAtom == null || menuAtom.El == null) { CloseMenu(); return; }

        var r = MenuRect;
        GUI.color = new Color(0.07f, 0.09f, 0.14f, 0.97f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.7f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float y = r.y + 4f;
        int used = 0;
        foreach (var b in menuAtom.Bonds) used += b.Order;
        GUI.Label(new Rect(r.x + 8f, y, r.width - 16f, 20f),
            menuAtom.El.Name + " (" + menuAtom.El.Sym + ")   связей " + used + " из " + menuAtom.El.Valence, sSmall);
        y += 22f;

        if (menuAtom.Bonds.Count > 0)
        {
            if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f),
                (menuBondList ? "- " : "+ ") + "Убрать связь с...", sTab)) menuBondList = !menuBondList;
            y += 24f;

            if (menuBondList)
            {
                foreach (var b in new List<Bond>(menuAtom.Bonds))
                {
                    var other = b.Other(menuAtom);
                    string kind = b.Order == 1 ? "одинарная" : (b.Order == 2 ? "двойная" : "тройная");
                    if (GUI.Button(new Rect(r.x + 18f, y, r.width - 24f, 22f),
                        "с " + other.El.Sym + " (" + kind + ")", sTab))
                    {
                        b.Break();
                        Lab.I.Recompute();
                        Lab.I.Say("Связь " + menuAtom.El.Sym + "-" + other.El.Sym + " разорвана.", new Color(1f, 0.85f, 0.6f));
                        if (menuAtom.Bonds.Count == 0) menuBondList = false;
                    }
                    y += 24f;
                }
            }

            if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), "Убрать все связи", sTab))
            {
                int n = menuAtom.Bonds.Count;
                for (int i = menuAtom.Bonds.Count - 1; i >= 0; i--) menuAtom.Bonds[i].Break();
                Lab.I.Recompute();
                Lab.I.Say("Оторвано связей: " + n, new Color(1f, 0.85f, 0.6f));
                CloseMenu();
                return;
            }
            y += 24f;
        }
        else { GUI.Label(new Rect(r.x + 8f, y, r.width - 16f, 20f), "связей нет", sNote); y += 46f; }

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), "Удалить атом", sTab))
        {
            menuAtom.Despawn();
            Lab.I.Recompute();
            CloseMenu();
            return;
        }
        y += 24f;

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), "Заменить (выбери в таблице)", sTab))
        {
            replaceTarget = menuAtom;
            showPresets = false;
            open = true;
            Lab.I.Say("Выбери элемент в таблице - " + menuAtom.El.Sym + " станет им. Связи, на которые не хватит запаса, оторвутся.",
                new Color(0.85f, 0.95f, 1f));
            CloseMenu();
            return;
        }
        y += 24f;

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), "Провести реакцию", sTab))
        {
            CloseMenu();
            Chemistry.React();
            return;
        }
    }


    // ==================== ускоритель ====================

    bool accelOn;
    int pendingSlot = -1;          // в какое гнездо ждём элемент из таблицы: 0 левое, 1 правое

    void ToggleAccelerator()
    {
        var acc = Accelerator.I;
        if (acc == null || Lab.I == null) return;
        accelOn = !accelOn;
        acc.Active = accelOn;
        pendingSlot = -1;
        if (accelOn)
        {
            Lab.I.LookAt(Accelerator.Rig, 13f);
            Lab.I.Say("Ускоритель. Щёлкни по гнезду, потом по элементу в таблице — и жми «Склеить вещества».",
                new Color(0.6f, 0.9f, 1f));
        }
        else
        {
            Lab.I.LookAt(Lab.ZoneCenter, 14f);
            Lab.I.Say("Назад в лабораторию.", new Color(0.8f, 0.9f, 1f));
        }
    }

    /// <summary>Пульт ускорителя: два гнезда, результат и кнопка склейки.</summary>
    void DrawAccelerator()
    {
        if (!accelOn) return;
        var acc = Accelerator.I;
        if (acc == null) return;

        float x = PanelRightPx + 20f;
        float w = Mathf.Min(560f, Screen.width - x - 20f);
        if (w < 260f) return;
        var r = new Rect(x, Screen.height - 176f, w, 156f);

        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), "УСКОРИТЕЛЬ ЧАСТИЦ", sTitle);
        GUI.Label(new Rect(r.x + 12f, r.y + 28f, r.width - 24f, 20f),
            "Ядра складываются: номер нового элемента — сумма номеров. Так и получили всё тяжелее урана.", sSmall);

        float bw = (r.width - 36f) / 3f;
        DrawSlotButton(new Rect(r.x + 12f, r.y + 52f, bw, 40f), 0, acc.SlotA, "гнездо слева");
        DrawSlotButton(new Rect(r.x + 18f + bw, r.y + 52f, bw, 40f), -1, acc.Result, "результат");
        DrawSlotButton(new Rect(r.x + 24f + bw * 2f, r.y + 52f, bw, 40f), 1, acc.SlotB, "гнездо справа");

        GUI.color = new Color(0.6f, 0.95f, 1f);
        if (GUI.Button(new Rect(r.x + 12f, r.y + 100f, bw * 1.6f, 30f), "Склеить вещества", sTab)) acc.Fuse();
        GUI.color = Color.white;
        if (acc.Result != null && GUI.Button(new Rect(r.x + 24f + bw * 1.6f, r.y + 100f, bw * 1.3f, 30f), "Забрать в зону", sTab))
            acc.TakeResult();

        if (pendingSlot >= 0)
            GUI.Label(new Rect(r.x + 12f, r.y + 132f, r.width - 24f, 20f),
                "Теперь выбери элемент в таблице слева — он встанет в гнездо.", sNote);
    }

    void DrawSlotButton(Rect r, int slot, Elements.El el, string title)
    {
        bool waiting = (slot >= 0 && pendingSlot == slot);
        GUI.color = waiting ? new Color(1f, 0.9f, 0.5f) : (el != null ? el.PaintColor : new Color(0.5f, 0.5f, 0.55f));
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        string text = el != null ? el.Sym + "  " + el.Name + "  (" + el.Z + ")" : "пусто";
        GUI.Label(new Rect(r.x + 8f, r.y + 2f, r.width - 12f, 18f), title, sSmall);
        GUI.Label(new Rect(r.x + 8f, r.y + 18f, r.width - 12f, 20f), text, sSmall);

        if (slot >= 0 && GUI.Button(r, "", GUIStyle.none)) pendingSlot = (pendingSlot == slot) ? -1 : slot;
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
