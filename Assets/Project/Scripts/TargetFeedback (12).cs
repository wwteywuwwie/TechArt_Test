using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// Мишень: Ready -> Hit -> Recover -> Ready. Hit(worldPoint) вызывается из PlayerStrike.
// Тест в Play Mode: клавиша H.
public class TargetFeedback : MonoBehaviour
{
    [Header("Движение мишени")]
    [SerializeField] TargetReaction reaction;       // если пусто, ищется на этом объекте или в дочерних

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
    [Tooltip("Цвет кромки в конце остывания: множители R,G,B от горячего цвета")]
    [SerializeField] Vector3 coolTint = new Vector3(0.6f, 0.05f, 0.02f);
    [Tooltip("Dissolve, до которого пятно сжимается в основную фазу остывания")]
    [SerializeField] float visibleFloorDissolve = 0.35f;
    [Tooltip("Во сколько раз падает яркость к концу основной фазы")]
    [SerializeField] float coolEmissionFactor = 0.3f;
    [Tooltip("Доля времени восстановления на финальное быстрое угасание")]
    [SerializeField] float fadeOutShare = 0.15f;

    Renderer[] rends;                // только те, у кого есть свойства растворения
    MaterialPropertyBlock block;
    int dissolveId, emissionId, edgeColorId, hitPosId, hitRadiusId;
    Color hotColor;
    Vector3[] localHit;              // точка попадания в локальных координатах каждой части
    float[] localRadius;             // радиус пятна в локальных единицах каждой части
    bool[] affected;                 // true только у частей, которых реально задел удар
    Coroutine routine;
    WaitForSeconds holdWait;

    void Awake()
    {
        if (!reaction) reaction = GetComponentInChildren<TargetReaction>();

        block = new MaterialPropertyBlock();
        holdWait = new WaitForSeconds(hitHoldTime);
        dissolveId = Shader.PropertyToID(dissolveProperty);
        emissionId = Shader.PropertyToID(emissionProperty);
        edgeColorId = Shader.PropertyToID(edgeColorProperty);
        hitPosId = Shader.PropertyToID(hitPositionProperty);
        hitRadiusId = Shader.PropertyToID(hitRadiusProperty);

        var found = new List<Renderer>();
        hotColor = new Color(1f, 0.15f, 0.01f);
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            // ищем материал с растворением в любом слоте, а не только в первом
            Material mat = null;
            foreach (var m in r.sharedMaterials)
            {
                if (m != null && m.HasProperty(dissolveId)) { mat = m; break; }
            }
            if (mat == null) continue;
            if (found.Count == 0 && mat.HasProperty(edgeColorId)) hotColor = mat.GetColor(edgeColorId);
            found.Add(r);
        }
        rends = found.ToArray();
        localHit = new Vector3[rends.Length];
        localRadius = new float[rends.Length];
        affected = new bool[rends.Length];
        for (int i = 0; i < localRadius.Length; i++) localRadius[i] = hitRadius;

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

    public void Hit(Vector3 worldPoint) { Hit(worldPoint, worldPoint); }

    // attacker - позиция героя, от него мишень отбрасывает назад
    public void Hit(Vector3 worldPoint, Vector3 attacker)
    {
        for (int i = 0; i < rends.Length; i++)
        {
            // притягиваем точку к самой части, а не к общему Box Collider мишени
            Vector3 p = rends[i].bounds.ClosestPoint(worldPoint);

            // растворяется только та часть, которая лежит в радиусе удара
            affected[i] = (p - worldPoint).sqrMagnitude <= hitRadius * hitRadius;

            // в локальных координатах части, чтобы пятно ехало вместе с ней
            localHit[i] = rends[i].transform.InverseTransformPoint(p);

            // радиус в метрах -> в локальные единицы этой части (учёт Scale)
            Vector3 s = rends[i].transform.lossyScale;
            float avg = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
            localRadius[i] = hitRadius / Mathf.Max(avg, 0.0001f);
        }

        if (reaction) reaction.Play(attacker);

        if (rends.Length == 0) return;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(HitRoutine());
    }

    IEnumerator HitRoutine()
    {
        Color coolColor = new Color(hotColor.r * coolTint.x, hotColor.g * coolTint.y, hotColor.b * coolTint.z, hotColor.a);

        Apply(hitDissolve, hitEmission, hotColor);
        yield return holdWait;

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

        Apply(readyDissolve, readyEmission, hotColor);
        routine = null;
    }

    void Apply(float dissolve, float emission, Color edgeColor)
    {
        for (int i = 0; i < rends.Length; i++)
        {
            var r = rends[i];
            r.GetPropertyBlock(block);
            bool on = affected[i];
            block.SetFloat(dissolveId, on ? dissolve : readyDissolve);
            block.SetFloat(emissionId, on ? emission : readyEmission);
            block.SetColor(edgeColorId, edgeColor);
            block.SetVector(hitPosId, localHit[i]);
            block.SetFloat(hitRadiusId, localRadius[i]);
            r.SetPropertyBlock(block);
        }
    }
}
