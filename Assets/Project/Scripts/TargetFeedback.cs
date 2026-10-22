using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Состояния мишени: Ready -> Hit -> Recover (плавно) -> вспышка -> Ready.
/// Работает через MaterialPropertyBlock, поэтому не создаёт копий материала.
/// Вызов из других скриптов: GetComponent<TargetFeedback>().Hit();
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TargetFeedback : MonoBehaviour
{
    [Header("Имена свойств (Reference из Blackboard Shader Graph)")]
    [SerializeField] string dissolveProperty = "_Dissolve_Amount";
    [SerializeField] string emissionProperty = "_Emission_Intensity";

    [Header("Состояние Ready")]
    [SerializeField] float readyDissolve = 0f;
    [SerializeField] float readyEmission = 0f;

    [Header("Состояние Hit")]
    [SerializeField] float hitDissolve = 0.25f;
    [SerializeField] float hitEmission = 2000f;
    [SerializeField] float hitHoldTime = 0.6f;   // сколько держим Hit до восстановления

    [Header("Восстановление")]
    [SerializeField] float recoverTime = 1.5f;
    [SerializeField] AnimationCurve recoverCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Вспышка в конце")]
    [SerializeField] float flashDissolve = 0.06f; // при 0 кромка почти не видна, нужен небольшой порог
    [SerializeField] float flashEmission = 1500f;
    [SerializeField] float flashTime = 0.15f;

    Renderer rend;
    MaterialPropertyBlock block;
    int dissolveId, emissionId;
    Coroutine routine;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        dissolveId = Shader.PropertyToID(dissolveProperty);
        emissionId = Shader.PropertyToID(emissionProperty);
        Apply(readyDissolve, readyEmission);
    }

    void Update()
    {
        // Быстрый тест в Play Mode: клавиша H
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.hKey.wasPressedThisFrame) Hit();
#else
        if (Input.GetKeyDown(KeyCode.H)) Hit();
#endif
    }

    [ContextMenu("Hit")]
    public void Hit()
    {
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(HitRoutine());
    }

    IEnumerator HitRoutine()
    {
        // 1. Hit
        Apply(hitDissolve, hitEmission);
        yield return new WaitForSeconds(hitHoldTime);

        // 2. Recover: плавно Hit -> Ready
        for (float t = 0; t < recoverTime; t += Time.deltaTime)
        {
            float k = recoverCurve.Evaluate(t / recoverTime);
            Apply(Mathf.Lerp(hitDissolve, readyDissolve, k),
                  Mathf.Lerp(hitEmission, readyEmission, k));
            yield return null;
        }

        // 3. Короткая вспышка
        Apply(flashDissolve, flashEmission);
        yield return new WaitForSeconds(flashTime);

        // 4. Ready
        Apply(readyDissolve, readyEmission);
        routine = null;
    }

    void Apply(float dissolve, float emission)
    {
        rend.GetPropertyBlock(block);
        block.SetFloat(dissolveId, dissolve);
        block.SetFloat(emissionId, emission);
        rend.SetPropertyBlock(block);
    }
}
