using System.Collections;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Мишень: Ready -> Hit -> плавное восстановление -> вспышка света -> Ready.
/// Тест в Play Mode: клавиша H.
/// </summary>
[RequireComponent(typeof(Renderer))]
public class TargetFeedback : MonoBehaviour
{
    [Header("Имена свойств шейдера")]
    [SerializeField] string dissolveProperty = "_Dissolve_Amount";
    [SerializeField] string emissionProperty = "_Emission_Intensity";

    [Header("Ready (целая)")]
    [SerializeField] float readyDissolve = 0f;
    [SerializeField] float readyEmission = 0f;

    [Header("Hit (попадание)")]
    [SerializeField] float hitDissolve = 0.26f;
    [SerializeField] float hitEmission = 2000f;
    [SerializeField] float hitHoldTime = 0.6f;

    [Header("Восстановление")]
    [SerializeField] float recoverTime = 1.5f;

    [Header("Вспышка в конце (свет)")]
    [SerializeField] Light flashLight;      // сюда перетащить Flash_Light
    [SerializeField] float flashTime = 0.2f;

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
        if (flashLight) flashLight.enabled = false;
    }

    void Update()
    {
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
        if (flashLight) flashLight.enabled = false;

        // 1. Hit
        Apply(hitDissolve, hitEmission);
        yield return new WaitForSeconds(hitHoldTime);

        // 2. Плавное восстановление
        for (float t = 0; t < recoverTime; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / recoverTime);
            Apply(Mathf.Lerp(hitDissolve, readyDissolve, k),
                  Mathf.Lerp(hitEmission, readyEmission, k));
            yield return null;
        }
        Apply(readyDissolve, readyEmission);

        // 3. Вспышка: короткий яркий свет на целой мишени
        if (flashLight) flashLight.enabled = true;
        yield return new WaitForSeconds(flashTime);
        if (flashLight) flashLight.enabled = false;

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
