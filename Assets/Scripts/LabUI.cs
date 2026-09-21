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
                MenuRect.Contains(MouseGui)) return true;
            if ((replaceTarget != null || pendingSlot >= 0) &&
                MouseGui.x <= PanelRightGui + 262f) return true;    // подменю справа от таблицы
            float mx = MouseGui.x;
            return mx <= panelX + W + HandleW;
        }
    }

    /// <summary>Правый край видимой части панели в пикселях — по нему камера решает,
    /// насколько сдвинуть зону вправо.</summary>
    public float PanelRightPx { get { return PanelRightGui * U; } }

    /// <summary>То же в единицах интерфейса — для раскладки внутри OnGUI.</summary>
    float PanelRightGui { get { return Mathf.Max(0f, panelX + W); } }

    // ==================== масштаб для телефона ====================
    //
    // 🔴 21.09, владелец: «управление для телефона: кнопки крупнее». Интерфейс нарисован в
    // пикселях под монитор: на телефоне с плотным экраном клетка таблицы выходила около двух
    // миллиметров — пальцем не попасть. Лечим одним множителем U на ВЕСЬ интерфейс: рисуем в
    // «единицах интерфейса», а GUI.matrix растягивает их на экран.
    //
    // Ловушка, из-за которой это нельзя сделать одной строкой: раскладка местами читает мышь
    // через Input.mousePosition (экранные пиксели), а местами через Event (уже в единицах
    // интерфейса). При U != 1 это два разных пространства. Поэтому всё экранное здесь явно
    // переводится: MouseGui, SW/SH, деление точек камеры на U. На ПК U = 1 — ни одна цифра
    // не меняется, мышь работает как работала.
    public static float U = 1f;
    float SW { get { return Screen.width / U; } }
    float SH { get { return Screen.height / U; } }
    Vector2 MouseGui { get { return new Vector2(Input.mousePosition.x / U, SH - Input.mousePosition.y / U); } }

    static float ComputeScale()
    {
        // Ручная установка — чтобы проверить телефонную раскладку на ПК: AtomLab.exe -uiscale 2.5
        var args = System.Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == "-uiscale") { float f; if (float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) return Mathf.Clamp(f, 1f, 4f); }
        if (!Application.isMobilePlatform) return 1f;
        float dpi = Screen.dpi > 1f ? Screen.dpi : 320f;       // некоторые телефоны dpi не отдают
        float k = Mathf.Clamp(dpi / 160f, 1f, 3.5f);
        // Не даём «виртуальному экрану» стать меньше 800 x 450 единиц: иначе пульты и карточка
        // перестают влезать. Крупнее, чем помещается, — тоже плохо.
        k = Mathf.Min(k, Mathf.Min(Screen.width / 800f, Screen.height / 450f));
        return Mathf.Max(1f, k);
    }

    void Awake() { I = this; }

    public void TogglePanel() { open = !open; }

    void Update()
    {
        U = ComputeScale();
        W = Mathf.Clamp(SW * 0.42f, 360f, 620f);
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
        if (e.Assembled)
        {
            // Собранные из частиц — своим рядом, ниже добытых в ускорителе.
            int idx = 0;
            foreach (var x in Elements.All) { if (x == e) break; if (x.Assembled) idx++; }
            row = 13 + idx / 18; col = idx % 18;
        }
        else if (e.Synthetic)
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

    Rect TableViewRect { get { return new Rect(panelX, tableTop, W, SH - tableTop - 8f); } }

    Elements.El HitTest(Vector2 p)
    {
        if (!TableViewRect.Contains(p)) return null;
        foreach (var e in Elements.All) if (CellRect(e).Contains(p)) return e;
        return null;
    }

    // ==================== рисование ====================

    void OnGUI()
    {
        GUI.matrix = Matrix4x4.Scale(new Vector3(U, U, 1f));
        Styles();
        var e = Event.current;
        float cw = CellW, ch = cw * 1.12f;
        tableTop = 274f;
        // Рядов теперь не десять: снизу прирастают голубые (ускоритель) и оранжевые (сборка).
        int extraRows = 0;
        foreach (var el in Elements.All) if (el.Assembled) extraRows++;
        int rows = 15 + extraRows / 18;
        scrollMax = Mathf.Max(0f, (rows * ch + 40f) - (SH - tableTop - 8f));
        // Под таблицей теперь сетка популярных соединений — прокрутка обязана до неё доставать.
        float popBottom = PopRect(Presets.Grid.Length - 1).yMax + scroll - tableTop + 24f;
        scrollMax = Mathf.Max(scrollMax, popBottom - (SH - tableTop - 8f));

        HandleInput(e);

        DrawWorldLabels();
        DrawSelection();
        DrawPanel(cw, ch);
        DrawHud();
        DrawFormulaCard();
        DrawZoneButtons();
        DrawAccelerator();
        DrawBuilder();
        DrawQuarks();
        DrawPicker();
        DrawMenu();
        DrawCarry(e);
        DrawHoverTip();
    }

    /// <summary>21.09, из очереди владельца на 2.2: «подсказка при наведении на атом:
    /// название, заряд, связи». Только для мыши: у пальца нет «наведения», на телефоне то же
    /// самое показывает меню по долгому нажатию. Не мешаем, когда что-то тащат, тянут рамку,
    /// открыто меню или курсор над панелью.</summary>
    void DrawHoverTip()
    {
        if (Input.touchCount > 0 || Application.isMobilePlatform) return;
        if (Lab.I == null || PointerOverUI || menuAtom != null || carrying != null || Lab.I.Banding) return;
        if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2)) return;
        var a = Lab.I.PickAtom(Input.mousePosition);
        if (a == null) return;
        var el = a.El;
        int used = a.UsedBonds;
        string cap = el.MaxBonds > el.Valence ? el.Valence + Lang.T(" (до ", " (up to ") + el.MaxBonds + ")" : el.Valence.ToString();
        string charge = el.Charge == 0 ? Lang.T("нейтральный", "neutral") : (el.Charge > 0 ? "+" + el.Charge : el.Charge.ToString());
        string text = Lang.Name(el) + " (" + el.Sym + ")\n" +
                      Lang.T("Заряд: ", "Charge: ") + charge + "\n" +
                      Lang.T("Связи: занято ", "Bonds: used ") + used + Lang.T(" из ", " of ") + cap;
        Vector2 m = MouseGui;
        var r = new Rect(m.x + 18f, m.y + 12f, 220f, 58f);
        if (r.xMax > SW - 6f) r.x = m.x - r.width - 12f;
        if (r.yMax > SH - 6f) r.y = m.y - r.height - 8f;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(el.Color.r, el.Color.g, el.Color.b, 1f);
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 9f, r.y + 3f, r.width - 12f, r.height - 4f), text, sSmall);
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
                    Vector3 sp = new Vector3(e.mousePosition.x * U, Screen.height - e.mousePosition.y * U, 0f);   // GUI -> экран
                    Lab.I.SpawnFromTable(carrying, sp);
                }
            }
            else if (drag == DragKind.Scroll && d.magnitude < 10f && candidate == null && !showPresets && Lab.I != null
                     && PopHit(e.mousePosition) >= 0)
            {
                // Касание, а не протяжка: протяжкой листают, и пролистанная кнопка не должна
                // срабатывать сама — поэтому сетка ловит нажатие здесь, как клетки таблицы,
                // а не через GUI.Button.
                Presets.SpawnPopular(Presets.Grid[PopHit(e.mousePosition)]);
            }
            else if (drag == DragKind.Scroll && d.magnitude < 10f && candidate != null && Lab.I != null)
            {
                if (pendingSlot >= 0 || replaceTarget != null) Choose(candidate);
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
        GUI.DrawTexture(new Rect(panelX, 0f, W, SH), Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Язычок: тянуть отсюда.
        var handle = new Rect(panelX + W, SH * 0.5f - 70f, HandleW, 140f);
        GUI.color = new Color(0.12f, 0.16f, 0.24f, 0.95f);
        GUI.DrawTexture(handle, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(handle.x + 4f, handle.y + 46f, HandleW, 60f), open ? "<\n<\n<" : ">\n>\n>", sSmall);

        GUI.Label(new Rect(panelX + 14f, 8f, W - 28f, 26f), Lang.T("АТОМНАЯ ЛАБОРАТОРИЯ", "ATOM LAB"), sTitle);
        GUI.Label(new Rect(panelX + 14f, 32f, W - 28f, 36f),
            Lang.T("Тяни элемент из таблицы вправо — в зону. Панель двигается свайпом, Esc — спрятать.", "Drag an element right into the zone. Swipe the panel, Esc hides it."), sSmall);

        // 🔴 21.09, владелец: вместо вкладок «Открытия / Задания / Как играть» — один тумблер.
        // Журнал и задания никуда не делись, они считаются как раньше; счётчик открытий виден
        // в правом верхнем углу. Просто перестали занимать половину панели.
        // 2D-режим — справа в той же строке, чтобы не сдвигать таблицу вниз.
        float w2d = Mathf.Round((W - 28f) * 0.3f);
        GUI.color = Lab.Mode2D ? new Color(0.55f, 1f, 0.75f) : Color.white;
        if (GUI.Button(new Rect(panelX + W - 14f - w2d, 74f, w2d, 28f),
            Lab.Mode2D ? Lang.T("2D: ВКЛ", "2D: ON") : Lang.T("2D: выкл", "2D: off"), sTab))
        {
            Lab.Mode2D = !Lab.Mode2D;
            Lab.I.Say(Lab.Mode2D
                ? Lang.T("2D-режим: вид спереди, все атомы в одной плоскости — как формула на бумаге.",
                         "2D mode: front view, all atoms in one plane — like a formula on paper.")
                : Lang.T("3D-режим: плоскость отпущена, молекулы расправляются в объём.",
                         "3D mode: the plane is released, molecules spread back into 3D."),
                new Color(0.7f, 1f, 0.8f));
        }
        GUI.color = Lab.GodMode ? new Color(1f, 0.85f, 0.35f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 74f, W - 28f - w2d - 6f, 28f),
            (Lab.GodMode ? Lang.T("РЕЖИМ БОГА: ВКЛ", "GOD MODE: ON") : Lang.T("Режим бога: выкл", "God mode: off")) + Lang.T("  —  все атомы могут соединиться", "  —  every atom can bond"), sTab))
        {
            Lab.GodMode = !Lab.GodMode;
            Lab.I.Say(Lab.GodMode
                ? Lang.T("Режим бога: валентность больше не считается, склеивается всё со всем — даже гелий.", "God mode: valence is ignored, everything sticks to everything — even helium.")
                : Lang.T("Обычный режим: работают валентность и правило благородных газов.", "Normal mode: valence and the noble gas rule apply."),
                Lab.GodMode ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.9f, 1f));
        }
        GUI.color = Color.white;

        GUI.color = quarkOn ? new Color(0.85f, 0.6f, 1f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 202f, W - 28f, 26f),
            quarkOn ? Lang.T("← выйти из сборки кварков", "← leave quark builder") : Lang.T("Сборка из кварков (этаж ниже атома)", "Quark builder (one level below the atom)"), sTab))
            ToggleQuarks();

        GUI.color = builderOn ? new Color(1f, 0.7f, 0.35f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 170f, W - 28f, 26f),
            builderOn ? Lang.T("← выйти из сборки атома", "← leave atom builder") : Lang.T("Сборка атома (протоны, нейтроны, электроны)", "Atom builder (protons, neutrons, electrons)"), sTab))
            ToggleBuilder();

        GUI.color = accelOn ? new Color(0.5f, 0.9f, 1f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 138f, W - 28f, 26f),
            accelOn ? Lang.T("← выйти из ускорителя", "← leave accelerator") : Lang.T("Ускоритель частиц (склеить ядра)", "Particle accelerator (fuse nuclei)"), sTab))
            ToggleAccelerator();
        GUI.color = Color.white;

        if (GUI.Button(new Rect(panelX + 14f, 106f, W - 28f, 26f),
            showPresets ? Lang.T("← назад к таблице", "← back to the table") : Lang.T("Готовые вещества (15 штук, со строением)", "Ready substances (15, with structure)"), sTab))
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
            Lang.T("Нажми — и вещество появится в зоне собранным.", "Tap one and it appears in the zone fully built."), sSmall);
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
        string[] names = { Lang.T("металлы", "metals"), Lang.T("неметаллы", "nonmetals"), Lang.T("радиация", "radioactive"), Lang.T("неизученные", "unexplored"), Lang.T("ускоритель", "accelerator"), Lang.T("собранные", "assembled") };
        Color[] cols =
        {
            new Color(0.82f, 0.20f, 0.22f), new Color(0.20f, 0.45f, 0.88f),
            new Color(0.20f, 0.72f, 0.32f), new Color(0.92f, 0.80f, 0.18f),
            new Color(0.35f, 0.80f, 1.00f), new Color(1.00f, 0.55f, 0.10f)
        };
        float x = r.x;
        for (int i = 0; i < names.Length; i++)
        {
            GUI.color = cols[i];
            GUI.DrawTexture(new Rect(x, r.y + 3f, 12f, 12f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            var sz = sSmall.CalcSize(new GUIContent(names[i]));
            GUI.Label(new Rect(x + 15f, r.y, sz.x + 4f, 18f), names[i], sSmall);
            x += 19f + sz.x;
        }
        GUI.Label(new Rect(r.x, r.y + 19f, r.width, 30f),
            Lang.T("Цвет клетки — класс элемента, цвет шарика в зоне — его собственный (палитра CPK).", "Cell colour is the element class; the ball in the zone has its own colour (CPK palette)."), sSmall);
    }

    void DrawTable(float cw, float ch)
    {
        // Подпись про выбранный элемент — над таблицей, чтобы не прыгала.
        var h = hover ?? carrying;
        if (h != null)
        {
            GUI.Label(new Rect(panelX + 14f, 232f, W - 28f, 44f),
                h.Z + ". " + h.Name + " (" + h.Sym + Lang.T(")   масса ", ")   mass ") + h.Mass.ToString("0.###") +
                Lang.T("   связей: ", "   bonds: ") + (h.MaxBonds > h.Valence ? h.Valence + Lang.T(", до ", ", up to ") + h.MaxBonds : h.Valence.ToString()) + (h.EN > 0f ? Lang.T("   ЭО ", "   EN ") + h.EN.ToString("0.00") : "") +
                "\n" + h.ClassName + "  ·  " + h.PaintName, sSmall);
        }
        else
        {
            DrawLegend(new Rect(panelX + 14f, 232f, W - 28f, 44f));
        }

        foreach (var el in Elements.All)
        {
            Rect r = CellRect(el);
            if (r.yMax < tableTop - 4f || r.y > SH) continue;    // вне видимой части — не рисуем
            Color c = el.PaintColor;
            bool isHover = (h == el);
            GUI.color = isHover ? Color.Lerp(c, Color.white, 0.45f) : c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;

            // Подпись поверх: тёмная на светлой клетке и наоборот.
            float lum = c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
            sCell.normal.textColor = lum > 0.5f ? Color.black : Color.white;

            // 🔴 Подписи собранных атомов длинные («Fe-56 3+») и в клетку не лезли — от них
            // оставались огрызки вроде «-34». Разбиваем на две строки и мельчим шрифт.
            if (el.Sym.Length > 3)
            {
                int cut = el.Sym.IndexOf('-');
                string top = cut > 0 ? el.Sym.Substring(0, cut) : el.Sym.Substring(0, 2);
                string bottom = cut > 0 ? el.Sym.Substring(cut + 1) : el.Sym.Substring(2);
                int keep = sCell.fontSize;
                sCell.fontSize = Mathf.Max(8, Mathf.RoundToInt(cw * 0.32f));
                GUI.Label(new Rect(r.x, r.y - 1f, r.width, r.height * 0.55f), top, sCell);
                GUI.Label(new Rect(r.x, r.y + r.height * 0.42f, r.width, r.height * 0.55f), bottom, sCell);
                sCell.fontSize = keep;
            }
            else GUI.Label(r, el.Sym, sCell);
        }

        DrawPopular();

        // Полоса прокрутки — тонкая, справа.
        if (scrollMax > 1f)
        {
            float viewH = SH - tableTop - 8f;
            float frac = viewH / (viewH + scrollMax);
            float barH = viewH * frac;
            float barY = tableTop + (viewH - barH) * (scroll / scrollMax);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTexture(new Rect(panelX + W - 6f, barY, 4f, barH), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
    }




    // ==================== 20 популярных соединений под таблицей ====================

    /// <summary>Верх сетки: сразу под самой нижней занятой клеткой таблицы. Считаем по самим
    /// клеткам, а не по номеру ряда: снизу прирастают голубые и оранжевые ряды, и сетка
    /// обязана уезжать вниз вместе с ними, а не налезать на них.</summary>
    float PopTop()
    {
        float bottom = tableTop - scroll;
        foreach (var el in Elements.All) bottom = Mathf.Max(bottom, CellRect(el).yMax);
        return bottom + 34f;
    }

    Rect PopRect(int i)
    {
        const int cols = 4; const float gap = 4f, bh = 44f;
        float bw = (W - 26f - gap * (cols - 1)) / cols;
        int nPop = Presets.Popular.Length;
        float extra = i >= nPop ? 24f : 0f;       // место под подпись «весь справочник»
        int row = i < nPop ? i / cols : (nPop + cols - 1) / cols + (i - nPop) / cols;
        return new Rect(panelX + 13f + ((i < nPop ? i : i - nPop) % cols) * (bw + gap), PopTop() + row * (bh + gap) + extra, bw, bh);
    }

    int PopHit(Vector2 p)
    {
        if (!TableViewRect.Contains(p)) return -1;
        for (int i = 0; i < Presets.Grid.Length; i++) if (PopRect(i).Contains(p)) return i;
        return -1;
    }

    void DrawPopular()
    {
        float top = PopTop();
        if (top - 24f < SH && top > tableTop - 40f)
            GUI.Label(new Rect(panelX + 14f, top - 24f, W - 28f, 20f),
                Lang.T("Популярные соединения — нажми, и появится в зоне", "Popular compounds — tap to drop into the zone"), sSmall);
        Vector2 m = Event.current.mousePosition;
        int nPop = Presets.Popular.Length;
        for (int i = 0; i < Presets.Grid.Length; i++)
        {
            var q = Presets.Grid[i];
            Rect r = PopRect(i);
            // Над первой клеткой справочника — своя подпись, чтобы было видно, где кончились
            // популярные и начался весь список.
            if (i == nPop && r.y - 22f < SH && r.y > tableTop - 20f)
                GUI.Label(new Rect(panelX + 14f, r.y - 22f, W - 28f, 20f),
                    Lang.T("Весь справочник игры — ", "The whole reference book — ") + (Presets.Grid.Length - nPop) + Lang.T(" веществ", " substances"), sSmall);
            if (r.yMax < tableTop - 4f || r.y > SH) continue;
            bool over = r.Contains(m) && drag == DragKind.None;
            GUI.color = over ? new Color(0.30f, 0.55f, 0.75f) : new Color(0.16f, 0.30f, 0.42f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            sCell.normal.textColor = Color.white;
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, r.height * 0.55f), q.Formula, sCell);
            int keep = sSmall.fontSize;
            var keepA = sSmall.alignment;
            sSmall.fontSize = 11; sSmall.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(r.x, r.y + r.height * 0.48f, r.width, r.height * 0.5f), Lang.T(q.Ru, q.En), sSmall);
            sSmall.fontSize = keep; sSmall.alignment = keepA;
        }
    }

    // ==================== мир: подписи над атомами и молекулами ====================

    void DrawWorldLabels()
    {
        var cam = Lab.I != null ? Lab.I.Cam : null;
        if (cam == null) return;

        foreach (var a in Atom.All)
        {
            Vector3 sp = cam.WorldToScreenPoint(a.transform.position); sp.x /= U; sp.y /= U;
            if (sp.z <= 0f) continue;
            float y = SH - sp.y;
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
                GUI.Label(new Rect(sp.x - 30f, y + 6f, 60f, 16f), Lang.T("занят", "full"), sWorld);
            }
        }

        foreach (var m in Lab.I.Mols)
        {
            if (m.Atoms.Count < 2) continue;
            Vector3 sp = cam.WorldToScreenPoint(m.Center + Vector3.up * 0.9f); sp.x /= U; sp.y /= U;
            if (sp.z <= 0f) continue;
            float y = SH - sp.y;
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
            Vector3 sp = cam.WorldToScreenPoint(a.transform.position); sp.x /= U; sp.y /= U;
            if (sp.z <= 0f) continue;
            float y = SH - sp.y;
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
                Mathf.Min(lab.BandA.x, lab.BandB.x) / U, SH - Mathf.Max(lab.BandA.y, lab.BandB.y) / U,
                Mathf.Max(lab.BandA.x, lab.BandB.x) / U, SH - Mathf.Min(lab.BandA.y, lab.BandB.y) / U);
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
        var r = new Rect(SW - 260f, 10f, 250f, 60f);
        GUI.color = new Color(0f, 0f, 0f, 0.45f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 22f), Lang.T("Очки: ", "Score: ") + lab.Score, sTitle);
        GUI.Label(new Rect(r.x + 10f, r.y + 30f, r.width - 20f, 22f),
            Lang.T("Открыто веществ: ", "Substances found: ") + lab.Discovered.Count + " / " + Molecules.Total + Lang.T("   ·   атомов: ", "   ·   atoms: ") + Atom.All.Count, sSmall);

        if (Time.time < lab.ToastUntil && !string.IsNullOrEmpty(lab.Toast))
        {
            var tr = new Rect(panelX + W + 40f, SH - 96f, SW - (panelX + W) - 80f, 64f);
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
        if (accelOn || builderOn || quarkOn) return;    // внизу стоит пульт режима, карточке там не место
        Lab.Mol best = null;
        foreach (var m in lab.Mols)
            if (m.Atoms.Count > 1 && (best == null || m.Atoms.Count > best.Atoms.Count)) best = m;
        if (best == null) return;

        float x = PanelRightGui + 20f;
        float w = Mathf.Min(430f, SW - x - 20f);
        if (w < 160f) return;
        bool hasUse = best.Info != null && !string.IsNullOrEmpty(best.Info.Use);
        float h = best.Info != null ? (hasUse ? 128f : 104f) : 78f;
        cardHeight = h;
        var r = new Rect(x, SH - h - 20f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = best.Info != null ? new Color(0.4f, 0.9f, 0.5f, 0.9f) : new Color(0.5f, 0.6f, 0.8f, 0.7f);
        GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), Texture2D.whiteTexture);   // цветная полоска слева
        GUI.color = Color.white;

        sBig.normal.textColor = best.Info != null ? new Color(0.65f, 1f, 0.7f) : Color.white;
        GUI.Label(new Rect(r.x + 14f, r.y + 6f, r.width - 24f, 32f), best.Formula, sBig);

        if (best.Info != null)
        {
            GUI.Label(new Rect(r.x + 14f, r.y + 38f, r.width - 24f, 22f), Lang.Name(best.Info), sTitle);
            GUI.Label(new Rect(r.x + 14f, r.y + 60f, r.width - 24f, 38f), Lang.Note(best.Info), sSmall);
            if (hasUse)
            {
                // 🔴 21.09, владелец: «добавь сюда область применения». Отдельной строкой и
                // другим цветом — это не рассказ о веществе, а ответ «где я его встречу».
                var keep = sSmall.normal.textColor;
                sSmall.normal.textColor = new Color(1f, 0.85f, 0.45f);
                GUI.Label(new Rect(r.x + 14f, r.y + 100f, r.width - 24f, 24f),
                          Lang.T("Применение: ", "Used for: ") + Lang.Use(best.Info), sSmall);
                sSmall.normal.textColor = keep;
            }
        }
        else
        {
            // Нет в справочнике — это ещё не «не бывает». Пробуем назвать по правилам.
            var counts = new Dictionary<string, int>();
            foreach (var at in best.Atoms)
            {
                int c; counts.TryGetValue(at.El.Sym, out c);
                counts[at.El.Sym] = c + 1;
            }
            var guess = Naming.Describe(counts);
            string text;
            if (guess != null && guess.Plausible)
                text = (Lang.EN ? guess.En : guess.Ru) + "  —  " + guess.Why + ".";
            else if (guess != null)
                text = (Lang.EN ? guess.En : guess.Ru) + "?  " + guess.Why + ".";
            else
                text = Lang.T("В справочнике игры такого нет. Это не значит, что его нет в природе.",
                              "Not in the game's reference book. That does not mean it does not exist.");
            GUI.Label(new Rect(r.x + 14f, r.y + 38f, r.width - 24f, 34f),
                text + "  " + Lang.T("Атомов: ", "Atoms: ") + best.Atoms.Count, sSmall);
        }
    }


    /// <summary>Кнопки зоны — слева внизу, НАД карточкой формулы (🔴 21.09, просьба
    /// владельца). Держатся над карточкой, а не на месте: карточка растёт, когда вещество
    /// узнано, и кнопки уезжали бы под неё.</summary>
    void DrawZoneButtons()
    {
        var lab = Lab.I;
        if (lab == null) return;

        if (accelOn || builderOn || quarkOn) return;   // внизу стоит пульт того режима, что включён
        float x = PanelRightGui + 20f;
        if (SW - x < 200f) return;
        float y = SH - cardHeight - 20f - 34f;

        if (GUI.Button(new Rect(x, y, 140f, 28f), Lang.T("Убрать атомы", "Clear atoms"), sTab)) lab.ClearZone();

        GUI.color = new Color(0.75f, 1f, 0.8f);
        if (GUI.Button(new Rect(x + 148f, y, 180f, 28f), Lang.T("Посмотреть реакцию", "Run reaction"), sTab)) Chemistry.React();
        GUI.color = new Color(1f, 0.8f, 0.8f);
        if (GUI.Button(new Rect(x + 336f, y, 140f, 28f), Lang.T("Выход из игры", "Quit game"), sTab)) lab.ExitGame();
        GUI.color = Color.white;

        if (lab.Selected.Count > 0 || lab.ClipboardCount > 0)
            GUI.Label(new Rect(x, y - 20f, 520f, 18f),
                Lang.T("Выделено: ", "Selected: ") + lab.Selected.Count + Lang.T("   ·   в буфере: ", "   ·   clipboard: ") + lab.ClipboardCount +
                Lang.T("   ·   Ctrl+C копировать, Ctrl+V (Ctrl+М) вставить, Ctrl+A выделить всё", "   ·   Ctrl+C copy, Ctrl+V paste, Ctrl+A select all"), sSmall);
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
        menuPos = new Vector2(screenPos.x / U, SH - screenPos.y / U);   // экран -> интерфейс
    }

    public void CloseMenu() { menuAtom = null; menuBondList = false; }

    Rect MenuRect
    {
        get
        {
            if (menuAtom == null) return new Rect();
            int rows = 6 + (menuBondList ? menuAtom.Bonds.Count : 0);
            float w = 260f, h = 26f + rows * 24f;
            float x = Mathf.Min(menuPos.x, SW - w - 6f);
            float y = Mathf.Min(menuPos.y, SH - h - 6f);
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
            menuAtom.El.Name + " (" + menuAtom.El.Sym + Lang.T(")   связей ", ")   bonds ") + used + Lang.T(" из ", " of ") + menuAtom.El.MaxBonds, sSmall);
        y += 22f;

        if (menuAtom.Bonds.Count > 0)
        {
            if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f),
                (menuBondList ? "- " : "+ ") + Lang.T("Убрать связь с...", "Remove bond with..."), sTab)) menuBondList = !menuBondList;
            y += 24f;

            if (menuBondList)
            {
                foreach (var b in new List<Bond>(menuAtom.Bonds))
                {
                    var other = b.Other(menuAtom);
                    string kind = b.Order == 1 ? Lang.T("одинарная", "single") : (b.Order == 2 ? Lang.T("двойная", "double") : Lang.T("тройная", "triple"));
                    if (GUI.Button(new Rect(r.x + 18f, y, r.width - 24f, 22f),
                        Lang.T("с ", "with ") + other.El.Sym + " (" + kind + ")", sTab))
                    {
                        b.Break();
                        Lab.I.Recompute();
                        Lab.I.Say(Lang.T("Связь ", "Bond ") + menuAtom.El.Sym + "-" + other.El.Sym + Lang.T(" разорвана.", " broken."), new Color(1f, 0.85f, 0.6f));
                        if (menuAtom.Bonds.Count == 0) menuBondList = false;
                    }
                    y += 24f;
                }
            }

            if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), Lang.T("Убрать все связи", "Remove all bonds"), sTab))
            {
                int n = menuAtom.Bonds.Count;
                for (int i = menuAtom.Bonds.Count - 1; i >= 0; i--) menuAtom.Bonds[i].Break();
                Lab.I.Recompute();
                Lab.I.Say(Lang.T("Оторвано связей: ", "Bonds broken: ") + n, new Color(1f, 0.85f, 0.6f));
                CloseMenu();
                return;
            }
            y += 24f;
        }
        else { GUI.Label(new Rect(r.x + 8f, y, r.width - 16f, 20f), Lang.T("связей нет", "no bonds"), sNote); y += 46f; }

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), Lang.T("Удалить атом", "Delete atom"), sTab))
        {
            menuAtom.Despawn();
            Lab.I.Recompute();
            CloseMenu();
            return;
        }
        y += 24f;

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), Lang.T("Заменить (выбери в таблице)", "Replace (pick in the table)"), sTab))
        {
            replaceTarget = menuAtom;
            showPresets = false;
            open = true;
            Lab.I.Say(Lang.T("Выбери элемент в таблице - ", "Pick an element in the table - ") + menuAtom.El.Sym + Lang.T(" станет им. Связи, на которые не хватит запаса, оторвутся.", " will become it. Bonds beyond its valence will break."),
                new Color(0.85f, 0.95f, 1f));
            CloseMenu();
            return;
        }
        y += 24f;

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), Lang.T("Провести реакцию", "Run reaction"), sTab))
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
        if (accelOn && builderOn) { builderOn = false; if (AtomBuilder.I != null) AtomBuilder.I.Active = false; }
        if (accelOn && quarkOn) { quarkOn = false; if (QuarkLab.I != null) QuarkLab.I.Active = false; }
        pendingSlot = -1;
        if (accelOn)
        {
            Lab.I.LookAt(Accelerator.Rig, 13f);
            Lab.I.Say(Lang.T("Ускоритель. Щёлкни по гнезду, потом по элементу в таблице — и жми «Склеить вещества».", "Accelerator. Click a slot, then an element in the table — then press 'Fuse'."),
                new Color(0.6f, 0.9f, 1f));
        }
        else
        {
            Lab.I.LookAt(Lab.ZoneCenter, 14f);
            Lab.I.Say(Lang.T("Назад в лабораторию.", "Back to the lab."), new Color(0.8f, 0.9f, 1f));
        }
    }

    /// <summary>Пульт ускорителя: два гнезда, результат и кнопка склейки.</summary>
    void DrawAccelerator()
    {
        if (!accelOn) return;
        var acc = Accelerator.I;
        if (acc == null) return;

        float x = PanelRightGui + 20f;
        float w = Mathf.Min(560f, SW - x - 20f);
        if (w < 260f) return;
        var r = new Rect(x, SH - 176f, w, 156f);

        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.8f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), Lang.T("УСКОРИТЕЛЬ ЧАСТИЦ", "PARTICLE ACCELERATOR"), sTitle);
        GUI.Label(new Rect(r.x + 12f, r.y + 28f, r.width - 24f, 20f),
            Lang.T("Ядра складываются: номер нового элемента — сумма номеров. Так и получили всё тяжелее урана.", "Nuclei add up: the new element number is the sum. This is how everything heavier than uranium was made."), sSmall);

        float bw = (r.width - 36f) / 3f;
        DrawSlotButton(new Rect(r.x + 12f, r.y + 52f, bw, 40f), 0, acc.SlotA, Lang.T("гнездо слева", "left slot"));
        DrawSlotButton(new Rect(r.x + 18f + bw, r.y + 52f, bw, 40f), -1, acc.Result, Lang.T("результат", "result"));
        DrawSlotButton(new Rect(r.x + 24f + bw * 2f, r.y + 52f, bw, 40f), 1, acc.SlotB, Lang.T("гнездо справа", "right slot"));

        GUI.color = new Color(0.6f, 0.95f, 1f);
        if (GUI.Button(new Rect(r.x + 12f, r.y + 100f, bw * 1.6f, 30f), Lang.T("Склеить вещества", "Fuse"), sTab)) acc.Fuse();
        GUI.color = Color.white;
        if (acc.Result != null && GUI.Button(new Rect(r.x + 24f + bw * 1.6f, r.y + 100f, bw * 1.3f, 30f), Lang.T("Забрать в зону", "Send to zone"), sTab))
            acc.TakeResult();

        if (pendingSlot >= 0)
            GUI.Label(new Rect(r.x + 12f, r.y + 132f, r.width - 24f, 20f),
                Lang.T("Теперь выбери элемент в таблице слева — он встанет в гнездо.", "Now pick an element in the table — it goes into the slot."), sNote);
        else if (Accelerator.Log.Count > 0)
        {
            // Журнал добытого: раньше результат жил только в гнезде и терялся при следующей
            // склейке. Теперь видно всё, что вышло за сеанс.
            int n = Mathf.Min(3, Accelerator.Log.Count);
            var sb = new System.Text.StringBuilder(Lang.T("добыто: ", "made: "));
            for (int i = 0; i < n; i++) sb.Append(Accelerator.Log[Accelerator.Log.Count - 1 - i]).Append(i < n - 1 ? ";  " : "");
            GUI.Label(new Rect(r.x + 12f, r.y + 132f, r.width - 24f, 20f), sb.ToString(), sNote);
        }
    }

    void DrawSlotButton(Rect r, int slot, Elements.El el, string title)
    {
        bool waiting = (slot >= 0 && pendingSlot == slot);
        GUI.color = waiting ? new Color(1f, 0.9f, 0.5f) : (el != null ? el.PaintColor : new Color(0.5f, 0.5f, 0.55f));
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;

        string text = el != null ? el.Sym + "  " + el.Name + "  (" + el.Z + ")" : Lang.T("пусто", "empty");
        GUI.Label(new Rect(r.x + 8f, r.y + 2f, r.width - 12f, 18f), title, sSmall);
        GUI.Label(new Rect(r.x + 8f, r.y + 18f, r.width - 12f, 20f), text, sSmall);

        if (slot >= 0 && GUI.Button(r, "", GUIStyle.none)) pendingSlot = (pendingSlot == slot) ? -1 : slot;
    }


    // ==================== сборка атома ====================

    bool builderOn;

    void ToggleBuilder()
    {
        var b = AtomBuilder.I;
        if (b == null || Lab.I == null) return;
        builderOn = !builderOn;
        b.Active = builderOn;
        if (builderOn)
        {
            if (accelOn) { accelOn = false; if (Accelerator.I != null) Accelerator.I.Active = false; }
            if (quarkOn) { quarkOn = false; if (QuarkLab.I != null) QuarkLab.I.Active = false; }
            Lab.I.LookAt(AtomBuilder.Rig, 9f);
            Lab.I.Say(Lang.T("Сборка атома. Протоны решают, ЧТО это за элемент; нейтроны — какой изотоп; электроны — заряд.", "Atom builder. Protons decide WHICH element; neutrons — which isotope; electrons — the charge."),
                new Color(1f, 0.8f, 0.5f));
        }
        else
        {
            Lab.I.LookAt(Lab.ZoneCenter, 14f);
            Lab.I.Say(Lang.T("Назад в лабораторию.", "Back to the lab."), new Color(0.8f, 0.9f, 1f));
        }
    }

    /// <summary>Пульт сборки: три счётчика и приговор о том, что вышло.</summary>
    void DrawBuilder()
    {
        if (!builderOn) return;
        var b = AtomBuilder.I;
        if (b == null) return;

        float x = PanelRightGui + 20f;
        float w = Mathf.Min(600f, SW - x - 20f);
        if (w < 280f) return;
        var r = new Rect(x, SH - 232f, w, 212f);

        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.6f, 0.2f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), Lang.T("СБОРКА АТОМА", "ATOM BUILDER"), sTitle);

        float rowY = r.y + 32f;
        Counter(new Rect(r.x + 12f, rowY, r.width - 24f, 26f), Lang.T("Протоны", "Protons"), b.Protons, new Color(0.9f, 0.3f, 0.25f), 0);
        Counter(new Rect(r.x + 12f, rowY + 30f, r.width - 24f, 26f), Lang.T("Нейтроны", "Neutrons"), b.Neutrons, new Color(0.7f, 0.7f, 0.75f), 1);
        Counter(new Rect(r.x + 12f, rowY + 60f, r.width - 24f, 26f), Lang.T("Электроны", "Electrons"), b.Electrons, new Color(0.35f, 0.8f, 1f), 2);

        // 🔴 21.09, владелец: «текст наехал». Приговор длинный и переносится, поэтому ему
        // отведена своя полоса в 56 пикселей, а кнопки стоят ПОД ней, а не поверх.
        GUI.Label(new Rect(r.x + 12f, rowY + 92f, r.width - 24f, 56f), b.Verdict, sSmall);

        GUI.color = new Color(1f, 0.8f, 0.45f);
        if (GUI.Button(new Rect(r.x + 12f, r.y + 176f, 210f, 28f), Lang.T("Записать в таблицу", "Save to table"), sTab)) b.SaveToTable();
        GUI.color = Color.white;
        if (GUI.Button(new Rect(r.x + 230f, r.y + 176f, 210f, 28f), Lang.T("Записать и в зону", "Save and send to zone"), sTab)) b.SendToZone();
    }

    void Counter(Rect r, string title, int value, Color c, int kind)
    {
        GUI.color = c;
        GUI.DrawTexture(new Rect(r.x, r.y + 6f, 14f, 14f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 20f, r.y + 3f, 110f, 22f), title, sSmall);
        GUI.Label(new Rect(r.x + 130f, r.y + 3f, 60f, 22f), value.ToString(), sTitle);

        var b = AtomBuilder.I;
        float bx = r.x + 190f;
        if (GUI.Button(new Rect(bx, r.y, 34f, 24f), "-", sTab)) Change(kind, -1);
        if (GUI.Button(new Rect(bx + 38f, r.y, 34f, 24f), "+", sTab)) Change(kind, +1);
        if (GUI.Button(new Rect(bx + 80f, r.y, 44f, 24f), "-10", sTab)) Change(kind, -10);
        if (GUI.Button(new Rect(bx + 128f, r.y, 44f, 24f), "+10", sTab)) Change(kind, +10);
    }

    void Change(int kind, int d)
    {
        var b = AtomBuilder.I;
        if (b == null) return;
        if (kind == 0) b.Add(d, 0, 0);
        else if (kind == 1) b.Add(0, d, 0);
        else b.Add(0, 0, d);
    }


    // ==================== сборка из кварков ====================

    bool quarkOn;

    void ToggleQuarks()
    {
        var q = QuarkLab.I;
        if (q == null || Lab.I == null) return;
        quarkOn = !quarkOn;
        q.Active = quarkOn;
        if (quarkOn)
        {
            if (accelOn) { accelOn = false; if (Accelerator.I != null) Accelerator.I.Active = false; }
            if (builderOn) { builderOn = false; if (AtomBuilder.I != null) AtomBuilder.I.Active = false; }
            Lab.I.LookAt(QuarkLab.Rig, 8f);
            Lab.I.Say(Lang.T("Кварки. Протон — это uud, нейтрон — udd. Собранное уходит наверх, в сборку атома.", "Quarks. A proton is uud, a neutron is udd. What you build goes up into the atom builder."),
                new Color(0.85f, 0.7f, 1f));
        }
        else
        {
            Lab.I.LookAt(Lab.ZoneCenter, 14f);
            Lab.I.Say(Lang.T("Назад в лабораторию.", "Back to the lab."), new Color(0.8f, 0.9f, 1f));
        }
    }

    /// <summary>Пульт кварков: два вида кварков, тройка и приговор.</summary>
    void DrawQuarks()
    {
        if (!quarkOn) return;
        var q = QuarkLab.I;
        if (q == null) return;

        float x = PanelRightGui + 20f;
        float w = Mathf.Min(600f, SW - x - 20f);
        if (w < 280f) return;
        var r = new Rect(x, SH - 216f, w, 196f);

        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.8f, 0.5f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), Lang.T("СБОРКА ИЗ КВАРКОВ", "QUARK BUILDER"), sTitle);
        GUI.Label(new Rect(r.x + 12f, r.y + 28f, r.width - 24f, 20f),
            Lang.T("Верхний кварк даёт +2/3, нижний −1/3. Три кварка — барион.", "An up quark gives +2/3, a down quark −1/3. Three quarks make a baryon."), sSmall);

        GUI.color = new Color(1f, 0.7f, 0.3f);
        if (GUI.Button(new Rect(r.x + 12f, r.y + 52f, 150f, 30f), Lang.T("+ верхний (u)", "+ up (u)"), sTab)) q.Add(true);
        GUI.color = new Color(0.5f, 0.7f, 1f);
        if (GUI.Button(new Rect(r.x + 170f, r.y + 52f, 150f, 30f), Lang.T("+ нижний (d)", "+ down (d)"), sTab)) q.Add(false);
        GUI.color = Color.white;
        if (GUI.Button(new Rect(r.x + 328f, r.y + 52f, 110f, 30f), Lang.T("очистить", "clear"), sTab)) q.Clear();

        GUI.Label(new Rect(r.x + 12f, r.y + 88f, r.width - 24f, 22f),
            Lang.T("В тройке: ", "In the triple: ") + q.Composition + Lang.T("    заряд: ", "    charge: ") + (q.Charge >= 0f ? "+" : "") + q.Charge.ToString("0.##"), sTitle);
        GUI.Label(new Rect(r.x + 12f, r.y + 110f, r.width - 24f, 42f), q.Verdict, sSmall);

        GUI.color = new Color(0.85f, 0.7f, 1f);
        if (GUI.Button(new Rect(r.x + 12f, r.y + 158f, 230f, 28f), Lang.T("Собрать частицу", "Build particle"), sTab)) q.Assemble();
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 252f, r.y + 160f, r.width - 264f, 22f),
            Lang.T("собрано: протонов ", "built: protons ") + q.MadeProtons + Lang.T(", нейтронов ", ", neutrons ") + q.MadeNeutrons, sSmall);
    }


    // ==================== подменю выбора элемента ====================

    /// <summary>🔴 21.09, владелец: «замена и выбор в таблице должны открывать подменю справа
    /// от окна с таблицей». Раньше игра просто писала подсказку в углу, и было неясно, чего
    /// она ждёт. Теперь рядом с таблицей встаёт колонка с ходовыми элементами: ткнул — готово.
    /// Клетка в самой таблице по-прежнему работает: подменю не запрещает, а сокращает путь.</summary>
    static readonly string[] COMMON =
    {
        "H", "C", "N", "O", "F", "Na", "Mg", "Al", "Si", "P",
        "S", "Cl", "K", "Ca", "Fe", "Cu", "Zn", "Ag", "Au", "Pb", "U"
    };

    void DrawPicker()
    {
        bool forReplace = replaceTarget != null;
        bool forSlot = pendingSlot >= 0;
        if (!forReplace && !forSlot) return;

        float x = PanelRightGui + 6f;
        float w = 250f;
        if (x + w > SW - 10f) return;
        float h = 92f + Mathf.Ceil(COMMON.Length / 3f) * 30f;
        var r = new Rect(x, tableTop - 40f, w, h);

        GUI.color = new Color(0.07f, 0.1f, 0.16f, 0.97f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = forReplace ? new Color(1f, 0.8f, 0.4f, 0.9f) : new Color(0.4f, 0.8f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string title = forReplace
            ? Lang.T("Заменить ", "Replace ") + replaceTarget.El.Sym + Lang.T(" на:", " with:")
            : Lang.T("В гнездо ", "Into slot ") + (pendingSlot == 0 ? Lang.T("слева", "left") : Lang.T("справа", "right")) + ":";
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 22f), title, sTitle);
        GUI.Label(new Rect(r.x + 10f, r.y + 28f, r.width - 20f, 20f), Lang.T("ходовые — или любая клетка слева", "common ones — or any cell on the left"), sSmall);

        float cw = (r.width - 28f) / 3f;
        for (int i = 0; i < COMMON.Length; i++)
        {
            var el = Elements.BySymbol(COMMON[i]);
            if (el == null) continue;
            var cell = new Rect(r.x + 10f + (i % 3) * (cw + 4f), r.y + 52f + (i / 3) * 30f, cw, 26f);
            GUI.color = el.PaintColor;
            GUI.DrawTexture(cell, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float lum = el.PaintColor.r * 0.3f + el.PaintColor.g * 0.59f + el.PaintColor.b * 0.11f;
            sCell.normal.textColor = lum > 0.5f ? Color.black : Color.white;
            GUI.Label(cell, el.Sym, sCell);
            if (GUI.Button(cell, "", GUIStyle.none)) Choose(el);
        }

        if (GUI.Button(new Rect(r.x + 10f, r.yMax - 32f, r.width - 20f, 24f), Lang.T("отмена", "cancel"), sTab))
        {
            replaceTarget = null;
            pendingSlot = -1;
        }
    }

    /// <summary>Один путь для обоих случаев: и подменю, и клетка таблицы приводят сюда.</summary>
    void Choose(Elements.El el)
    {
        if (pendingSlot >= 0 && Accelerator.I != null)
        {
            Accelerator.I.Put(pendingSlot, el);
            pendingSlot = -1;
            return;
        }
        if (replaceTarget != null && Lab.I != null)
        {
            string was = replaceTarget.El.Sym;
            replaceTarget.Become(el);
            Fx.Pop(1.2f);
            Fx.Sparks(replaceTarget.transform.position, new Color(0.8f, 0.95f, 1f), 25, 3f);
            Lab.I.Recompute();
            Lab.I.Say(was + Lang.T(" стал ", " became ") + el.Sym + " (" + el.Name + ").", new Color(0.85f, 0.95f, 1f));
            replaceTarget = null;
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
        GUI.Label(new Rect(r.x - 30f, r.yMax + 2f, cw + 60f, 20f), Lang.T("отпусти в зоне", "drop in the zone"), sSmall);
    }
}
