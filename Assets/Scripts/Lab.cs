using System.Collections.Generic;
using UnityEngine;

/// <summary>Мозг лаборатории: зона сборки, камера, склейка атомов, разбор того, что собралось,
/// счёт и журнал открытий.
///
/// Правила склейки (школьные, нарочно простые):
///   • у каждого атома есть запас связей (валентность); связь тратит по одной у обоих;
///   • благородные газы (валентность 0) не липнут ни к кому — и это правда про них;
///   • атомы слипаются, когда их поднесли вплотную;
///   • если сжать уже связанную пару ещё сильнее — связь станет двойной, потом тройной.</summary>
public class Lab : MonoBehaviour
{
    public static Lab I;

    /// <summary>🔴 21.09, владелец: тумблер вместо вкладок. В режиме бога валентность не
    /// считается совсем: склеивается всё со всем, включая благородные газы, которые по
    /// правилам не соединяются ни с кем. Это нарочная неправда — песочница, а не урок.</summary>
    public static bool GodMode;

    // ==================== уровни точности (21.09, владелец) ====================
    // «сделай в игре уровни: фан — немного точности; школяр — для школьного обучения;
    //  ВУЗник — серьёзная физика; эйнштейн — гений».
    //   Фан      — собирается почти всё (высшая валентность у всех), без предупреждений и советов;
    //   Школяр   — как было: школьная химия, предупреждения про металлы, советы;
    //   ВУЗник   — + радикалы нестабильны, полярность связей, реакции ТОЛЬКО по правилам;
    //   Эйнштейн — + энергия связей в кДж/моль, кулоновский барьер и время жизни ядер.
    public enum Level { Fun, School, Uni, Einstein }
    static bool levelLoaded; static Level level = Level.School;
    public static Level Mode
    {
        get { if (!levelLoaded) { level = (Level)Mathf.Clamp(PlayerPrefs.GetInt("atomlab.level", 1), 0, 3); levelLoaded = true; } return level; }
        set { level = value; levelLoaded = true; if (SelfTest.Requested) return; PlayerPrefs.SetInt("atomlab.level", (int)value); PlayerPrefs.Save(); }
    }
    public static string LevelName(Level l)
    {
        switch (l)
        {
            case Level.Fun: return Lang.T("Фан", "Fun");
            case Level.School: return Lang.T("Школяр", "School");
            case Level.Uni: return Lang.T("ВУЗник", "University");
            default: return Lang.T("Эйнштейн", "Einstein");
        }
    }
    public static string LevelHint(Level l)
    {
        switch (l)
        {
            case Level.Fun: return Lang.T("Фан: собирается почти всё, без занудства. Точности немного — это игрушка.", "Fun: almost anything goes, no nagging. Little accuracy — it is a toy.");
            case Level.School: return Lang.T("Школяр: школьная химия — валентность, ряд активности, заряды ионов, советы.", "School: school chemistry — valence, activity series, ion charges, hints.");
            case Level.Uni: return Lang.T("ВУЗник: радикалы нестабильны, видна полярность связей, реакции только по правилам — «собрать что получится» выключено.", "University: radicals are unstable, bond polarity is shown, reactions only by the rules.");
            default: return Lang.T("Эйнштейн: плюс энергия связей в кДж/моль, кулоновский барьер и время жизни ядер в ускорителе.", "Einstein: plus bond energies in kJ/mol, the Coulomb barrier and nuclear lifetimes in the accelerator.");
        }
    }

    // ==================== 2D-режим (21.09, владелец: «добавь в игру 2d режим») ====================
    // Плоский вид, как структурная формула на бумаге: камера смотрит строго спереди без
    // перспективы, а все атомы держатся в одной плоскости — той, что проходит через центр
    // зоны. Молекулы при этом не теряют связей: объёмные (метан-тетраэдр) просто
    // распластываются, как их и рисуют в учебнике. Выключил — плоскость отпускается, и
    // молекулы снова расправляются в объём сами, пружинами связей.
    // Выбор запоминается между запусками.
    static bool mode2DLoaded, mode2D;
    public static bool Mode2D
    {
        get { if (!mode2DLoaded) { mode2D = PlayerPrefs.GetInt("atomlab.2d", 0) == 1; mode2DLoaded = true; } return mode2D; }
        set
        {
            mode2D = value; mode2DLoaded = true;
            if (SelfTest.Requested) return;           // проверка не трогает настройки игрока
            PlayerPrefs.SetInt("atomlab.2d", value ? 1 : 0); PlayerPrefs.Save();
        }
    }

    /// <summary>Держим атомы в плоскости. Не только замком по оси Z, но и возвратом на
    /// плоскость: замок держит скорость, а положение, набранное до включения, осталось бы.</summary>
    void KeepFlat()
    {
        float z0 = ZoneCenter.z;
        foreach (var a in Atom.All)
        {
            if (a == null || a.Body == null) continue;
            if (Mode2D)
            {
                if ((a.Body.constraints & RigidbodyConstraints.FreezePositionZ) == 0)
                    a.Body.constraints |= RigidbodyConstraints.FreezePositionZ;
                // 21.09: прижим через одно Body.position НЕ работал — при замке по Z физика
                // держала тело на старой глубине, и атомы оставались «рядом на экране, далеко
                // на деле» (проверка: разнос 2.7 после включения, вода не собиралась).
                // Ставим и трансформ, и тело — так телепорт доходит до физики наверняка.
                var p = a.transform.position;
                if (Mathf.Abs(p.z - z0) > 0.0005f)
                {
                    p.z = z0;
                    a.transform.position = p;
                    a.Body.position = p;
                    var v = a.Body.linearVelocity; v.z = 0f; a.Body.linearVelocity = v;
                }
            }
            else if ((a.Body.constraints & RigidbodyConstraints.FreezePositionZ) != 0)
            {
                a.Body.constraints &= ~RigidbodyConstraints.FreezePositionZ;
                // Лёгкий толчок из плоскости: иначе плоская молекула так и осталась бы плоской,
                // ведь все силы связей лежат в той же плоскости.
                a.Body.AddForce(new Vector3(0f, 0f, Random.Range(-0.6f, 0.6f)), ForceMode.VelocityChange);
            }
        }
    }

    public static readonly Vector3 ZoneCenter = new Vector3(0f, 1.6f, 0f);
    public static readonly Vector3 ZoneHalf = new Vector3(5.0f, 3.0f, 4.0f);

    // ——— камера ———
    public Camera Cam;
    float camYaw = 20f, camPitch = 14f, camDist = 14f;
    Vector3 camTarget = ZoneCenter;
    Vector3 viewCenter = ZoneCenter;     // вокруг чего сейчас крутится камера: зона или ускоритель

    // ——— перетаскивание атома мышью/пальцем ———
    Atom dragged;
    float dragDepth;
    // ==================== тач (21.09, владелец: «управление для телефона») ====================
    // Unity сама превращает ПЕРВЫЙ палец в левую кнопку мыши — поэтому «пальцем двигать атом»
    // и выдвигать таблицу работало и раньше. Не хватало трёх вещей, их и добавляем:
    //   • два пальца: развести/свести — приблизить/отдалить, повести вместе — вращать камеру;
    //   • долгое нажатие на атом — его меню (правой кнопки у телефона нет, а удалить атом,
    //     разорвать связь и провести реакцию можно только из меню);
    //   • допуск попадания: атом на экране телефона мельче подушечки пальца.
    // Всё это включается только при касаниях: у мыши Input.touchCount всегда 0, на ПК
    // ни одно поведение не меняется.
    bool touchGesture;          // были два пальца — до полного отпускания эмуляцию мыши не слушаем
    bool oneFingerOrbit;        // один палец по пустому месту — крутим камеру
    Vector2 twoPrev; float pinchPrev;
    Vector2 holdPos; float holdStart; Atom holdAtom; bool holdFired;

    /// <summary>Сколько экранных пикселей в одном «пальцевом» миллиметре-эквиваленте:
    /// на плотном экране палец сдвигается на больше пикселей при том же движении руки.</summary>
    static float TouchK { get { return (Screen.dpi > 1f ? Screen.dpi : 320f) / 160f; } }

    Vector3 rmbDown;        // где нажали правую кнопку — чтобы отличить щелчок от вращения камеры
    float rmbTime;

    // ——— выделение рамкой и буфер обмена (🔴 21.09, владелец: «выделятор как в виндовс») ———
    public readonly HashSet<Atom> Selected = new HashSet<Atom>();
    public bool Banding;                 // тянем рамку прямо сейчас
    public Vector2 BandA, BandB;         // углы рамки в экранных координатах (снизу вверх)

    class ClipAtom { public string Sym; public Vector3 Rel; }
    class ClipBond { public int A, B, Order; }
    List<ClipAtom> clipAtoms;
    List<ClipBond> clipBonds;

    public int ClipboardCount { get { return clipAtoms == null ? 0 : clipAtoms.Count; } }

    // ——— что собралось ———
    public class Mol
    {
        public List<Atom> Atoms = new List<Atom>();
        public string Formula;
        public Molecules.Info Info;
        public Vector3 Center;
        public int FreeLeft;
    }
    public readonly List<Mol> Mols = new List<Mol>();

    public readonly HashSet<string> Discovered = new HashSet<string>();
    public int Score;

    float refusedAt = -99f;    // когда последний раз объясняли отказ склейки

    public string Toast = "";
    public float ToastUntil;
    public Color ToastColor = Color.white;

    void Awake()
    {
        I = this;
        Application.targetFrameRate = 120;
        LoadProgress();
    }

    public void Say(string msg, Color c)
    {
        Toast = msg; ToastColor = c; ToastUntil = Time.time + 6f;
    }

    // ==================== камера ====================

    void LateUpdate()
    {
        if (Cam == null) return;

        bool overPanel = LabUI.I != null && LabUI.I.PointerOverUI;

        if (Input.touchCount >= 2)
        {
            var t0 = Input.GetTouch(0); var t1 = Input.GetTouch(1);
            Vector2 mid = (t0.position + t1.position) * 0.5f;
            float dist = (t0.position - t1.position).magnitude;
            // Новый палец — новая точка отсчёта, иначе камера прыгает на весь разнос пальцев.
            if (!touchGesture || t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
            { twoPrev = mid; pinchPrev = dist; }
            touchGesture = true;
            Vector2 dm = (mid - twoPrev) / TouchK;
            if (Mode2D)
            {
                float k2 = camDist * 0.0016f;         // в 2D два пальца двигают вид, а не крутят
                camTarget -= Cam.transform.right * dm.x * 18f * k2 * 0.3f;
                camTarget -= Cam.transform.up * dm.y * 18f * k2 * 0.3f;
            }
            else
            {
                camYaw += dm.x * 0.35f;
                camPitch = Mathf.Clamp(camPitch - dm.y * 0.3f, -60f, 80f);
            }
            // Щипок в ОТНОШЕНИИ, а не в разнице пикселей: одинаково на любом экране.
            if (pinchPrev > 1f && dist > 1f) camDist = Mathf.Clamp(camDist * pinchPrev / dist, 4f, 30f);
            twoPrev = mid; pinchPrev = dist;
        }
        else if (Input.touchCount == 0) { touchGesture = false; oneFingerOrbit = false; }
        if (oneFingerOrbit && Input.touchCount == 1 && !touchGesture && dragged == null)
        {
            Vector2 d1 = Input.GetTouch(0).deltaPosition / TouchK;
            if (Mode2D)
            {
                float k2 = camDist * 0.0016f;
                camTarget -= Cam.transform.right * d1.x * 18f * k2 * 0.3f;
                camTarget -= Cam.transform.up * d1.y * 18f * k2 * 0.3f;
            }
            else
            {
                camYaw += d1.x * 0.35f;
                camPitch = Mathf.Clamp(camPitch - d1.y * 0.3f, -60f, 80f);
            }
        }

        bool orbitGesture = Input.GetMouseButton(1) || (Input.GetMouseButton(0) && dragged == null && !overPanel && Input.GetKey(KeyCode.LeftAlt));
        if (Mode2D && orbitGesture)
        {
            // В плоском виде вращать нечего — та же рука двигает вид.
            float k2 = camDist * 0.0016f;
            camTarget -= Cam.transform.right * Input.GetAxisRaw("Mouse X") * 18f * k2;
            camTarget -= Cam.transform.up * Input.GetAxisRaw("Mouse Y") * 18f * k2;
        }
        else if (orbitGesture)
        {
            camYaw += Input.GetAxisRaw("Mouse X") * 3.2f;
            camPitch = Mathf.Clamp(camPitch - Input.GetAxisRaw("Mouse Y") * 2.4f, -60f, 80f);
        }
        if (!overPanel)
        {
            float wheel = Input.GetAxisRaw("Mouse ScrollWheel");
            if (Mathf.Abs(wheel) > 0.001f) camDist = Mathf.Clamp(camDist - wheel * 12f, 4f, 30f);
        }

        // 🔴 21.09, владелец: на средней кнопке камера ходит свободно — тянешь, и вид едет
        // за рукой, в плоскости экрана. Правая по-прежнему вертит, WASD двигает по полу.
        if (Input.GetMouseButton(2) && !overPanel)
        {
            float k = camDist * 0.0016f;
            camTarget -= Cam.transform.right * Input.GetAxisRaw("Mouse X") * 18f * k;
            camTarget -= Cam.transform.up * Input.GetAxisRaw("Mouse Y") * 18f * k;
        }

        float pan = 6f * Time.deltaTime;
        Vector3 fwd = Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, camYaw, 0f) * Vector3.right;
        if (Input.GetKey(KeyCode.W)) camTarget += fwd * pan;
        if (Input.GetKey(KeyCode.S)) camTarget -= fwd * pan;
        if (Input.GetKey(KeyCode.D)) camTarget += right * pan;
        if (Input.GetKey(KeyCode.A)) camTarget -= right * pan;
        camTarget = new Vector3(
            Mathf.Clamp(camTarget.x, viewCenter.x - 5f, viewCenter.x + 5f),
            Mathf.Clamp(camTarget.y, viewCenter.y - 3f, viewCenter.y + 3f),
            Mathf.Clamp(camTarget.z, viewCenter.z - 5f, viewCenter.z + 5f));

        Cam.orthographic = Mode2D;
        if (Mode2D) Cam.orthographicSize = camDist * 0.42f;
        Quaternion rot = Mode2D ? Quaternion.identity : Quaternion.Euler(camPitch, camYaw, 0f);
        Cam.transform.position = camTarget - rot * Vector3.forward * camDist;
        Cam.transform.rotation = rot;

        // Панель закрывает левую часть экрана, поэтому зону сборки смещаем вправо ровно на
        // столько, сколько панель отъела: иначе половина зоны всегда прячется под таблицей.
        float panelRight = (LabUI.I != null) ? LabUI.I.PanelRightPx : 0f;
        float f = Mathf.Clamp01(panelRight / Mathf.Max(1f, Screen.width));
        float shiftNdc = f * 0.5f;                       // середина свободной части вместо середины экрана
        float halfWidthAtTarget = Mode2D ? Cam.orthographicSize * Cam.aspect
                                         : camDist * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Cam.aspect;
        Cam.transform.position -= Cam.transform.right * (shiftNdc * 2f * halfWidthAtTarget);
    }

    // ==================== ввод по зоне ====================

    void Update()
    {
        bool overPanel = LabUI.I != null && LabUI.I.PointerOverUI;

        saveTimer -= Time.deltaTime;
        if (saveTimer <= 0f) { saveTimer = 8f; if (zoneDirty) SaveZone(); }
        KeepFlat();
        // На стенде вещей мышь двигает стаканы, а не атомы: иначе каждый клик по столу
        // заодно тянул бы рамку выделения в зоне молекул.
        if (PhysLab.I != null && PhysLab.I.Active) return;

        // Второй палец лёг — это жест камеры, а не перетаскивание и не рамка. Первый палец
        // успел схватить атом или начать рамку за кадр-другой до второго: отменяем, рамку —
        // без выделения. Пока пальцы не отпущены все, первый палец атомы не трогает.
        if (touchGesture || Input.touchCount >= 2)
        {
            dragged = null; Banding = false; holdAtom = null;
            return;
        }

        // Долгое нажатие на атом = меню атома (замена правой кнопки).
        if (Input.touchCount == 1)
        {
            var t = Input.GetTouch(0);
            if (t.phase == TouchPhase.Began)
            {
                holdPos = t.position; holdStart = Time.time; holdFired = false;
                holdAtom = overPanel ? null : PickAtom(t.position);
            }
            else if (!holdFired && holdAtom != null)
            {
                if ((t.position - holdPos).magnitude > 12f * TouchK) holdAtom = null;   // повёл — значит тащит
                else if (Time.time - holdStart > 0.5f)
                {
                    holdFired = true;
                    dragged = null;                          // атом остаётся на месте, меню открыто
                    if (LabUI.I != null) LabUI.I.OpenMenu(holdAtom, t.position);
                    holdAtom = null;
                }
            }
        }

        // Shift + ЛКМ — оторвать все связи атома. Без этого собранное нельзя переделать,
        // а «занято» становилось тупиком: только выкинуть атом целиком.
        if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftShift) && !overPanel)
        {
            var t = PickAtom(Input.mousePosition);
            if (t != null && t.Bonds.Count > 0)
            {
                int n = t.Bonds.Count;
                for (int i = t.Bonds.Count - 1; i >= 0; i--) BreakByHand(t.Bonds[i]);
                Say(Lang.T("Оторвано связей: ", "Bonds broken: ") + n + Lang.T(" у ", " on ") + t.El.Sym + Lang.T(". Теперь он свободен.", ". It is free now."), new Color(1f, 0.85f, 0.6f));
                Recompute();
                return;
            }
        }

        if (Input.GetMouseButtonDown(0) && !overPanel && !Input.GetKey(KeyCode.LeftAlt) && !Input.GetKey(KeyCode.LeftShift))
        {
            var a = PickAtom(Input.mousePosition);
            if (a != null)
            {
                dragged = a;
                dragDepth = Cam.WorldToScreenPoint(a.transform.position).z;
            }
            else if (Input.touchCount > 0)
            {
                // 21.09, владелец: «в апк убери селектор, пусть свайп одним пальцем крутит экран».
                // На телефоне рамка выделения мешала: палец по пустому месту тянул рамку, а
                // хотелось повернуть вид. Теперь один палец по пустому — вращение (в 2D — сдвиг).
                oneFingerOrbit = true;
            }
            else
            {
                // Промах по атому — значит, тянем рамку выделения, как в проводнике.
                Banding = true;
                BandA = BandB = Input.mousePosition;
            }
        }
        if (Banding) BandB = Input.mousePosition;
        if (Input.GetMouseButtonUp(0))
        {
            if (Banding) { Banding = false; ApplyBand(); }
            dragged = null;
        }

        if (dragged != null)
        {
            Vector3 sp = Input.mousePosition; sp.z = dragDepth;
            Vector3 target = Cam.ScreenToWorldPoint(sp);
            Vector3 d = target - dragged.transform.position;
            dragged.Body.linearVelocity = d * 12f;      // тянем скоростью, а не телепортом — связи целы
        }

        // 🔴 21.09, владелец: «контекстное меню не открывается у атомов». Так и было: меню
        // в 1.8 нарисовано, а открывать его оказалось НЕКОМУ — кусок кода, который ловит щелчок,
        // не записался при сбое правки, и никто этого не заметил: проверки игры гоняют химию,
        // а мышь не трогают. Вот он.
        //
        // Правая кнопка БЕЗ протяжки — меню атома. С протяжкой правая по-прежнему вертит
        // камеру, поэтому меню открывается, только если мышь стояла на месте.
        if (Input.GetMouseButtonDown(1)) { rmbDown = Input.mousePosition; rmbTime = Time.time; }
        if (Input.GetMouseButtonUp(1) && !overPanel && !Input.GetKey(KeyCode.LeftShift))
        {
            if ((Input.mousePosition - rmbDown).magnitude < 8f && Time.time - rmbTime < 0.7f)
            {
                var hit = PickAtom(Input.mousePosition);
                if (hit != null && LabUI.I != null) LabUI.I.OpenMenu(hit, Input.mousePosition);
            }
        }

        // Правая кнопка по атому с Shift — убрать атом.
        if (Input.GetMouseButtonDown(1) && Input.GetKey(KeyCode.LeftShift) && !overPanel)
        {
            var a = PickAtom(Input.mousePosition);
            if (a != null) { a.Despawn(); Recompute(); }
        }

        // 🔴 21.09, владелец: Del должен убирать АТОМ ПОД КУРСОРОМ, а не всю комнату.
        // Раньше одна клавиша сносила час работы. Всю зону чистит кнопка «Убрать атомы».
        if (Input.GetKeyDown(KeyCode.Delete) && !overPanel)
        {
            var victim = dragged != null ? dragged : PickAtom(Input.mousePosition);
            if (victim == null && Selected.Count > 0)
            {
                int n = Selected.Count;
                foreach (var a in new List<Atom>(Selected)) a.Despawn();
                Selected.Clear();
                Recompute();
                Say(Lang.T("Убрано выделенных атомов: ", "Selected atoms removed: ") + n, new Color(0.9f, 0.9f, 0.9f));
            }
            else if (victim != null)
            {
                string sym = victim.El.Sym;
                if (dragged == victim) dragged = null;
                victim.Despawn();
                Recompute();
                Say(Lang.T("Убран атом ", "Removed atom ") + sym + Lang.T(". Всю зону чистит кнопка «Убрать атомы».", ". The 'Clear atoms' button empties the whole zone."), new Color(0.9f, 0.9f, 0.9f));
            }
            else Say(Lang.T("Наведи на атом и нажми Del — уберётся он один.", "Hover over an atom and press Del — only that one is removed."), new Color(0.9f, 0.9f, 0.7f));
        }
        if (Input.GetKeyDown(KeyCode.Escape) && LabUI.I != null) LabUI.I.TogglePanel();

        // Ctrl+C копирует выделенное, Ctrl+V вставляет. Ctrl+M — то же самое: на русской
        // раскладке клавиша V печатает «м», и владелец назвал её так, как она подписана.
        bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (ctrl && Input.GetKeyDown(KeyCode.C)) CopySelection();
        if (ctrl && (Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.M))) PasteClipboard();
        if (ctrl && Input.GetKeyDown(KeyCode.A))
        {
            Selected.Clear();
            foreach (var a in Atom.All) Selected.Add(a);
            Say(Lang.T("Выделено всё: ", "Selected all: ") + Selected.Count + Lang.T(" атомов.", " atoms."), new Color(0.8f, 0.95f, 1f));
        }
    }

    /// <summary>Переезд камеры к ускорителю и обратно. Стенд стоит поодаль, поэтому это
    /// именно смена места, а не другой угол обзора.</summary>
    public void LookAt(Vector3 center, float dist)
    {
        viewCenter = center;
        camTarget = center;
        camDist = dist;
        camPitch = 10f;
        camYaw = 0f;
    }

    /// <summary>Этот атом сейчас тянут мышью. По этому признаку связь и решает, рвать ли её:
    /// рвать должна РУКА, а не случайный толчок соседа.</summary>
    public bool IsDragged(Atom a) { return dragged == a; }

    public Atom PickAtom(Vector3 screenPos)
    {
        if (Cam == null) return null;
        RaycastHit hit;
        if (Physics.Raycast(Cam.ScreenPointToRay(screenPos), out hit, 200f))
            return hit.collider.GetComponentInParent<Atom>();
        // Пальцем: промах по шару — берём ближайший атом в пределах подушечки пальца
        // (около 7 мм). Мышью точность пиксельная, ей допуск не нужен и не даётся.
        if (Input.touchCount == 0) return null;
        float best = 45f * TouchK;
        Atom pick = null;
        foreach (var a in Atom.All)
        {
            if (a == null) continue;
            Vector3 sp = Cam.WorldToScreenPoint(a.transform.position);
            if (sp.z <= 0f) continue;
            float d = ((Vector2)sp - (Vector2)screenPos).magnitude;
            if (d < best) { best = d; pick = a; }
        }
        return pick;
    }

    /// <summary>Точка в зоне под курсором — куда падает элемент, брошенный из таблицы.</summary>
    public Vector3 DropPoint(Vector3 screenPos)
    {
        if (Cam == null) return ZoneCenter;
        Ray r = Cam.ScreenPointToRay(screenPos);
        // Плоскость, проходящая через центр зоны лицом к камере.
        var plane = new Plane(-Cam.transform.forward, ZoneCenter);
        float t;
        if (plane.Raycast(r, out t)) return r.GetPoint(t);
        return ZoneCenter;
    }

    public Atom SpawnFromTable(Elements.El el, Vector3 screenPos)
    {
        Vector3 p = DropPoint(screenPos);
        p.x = Mathf.Clamp(p.x, ZoneCenter.x - ZoneHalf.x, ZoneCenter.x + ZoneHalf.x);
        p.y = Mathf.Clamp(p.y, ZoneCenter.y - ZoneHalf.y, ZoneCenter.y + ZoneHalf.y);
        p.z = Mathf.Clamp(p.z, ZoneCenter.z - ZoneHalf.z, ZoneCenter.z + ZoneHalf.z);
        var a = Atom.Spawn(el, p);
        if (el.MaxBonds == 0 && !GodMode) Say(el.Name + Lang.T(" — благородный газ: ни с кем не соединяется. Так и в жизни.", " is a noble gas: it bonds with nothing. Same as in real life."), new Color(0.7f, 0.9f, 1f));
        Recompute();
        return a;
    }

    public void ClearZone()
    {
        for (int i = Atom.All.Count - 1; i >= 0; i--) Atom.All[i].Despawn();
        Recompute();
    }


    // ==================== выделение и буфер ====================

    /// <summary>Что попало в рамку. Считаем по ЭКРАНУ, а не по объёму: рамка плоская, и
    /// игрок выделяет то, что видит, даже если атомы стоят на разной глубине.</summary>
    void ApplyBand()
    {
        var r = Rect.MinMaxRect(Mathf.Min(BandA.x, BandB.x), Mathf.Min(BandA.y, BandB.y),
                                Mathf.Max(BandA.x, BandB.x), Mathf.Max(BandA.y, BandB.y));
        if (r.width < 6f && r.height < 6f) { Selected.Clear(); return; }   // просто щелчок — снять выделение

        if (!Input.GetKey(KeyCode.LeftShift)) Selected.Clear();            // Shift добавляет к выделенному
        foreach (var a in Atom.All)
        {
            Vector3 sp = Cam.WorldToScreenPoint(a.transform.position);
            if (sp.z > 0f && r.Contains(new Vector2(sp.x, sp.y))) Selected.Add(a);
        }
        Say(Lang.T("Выделено атомов: ", "Atoms selected: ") + Selected.Count + Lang.T(". Ctrl+C — копировать, Ctrl+V — вставить.", ". Ctrl+C to copy, Ctrl+V to paste."),
            new Color(0.8f, 0.95f, 1f));
    }

    public void CopySelection()
    {
        if (Selected.Count == 0) { Say(Lang.T("Сначала выдели атомы рамкой.", "Select atoms with a frame first."), new Color(1f, 0.9f, 0.7f)); return; }

        var list = new List<Atom>(Selected);
        Vector3 c = Vector3.zero;
        foreach (var a in list) c += a.transform.position;
        c /= list.Count;

        clipAtoms = new List<ClipAtom>();
        foreach (var a in list) clipAtoms.Add(new ClipAtom { Sym = a.El.Sym, Rel = a.transform.position - c });

        // Связи копируем ТОЛЬКО внутри выделения: связь наружу копировать некуда.
        clipBonds = new List<ClipBond>();
        foreach (var b in Bond.All)
        {
            int i = list.IndexOf(b.A), j = list.IndexOf(b.B);
            if (i >= 0 && j >= 0) clipBonds.Add(new ClipBond { A = i, B = j, Order = b.Order });
        }
        Say(Lang.T("Скопировано: ", "Copied: ") + clipAtoms.Count + Lang.T(" атомов и ", " atoms and ") + clipBonds.Count + Lang.T(" связей.", " bonds."),
            new Color(0.8f, 0.95f, 1f));
    }

    public void PasteClipboard()
    {
        if (clipAtoms == null || clipAtoms.Count == 0) { Say(Lang.T("Буфер пуст: сначала Ctrl+C.", "Clipboard is empty: press Ctrl+C first."), new Color(1f, 0.9f, 0.7f)); return; }

        Vector3 c = ZoneCenter + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.8f, 0.8f), Random.Range(-1f, 1f));
        var made = new List<Atom>();
        foreach (var ca in clipAtoms) made.Add(Atom.Spawn(Elements.BySymbol(ca.Sym), c + ca.Rel));
        foreach (var cb in clipBonds) Bond.Create(made[cb.A], made[cb.B], cb.Order);

        Selected.Clear();
        foreach (var a in made) Selected.Add(a);
        Fx.Pop(1.1f);
        Fx.Sparks(c, new Color(0.7f, 0.95f, 1f), 25, 3f);
        Recompute();
        Say(Lang.T("Вставлено: ", "Pasted: ") + made.Count + Lang.T(" атомов. Копия выделена — можно сразу оттащить.", " atoms. The copy is selected — drag it away."),
            new Color(0.8f, 0.95f, 1f));
    }

    /// <summary>Выход из игры. В редакторе Unity Application.Quit ничего не делает, поэтому
    /// там просто говорим об этом вслух, а не делаем вид, что вышли.</summary>
    public void ExitGame()
    {
        Say(Lang.T("Выходим...", "Quitting..."), Color.white);
        Application.Quit();
    }

    // ==================== склейка ====================

    // 21.09, владелец: «кнопки убрать связи в контекстном меню не работают». Работали — и
    // тут же откатывались: после разрыва атомы оставались вплотную, а склейка связывает
    // ЛЮБЫЕ касающиеся атомы со свободными местами. Связь рвалась и в тот же кадр
    // завязывалась снова. Теперь разорванная рукой пара держится врозь, пока атомы не
    // разойдутся дальше дистанции склейки; свести их снова можно — поднеся руками.
    readonly HashSet<long> heldApart = new HashSet<long>();

    static long PairKey(Atom a, Atom b)
    {
        int x = a.GetInstanceID(), y = b.GetInstanceID();
        if (x > y) { int t = x; x = y; y = t; }
        return ((long)x << 32) ^ (uint)y;
    }

    /// <summary>Разорвать связь РУКОЙ: разрыв, лёгкий толчок врозь и запрет на немедленную
    /// обратную склейку этой пары.</summary>
    public void BreakByHand(Bond b)
    {
        if (b == null) return;
        var a = b.A; var c = b.B;
        b.Break();
        if (a == null || c == null) return;
        heldApart.Add(PairKey(a, c));
        Vector3 dir = (a.transform.position - c.transform.position);
        if (dir.sqrMagnitude < 1e-6f) dir = Random.onUnitSphere;
        dir.Normalize();
        a.Body.AddForce(dir * 2.5f, ForceMode.VelocityChange);
        c.Body.AddForce(-dir * 2.5f, ForceMode.VelocityChange);
    }

    /// <summary>21.09, владелец: «добавь "образовать связь с..."». Связать два атома из
    /// меню: второй подтягивается к первому и связь ставится сразу — если у обоих есть место
    /// для такой пары. Возвращает причину отказа или null.</summary>
    public string BondByHand(Atom a, Atom b)
    {
        if (a == null || b == null || a == b) return Lang.T("Не с чем связывать.", "Nothing to bond with.");
        if (a.BondWith(b) != null) return Lang.T("Они уже связаны.", "They are already bonded.");
        if (a.FreeBondsWith(b) < 1) return a.El.Sym + Lang.T(" занят: мест для связи с ", " is full: no room for a bond with ") + b.El.Sym + ".";
        if (b.FreeBondsWith(a) < 1) return b.El.Sym + Lang.T(" занят: мест для связи с ", " is full: no room for a bond with ") + a.El.Sym + ".";
        heldApart.Remove(PairKey(a, b));
        Vector3 dir = b.transform.position - a.transform.position;
        if (dir.sqrMagnitude < 1e-6f) dir = Random.onUnitSphere;
        dir.Normalize();
        b.transform.position = a.transform.position + dir * (a.El.Radius + b.El.Radius) * 1.3f;
        b.Body.position = b.transform.position;
        b.Body.linearVelocity = Vector3.zero;
        Bond.Create(a, b);
        ReactOnBond(a, b);
        Recompute();
        return null;
    }

    void FixedUpdate()
    {
        var list = Atom.All;
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.El.MaxBonds == 0 && !GodMode) continue;   // ксенон теперь соединяется, гелий — нет
            for (int j = i + 1; j < list.Count; j++)
            {
                var b = list[j];
                if (b.El.MaxBonds == 0 && !GodMode) continue;
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                // 🔴 21.09. С настоящими радиусами атомы стали вдвое мельче, и прежний
                // множитель 1.25 требовал сводить их почти вплотную — вода переставала
                // собираться вообще (поймала проверка: water=False). Тянемся дальше собственных
                // размеров: связь возникает примерно на двух радиусах.
                float touch = (a.El.Radius + b.El.Radius) * 1.9f;

                if (heldApart.Count > 0 && heldApart.Contains(PairKey(a, b)))
                {
                    // Разорвано рукой: не склеиваем, пока пара не разойдётся.
                    if (dist > touch * 1.15f) heldApart.Remove(PairKey(a, b));
                    continue;
                }

                var existing = a.BondWith(b);
                if (existing != null)
                {
                    // Сжали связанную пару сильнее длины покоя — поднимаем кратность.
                    if (dist < existing.RestLength * 0.72f && existing.Raise())
                    {
                        changed = true;
                        Say(Lang.T("Связь стала ", "The bond became ") + (existing.Order == 2 ? Lang.T("двойной", "double") : Lang.T("тройной", "triple")) + ": " + a.El.Sym + (existing.Order == 2 ? "=" : "≡") + b.El.Sym, new Color(0.8f, 1f, 0.8f));
                    }
                    continue;
                }

                if (dist <= touch && a.FreeBondsWith(b) > 0 && b.FreeBondsWith(a) > 0)
                {
                    Bond.Create(a, b);
                    ReactOnBond(a, b);
                    changed = true;
                }
                else if (dist <= touch * 0.95f && (a.FreeBondsWith(b) == 0 || b.FreeBondsWith(a) == 0))
                {
                    // Место кончилось — отталкиваем, чтобы было видно: связей больше нет.
                    // Толчок мягкий (было 6): сильный расталкивал соседние молекулы так, что
                    // рвал их собственные связи.
                    Vector3 dir = (a.transform.position - b.transform.position).normalized;
                    a.Body.AddForce(dir * 2.5f, ForceMode.Acceleration);
                    b.Body.AddForce(-dir * 2.5f, ForceMode.Acceleration);

                    // 🔴 21.09, владелец: «не хочет стыковаться» — и игра МОЛЧАЛА. Отказ без
                    // причины выглядит как поломка. Теперь говорим, у кого кончились связи.
                    if (Time.time > refusedAt + 2.5f)
                    {
                        refusedAt = Time.time;
                        Atom full = a.FreeBondsWith(b) == 0 ? a : b;
                        Atom other = full == a ? b : a;
                        // 21.09. Было «все 2 связи заняты» у серы, которая держала шесть: число
                        // бралось из предела для этой пары (с водородом у серы две), а не из того,
                        // сколько занято. Показываем занятое, а если предел ниже занятого — говорим
                        // почему: с этим соседом у атома меньше связей, чем с кислородом.
                        int usedNow = full.UsedBonds;
                        string why = full.CapWith(other) < full.El.MaxBonds && usedNow < full.El.MaxBonds
                            ? Lang.T(" С " + other.El.Sym + " у него связей меньше, чем с кислородом.", " With " + other.El.Sym + " it bonds less than with oxygen.")
                            : "";
                        Say(full.El.Name + " (" + full.El.Sym + Lang.T(") уже занят: связей занято ", ") is full: bonds taken ") + usedNow + "." + why +
                            Lang.T(" Shift + ЛКМ по нему — оторвать соседей.", " bonds are taken. Shift + left click on it breaks its neighbours off."),
                            new Color(1f, 0.8f, 0.5f));
                    }
                }
            }
        }
        if (changed) Recompute();
    }

    // ==================== разбор собранного ====================

    public void Recompute()
    {
        zoneDirty = true;
        Mols.Clear();
        var seen = new HashSet<Atom>();
        foreach (var start in Atom.All)
        {
            if (seen.Contains(start)) continue;
            var m = new Mol();
            var stack = new Stack<Atom>();
            stack.Push(start); seen.Add(start);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                m.Atoms.Add(cur);
                foreach (var b in cur.Bonds)
                {
                    var o = b.Other(cur);
                    if (o != null && !seen.Contains(o)) { seen.Add(o); stack.Push(o); }
                }
            }

            var counts = new Dictionary<string, int>();
            Vector3 sum = Vector3.zero;
            int free = 0;
            foreach (var a in m.Atoms)
            {
                int c; counts.TryGetValue(a.El.Sym, out c);
                counts[a.El.Sym] = c + 1;
                sum += a.transform.position;
                free += a.FreeValence;
            }
            m.Info = Molecules.LookupByComposition(counts);
            // Узнанное показываем так, как пишут люди (NaCl, H2SO4), а неузнанное — по Гиллу.
            m.Formula = m.Info != null ? m.Info.Formula : Molecules.Formula(counts);
            m.Center = sum / m.Atoms.Count;
            m.FreeLeft = free;
            Mols.Add(m);

            // 21.09. Правило «щелочной металл + O + H = натрий в воде, взрыв» взрывало и
            // NaOH, KOH, пищевую соду — хотя все три есть в справочнике игры. Едкий натр нельзя
            // было собрать вообще. Поймала это проверка 20 популярных соединений. Настоящее
            // вещество — это уже итог реакции, а не металл в стакане: его не взрываем.
            if (m.Atoms.Count > 2 && m.Info == null && IsAlkaliInWater(m)) { Explode(m); continue; }

            if (m.Info != null && m.Atoms.Count > 1 && !Discovered.Contains(m.Formula))
            {
                Discovered.Add(m.Formula);
                Fx.Chime();
                Fx.Flash(m.Center, new Color(0.5f, 1f, 0.6f), 7f, 10f, 0.7f);
                Fx.Sparks(m.Center, new Color(0.55f, 1f, 0.65f), 60, 4.5f, 0.11f);
                Score += 10 + m.Atoms.Count * 2;
                Say(Lang.T("ОТКРЫТО: ", "DISCOVERED: ") + m.Info.Name + " (" + m.Formula + ") — " + m.Info.Note, new Color(0.6f, 1f, 0.6f));
                CheckQuests(m.Formula);
                SaveProgress();
            }
        }
    }


    // ==================== реакции ====================

    /// <summary>Бурная встреча двух атомов. Правило настоящее и простое: щелочной металл с
    /// галогеном или кислородом соединяется со вспышкой и хлопком — так натрий горит в хлоре
    /// жёлтым пламенем. Вещество при этом ОСТАЁТСЯ: вспышка сопровождает рождение соли,
    /// а не ломает её.</summary>
    void ReactOnBond(Atom a, Atom b)
    {
        bool violent =
            (a.El.Class == Elements.Cls.Alkali && (b.El.Class == Elements.Cls.Halogen || b.El.Sym == "O")) ||
            (b.El.Class == Elements.Cls.Alkali && (a.El.Class == Elements.Cls.Halogen || a.El.Sym == "O"));
        if (!violent) return;

        Vector3 p = (a.transform.position + b.transform.position) * 0.5f;
        Fx.Boom();
        Fx.Flash(p, new Color(1f, 0.85f, 0.4f), 9f, 11f, 0.6f);
        Fx.Sparks(p, new Color(1f, 0.75f, 0.25f), 70, 6.5f, 0.13f);
        Atom metal = a.El.Class == Elements.Cls.Alkali ? a : b;
        Say(metal.El.Name + Lang.T(" вспыхнул: щелочные металлы соединяются бурно, со светом и жаром.", " flashed: alkali metals react violently, with light and heat."),
            new Color(1f, 0.85f, 0.5f));
    }

    /// <summary>Взрыв собранного: щелочной металл в воде. Связи рвутся, атомы разлетаются —
    /// ровно то, что делает кусочек натрия, брошенный в стакан.</summary>
    void Explode(Mol m)
    {
        Fx.Boom();
        Fx.Flash(m.Center, new Color(1f, 0.7f, 0.3f), 14f, 16f, 0.8f);
        Fx.Sparks(m.Center, new Color(1f, 0.6f, 0.2f), 140, 9f, 0.16f);
        foreach (var a in m.Atoms)
        {
            for (int i = a.Bonds.Count - 1; i >= 0; i--) a.Bonds[i].Break();
            Vector3 dir = (a.transform.position - m.Center).normalized;
            if (dir.sqrMagnitude < 0.01f) dir = Random.onUnitSphere;
            a.Body.AddForce(dir * 14f, ForceMode.VelocityChange);
        }
        Say(Lang.T("ВЗРЫВ: щелочной металл в воде. Так натрий и ведёт себя в стакане — вспышка и разлёт.", "EXPLOSION: an alkali metal in water. That is exactly what sodium does in a glass — a flash and scatter."),
            new Color(1f, 0.6f, 0.4f));
    }

    /// <summary>Есть ли в молекуле щелочной металл вместе с водой (и кислород, и водород).</summary>
    static bool IsAlkaliInWater(Mol m)
    {
        // 21.09. Было «есть щелочной металл, кислород и водород» — под это попадал любой
        // продукт: ацетат натрия из соды с уксусом взрывался прямо в момент рождения (поймала
        // проверка пар молекул). Теперь нужна именно ВОДА: кислород, у которого два соседа —
        // водороды. Ни в щёлочи, ни в соде, ни в ацетате такого кислорода нет.
        bool alkali = false, water = false;
        foreach (var a in m.Atoms)
        {
            if (a.El.Class == Elements.Cls.Alkali) alkali = true;
            if (a.El.Sym != "O") continue;
            int hs = 0;
            foreach (var b in a.Bonds)
            {
                var other = b.A == a ? b.B : b.A;
                if (other != null && other.El.Sym == "H") hs++;
            }
            if (hs >= 2) water = true;
        }
        return alkali && water;
    }

    void CheckQuests(string formula)
    {
        foreach (var q in Quests.All)
        {
            if (!q.Done && q.Formula == formula)
            {
                q.Done = true;
                Score += q.Reward;
                Say(Lang.T("ЗАДАНИЕ ВЫПОЛНЕНО: ", "QUEST COMPLETE: ") + q.Title + "  +" + q.Reward, new Color(1f, 0.9f, 0.5f));
            }
        }
    }


    // ==================== сохранение зоны ====================

    /// <summary>🔴 21.09, владелец: «добавь сохранения, чтоб не терять прогресс». Зона
    /// пишется в файл: элементы с местами и связи с кратностями. Сохраняем сами — каждые
    /// восемь секунд, если что-то менялось, и при выходе. Ручной кнопки нет нарочно: терялось
    /// бы именно то, что забыли нажать.</summary>
    string ZonePath { get { return System.IO.Path.Combine(Application.persistentDataPath, "zone.txt"); } }

    float saveTimer = 8f;
    bool zoneDirty;

    public void MarkDirty() { zoneDirty = true; }

    const char NL = (char)10;   // перенос строки в файле сохранения

    public void SaveZone()
    {
        // 21.09. Самопроверка писала в ТЕ ЖЕ настройки, что и игра игрока: собрав по разу все
        // 92 вещества, она «открыла» владельцу весь справочник, накрутила очки и стёрла его
        // зону (проверка чистит зону перед каждым опытом). Прогон проверки — не игра: ничего
        // не сохраняем.
        if (SelfTest.Requested) return;
        try
        {
            var list = Atom.All;
            var sb = new System.Text.StringBuilder();
            sb.Append("v1").Append(NL);
            foreach (var a in list)
                sb.Append("a ").Append(a.El.Sym).Append(' ')
                  .Append(a.transform.position.x.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
                  .Append(a.transform.position.y.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(' ')
                  .Append(a.transform.position.z.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)).Append(NL);
            foreach (var b in Bond.All)
            {
                int i = list.IndexOf(b.A), j = list.IndexOf(b.B);
                if (i >= 0 && j >= 0) sb.Append("b ").Append(i).Append(' ').Append(j).Append(' ').Append(b.Order).Append(NL);
            }
            System.IO.File.WriteAllText(ZonePath, sb.ToString());
            zoneDirty = false;
        }
        catch (System.Exception e) { Debug.LogWarning(Lang.T("Не вышло сохранить зону: ", "Could not save the zone: ") + e.Message); }
    }

    public void LoadZone()
    {
        try
        {
            if (!System.IO.File.Exists(ZonePath)) return;
            var lines = System.IO.File.ReadAllLines(ZonePath);
            var made = new List<Atom>();
            foreach (var line in lines)
            {
                var p = line.Split(' ');
                if (p.Length == 5 && p[0] == "a")
                {
                    var el = Elements.BySymbol(p[1]);
                    if (el == null) continue;
                    made.Add(Atom.Spawn(el, new Vector3(
                        float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(p[3], System.Globalization.CultureInfo.InvariantCulture),
                        float.Parse(p[4], System.Globalization.CultureInfo.InvariantCulture))));
                }
                else if (p.Length == 4 && p[0] == "b")
                {
                    int i = int.Parse(p[1]), j = int.Parse(p[2]), o = int.Parse(p[3]);
                    if (i >= 0 && i < made.Count && j >= 0 && j < made.Count) Bond.Create(made[i], made[j], o);
                }
            }
            if (made.Count > 0)
            {
                Recompute();
                Say(Lang.T("Зона восстановлена: ", "Zone restored: ") + made.Count + Lang.T(" атомов с прошлого раза.", " atoms from last time."), new Color(0.8f, 0.95f, 1f));
            }
        }
        catch (System.Exception e) { Debug.LogWarning(Lang.T("Не вышло прочитать зону: ", "Could not read the zone: ") + e.Message); }
    }

    void OnApplicationQuit() { SaveZone(); SaveProgress(); }

    // ==================== память между запусками ====================

    void SaveProgress()
    {
        // 21.09. Самопроверка писала в ТЕ ЖЕ настройки, что и игра игрока: собрав по разу все
        // 92 вещества, она «открыла» владельцу весь справочник, накрутила очки и стёрла его
        // зону (проверка чистит зону перед каждым опытом). Прогон проверки — не игра: ничего
        // не сохраняем.
        if (SelfTest.Requested) return;
        PlayerPrefs.SetString("atomlab.discovered", string.Join(",", new List<string>(Discovered).ToArray()));
        PlayerPrefs.SetInt("atomlab.score", Score);
        var done = new List<string>();
        foreach (var q in Quests.All) if (q.Done) done.Add(q.Formula);
        PlayerPrefs.SetString("atomlab.quests", string.Join(",", done.ToArray()));
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        Score = PlayerPrefs.GetInt("atomlab.score", 0);
        string d = PlayerPrefs.GetString("atomlab.discovered", "");
        if (!string.IsNullOrEmpty(d)) foreach (var f in d.Split(',')) if (f.Length > 0) Discovered.Add(f);
        string q = PlayerPrefs.GetString("atomlab.quests", "");
        if (!string.IsNullOrEmpty(q))
        {
            var set = new HashSet<string>(q.Split(','));
            foreach (var it in Quests.All) if (set.Contains(it.Formula)) it.Done = true;
        }
    }

}
