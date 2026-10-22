using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Мишень: Ready -> Hit (локально вокруг точки контакта) -> плавное восстановление -> Ready.
/// Hit(worldPoint) вызывается из PlayerStrike. Тест в Play Mode: клавиша H.
/// Работает через MaterialPropertyBlock (без копий материала).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TargetFeedback : MonoBehaviour
{
    [Header("Имена свойств шейдера (Reference в Blackboard)")]
    [SerializeField] string dissolveProperty = "_Dissolve_Amount";
    [SerializeField] string emissionProperty = "_Emission_Intensity";
    [SerializeField] string hitPositionProperty = "_Hit_Position";
    [SerializeField] string hitRadiusProperty = "_Hit_Radius";

    [Header("Ready (целая)")]
    [SerializeField] float readyDissolve = 0f;
    [SerializeField] float readyEmission = 0f;

    [Header("Hit (попадание)")]
    [SerializeField] float hitDissolve = 0.35f;
    [SerializeField] float hitEmission = 2000f;
    [SerializeField] float hitRadius = 0.45f;       // радиус пятна в метрах
    [SerializeField] float hitHoldTime = 0.4f;

    [Header("Восстановление")]
    [SerializeField] float recoverTime = 1.5f;

    Renderer rend;
    MaterialPropertyBlock block;
    int dissolveId, emissionId, hitPosId, hitRadiusId;
    Vector3 lastHitPoint;
    Coroutine routine;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        dissolveId = Shader.PropertyToID(dissolveProperty);
        emissionId = Shader.PropertyToID(emissionProperty);
        hitPosId = Shader.PropertyToID(hitPositionProperty);
        hitRadiusId = Shader.PropertyToID(hitRadiusProperty);
        Apply(readyDissolve, readyEmission);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            Hit(transform.position + Vector3.up * 0.5f);
#else
        if (Input.GetKeyDown(KeyCode.H)) Hit(transform.position + Vector3.up * 0.5f);
#endif
    }

    public void Hit(Vector3 worldPoint)
    {
        lastHitPoint = worldPoint;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(HitRoutine());
    }

    IEnumerator HitRoutine()
    {
        Apply(hitDissolve, hitEmission);
        yield return new WaitForSeconds(hitHoldTime);

        for (float t = 0; t < recoverTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / recoverTime);
            Apply(Mathf.Lerp(hitDissolve, readyDissolve, k),
                  Mathf.Lerp(hitEmission, readyEmission, k));
            yield return null;
        }
        Apply(readyDissolve, readyEmission);
        routine = null;
    }

    void Apply(float dissolve, float emission)
    {
        rend.GetPropertyBlock(block);
        block.SetFloat(dissolveId, dissolve);
        block.SetFloat(emissionId, emission);
        block.SetVector(hitPosId, lastHitPoint);
        block.SetFloat(hitRadiusId, hitRadius);
        rend.SetPropertyBlock(block);
    }
}
