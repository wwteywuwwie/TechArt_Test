using UnityEngine;

// Мишень: Ready -> Hit (сильный отброс назад) -> Recovering (медленное покачивание в разные стороны) -> Ready
public class TargetKnockback : MonoBehaviour
{
    public enum State { Ready, Hit, Recovering }

    [SerializeField] Transform visual;        // pivot у основания мишени

    [Header("Hit")]
    [SerializeField] float hitAngle = 45f;    // градусы, наклон назад от героя
    [SerializeField] float hitTime = 0.12f;   // сек

    [Header("Recovering")]
    [SerializeField] float recoverTime = 5f;
    [SerializeField] float swayFrequency = 0.5f;     // качание вперёд-назад, Гц
    [SerializeField] float swayDecay = 0.8f;         // затухание
    [SerializeField] [Range(0.1f, 0.8f)] float settleShare = 0.4f;   // доля времени на плавную остановку в конце
    [SerializeField] float sideAmount = 0.6f;        // боковое качание, доля от hitAngle
    [SerializeField] float sideFrequencyMult = 1.35f;

    public State Current { get; private set; } = State.Ready;

    Quaternion restLocal;
    Quaternion tilt = Quaternion.identity;
    Quaternion fromTilt = Quaternion.identity;
    Quaternion toTilt = Quaternion.identity;
    Vector3 backAxis, sideAxis;               // в пространстве родителя
    float timer;

    void Awake()
    {
        if (!visual) visual = transform;
        restLocal = visual.localRotation;
    }

    // attacker - позиция героя: мишень отбрасывает строго от него
    public void Play(Vector3 attacker)
    {
        Vector3 away = transform.position - attacker;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = transform.forward;
        away.Normalize();

        Vector3 worldBack = Vector3.Cross(Vector3.up, away);   // наклон верха от героя
        Vector3 worldSide = away;                              // наклон вбок
        if (visual.parent)
        {
            backAxis = visual.parent.InverseTransformDirection(worldBack);
            sideAxis = visual.parent.InverseTransformDirection(worldSide);
        }
        else
        {
            backAxis = worldBack;
            sideAxis = worldSide;
        }

        fromTilt = tilt;
        toTilt = Quaternion.AngleAxis(hitAngle, backAxis);
        Current = State.Hit;
        timer = 0f;
    }

    void Update()
    {
        if (Current == State.Ready) return;

        timer += Time.deltaTime;

        if (Current == State.Hit)
        {
            float k = Mathf.Clamp01(timer / hitTime);
            k = 1f - (1f - k) * (1f - k);
            tilt = Quaternion.Slerp(fromTilt, toTilt, k);
            if (timer >= hitTime) { Current = State.Recovering; timer = 0f; }
        }
        else
        {
            float u = timer / recoverTime;
            float settle = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - settleShare, 1f, u));
            float env = Mathf.Exp(-swayDecay * timer) * settle;   // к концу амплитуда плавно доходит до нуля
            float w = 2f * Mathf.PI * swayFrequency;
            float back = hitAngle * env * Mathf.Cos(w * timer);
            float side = hitAngle * sideAmount * env * Mathf.Sin(w * sideFrequencyMult * timer);
            tilt = Quaternion.AngleAxis(back, backAxis) * Quaternion.AngleAxis(side, sideAxis);
            if (timer >= recoverTime) { Current = State.Ready; tilt = Quaternion.identity; }
        }

        visual.localRotation = tilt * restLocal;
    }
}
