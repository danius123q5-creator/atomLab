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

    public static readonly Vector3 ZoneCenter = new Vector3(0f, 1.6f, 0f);
    public static readonly Vector3 ZoneHalf = new Vector3(5.0f, 3.0f, 4.0f);

    // ——— камера ———
    public Camera Cam;
    float camYaw = 20f, camPitch = 14f, camDist = 14f;
    Vector3 camTarget = ZoneCenter;

    // ——— перетаскивание атома мышью/пальцем ———
    Atom dragged;
    float dragDepth;

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
        }
        if (Input.GetMouseButtonUp(0)) dragged = null;

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

        if (Input.GetKeyDown(KeyCode.Delete)) ClearZone();
        if (Input.GetKeyDown(KeyCode.Escape) && LabUI.I != null) LabUI.I.TogglePanel();
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
        if (el.Valence == 0) Say(el.Name + " — благородный газ: ни с кем не соединяется. Так и в жизни.", new Color(0.7f, 0.9f, 1f));
        Recompute();
        return a;
    }

    public void ClearZone()
    {
        for (int i = Atom.All.Count - 1; i >= 0; i--) Atom.All[i].Despawn();
        Recompute();
    }

    // ==================== склейка ====================

    void FixedUpdate()
    {
        var list = Atom.All;
        bool changed = false;
        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a.El.Valence == 0) continue;
            for (int j = i + 1; j < list.Count; j++)
            {
                var b = list[j];
                if (b.El.Valence == 0) continue;
                float dist = Vector3.Distance(a.transform.position, b.transform.position);
                float touch = (a.El.Radius + b.El.Radius) * 1.25f;

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
            m.Formula = Molecules.Formula(counts);
            m.Info = Molecules.Lookup(m.Formula);
            m.Center = sum / m.Atoms.Count;
            m.FreeLeft = free;
            Mols.Add(m);

            if (m.Info != null && m.Atoms.Count > 1 && !Discovered.Contains(m.Formula))
            {
                Discovered.Add(m.Formula);
                Score += 10 + m.Atoms.Count * 2;
                Say("ОТКРЫТО: " + m.Info.Name + " (" + m.Formula + ") — " + m.Info.Note, new Color(0.6f, 1f, 0.6f));
                CheckQuests(m.Formula);
                SaveProgress();
            }
        }
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

    /// <summary>Сброс журнала — по кнопке в панели, с подтверждением.</summary>
    public void ResetProgress()
    {
        Discovered.Clear();
        Score = 0;
        foreach (var q in Quests.All) q.Done = false;
        SaveProgress();
        Say("Журнал открытий очищен.", new Color(1f, 0.8f, 0.8f));
    }
}
