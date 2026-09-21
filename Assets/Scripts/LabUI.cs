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
            if (updRect.width > 0f && updRect.Contains(MouseGui)) return true;   // плашка обновления
            if (thermoRect.width > 0f && thermoRect.Contains(MouseGui)) return true;   // панель нагрева
            if (gravRect.width > 0f && gravRect.Contains(MouseGui)) return true;       // карта притяжения
            if (menuAtom != null &&
                MenuRect.Contains(MouseGui)) return true;
            if (insideEl != null) return true;                  // окно устройства атома — модальное
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

    void Awake()
    {
        I = this;
        // 21.09, владелец (скриншот с телефона): «в телефон версии атомлаба места нет».
        // На телефоне панель с таблицей съедала почти половину экрана, и зоне оставался угол.
        // Поэтому на телефоне игра стартует со СПРЯТАННОЙ панелью: язычок у левого края и
        // свайп вправо её достают. На ПК всё как было — там места хватает.
        if (Application.isMobilePlatform || Touchy) { open = false; startClosed = true; }
    }

    bool startClosed;

    /// <summary>Телефонная раскладка включается и ключом -uiscale (чтобы проверить её на ПК).</summary>
    static bool Touchy
    {
        get
        {
            foreach (var a in System.Environment.GetCommandLineArgs()) if (a == "-uiscale") return true;
            return false;
        }
    }

    /// <summary>На телефоне после готового вещества панель уезжает: молекулу надо видеть на
    /// весь экран. После отдельного элемента — нет: молекулу собирают по атому, и открывать
    /// панель на каждый атом было бы мучением.</summary>
    void AfterCompoundOnPhone()
    {
        if (startClosed) open = false;
    }

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

        DrawColdTint();
        DrawWorldLabels();
        DrawSelection();
        DrawPanel(cw, ch);
        DrawHud();
        DrawFormulaCard();
        DrawZoneButtons();
        DrawAccelerator();
        DrawBuilder();
        DrawQuarks();
        DrawPhys();
        DrawThermo();
        DrawGravity();
        DrawPicker();
        DrawMenu();
        DrawInside();
        DrawCarry(e);
        DrawUpdate();
        DrawHoverTip();
        DrawCellTip();
    }

    /// <summary>21.09, из очереди владельца на 2.2: «подсказка при наведении на атом:
    /// название, заряд, связи». Только для мыши: у пальца нет «наведения», на телефоне то же
    /// самое показывает меню по долгому нажатию. Не мешаем, когда что-то тащат, тянут рамку,
    /// открыто меню или курсор над панелью.</summary>
    /// <summary>21.09, владелец: «при наведении курсора на элемент выводить стату над
    /// курсором». Раньше данные элемента печатались строкой над таблицей — глаз уходил
    /// от клетки. Теперь та же строка всплывает прямо над курсором.</summary>
    void DrawCellTip()
    {
        if (Input.touchCount > 0 || carrying != null || showPresets) return;
        if (hover == null) return;
        var h = hover;
        string cap = h.MaxBonds > h.Valence ? h.Valence + Lang.T(" (до ", " (up to ") + h.MaxBonds + ")" : h.Valence.ToString();
        string text = h.Z + ". " + Lang.Name(h) + " (" + h.Sym + ")\n" +
                      Lang.T("Масса: ", "Mass: ") + h.Mass.ToString("0.###") + "\n" +
                      Lang.T("Связей: ", "Bonds: ") + cap +
                      (h.EN > 0f ? Lang.T("   ЭО ", "   EN ") + h.EN.ToString("0.00") : "") + "\n" +
                      h.ClassName + "  ·  " + h.PaintName;
        Vector2 m = Event.current.mousePosition;
        var r = new Rect(m.x - 110f, m.y - 92f, 250f, 80f);           // НАД курсором, чтобы не закрывать клетку
        if (r.y < 4f) r.y = m.y + 22f;
        if (r.x < 4f) r.x = 4f;
        if (r.xMax > SW - 4f) r.x = SW - 4f - r.width;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = h.PaintColor;
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 9f, r.y + 3f, r.width - 12f, r.height - 4f), text, sSmall);
    }

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
                AfterCompoundOnPhone();
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
        // 21.09: всё, что шире панели (хвост легенды «собранные»), торчало у левого края
        // экрана, когда панель спрятана. Режем рисование по правому краю панели. Начало клипа
        // в (0,0), поэтому координаты внутри не меняются.
        GUI.BeginClip(new Rect(0f, 0f, Mathf.Max(0f, panelX + W + HandleW), SH));
        DrawPanelInner(cw, ch);
        GUI.EndClip();
    }

    void DrawPanelInner(float cw, float ch)
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

        // Притяжение масс — кнопкой здесь, а не на карте: карта только показывает (владелец, 21.09).
        float wGrav = Mathf.Round((W - 28f) * 0.36f);
        var gv = Gravity.I;
        if (gv != null)
        {
            GUI.color = gv.On ? new Color(0.8f, 0.65f, 1f) : Color.white;
            if (GUI.Button(new Rect(panelX + W - 14f - wGrav, 202f, wGrav, 26f),
                gv.On ? Lang.T("Притяжение: ВКЛ", "Gravity: ON") : Lang.T("Притяжение: выкл", "Gravity: off"), sTab))
            {
                gv.On = !gv.On;
                Lab.I.Say(gv.On ? Lang.T("Притяжение включено: лёгкие атомы потянутся к тяжёлым.", "Gravity on: light atoms will drift to heavy ones.")
                                : Lang.T("Притяжение выключено.", "Gravity off."), new Color(0.8f, 0.65f, 1f));
            }
        }
        GUI.color = quarkOn ? new Color(0.85f, 0.6f, 1f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 202f, gv != null ? W - 28f - wGrav - 6f : W - 28f, 26f),
            quarkOn ? Lang.T("← выйти из сборки кварков", "← leave quark builder") : Lang.T("Сборка из кварков", "Quark builder"), sTab))
            ToggleQuarks();

        GUI.color = builderOn ? new Color(1f, 0.7f, 0.35f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 170f, W - 28f, 26f),
            builderOn ? Lang.T("← выйти из сборки атома", "← leave atom builder") : Lang.T("Сборка атома (протоны, нейтроны, электроны)", "Atom builder (protons, neutrons, electrons)"), sTab))
            ToggleBuilder();

        // Верхний уровень — на той же строке, что ускоритель: таблицу вниз не сдвигаем.
        float wPhys = Mathf.Round((W - 28f) * 0.42f);
        GUI.color = physOn ? new Color(0.95f, 0.8f, 0.45f) : Color.white;
        if (GUI.Button(new Rect(panelX + W - 14f - wPhys, 138f, wPhys, 26f),
            physOn ? Lang.T("← выйти со стола", "← leave the bench") : Lang.T("Вещи: стакан, натрий, колбы", "Things: glass, sodium, flasks"), sTab))
            TogglePhys();
        GUI.color = accelOn ? new Color(0.5f, 0.9f, 1f) : Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 138f, W - 28f - wPhys - 6f, 26f),
            accelOn ? Lang.T("← выйти из ускорителя", "← leave accelerator") : Lang.T("Ускоритель частиц (склеить ядра)", "Particle accelerator (fuse nuclei)"), sTab))
            ToggleAccelerator();
        GUI.color = Color.white;

        // Уровень точности — на строке готовых веществ, справа. Нажатие листает по кругу.
        float wLvl = Mathf.Round((W - 28f) * 0.4f);
        Color[] lvlCol = { new Color(1f, 0.6f, 0.9f), Color.white, new Color(0.6f, 0.85f, 1f), new Color(1f, 0.85f, 0.35f) };
        GUI.color = lvlCol[(int)Lab.Mode];
        if (GUI.Button(new Rect(panelX + W - 14f - wLvl, 106f, wLvl, 26f), Lang.T("Уровень: ", "Level: ") + Lab.LevelName(Lab.Mode) + "  ▸", sTab))
        {
            Lab.Mode = (Lab.Level)(((int)Lab.Mode + 1) % 4);
            Lab.I.Say(Lab.LevelHint(Lab.Mode), lvlCol[(int)Lab.Mode]);
        }
        GUI.color = Color.white;
        if (GUI.Button(new Rect(panelX + 14f, 106f, W - 28f - wLvl - 6f, 26f),
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
            { Presets.Spawn(p); AfterCompoundOnPhone(); }
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
            sCell.richText = true;
            GUI.Label(new Rect(r.x, r.y + 2f, r.width, r.height * 0.55f), Lang.Sub(q.Formula, 10), sCell);
            int keep = sSmall.fontSize;
            var keepA = sSmall.alignment;
            sSmall.fontSize = 11; sSmall.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(r.x, r.y + r.height * 0.48f, r.width, r.height * 0.5f), Lang.T(q.Ru, q.En), sSmall);
            sSmall.fontSize = keep; sSmall.alignment = keepA;
        }
    }

    // ==================== мир: подписи над атомами и молекулами ====================

    static Texture2D _dot;
    /// <summary>Круглая точка: квадрат выглядел бы пикселем, а не «местом для связи».</summary>
    static Texture2D DotTex
    {
        get
        {
            if (_dot != null) return _dot;
            const int N = 32;
            _dot = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            float c = (N - 1) * 0.5f;
            for (int yy = 0; yy < N; yy++)
                for (int xx = 0; xx < N; xx++)
                {
                    float d = Mathf.Sqrt((xx - c) * (xx - c) + (yy - c) * (yy - c));
                    px[yy * N + xx] = new Color(1f, 1f, 1f, Mathf.Clamp01(c - d + 0.5f));
                }
            _dot.SetPixels(px); _dot.Apply();
            return _dot;
        }
    }

    void DrawWorldLabels()
    {
        var cam = Lab.I != null ? Lab.I.Cam : null;
        if (cam == null) return;

        // 21.09, владелец: «значки у молекул крупнее и рисовать зелёные и красные точки».
        // Символ теперь растёт вместе с шариком на экране (крупный атом вблизи — крупная
        // буква), с тенью, чтобы читался и на жёлтой сере, и на белом водороде. Вместо точек-
        // крапинок и слова «занят» — кольцо точек вокруг символа: КРАСНАЯ — занятая связь,
        // ЗЕЛЁНАЯ — свободная. Сколько точек, столько связей: полный атом виден сразу.
        foreach (var a in Atom.All)
        {
            Vector3 sp = cam.WorldToScreenPoint(a.transform.position);
            if (sp.z <= 0f) continue;
            Vector3 edge = cam.WorldToScreenPoint(a.transform.position + cam.transform.right * a.El.Radius);
            float rad = Mathf.Max(6f, ((Vector2)edge - (Vector2)sp).magnitude) / U;   // радиус шарика в единицах интерфейса
            sp.x /= U; sp.y /= U;
            float y = SH - sp.y;

            int keep = sWorld.fontSize;
            sWorld.fontSize = Mathf.RoundToInt(Mathf.Clamp(rad * 0.62f, 13f, 44f));
            var box = new Rect(sp.x - rad * 1.5f, y - rad * 0.6f, rad * 3f, rad * 1.2f);
            sWorld.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(box.x + 1.5f, box.y + 1.5f, box.width, box.height), a.El.Sym, sWorld);
            sWorld.normal.textColor = Color.white;
            GUI.Label(box, a.El.Sym, sWorld);
            sWorld.fontSize = keep;

            int used = a.UsedBonds;
            int free = a.FreeValence;
            // 21.09, владелец: «3 или 4 коннекта?» у железа. Зелёные точки считались только по
            // обычной валентности, а связь с кислородом разрешалась по высшей — и точек было
            // то три, то четыре без объяснения. Теперь места, которые открываются только с
            // кислородом/фтором/хлором, нарисованы ПУСТЫМИ кружками: видно, что они есть, но не
            // для всякого соседа.
            int extra = Mathf.Max(0, a.El.MaxBonds - Mathf.Max(a.El.Valence, used));
            if (Lab.GodMode) extra = 0;
            int n = used + free + extra;
            if (n <= 0) continue;
            float dot = Mathf.Clamp(rad * 0.2f, 5f, 14f);
            float ring = rad * 0.8f;
            for (int k = 0; k < n; k++)
            {
                float ang = (90f - 360f * k / n) * Mathf.Deg2Rad;      // первая точка сверху, дальше по часовой
                float dx = Mathf.Cos(ang) * ring, dy = -Mathf.Sin(ang) * ring;
                var r = new Rect(sp.x + dx - dot * 0.5f, y + dy - dot * 0.5f, dot, dot);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), DotTex);
                if (k < used + free)
                {
                    GUI.color = k < used ? new Color(1f, 0.25f, 0.2f) : new Color(0.25f, 1f, 0.35f);
                    GUI.DrawTexture(r, DotTex);
                }
                else
                {
                    // пустой кружок: светлый обод, тёмная середина
                    GUI.color = new Color(0.55f, 1f, 0.65f, 0.9f);
                    GUI.DrawTexture(r, DotTex);
                    GUI.color = new Color(0.05f, 0.08f, 0.1f, 1f);
                    GUI.DrawTexture(new Rect(r.x + r.width * 0.25f, r.y + r.height * 0.25f, r.width * 0.5f, r.height * 0.5f), DotTex);
                }
            }
            GUI.color = Color.white;
        }

        foreach (var m in Lab.I.Mols)
        {
            if (m.Atoms.Count < 2) continue;
            Vector3 sp = cam.WorldToScreenPoint(m.Center + Vector3.up * 0.9f); sp.x /= U; sp.y /= U;
            if (sp.z <= 0f) continue;
            float y = SH - sp.y;
            sWorld.richText = true;
            string text = Lang.Sub(m.Formula, 10);
            if (m.Info != null) text += "  —  " + Lang.Name(m.Info);
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

    Rect updRect;

    // ==================== нагрев и заморозка (21.09, владелец) ====================

    Rect thermoRect;

    // ==================== карта притяжения (21.09, владелец) ====================
    // «Создай карту гравитационного притяжения: глубиной воронки показан вес, и при движении
    // частиц воронки движутся». Сетка — как натянутая ткань: каждый атом продавливает её тем
    // глубже, чем он тяжелее. Карта пересчитывается каждый кадр, поэтому воронки ездят за атомами.

    Rect gravRect;
    bool gravOpen = !Application.isMobilePlatform;
    Texture2D mapTex;
    Color32[] mapBuf;

    void DrawGravity()
    {
        gravRect = new Rect();
        var g = Gravity.I;
        if (g == null || accelOn || builderOn || quarkOn || physOn) return;
        float x = PanelRightGui + 10f, w = 260f;
        float y = thermoRect.width > 0f ? thermoRect.yMax + 8f : 80f;
        // Высота карты — ровно под сетку: косой вид занимает 0.9·глубину зоны, плюс провал воронок.
        Vector2 mh = Gravity.MapHalf;
        float mapS = (w - 12f - 14f) / (2f * mh.x + 0.7f * mh.y);
        float idealMap = 0.9f * mh.y * mapS + 64f + 14f;
        // Низ панели не заходит на кнопки зоны и карточку формулы (владелец, 21.09: «поправь
        // налезание»). Кнопки стоят над карточкой, их верх — SH - cardHeight - 54, над ними ещё
        // строка «Выделено…». Не влезает под «Температурой» — ставим карту справа от неё.
        float bottomLimit = SH - cardHeight - 54f - 24f;
        if (gravOpen && thermoRect.width > 0f && y + 34f + idealMap + 42f > bottomLimit)
        {
            x = thermoRect.xMax + 8f;
            y = thermoRect.y;
        }
        float mapH = gravOpen ? Mathf.Clamp(bottomLimit - y - 34f - 42f, 60f, idealMap) : 0f;
        float h = gravOpen ? 34f + mapH + 42f : 30f;
        var r = new Rect(x, y, w, h);
        gravRect = r;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.75f, 0.5f, 1f);
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(new Rect(r.x + 6f, r.y + 3f, w - 12f, 24f), Lang.T("Карта притяжения ", "Gravity map ") + (gravOpen ? "▾" : "▸"), sTab))
            gravOpen = !gravOpen;
        if (!gravOpen) return;

        var mr = new Rect(r.x + 6f, r.y + 32f, w - 12f, mapH);
        GUI.color = new Color(0.05f, 0.04f, 0.12f, 0.85f);
        GUI.DrawTexture(mr, Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (Event.current.type == EventType.Repaint) DrawFunnels(mr);

        float yy = mr.yMax + 4f;
        GUI.Label(new Rect(r.x + 8f, yy, w - 16f, 36f),
            Lab.Mode >= Lab.Level.Uni ? Lang.T("Глубина = масса. В жизни тяготение атомов в 10³⁶ раз слабее их зарядов.", "Depth = mass. Real atomic gravity is 10³⁶ times weaker than charge.")
                                      : Lang.T("Молекула — одна точка с массой всех её атомов. Тяжелее — глубже воронка.", "A molecule is one point with the mass of all its atoms. Heavier — deeper funnel."), sSmall);
    }

    /// <summary>Сетка-«ткань», продавленная атомами. Косой вид сверху: даль уходит вверх,
    /// глубина — вниз. Рисуем линиями GL прямо в пикселях экрана.</summary>
    void DrawFunnels(Rect mr)
    {
        Vector2 half = Gravity.MapHalf;
        const int NU = 28;
        int NV = Mathf.Max(8, Mathf.RoundToInt(NU * half.y / half.x));
        float depthPx = Mathf.Min(64f, mr.height * 0.45f);
        float S = Mathf.Min((mr.width - 14f) / (2f * half.x + 0.7f * half.y), (mr.height - depthPx - 12f) / (0.9f * half.y));
        float cx = mr.center.x;
        float cy = mr.y + 6f + half.y * S * 0.45f;

        var dep = new float[NU + 1, NV + 1];
        var pts = new Vector2[NU + 1, NV + 1];
        for (int i = 0; i <= NU; i++)
            for (int j = 0; j <= NV; j++)
            {
                float u = -half.x + 2f * half.x * i / NU, v = -half.y + 2f * half.y * j / NV;
                float d = Gravity.Depth(u, v);
                dep[i, j] = d;
                pts[i, j] = new Vector2(cx + u * S + v * S * 0.35f, cy - v * S * 0.45f + d * depthPx);
            }

        // Сетку рисуем в свою текстуру (линии по пикселям), а текстуру — обычным GUI: так нет
        // вопросов, какой шейдер попал в сборку и куда у GL смотрит ось y на телефоне.
        int tw = Mathf.Max(8, Mathf.RoundToInt(mr.width)), th = Mathf.Max(8, Mathf.RoundToInt(mr.height));
        if (mapTex == null || mapTex.width != tw || mapTex.height != th)
        {
            if (mapTex != null) Destroy(mapTex);
            mapTex = new Texture2D(tw, th, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            mapBuf = new Color32[tw * th];
        }
        System.Array.Clear(mapBuf, 0, mapBuf.Length);
        for (int j = 0; j <= NV; j++)
            for (int i = 0; i < NU; i++) Seg(pts[i, j] - mr.position, pts[i + 1, j] - mr.position, (dep[i, j] + dep[i + 1, j]) * 0.5f, tw, th);
        for (int i = 0; i <= NU; i++)
            for (int j = 0; j < NV; j++) Seg(pts[i, j] - mr.position, pts[i, j + 1] - mr.position, (dep[i, j] + dep[i, j + 1]) * 0.5f, tw, th);
        mapTex.SetPixels32(mapBuf);
        mapTex.Apply(false);
        GUI.DrawTexture(mr, mapTex);

        // Молекула — одна точка на дне своей воронки (сумма масс), атом-одиночка — своя.
        foreach (var pt in Gravity.Points())
        {
            Vector2 p = pt.Pos;
            if (Mathf.Abs(p.x) > half.x + 0.2f || Mathf.Abs(p.y) > half.y + 0.2f) continue;
            float d = Gravity.Depth(p.x, p.y);
            var sp = new Vector2(cx + p.x * S + p.y * S * 0.35f, cy - p.y * S * 0.45f + d * depthPx);
            float size = 4f + Mathf.Pow(Mathf.Max(1f, pt.Mass), 1f / 3f) * 1.3f;
            GUI.color = pt.Color;
            GUI.DrawTexture(new Rect(sp.x - size * 0.5f, sp.y - size * 0.5f, size, size), DotTex);
        }
        GUI.color = Color.white;
    }

    void Seg(Vector2 a, Vector2 b, float depth, int tw, int th)
    {
        Color32 c = Color.Lerp(new Color(0.35f, 0.6f, 1f, 0.6f), new Color(1f, 0.45f, 0.85f, 1f), Mathf.Clamp01(depth * 1.3f));
        int n = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x - a.x), Mathf.Abs(b.y - a.y))));
        for (int k = 0; k <= n; k++)
        {
            float t = (float)k / n;
            int x = Mathf.RoundToInt(Mathf.Lerp(a.x, b.x, t));
            int y = Mathf.RoundToInt(Mathf.Lerp(a.y, b.y, t));
            if (x < 0 || x >= tw || y < 0 || y >= th) continue;
            mapBuf[(th - 1 - y) * tw + x] = c;        // у текстуры строка 0 внизу, у GUI y сверху
        }
    }
    bool thermoOpen = !Application.isMobilePlatform;   // на телефоне свёрнута: место дорого

    /// <summary>Синий оттенок охлаждённой зоны — поверх мира, под интерфейсом.</summary>
    void DrawColdTint()
    {
        var t = Thermo.I;
        if (t == null || !t.ZoneCold || accelOn || builderOn || quarkOn || physOn) return;
        GUI.color = new Color(0.35f, 0.6f, 1f, 0.16f);
        GUI.DrawTexture(new Rect(PanelRightGui, 0f, SW - PanelRightGui, SH), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    /// <summary>Левая панель у края зоны: нагрев, холод, радиус, сила, «охладить всю зону».
    /// Плюс круг инструмента под курсором, чтобы было видно, что попадёт под действие.</summary>
    void DrawThermo()
    {
        thermoRect = new Rect();
        var t = Thermo.I;
        if (t == null || accelOn || builderOn || quarkOn || physOn) return;
        float x = PanelRightGui + 10f, y = 80f, w = 200f;
        float h = thermoOpen ? 250f : 30f;
        var r = new Rect(x, y, w, h);
        thermoRect = r;
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = t.ZoneCold ? new Color(0.45f, 0.75f, 1f) : new Color(1f, 0.6f, 0.3f);
        GUI.DrawTexture(new Rect(r.x, r.y, 3f, r.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(new Rect(r.x + 6f, r.y + 3f, w - 12f, 24f), Lang.T("Температура ", "Temperature ") + (thermoOpen ? "▾" : "▸"), sTab))
            thermoOpen = !thermoOpen;
        if (!thermoOpen) { t.Current = Thermo.Tool.None; return; }

        float bw = (w - 20f) / 3f, yy = r.y + 32f;
        GUI.color = t.Current == Thermo.Tool.Heat ? new Color(1f, 0.55f, 0.25f) : Color.white;
        if (GUI.Button(new Rect(r.x + 6f, yy, bw, 26f), Lang.T("Нагрев", "Heat"), sTab)) t.Current = t.Current == Thermo.Tool.Heat ? Thermo.Tool.None : Thermo.Tool.Heat;
        GUI.color = t.Current == Thermo.Tool.Cool ? new Color(0.45f, 0.75f, 1f) : Color.white;
        if (GUI.Button(new Rect(r.x + 10f + bw, yy, bw, 26f), Lang.T("Холод", "Cold"), sTab)) t.Current = t.Current == Thermo.Tool.Cool ? Thermo.Tool.None : Thermo.Tool.Cool;
        GUI.color = t.Current == Thermo.Tool.None ? new Color(0.8f, 1f, 0.8f) : Color.white;
        if (GUI.Button(new Rect(r.x + 14f + 2f * bw, yy, bw, 26f), Lang.T("Рука", "Hand"), sTab)) t.Current = Thermo.Tool.None;
        GUI.color = Color.white;
        yy += 32f;

        GUI.Label(new Rect(r.x + 8f, yy, w - 16f, 18f), Lang.T("Радиус: ", "Radius: ") + t.Radius.ToString("0.0"), sSmall);
        t.Radius = GUI.HorizontalSlider(new Rect(r.x + 8f, yy + 20f, w - 16f, 16f), t.Radius, 0.4f, 4f);
        yy += 40f;
        GUI.Label(new Rect(r.x + 8f, yy, w - 16f, 18f), Lang.T("Сила: ", "Strength: ") + Mathf.RoundToInt(t.Strength * 100f) + "%", sSmall);
        t.Strength = GUI.HorizontalSlider(new Rect(r.x + 8f, yy + 20f, w - 16f, 16f), t.Strength, 0.05f, 1f);
        yy += 42f;

        bool cold = GUI.Toggle(new Rect(r.x + 8f, yy, w - 16f, 22f), t.ZoneCold, Lang.T("  Охладить всю зону", "  Cool the whole zone"));
        if (cold != t.ZoneCold)
        {
            t.ZoneCold = cold;
            Lab.I.Say(cold ? Lang.T("Зона охлаждена: атомы везде замедляются и замирают.", "Zone cooled: atoms slow down and freeze everywhere.")
                           : Lang.T("Охлаждение зоны выключено.", "Zone cooling off."), new Color(0.6f, 0.85f, 1f));
        }
        yy += 26f;
        GUI.Label(new Rect(r.x + 8f, yy, w - 16f, 44f), t.Hint(), sSmall);

        // круг инструмента под курсором
        if (t.Current != Thermo.Tool.None && !PointerOverUI && Lab.I != null && Lab.I.Cam != null)
        {
            var cam = Lab.I.Cam;
            Vector3 c0 = cam.WorldToScreenPoint(t.Point);
            Vector3 c1 = cam.WorldToScreenPoint(t.Point + cam.transform.right * t.Radius);
            if (c0.z > 0f)
            {
                float rad = ((Vector2)c1 - (Vector2)c0).magnitude / U;
                var cr = new Rect(c0.x / U - rad, SH - c0.y / U - rad, rad * 2f, rad * 2f);
                GUI.color = t.Current == Thermo.Tool.Heat ? new Color(1f, 0.45f, 0.15f, t.Applying ? 0.28f : 0.14f)
                                                          : new Color(0.4f, 0.7f, 1f, t.Applying ? 0.28f : 0.14f);
                GUI.DrawTexture(cr, DotTex);
                GUI.color = Color.white;
            }
        }
    }

    /// <summary>Плашка «вышла новая версия» под счётом (21.09, владелец: «пусть игра говорит
    /// об обновлении и качает сама»).</summary>
    void DrawUpdate()
    {
        var u = Updater.I;
        updRect = new Rect();
        if (u == null || u.Latest == null || u.Dismissed) return;
        var r = new Rect(SW - 360f, 76f, 350f, u.Status.Length > 0 ? 104f : 74f);
        updRect = r;
        GUI.color = new Color(0.05f, 0.2f, 0.1f, 0.92f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 1f, 0.55f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 22f),
            Lang.T("Вышла версия ", "Version ") + u.Latest + Lang.T(" (у тебя ", " is out (you have ") + Updater.Version + ")", sTitle);
        if (u.Progress >= 0f && u.Progress < 1f)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.2f);
            GUI.DrawTexture(new Rect(r.x + 10f, r.y + 40f, r.width - 20f, 16f), Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 1f, 0.55f);
            GUI.DrawTexture(new Rect(r.x + 10f, r.y + 40f, (r.width - 20f) * u.Progress, 16f), Texture2D.whiteTexture);
            GUI.color = Color.white;
        }
        else
        {
            if (GUI.Button(new Rect(r.x + 10f, r.y + 38f, 160f, 26f), Lang.T("Скачать", "Download"), sTab)) u.Download();
            if (GUI.Button(new Rect(r.x + 180f, r.y + 38f, 100f, 26f), Lang.T("Позже", "Later"), sTab)) u.Dismissed = true;
        }
        if (u.Status.Length > 0) GUI.Label(new Rect(r.x + 10f, r.y + 66f, r.width - 20f, 36f), u.Status, sSmall);
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
            // Над кнопками зоны, а те — над карточкой формулы: раньше сообщение лежало поверх
            // карточки и кнопок (фото владельца 13:08, кадр 21.09). В режимах с пультом внизу
            // кнопок зоны нет — там остаётся прежнее место.
            bool zoneBar = !(accelOn || builderOn || quarkOn || physOn);
            float ty = zoneBar ? SH - cardHeight - 54f - 26f - 64f : SH - 96f;
            var tr = new Rect(panelX + W + 40f, Mathf.Max(80f, ty), SW - (panelX + W) - 80f, 64f);
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
        if (accelOn || builderOn || quarkOn || physOn) return;    // внизу стоит пульт режима, карточке там не место
        Lab.Mol best = null;
        foreach (var m in lab.Mols)
            if (m.Atoms.Count > 1 && (best == null || m.Atoms.Count > best.Atoms.Count)) best = m;
        if (best == null) return;

        float x = PanelRightGui + 20f;
        float w = Mathf.Min(620f, SW - x - 20f);   // 21.09, владелец: «расширь окно формулы» (было 430)
        if (w < 160f) return;
        bool hasUse = best.Info != null && !string.IsNullOrEmpty(best.Info.Use);
        // 21.09, владелец: «добавь под формулой размер молекулы. и напиши что не держится».
        string unstable = MolFacts.Instability(best);
        // Расшифровка длинной формулы не влезала в строку и обрезалась («… никель · 7»):
        // даём ей переноситься и растим карточку на лишние строки.
        string decode = Lang.Decode(best.Formula);
        float decW = Mathf.Min(620f, SW - (PanelRightGui + 20f) - 20f) - 24f;
        string advice = MolFacts.Advice(best);
        // ВУЗник: полярность связей; Эйнштейн: ещё и энергия связей. Одна строка на каждое.
        string pol = MolFacts.Polarity(best), ener = MolFacts.BondEnergy(best);
        string sci = pol == null ? ener : (ener == null ? pol : pol + "\n" + ener);
        float sciH = sci == null ? 0f : Mathf.Clamp(sSmall.CalcHeight(new GUIContent(sci), Mathf.Max(60f, decW)), 18f, 72f);
        float advH = advice == null ? 0f : Mathf.Clamp(sSmall.CalcHeight(new GUIContent(advice), Mathf.Max(60f, decW)), 18f, 70f) + 4f;
        float unsH = unstable == null ? 0f : Mathf.Clamp(sSmall.CalcHeight(new GUIContent(unstable), Mathf.Max(60f, decW)), 18f, 70f) + 4f;
        // 2.6, владелец: у незнакомой молекулы — не только «не держится», но и её свойства.
        string props = best.Info == null ? MolFacts.Properties(best) : null;
        float propH = props == null ? 0f : Mathf.Clamp(sSmall.CalcHeight(new GUIContent(props), Mathf.Max(60f, decW)), 18f, 90f) + 4f;
        float decH = Mathf.Clamp(sSmall.CalcHeight(new GUIContent(decode), Mathf.Max(60f, decW)), 18f, 54f);
        float dy = decH - 18f;                                                       // сколько добавили переносы
        float h = (best.Info != null ? (hasUse ? 128f : 104f) : 78f) + 16f + 18f + dy + sciH   // +16 расшифровка, +18 размер
                  + unsH + advH + propH;
        cardHeight = h;
        var r = new Rect(x, SH - h - 20f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = best.Info != null ? new Color(0.4f, 0.9f, 0.5f, 0.9f) : new Color(0.5f, 0.6f, 0.8f, 0.7f);
        GUI.DrawTexture(new Rect(r.x, r.y, 4f, r.height), Texture2D.whiteTexture);   // цветная полоска слева
        GUI.color = Color.white;

        sBig.normal.textColor = best.Info != null ? new Color(0.65f, 1f, 0.7f) : Color.white;
        sBig.richText = true;
        GUI.Label(new Rect(r.x + 14f, r.y + 6f, r.width - 24f, 34f), Lang.Sub(best.Formula, 18), sBig);
        var keepDec = sSmall.normal.textColor;
        sSmall.normal.textColor = new Color(0.8f, 0.85f, 0.95f);
        GUI.Label(new Rect(r.x + 14f, r.y + 36f, r.width - 24f, decH), decode, sSmall);
        sSmall.normal.textColor = new Color(0.6f, 0.9f, 1f);
        GUI.Label(new Rect(r.x + 14f, r.y + 54f + dy, r.width - 24f, 18f), MolFacts.SizeLine(best), sSmall);
        if (sci != null)
        {
            sSmall.normal.textColor = Lab.Mode == Lab.Level.Einstein ? new Color(1f, 0.85f, 0.35f) : new Color(0.6f, 0.85f, 1f);
            GUI.Label(new Rect(r.x + 14f, r.y + 72f + dy, r.width - 24f, sciH), sci, sSmall);
        }
        sSmall.normal.textColor = keepDec;
        float oy = 18f + dy + sciH;   // всё ниже сдвинуто на строку размера, переносы и научные строки

        if (best.Info != null)
        {
            GUI.Label(new Rect(r.x + 14f, r.y + 54f + oy, r.width - 24f, 22f), Lang.Name(best.Info), sTitle);
            GUI.Label(new Rect(r.x + 14f, r.y + 76f + oy, r.width - 24f, 38f), Lang.Note(best.Info), sSmall);
            if (hasUse)
            {
                // 🔴 21.09, владелец: «добавь сюда область применения». Отдельной строкой и
                // другим цветом — это не рассказ о веществе, а ответ «где я его встречу».
                var keep = sSmall.normal.textColor;
                sSmall.normal.textColor = new Color(1f, 0.85f, 0.45f);
                GUI.Label(new Rect(r.x + 14f, r.y + 116f + oy, r.width - 24f, 24f),
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
                int c; counts.TryGetValue(at.El.ChemSym, out c);
                counts[at.El.ChemSym] = c + 1;
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
            GUI.Label(new Rect(r.x + 14f, r.y + 54f + oy, r.width - 24f, 34f),
                text + "  " + Lang.T("Атомов: ", "Atoms: ") + best.Atoms.Count, sSmall);
            if (props != null)
            {
                var keepP = sSmall.normal.textColor;
                sSmall.normal.textColor = new Color(0.7f, 0.95f, 0.8f);
                GUI.Label(new Rect(r.x + 14f, r.y + 88f + oy, r.width - 24f, propH), props, sSmall);
                sSmall.normal.textColor = keepP;
            }
            if (unstable != null)
            {
                var keepU = sSmall.normal.textColor;
                sSmall.normal.textColor = new Color(1f, 0.45f, 0.4f);
                GUI.Label(new Rect(r.x + 14f, r.y + 88f + oy + propH, r.width - 24f, unsH), unstable, sSmall);
                sSmall.normal.textColor = keepU;
            }
            if (advice != null)
            {
                var keepA = sSmall.normal.textColor;
                sSmall.normal.textColor = new Color(1f, 0.85f, 0.45f);
                GUI.Label(new Rect(r.x + 14f, r.y + 88f + oy + propH + unsH, r.width - 24f, advH), advice, sSmall);
                sSmall.normal.textColor = keepA;
            }
        }
    }


    /// <summary>Кнопки зоны — слева внизу, НАД карточкой формулы (🔴 21.09, просьба
    /// владельца). Держатся над карточкой, а не на месте: карточка растёт, когда вещество
    /// узнано, и кнопки уезжали бы под неё.</summary>
    void DrawZoneButtons()
    {
        var lab = Lab.I;
        if (lab == null) return;

        if (accelOn || builderOn || quarkOn || physOn) return;   // внизу стоит пульт того режима, что включён
        float x = PanelRightGui + 20f;
        if (SW - x < 200f) return;
        float y = SH - cardHeight - 20f - 34f;

        if (GUI.Button(new Rect(x, y, 140f, 28f), Lang.T("Убрать атомы", "Clear atoms"), sTab)) lab.ClearZone();

        GUI.color = new Color(0.75f, 1f, 0.8f);
        if (GUI.Button(new Rect(x + 148f, y, 180f, 28f), Lang.T("Посмотреть реакцию", "Run reaction"), sTab)) Chemistry.React();
        GUI.color = new Color(1f, 0.8f, 0.8f);
        if (GUI.Button(new Rect(x + 336f, y, 140f, 28f), Lang.T("Выход из игры", "Quit game"), sTab)) lab.ExitGame();
        GUI.color = Color.white;

        // На телефоне клавиш нет — подсказка про Ctrl только мешала карточке (фото 13:08).
        if ((lab.Selected.Count > 0 || lab.ClipboardCount > 0) && !Application.isMobilePlatform)
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

    public void CloseMenu() { menuAtom = null; menuBondList = false; menuLinkList = false; }

    bool menuLinkList;

    /// <summary>В какой молекуле атом — чтобы в списке было видно «H (H2O)», а не просто «H».</summary>
    string MolTag(Atom a)
    {
        if (Lab.I == null) return "";
        foreach (var m in Lab.I.Mols) if (m.Atoms.Count > 1 && m.Atoms.Contains(a)) return "  (" + m.Formula + ")";
        return "";
    }

    /// <summary>Кого можно предложить в «образовать связь с...»: до восьми ближайших атомов,
    /// с которыми этот ещё не связан.</summary>
    List<Atom> LinkCandidates()
    {
        var res = new List<Atom>();
        if (menuAtom == null) return res;
        foreach (var a in Atom.All)
            if (a != null && a != menuAtom && menuAtom.BondWith(a) == null) res.Add(a);
        var p = menuAtom.transform.position;
        res.Sort((x, z) => (x.transform.position - p).sqrMagnitude.CompareTo((z.transform.position - p).sqrMagnitude));
        if (res.Count > 8) res.RemoveRange(8, res.Count - 8);
        return res;
    }

    Rect MenuRect
    {
        get
        {
            if (menuAtom == null) return new Rect();
            int rows = 8 + (menuBondList ? menuAtom.Bonds.Count : 0) + (menuLinkList ? LinkCandidates().Count : 0);
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

        // 21.09, владелец: «добавь "образовать связь с..."». Ближайшие атомы зоны — по
        // расстоянию. Кто не может связаться с этим атомом, показан серым с причиной.
        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f),
            (menuLinkList ? "- " : "+ ") + Lang.T("Образовать связь с...", "Bond with..."), sTab)) { menuLinkList = !menuLinkList; menuBondList = false; }
        y += 24f;
        if (menuLinkList)
        {
            var cands = LinkCandidates();
            if (cands.Count == 0) { GUI.Label(new Rect(r.x + 18f, y, r.width - 24f, 22f), Lang.T("рядом нет свободных атомов", "no free atoms nearby"), sNote); y += 24f; }
            foreach (var other in cands)
            {
                bool can = menuAtom.FreeBondsWith(other) > 0 && other.FreeBondsWith(menuAtom) > 0;
                GUI.color = can ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                string label = Lang.T("с ", "with ") + other.El.Sym + MolTag(other) +
                               (can ? "" : Lang.T("  — занят", "  — full"));
                if (GUI.Button(new Rect(r.x + 18f, y, r.width - 24f, 22f), label, sTab) && can)
                {
                    string why = Lab.I.BondByHand(menuAtom, other);
                    Lab.I.Say(why ?? (Lang.T("Связь ", "Bond ") + menuAtom.El.Sym + "-" + other.El.Sym + Lang.T(" образована.", " made.")),
                              why == null ? new Color(0.7f, 1f, 0.75f) : new Color(1f, 0.85f, 0.6f));
                    menuLinkList = false;
                }
                GUI.color = Color.white;
                y += 24f;
            }
        }

        if (menuAtom.Bonds.Count > 0)
        {
            if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f),
                (menuBondList ? "- " : "+ ") + Lang.T("Убрать связь с...", "Remove bond with..."), sTab)) { menuBondList = !menuBondList; menuLinkList = false; }
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
                        Lab.I.BreakByHand(b);
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
                for (int i = menuAtom.Bonds.Count - 1; i >= 0; i--) Lab.I.BreakByHand(menuAtom.Bonds[i]);
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

        if (GUI.Button(new Rect(r.x + 6f, y, r.width - 12f, 22f), Lang.T("Посмотреть внутреннее устройство", "Look inside the atom"), sTab))
        {
            insideEl = menuAtom.El;
            insideT0 = Time.time;
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

    // ==================== внутреннее устройство атома (21.09, владелец, 2.5.6) ====================
    // «Добавь в контекстное меню "посмотреть внутреннее устройство": показать протоны, ядро,
    // нейтроны, электроны». Модель Бора: ядро из протонов и нейтронов в центре, вокруг оболочки,
    // по ним бегут электроны. Число электронов на оболочках — по порядку заполнения (Маделунг).

    Elements.El insideEl;
    float insideT0;
    public void ShowInside(Elements.El el) { insideEl = el; insideT0 = Time.time; }

    /// <summary>Настоящие оболочки нейтральных атомов, у которых электроны стоят НЕ по общему
    /// правилу (хром, медь, серебро, золото, платина, уран…). Самопроверка поймала: по правилу
    /// у урана выходило 2·8·18·32·22·8·2, а на деле 2·8·18·32·21·9·2.</summary>
    static readonly Dictionary<int, int[]> ShellExceptions = new Dictionary<int, int[]>
    {
        { 24, new[] { 2, 8, 13, 1 } },            { 29, new[] { 2, 8, 18, 1 } },
        { 41, new[] { 2, 8, 18, 12, 1 } },        { 42, new[] { 2, 8, 18, 13, 1 } },
        { 44, new[] { 2, 8, 18, 15, 1 } },        { 45, new[] { 2, 8, 18, 16, 1 } },
        { 46, new[] { 2, 8, 18, 18 } },           { 47, new[] { 2, 8, 18, 18, 1 } },
        { 57, new[] { 2, 8, 18, 18, 9, 2 } },     { 58, new[] { 2, 8, 18, 19, 9, 2 } },
        { 64, new[] { 2, 8, 18, 25, 9, 2 } },     { 78, new[] { 2, 8, 18, 32, 17, 1 } },
        { 79, new[] { 2, 8, 18, 32, 18, 1 } },    { 89, new[] { 2, 8, 18, 32, 18, 9, 2 } },
        { 90, new[] { 2, 8, 18, 32, 18, 10, 2 } },{ 91, new[] { 2, 8, 18, 32, 20, 9, 2 } },
        { 92, new[] { 2, 8, 18, 32, 21, 9, 2 } }, { 93, new[] { 2, 8, 18, 32, 22, 9, 2 } },
        { 96, new[] { 2, 8, 18, 32, 25, 9, 2 } }, { 103, new[] { 2, 8, 18, 32, 32, 8, 3 } },
    };

    /// <summary>Электроны по оболочкам K, L, M… Подуровни заполняются в порядке Маделунга
    /// (1s 2s 2p 3s 3p 4s 3d …), потом складываются по номеру оболочки. Для нейтральных атомов
    /// с известными исключениями берём настоящую раскладку из таблицы выше.</summary>
    public static int[] Shells(int electrons, int z = 0)
    {
        int[] known;
        if (z > 0 && electrons == z && ShellExceptions.TryGetValue(z, out known)) return known;
        int[,] order = { {1,0},{2,0},{2,1},{3,0},{3,1},{4,0},{3,2},{4,1},{5,0},{4,2},{5,1},{6,0},{4,3},{5,2},{6,1},{7,0},{5,3},{6,2},{7,1} };
        var sh = new int[7];
        int e = Mathf.Max(0, electrons);
        for (int i = 0; i < order.GetLength(0) && e > 0; i++)
        {
            int take = Mathf.Min(2 * (2 * order[i, 1] + 1), e);
            sh[order[i, 0] - 1] += take;
            e -= take;
        }
        int n = 7; while (n > 0 && sh[n - 1] == 0) n--;
        var res = new int[n];
        System.Array.Copy(sh, res, n);
        return res;
    }

    void DrawInside()
    {
        var el = insideEl;
        if (el == null) return;
        int z = Mathf.Max(1, el.Z);
        int nN = Mathf.Max(0, Mathf.RoundToInt(el.Mass) - z);
        int eCount = Mathf.Max(0, z - el.Charge);
        var shells = Shells(eCount, z);

        float w = Mathf.Min(600f, SW - 20f), h = Mathf.Min(560f, SH - 20f);
        var r = new Rect((SW - w) * 0.5f, (SH - h) * 0.5f, w, h);
        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(0f, 0f, SW, SH), Texture2D.whiteTexture);   // затемняем всё вокруг
        GUI.color = new Color(0.05f, 0.06f, 0.1f, 0.98f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.4f, 0.7f, 1f, 0.8f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 14f, r.y + 8f, w - 130f, 26f), Lang.T("Внутри: ", "Inside: ") + Lang.Name(el) + " (" + el.Sym + ")", sTitle);
        if (GUI.Button(new Rect(r.xMax - 110f, r.y + 8f, 100f, 26f), Lang.T("Закрыть", "Close"), sTab)) { insideEl = null; return; }

        string shellText = "";
        for (int i = 0; i < shells.Length; i++) shellText += (i > 0 ? " · " : "") + shells[i];
        GUI.Label(new Rect(r.x + 14f, r.y + 38f, w - 28f, 20f),
            "<color=#ff6b5a>" + z + Lang.T(" протонов", " protons") + "</color>  ·  <color=#b8b8c0>" + nN + Lang.T(" нейтронов", " neutrons") +
            "</color>  ·  <color=#6fd0ff>" + eCount + Lang.T(" электронов", " electrons") + "</color>" +
            (el.Charge != 0 ? Lang.T("  (ион ", "  (ion ") + (el.Charge > 0 ? "+" : "") + el.Charge + ")" : ""), sSmall);
        GUI.Label(new Rect(r.x + 14f, r.y + 58f, w - 28f, 20f), Lang.T("Оболочки (от ядра наружу): ", "Shells (from the nucleus out): ") + shellText, sSmall);

        // ---- рисунок: центр и масштаб под размер окна ----
        float areaTop = r.y + 82f, areaBottom = r.yMax - 48f;
        Vector2 c = new Vector2(r.center.x, (areaTop + areaBottom) * 0.5f);
        float maxR = Mathf.Min(w * 0.5f - 16f, (areaBottom - areaTop) * 0.5f);

        // Ядро: протоны и нейтроны вперемешку, спиралью подсолнуха. Больше 120 не рисуем.
        int total = z + nN, shown = Mathf.Min(120, total);
        int showP = Mathf.Max(1, Mathf.RoundToInt(shown * (float)z / total)), showN = shown - showP;
        float nucR = Mathf.Clamp(maxR * 0.28f, 18f, 70f);
        float step = nucR / Mathf.Sqrt(Mathf.Max(1, shown));
        float ball = Mathf.Max(5f, step * 1.9f);
        var rnd = new System.Random(z * 1000 + nN);
        int leftP = showP, leftN = showN;
        for (int i = 0; i < shown; i++)
        {
            float rr = step * Mathf.Sqrt(i + 0.5f), ang = i * 2.39996f;
            bool proton = leftN == 0 || (leftP > 0 && rnd.NextDouble() < (double)leftP / (leftP + leftN));
            if (proton) leftP--; else leftN--;
            var p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr;
            GUI.color = proton ? new Color(0.92f, 0.3f, 0.25f) : new Color(0.7f, 0.7f, 0.75f);
            GUI.DrawTexture(new Rect(p.x - ball * 0.5f, p.y - ball * 0.5f, ball, ball), DotTex);
        }

        // Оболочки и электроны. Внутренние бегут быстрее — как и в модели Бора.
        float t = Time.time - insideT0;
        float gap = (maxR - nucR - 14f) / Mathf.Max(1, shells.Length);
        for (int k = 0; k < shells.Length; k++)
        {
            float rad = nucR + 14f + gap * (k + 1) - gap * 0.3f;
            GUI.color = new Color(0.45f, 0.7f, 1f, 0.28f);
            int dots = Mathf.Clamp(Mathf.RoundToInt(rad * 0.9f), 40, 160);
            for (int d = 0; d < dots; d++)
            {
                float a = d * Mathf.PI * 2f / dots;
                GUI.DrawTexture(new Rect(c.x + Mathf.Cos(a) * rad - 1f, c.y + Mathf.Sin(a) * rad - 1f, 2f, 2f), Texture2D.whiteTexture);
            }
            GUI.color = new Color(0.45f, 0.85f, 1f);
            float spin = t * 1.4f / (k + 1);
            for (int j = 0; j < shells[k]; j++)
            {
                float a = spin + j * Mathf.PI * 2f / shells[k];
                var p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rad;
                GUI.DrawTexture(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), DotTex);
            }
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(c.x - 60f, c.y + nucR + 2f, 120f, 18f), Lang.T("ядро", "nucleus"), sNote);

        string foot = total > shown
            ? Lang.T("В ядре показано " + shown + " частиц из " + total + ", доля протонов сохранена. ", "The nucleus shows " + shown + " of " + total + " particles, proton share kept. ")
            : "";
        foot += Lab.Mode >= Lab.Level.Uni
            ? Lang.T("Модель Бора. На деле электроны — облака (орбитали), а ядро в ~100 000 раз меньше атома.", "Bohr model. Really electrons are clouds (orbitals), and the nucleus is ~100,000 times smaller than the atom.")
            : Lang.T("Так атом рисуют в школе. На самом деле ядро в 100 000 раз меньше атома — здесь оно увеличено.", "The school picture of an atom. The real nucleus is 100,000 times smaller — enlarged here.");
        GUI.Label(new Rect(r.x + 14f, r.yMax - 44f, w - 28f, 40f), foot, sSmall);
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
        if (accelOn && physOn) { physOn = false; if (PhysLab.I != null) PhysLab.I.Active = false; }
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
        if (builderOn && physOn) { physOn = false; if (PhysLab.I != null) PhysLab.I.Active = false; }
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
            if (physOn) { physOn = false; if (PhysLab.I != null) PhysLab.I.Active = false; }
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

    // ==================== верхний уровень: стол с вещами ====================

    bool physOn;
    Rect physRect;

    /// <summary>Курсор над пультом стола — тогда нажатие принадлежит кнопке, а не стакану
    /// за ней.</summary>
    public bool PointerOverPhys { get { return physOn && physRect.Contains(MouseGui); } }

    void TogglePhys()
    {
        var ph = PhysLab.I;
        if (ph == null || Lab.I == null) return;
        physOn = !physOn;
        ph.Active = physOn;
        if (physOn)
        {
            if (accelOn) { accelOn = false; if (Accelerator.I != null) Accelerator.I.Active = false; }
            if (builderOn) { builderOn = false; if (AtomBuilder.I != null) AtomBuilder.I.Active = false; }
            if (quarkOn) { quarkOn = false; if (QuarkLab.I != null) QuarkLab.I.Active = false; }
            Lab.I.LookAt(PhysLab.Rig + new Vector3(0f, -0.4f, 0f), 11f);
            if (ph.Items.Count == 0) { ph.PutById("beaker_water"); ph.PutById("na"); ph.PutById("phph"); }
            Lab.I.Say(Lang.T("Верхний уровень: вещи из вещества. Перетащи кусочек натрия в стакан воды. Капни фенолфталеин — увидишь, что вода стала щёлочью.",
                             "Top level: things made of substances. Drag the piece of sodium into the glass of water. Add phenolphthalein to see the water turn alkaline."),
                new Color(0.95f, 0.85f, 0.55f));
        }
        else
        {
            Lab.I.LookAt(Lab.ZoneCenter, 14f);
            Lab.I.Say(Lang.T("Назад в лабораторию.", "Back to the lab."), new Color(0.8f, 0.9f, 1f));
        }
    }

    /// <summary>Пульт стола: полка вещей, что выбрано, «в зону», «очистить».</summary>
    void DrawPhys()
    {
        if (!physOn) return;
        var ph = PhysLab.I;
        if (ph == null) return;
        float x = PanelRightGui + 20f;
        float w = Mathf.Min(720f, SW - x - 20f);
        if (w < 300f) return;
        const int cols = 4;
        int rows = (PhysLab.Shelf.Length + cols - 1) / cols;
        float bh = 24f, gap = 3f;
        float h = 60f + rows * (bh + gap) + 98f;
        var r = new Rect(x, SH - h - 20f, w, h);
        physRect = r;

        GUI.color = new Color(0f, 0f, 0f, 0.62f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.95f, 0.8f, 0.45f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 3f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 22f), Lang.T("ЛАБОРАТОРНЫЙ СТОЛ", "LAB BENCH"), sTitle);
        GUI.Label(new Rect(r.x + 12f, r.y + 28f, r.width - 24f, 30f),
            Lang.T("Нажми — вещь встанет на стол. Перетащи её в стакан, колбу или чашку. Посуду в посуду — перельётся. Количества условные.",
                   "Tap to put a thing on the bench. Drag it into a glass, flask or dish. A vessel onto a vessel pours it. Amounts are nominal."), sSmall);

        float bw = (r.width - 24f - gap * (cols - 1)) / cols;
        for (int i = 0; i < PhysLab.Shelf.Length; i++)
        {
            var d = PhysLab.Shelf[i];
            var br = new Rect(r.x + 12f + (i % cols) * (bw + gap), r.y + 60f + (i / cols) * (bh + gap), bw, bh);
            if (GUI.Button(br, Lang.T(d.Ru, d.En), sTab)) ph.Put(d);
        }

        float y = r.y + 60f + rows * (bh + gap) + 4f;
        GUI.Label(new Rect(r.x + 12f, y, r.width - 24f, 30f), ph.Describe(ph.Selected), sSmall);
        y += 30f;
        // Что случилось в последний раз — остаётся на пульте. Внизу экрана строка живёт шесть
        // секунд, и «почему не растворилось» легко пропустить.
        if (!string.IsNullOrEmpty(ph.LastText))
        {
            var keep = sSmall.normal.textColor;
            sSmall.normal.textColor = new Color(0.75f, 1f, 0.8f);
            GUI.Label(new Rect(r.x + 12f, y, r.width - 24f, 34f), ph.LastText, sSmall);
            sSmall.normal.textColor = keep;
        }
        y += 36f;
        if (GUI.Button(new Rect(r.x + 12f, y, 220f, 24f), Lang.T("Молекулу — в зону ↓", "Send molecule down ↓"), sTab))
        {
            string f = ph.SendDown(ph.Selected);
            Lab.I.Say(f != null ? Lang.T("В зону молекул ушло: ", "Sent to the molecule zone: ") + f + Lang.T(". Выйди со стола, чтобы увидеть.", ". Leave the bench to see it.")
                                : Lang.T("Сначала выбери вещь (нажми на неё на столе).", "Pick a thing first (tap it on the bench)."),
                      new Color(0.8f, 0.9f, 1f));
        }
        if (GUI.Button(new Rect(r.x + 240f, y, 160f, 24f), Lang.T("Очистить стол", "Clear the bench"), sTab)) ph.ClearTable();
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

    Vector2 pickerScroll;

    void DrawPicker()
    {
        bool forReplace = replaceTarget != null;
        bool forSlot = pendingSlot >= 0;
        if (!forReplace && !forSlot) return;

        float x = PanelRightGui + 6f;
        float w = 250f;
        if (x + w > SW - 10f) return;
        // 21.09, владелец: «надо полную таблицу». Было восемнадцать ходовых элементов — теперь
        // все клетки по порядку номеров, листаются колёсиком или пальцем.
        var all = new List<Elements.El>(Elements.All);
        const int pc = 6;
        float cellH = 26f;
        int prow = (all.Count + pc - 1) / pc;
        float listH = prow * (cellH + 4f);
        float top = Mathf.Max(8f, tableTop - 40f);
        float h = Mathf.Min(92f + listH, SH - top - 10f);
        var r = new Rect(x, top, w, h);

        GUI.color = new Color(0.07f, 0.1f, 0.16f, 0.97f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = forReplace ? new Color(1f, 0.8f, 0.4f, 0.9f) : new Color(0.4f, 0.8f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, 2f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        string title = forReplace
            ? Lang.T("Заменить ", "Replace ") + replaceTarget.El.Sym + Lang.T(" на:", " with:")
            : Lang.T("В гнездо ", "Into slot ") + (pendingSlot == 0 ? Lang.T("слева", "left") : Lang.T("справа", "right")) + ":";
        GUI.Label(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 22f), title, sTitle);
        GUI.Label(new Rect(r.x + 10f, r.y + 28f, r.width - 20f, 20f), Lang.T("все элементы по номеру — или клетка слева", "all elements by number — or a cell on the left"), sSmall);

        float cw = (r.width - 28f - (pc - 3) * 4f) / pc;
        var view = new Rect(r.x, r.y + 52f, r.width, r.height - 52f - 36f);
        pickerScroll = GUI.BeginScrollView(view, pickerScroll, new Rect(0f, 0f, r.width - 18f, listH));
        for (int i = 0; i < all.Count; i++)
        {
            var el = all[i];
            if (el == null) continue;
            var cell = new Rect(10f + (i % pc) * (cw + 4f), (i / pc) * (cellH + 4f), cw, cellH);
            GUI.color = el.PaintColor;
            GUI.DrawTexture(cell, Texture2D.whiteTexture);
            GUI.color = Color.white;
            float lum = el.PaintColor.r * 0.3f + el.PaintColor.g * 0.59f + el.PaintColor.b * 0.11f;
            sCell.normal.textColor = lum > 0.5f ? Color.black : Color.white;
            GUI.Label(cell, el.Sym, sCell);
            if (GUI.Button(cell, "", GUIStyle.none)) Choose(el);
        }
        GUI.EndScrollView();

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
