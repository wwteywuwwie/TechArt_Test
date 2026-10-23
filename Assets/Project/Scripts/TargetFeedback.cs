using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Мишень: Ready -> Hit (пятно вокруг точки контакта) -> Recover (пятно зарастает,
/// кромка остывает: яркий оранжевый -> тёмно-красный) -> Ready.
/// Hit(worldPoint) вызывается из PlayerStrike. Тест в Play Mode: клавиша H.
/// Работает через MaterialPropertyBlock (без копий материала).
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TargetFeedback : MonoBehaviour
{
    [Header("Имена свойств шейдера (Reference в Blackboard)")]
    [SerializeField] string dissolveProperty = "_Dissolve_Amount";
    [SerializeField] string emissionProperty = "_Emission_Intensity";
    [SerializeField] string edgeColorProperty = "_Edge_Emission_Color";
    [SerializeField] string hitPositionProperty = "_Hit_Position";
    [SerializeField] string hitRadiusProperty = "_Hit_Radius";

    [Header("Ready (целая)")]
    [SerializeField] float readyDissolve = 0f;
    [SerializeField] float readyEmission = 0f;

    [Header("Hit (попадание)")]
    [SerializeField] float hitDissolve = 0.9f;
    [SerializeField] float hitEmission = 2000f;
    [SerializeField] float hitRadius = 0.6f;        // радиус пятна в метрах
    [SerializeField] float hitHoldTime = 0.8f;

    [Header("Recover (остывание)")]
    [SerializeField] float recoverTime = 4f;
    [Tooltip("Цвет кромки в конце остывания: множители R,G,B от горячего цвета (красный остаётся)")]
    [SerializeField] Vector3 coolTint = new Vector3(0.6f, 0.05f, 0.02f);
    [Tooltip("Dissolve, до которого пятно сжимается в основную фазу остывания (ниже ~0.3 пятна уже не видно)")]
    [SerializeField] float visibleFloorDissolve = 0.35f;
    [Tooltip("Во сколько раз падает яркость к концу основной фазы")]
    [SerializeField] float coolEmissionFactor = 0.3f;
    [Tooltip("Доля времени восстановления на финальное быстрое угасание")]
    [SerializeField] float fadeOutShare = 0.15f;

    Renderer rend;
    MaterialPropertyBlock block;
    int dissolveId, emissionId, edgeColorId, hitPosId, hitRadiusId;
    Color hotColor;                 // берётся из материала (Edge Emission Color)
    Vector3 lastHitPoint;
    Coroutine routine;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        dissolveId = Shader.PropertyToID(dissolveProperty);
        emissionId = Shader.PropertyToID(emissionProperty);
        edgeColorId = Shader.PropertyToID(edgeColorProperty);
        hitPosId = Shader.PropertyToID(hitPositionProperty);
        hitRadiusId = Shader.PropertyToID(hitRadiusProperty);

        var mat = rend.sharedMaterial;
        hotColor = (mat != null && mat.HasProperty(edgeColorId)) ? mat.GetColor(edgeColorId) : new Color(1f, 0.15f, 0.01f);

        Apply(readyDissolve, readyEmission, hotColor);
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame)
            Hit(transform.position + new Vector3(0f, 0.5f, -0.4f));
#else
        if (Input.GetKeyDown(KeyCode.H)) Hit(transform.position + new Vector3(0f, 0.5f, -0.4f));
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
        Color coolColor = new Color(hotColor.r * coolTint.x, hotColor.g * coolTint.y, hotColor.b * coolTint.z, hotColor.a);

        // 2. Удар: яркое горячее пятно
        Apply(hitDissolve, hitEmission, hotColor);
        yield return new WaitForSeconds(hitHoldTime);

        // 3. Восстановление: пятно медленно сжимается, кромка остывает, в конце быстро гаснет
        for (float t = 0; t < recoverTime; t += Time.deltaTime)
        {
            float u = t / recoverTime;
            float cool = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u / (1f - fadeOutShare)));
            float fade = Mathf.Clamp01((u - (1f - fadeOutShare)) / fadeOutShare);

            float dissolve = Mathf.Lerp(Mathf.Lerp(hitDissolve, visibleFloorDissolve, cool), readyDissolve, fade);
            float emission = Mathf.Lerp(Mathf.Lerp(hitEmission, hitEmission * coolEmissionFactor, cool), readyEmission, fade);
            Apply(dissolve, emission, Color.Lerp(hotColor, coolColor, cool));
            yield return null;
        }

        // 1. Готова
        Apply(readyDissolve, readyEmission, hotColor);
        routine = null;
    }

    void Apply(float dissolve, float emission, Color edgeColor)
    {
        rend.GetPropertyBlock(block);
        block.SetFloat(dissolveId, dissolve);
        block.SetFloat(emissionId, emission);
        block.SetColor(edgeColorId, edgeColor);
        block.SetVector(hitPosId, lastHitPoint);
        block.SetFloat(hitRadiusId, hitRadius);
        rend.SetPropertyBlock(block);
    }
}
