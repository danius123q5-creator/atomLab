using System.Collections.Generic;
using UnityEngine;

/// <summary>Связь между двумя атомами: палочка (или две/три — двойная и тройная связь) плюс
/// пружина, которая держит атомы на нужном расстоянии.
///
/// Тип связи определяем по разнице электроотрицательностей, как в школьном правиле:
/// больше 1.7 — ионная (электрон отобран, красим в жёлтый), меньше — ковалентная (общий).</summary>
public class Bond : MonoBehaviour
{
    public Atom A, B;
    public int Order = 1;                 // 1 одинарная, 2 двойная, 3 тройная
    public bool Ionic;
    Transform[] sticks;

    public static readonly List<Bond> All = new List<Bond>();

    public Atom Other(Atom a) { return a == A ? B : A; }
    public float RestLength { get { return (A.El.Radius + B.El.Radius) * 1.45f; } }

    public static Bond Create(Atom a, Atom b)
    {
        var go = new GameObject("Bond_" + a.El.Sym + "-" + b.El.Sym);
        var bo = go.AddComponent<Bond>();
        bo.A = a; bo.B = b;
        bo.Ionic = (a.El.EN > 0f && b.El.EN > 0f) && Mathf.Abs(a.El.EN - b.El.EN) >= 1.7f;
        a.Bonds.Add(bo); b.Bonds.Add(bo);
        All.Add(bo);
        bo.Rebuild();

        // Отклик: щелчок тем ниже, чем тяжелее пара, и щепоть искр в месте стыка.
        Vector3 mid = (a.transform.position + b.transform.position) * 0.5f;
        float heavy = Mathf.Clamp01((a.El.Mass + b.El.Mass) / 300f);
        Fx.Pop(Mathf.Lerp(1.25f, 0.65f, heavy));
        Fx.Sparks(mid, bo.Ionic ? new Color(1f, 0.8f, 0.3f) : new Color(0.85f, 0.95f, 1f), 14, 2.2f);
        Fx.Flash(mid, bo.Ionic ? new Color(1f, 0.8f, 0.3f) : Color.white, 2.2f, 4f, 0.22f);
        return bo;
    }

    /// <summary>Повысить кратность: так одинарная O-O становится двойной O=O.</summary>
    public bool Raise()
    {
        if (Order >= 3) return false;
        if (A.FreeValence < 1 || B.FreeValence < 1) return false;
        Order++;
        Rebuild();
        Vector3 m = (A.transform.position + B.transform.position) * 0.5f;
        Fx.Pop(1.45f);
        Fx.Sparks(m, new Color(0.7f, 1f, 0.9f), 20, 3f);
        Fx.Flash(m, new Color(0.7f, 1f, 0.9f), 3f, 4.5f, 0.25f);
        return true;
    }

    void Rebuild()
    {
        if (sticks != null) foreach (var s in sticks) if (s) Destroy(s.gameObject);
        sticks = new Transform[Order];
        var mat = Ionic ? LabMaterials.BondIonic() : LabMaterials.BondCovalent();
        for (int i = 0; i < Order; i++)
        {
            var c = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(c.GetComponent<Collider>());     // палочка — только вид, физику ведут шарики
            c.transform.SetParent(transform, false);
            c.GetComponent<Renderer>().sharedMaterial = mat;
            sticks[i] = c.transform;
        }
    }

    public void Break()
    {
        A.Bonds.Remove(this); B.Bonds.Remove(this);
        All.Remove(this);
        Destroy(gameObject);
    }

    void FixedUpdate()
    {
        if (A == null || B == null) { Break(); return; }

        // Пружина: тянет к длине покоя, тем жёстче, чем больше кратность.
        Vector3 d = B.transform.position - A.transform.position;
        float len = d.magnitude;
        if (len < 1e-4f) return;
        Vector3 dir = d / len;
        float k = 18f * Order;
        Vector3 f = dir * (len - RestLength) * k;
        A.Body.AddForce(f, ForceMode.Acceleration);
        B.Body.AddForce(-f, ForceMode.Acceleration);
    }

    void LateUpdate()
    {
        if (A == null || B == null || sticks == null) return;
        Vector3 pa = A.transform.position, pb = B.transform.position;
        Vector3 mid = (pa + pb) * 0.5f;
        Vector3 d = pb - pa;
        float len = d.magnitude;
        if (len < 1e-4f) return;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, d / len);

        // Кратные связи разводим в стороны перпендикулярно палочке, чтобы были видны обе.
        Vector3 side = Vector3.Cross(d / len, Camera.main ? Camera.main.transform.forward : Vector3.forward);
        if (side.sqrMagnitude < 1e-4f) side = Vector3.right; else side.Normalize();
        float gap = 0.11f;

        for (int i = 0; i < sticks.Length; i++)
        {
            float off = (i - (Order - 1) * 0.5f) * gap;
            var t = sticks[i];
            t.position = mid + side * off;
            t.rotation = rot;
            t.localScale = new Vector3(0.085f, len * 0.5f, 0.085f);   // цилиндр Unity высотой 2
        }
    }
}
