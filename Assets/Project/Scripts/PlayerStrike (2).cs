using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Удар по ЛКМ: процедурный замах оружия (без Animator).
/// Попадание определяется по реальному касанию клинка с мишенью,
/// эффект появляется в точке касания. Вешается на PlayerArmature.
/// </summary>
public class PlayerStrike : MonoBehaviour
{
    [Header("Оружие")]
    [SerializeField] Transform weaponPivot;          // пустой объект в плече, меч - его дочерний
    [SerializeField] float bladeLength = 1f;         // длина клинка от pivot вдоль оси Z
    [SerializeField] float bladeRadius = 0.08f;      // "толщина" для проверки касания
    [SerializeField] float restAngle = 20f;
    [SerializeField] float windupAngle = -80f;
    [SerializeField] float strikeAngle = 60f;
    [SerializeField] float windupTime = 0.10f;
    [SerializeField] float swingTime = 0.12f;
    [SerializeField] float recoverTime = 0.20f;

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
        if (weaponPivot) weaponPivot.localRotation = Quaternion.Euler(angle, 0f, 0f);
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

            // Ближайшая к клинку точка на мишени = место контакта
            Vector3 contact = col.ClosestPoint(b);
            float best = float.MaxValue;
            for (int s = 0; s <= SamplePoints; s++)
            {
                Vector3 p = Vector3.Lerp(a, b, s / (float)SamplePoints);
                Vector3 cp = col.ClosestPoint(p);
                float d = (cp - p).sqrMagnitude;
                if (d < best) { best = d; contact = cp; }
            }

            // Нормаль для цилиндра: от оси мишени к точке контакта
            Vector3 c = col.bounds.center;
            Vector3 normal = contact - new Vector3(c.x, contact.y, c.z);
            normal = normal.sqrMagnitude > 0.0001f ? normal.normalized : -transform.forward;

            target.Hit();

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
