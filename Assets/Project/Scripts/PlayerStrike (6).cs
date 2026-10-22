using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Удар по ЛКМ: процедурный замах оружия (без Animator).
/// Попадание определяется по реальному касанию клинка с мишенью,
/// эффект появляется в точке, где клинок входит в поверхность.
/// Вешается на PlayerArmature.
/// </summary>
public class PlayerStrike : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] Transform weaponPivot;          // пустой объект в плече, меч - его дочерний
    [SerializeField] float bladeLength = 1.4f;       // длина клинка от pivot вдоль оси Z
    [SerializeField] float bladeRadius = 0.15f;      // "толщина" для проверки касания
    [SerializeField] Vector3 swingAxis = Vector3.up; // up = горизонтальный взмах, (1,0,0) = сверху вниз
    [SerializeField] float restAngle = 50f;
    [SerializeField] float windupAngle = 90f;        // замах вправо
    [SerializeField] float strikeAngle = -50f;       // рубит справа налево
    [SerializeField] float windupTime = 0.12f;
    [SerializeField] float swingTime = 0.16f;
    [SerializeField] float recoverTime = 0.22f;

    [Header("Попадание")]
    [SerializeField] LayerMask hitMask = ~0;         // выбрать слой Target

    [Header("Эффект контакта (можно пусто)")]
    [SerializeField] GameObject impactVfxPrefab;
    [SerializeField] float vfxLifetime = 2f;

    const int SamplePoints = 6;
    readonly Collider[] overlapBuffer = new Collider[8];   // без аллокаций (нет мусора для GC)
    bool swinging;
    bool hitThisSwing;

    void Start() => SetAngle(restAngle);

    void Update()
    {
        if (swinging) return;
#if ENABLE_INPUT_SYSTEM
        bool pressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
#else
        bool pressed = Input.GetMouseButtonDown(0);
#endif
        if (pressed) StartCoroutine(SwingRoutine());
    }

    IEnumerator SwingRoutine()
    {
        swinging = true;
        hitThisSwing = false;

        yield return Rotate(restAngle, windupAngle, windupTime, false);   // замах
        yield return Rotate(windupAngle, strikeAngle, swingTime, true);   // удар: проверяем касание
        yield return Rotate(strikeAngle, restAngle, recoverTime, false);  // возврат

        swinging = false;
    }

    IEnumerator Rotate(float from, float to, float time, bool checkContact)
    {
        for (float t = 0; t < time; t += Time.deltaTime)
        {
            SetAngle(Mathf.Lerp(from, to, t / time));
            if (checkContact && !hitThisSwing) hitThisSwing = TryContact();
            yield return null;
        }
        SetAngle(to);
        if (checkContact && !hitThisSwing) hitThisSwing = TryContact();
    }

    void SetAngle(float angle)
    {
        if (weaponPivot) weaponPivot.localRotation = Quaternion.AngleAxis(angle, swingAxis);
    }

    bool TryContact()
    {
        if (!weaponPivot) return false;

        Vector3 a = weaponPivot.position;
        Vector3 b = weaponPivot.TransformPoint(0f, 0f, bladeLength);

        int count = Physics.OverlapCapsuleNonAlloc(a, b, bladeRadius, overlapBuffer, hitMask);
        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            var target = col.GetComponentInParent<TargetFeedback>();
            if (target == null) continue;

            Vector3 contact;
            Vector3 normal;

            // Основной способ: точка, где клинок (луч от pivot к кончику) входит в поверхность
            Vector3 dir = (b - a).normalized;
            if (col.Raycast(new Ray(a, dir), out RaycastHit surface, bladeLength + bladeRadius))
            {
                contact = surface.point;
                normal = surface.normal;
            }
            else
            {
                // Запасной способ (касание боком клинка): ближайшая точка на мишени
                contact = col.ClosestPoint(b);
                float best = float.MaxValue;
                for (int s = 0; s <= SamplePoints; s++)
                {
                    Vector3 p = Vector3.Lerp(a, b, s / (float)SamplePoints);
                    Vector3 cp = col.ClosestPoint(p);
                    float d = (cp - p).sqrMagnitude;
                    if (d < best) { best = d; contact = cp; }
                }
                Vector3 c = col.bounds.center;
                normal = contact - new Vector3(c.x, contact.y, c.z);
                normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -transform.forward;
            }

            // Подтягиваем контакт на видимую поверхность: луч от груди героя к точке контакта
            Vector3 chest = transform.position + Vector3.up * 1.2f;
            Vector3 toContact = contact - chest;
            if (toContact.sqrMagnitude > 0.0001f &&
                col.Raycast(new Ray(chest, toContact.normalized), out RaycastHit snap, toContact.magnitude + 1f))
            {
                contact = snap.point;
                normal = snap.normal;
            }

            target.Hit(contact);

            if (impactVfxPrefab)
            {
                var vfx = Instantiate(impactVfxPrefab, contact, Quaternion.LookRotation(normal));
                Destroy(vfx, vfxLifetime);
            }
            return true;
        }
        return false;
    }
}
