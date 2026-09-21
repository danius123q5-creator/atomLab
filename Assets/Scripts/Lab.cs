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

    public static readonly Vector3 ZoneCenter = new Vector3(0f, 1.6f, 0f);
    public static readonly Vector3 ZoneHalf = new Vector3(5.0f, 3.0f, 4.0f);

    // ——— камера ———
    public Camera Cam;
    float camYaw = 20f, camPitch = 14f, camDist = 14f;
    Vector3 camTarget = ZoneCenter;

    // ——— перетаскивание атома мышью/пальцем ———
    Atom dragged;
    float dragDepth;

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

        if (Input.GetMouseButton(1) || (Input.GetMouseButton(0) && dragged == null && !overPanel && Input.GetKey(KeyCode.LeftAlt)))
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
            Mathf.Clamp(camTarget.x, ZoneCenter.x - 4f, ZoneCenter.x + 4f),
            Mathf.Clamp(camTarget.y, 0.5f, 4f),
            Mathf.Clamp(camTarget.z, ZoneCenter.z - 4f, ZoneCenter.z + 4f));

        Quaternion rot = Quaternion.Euler(camPitch, camYaw, 0f);
        Cam.transform.position = camTarget - rot * Vector3.forward * camDist;
        Cam.transform.rotation = rot;

        // Панель закрывает левую часть экрана, поэтому зону сборки смещаем вправо ровно на
        // столько, сколько панель отъела: иначе половина зоны всегда прячется под таблицей.
        float panelRight = (LabUI.I != null) ? LabUI.I.PanelRightPx : 0f;
        float f = Mathf.Clamp01(panelRight / Mathf.Max(1f, Screen.width));
        float shiftNdc = f * 0.5f;                       // середина свободной части вместо середины экрана
        float halfWidthAtTarget = camDist * Mathf.Tan(Cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * Cam.aspect;
        Cam.transform.position -= Cam.transform.right * (shiftNdc * 2f * halfWidthAtTarget);
    }

    // ==================== ввод по зоне ====================

    void Update()
    {
        bool overPanel = LabUI.I != null && LabUI.I.PointerOverUI;

        saveTimer -= Time.deltaTime;
        if (saveTimer <= 0f) { saveTimer = 8f; if (zoneDirty) SaveZone(); }

        // Shift + ЛКМ — оторвать все связи атома. Без этого собранное нельзя переделать,
        // а «занято» становилось тупиком: только выкинуть атом целиком.
        if (Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftShift) && !overPanel)
        {
            var t = PickAtom(Input.mousePosition);
            if (t != null && t.Bonds.Count > 0)
            {
                int n = t.Bonds.Count;
                for (int i = t.Bonds.Count - 1; i >= 0; i--) t.Bonds[i].Break();
                Say("Оторвано связей: " + n + " у " + t.El.Sym + ". Теперь он свободен.", new Color(1f, 0.85f, 0.6f));
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
                Say("Убрано выделенных атомов: " + n, new Color(0.9f, 0.9f, 0.9f));
            }
            else if (victim != null)
            {
                string sym = victim.El.Sym;
                if (dragged == victim) dragged = null;
                victim.Despawn();
                Recompute();
                Say("Убран атом " + sym + ". Всю зону чистит кнопка «Убрать атомы».", new Color(0.9f, 0.9f, 0.9f));
            }
            else Say("Наведи на атом и нажми Del — уберётся он один.", new Color(0.9f, 0.9f, 0.7f));
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
            Say("Выделено всё: " + Selected.Count + " атомов.", new Color(0.8f, 0.95f, 1f));
        }
    }

    public Atom PickAtom(Vector3 screenPos)
    {
        if (Cam == null) return null;
        RaycastHit hit;
        if (Physics.Raycast(Cam.ScreenPointToRay(screenPos), out hit, 200f))
            return hit.collider.GetComponentInParent<Atom>();
        return null;
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
        if (el.Valence == 0 && !GodMode) Say(el.Name + " — благородный газ: ни с кем не соединяется. Так и в жизни.", new Color(0.7f, 0.9f, 1f));
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
        Say("Выделено атомов: " + Selected.Count + ". Ctrl+C — копировать, Ctrl+V — вставить.",
            new Color(0.8f, 0.95f, 1f));
    }

    public void CopySelection()
    {
        if (Selected.Count == 0) { Say("Сначала выдели атомы рамкой.", new Color(1f, 0.9f, 0.7f)); return; }

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
        Say("Скопировано: " + clipAtoms.Count + " атомов и " + clipBonds.Count + " связей.",
            new Color(0.8f, 0.95f, 1f));
    }

    public void PasteClipboard()
    {
        if (clipAtoms == null || clipAtoms.Count == 0) { Say("Буфер пуст: сначала Ctrl+C.", new Color(1f, 0.9f, 0.7f)); return; }

        Vector3 c = ZoneCenter + new Vector3(Random.Range(-1.5f, 1.5f), Random.Range(-0.8f, 0.8f), Random.Range(-1f, 1f));
        var made = new List<Atom>();
        foreach (var ca in clipAtoms) made.Add(Atom.Spawn(Elements.BySymbol(ca.Sym), c + ca.Rel));
        foreach (var cb in clipBonds) Bond.Create(made[cb.A], made[cb.B], cb.Order);

        Selected.Clear();
        foreach (var a in made) Selected.Add(a);
        Fx.Pop(1.1f);
        Fx.Sparks(c, new Color(0.7f, 0.95f, 1f), 25, 3f);
        Recompute();
        Say("Вставлено: " + made.Count + " атомов. Копия выделена — можно сразу оттащить.",
            new Color(0.8f, 0.95f, 1f));
    }

    /// <summary>Выход из игры. В редакторе Unity Application.Quit ничего не делает, поэтому
    /// там просто говорим об этом вслух, а не делаем вид, что вышли.</summary>
    public void ExitGame()
    {
        Say("Выходим...", Color.white);
        Application.Quit();
    }

    // ==================== склейка ====================

    void FixedUpdate()
    {
        var list = Atom.All;
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.El.Valence == 0 && !GodMode) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                var b = list[j];
                if (b.El.Valence == 0 && !GodMode) continue;
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                // 🔴 21.09. С настоящими радиусами атомы стали вдвое мельче, и прежний
                // множитель 1.25 требовал сводить их почти вплотную — вода переставала
                // собираться вообще (поймала проверка: water=False). Тянемся дальше собственных
                // размеров: связь возникает примерно на двух радиусах.
                float touch = (a.El.Radius + b.El.Radius) * 1.9f;

                var existing = a.BondWith(b);
                if (existing != null)
                {
                    // Сжали связанную пару сильнее длины покоя — поднимаем кратность.
                    if (dist < existing.RestLength * 0.72f && existing.Raise())
                    {
                        changed = true;
                        Say("Связь стала " + (existing.Order == 2 ? "двойной" : "тройной") + ": " + a.El.Sym + (existing.Order == 2 ? "=" : "≡") + b.El.Sym, new Color(0.8f, 1f, 0.8f));
                    }
                    continue;
                }

                if (dist <= touch && a.FreeValence > 0 && b.FreeValence > 0)
                {
                    Bond.Create(a, b);
                    ReactOnBond(a, b);
                    changed = true;
                }
                else if (dist <= touch * 0.95f && (a.FreeValence == 0 || b.FreeValence == 0))
                {
                    // Место кончилось — отталкиваем, чтобы было видно: связей больше нет.
                    Vector3 dir = (a.transform.position - b.transform.position).normalized;
                    a.Body.AddForce(dir * 6f, ForceMode.Acceleration);
                    b.Body.AddForce(-dir * 6f, ForceMode.Acceleration);

                    // 🔴 21.09, владелец: «не хочет стыковаться» — и игра МОЛЧАЛА. Отказ без
                    // причины выглядит как поломка. Теперь говорим, у кого кончились связи.
                    if (Time.time > refusedAt + 2.5f)
                    {
                        refusedAt = Time.time;
                        Atom full = a.FreeValence == 0 ? a : b;
                        Say(full.El.Name + " (" + full.El.Sym + ") уже занят: все " + full.El.Valence +
                            " связи заняты. Shift + ЛКМ по нему — оторвать соседей.",
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

            if (m.Atoms.Count > 2 && IsAlkaliInWater(m)) { Explode(m); continue; }

            if (m.Info != null && m.Atoms.Count > 1 && !Discovered.Contains(m.Formula))
            {
                Discovered.Add(m.Formula);
                Fx.Chime();
                Fx.Flash(m.Center, new Color(0.5f, 1f, 0.6f), 7f, 10f, 0.7f);
                Fx.Sparks(m.Center, new Color(0.55f, 1f, 0.65f), 60, 4.5f, 0.11f);
                Score += 10 + m.Atoms.Count * 2;
                Say("ОТКРЫТО: " + m.Info.Name + " (" + m.Formula + ") — " + m.Info.Note, new Color(0.6f, 1f, 0.6f));
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
        Say(metal.El.Name + " вспыхнул: щелочные металлы соединяются бурно, со светом и жаром.",
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
        Say("ВЗРЫВ: щелочной металл в воде. Так натрий и ведёт себя в стакане — вспышка и разлёт.",
            new Color(1f, 0.6f, 0.4f));
    }

    /// <summary>Есть ли в молекуле щелочной металл вместе с водой (и кислород, и водород).</summary>
    static bool IsAlkaliInWater(Mol m)
    {
        bool alkali = false, o = false, h = false;
        foreach (var a in m.Atoms)
        {
            if (a.El.Class == Elements.Cls.Alkali) alkali = true;
            if (a.El.Sym == "O") o = true;
            if (a.El.Sym == "H") h = true;
        }
        return alkali && o && h;
    }

    void CheckQuests(string formula)
    {
        foreach (var q in Quests.All)
        {
            if (!q.Done && q.Formula == formula)
            {
                q.Done = true;
                Score += q.Reward;
                Say("ЗАДАНИЕ ВЫПОЛНЕНО: " + q.Title + "  +" + q.Reward, new Color(1f, 0.9f, 0.5f));
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
        catch (System.Exception e) { Debug.LogWarning("Не вышло сохранить зону: " + e.Message); }
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
                Say("Зона восстановлена: " + made.Count + " атомов с прошлого раза.", new Color(0.8f, 0.95f, 1f));
            }
        }
        catch (System.Exception e) { Debug.LogWarning("Не вышло прочитать зону: " + e.Message); }
    }

    void OnApplicationQuit() { SaveZone(); SaveProgress(); }

    // ==================== память между запусками ====================

    void SaveProgress()
    {
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
