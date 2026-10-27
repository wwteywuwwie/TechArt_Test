using UnityEngine;

// Мишень B: Ready -> Broken (рассыпается на куски) -> Reassembling (медленно собирается) -> Ready
public class TargetBreakApart : TargetReaction
{
    public enum State { Ready, Broken, Reassembling }

    [Tooltip("Куски мишени. Если пусто, берутся все прямые дочерние объекты")]
    [SerializeField] Transform[] pieces;

    [Header("Broken")]
    [SerializeField] float scatterDistance = 1.6f;   // как далеко разлетаются (м)
    [SerializeField] float scatterHeight = 0.8f;     // высота дуги полёта (м)
    [SerializeField] float groundY = 0.15f;          // на какой высоте (локально) куски лежат
    [SerializeField] float scatterTime = 0.35f;
    [SerializeField] float lieTime = 1.0f;           // сколько лежат до сборки

    [Header("Reassembling")]
    [SerializeField] float assembleTime = 3f;
    [Range(0f, 0.8f)]
    [SerializeField] float stagger = 0.35f;          // разброс старта сборки: нижние куски раньше верхних
    [SerializeField] float liftHeight = 0.4f;        // куски чуть взлетают на пути обратно

    public State Current { get; private set; } = State.Ready;

    Vector3[] homePos, landPos;
    Quaternion[] homeRot, landRot;
    float[] arcHeight, height01;
    float timer;

    void Awake()
    {
        if (pieces == null || pieces.Length == 0)
        {
            pieces = new Transform[transform.childCount];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = transform.GetChild(i);
        }

        int n = pieces.Length;
        homePos = new Vector3[n];
        landPos = new Vector3[n];
        homeRot = new Quaternion[n];
        landRot = new Quaternion[n];
        arcHeight = new float[n];
        height01 = new float[n];

        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < n; i++)
        {
            homePos[i] = pieces[i].localPosition;
            homeRot[i] = pieces[i].localRotation;
            minY = Mathf.Min(minY, homePos[i].y);
            maxY = Mathf.Max(maxY, homePos[i].y);
        }
        for (int i = 0; i < n; i++) height01[i] = Mathf.InverseLerp(minY, maxY, homePos[i].y);
    }

    public override void Play(Vector3 attacker)
    {
        if (Current != State.Ready) return;

        Vector3 away = transform.position - attacker;
        away.y = 0f;
        if (away.sqrMagnitude < 0.0001f) away = transform.forward;
        Vector3 localAway = transform.InverseTransformDirection(away.normalized);

        for (int i = 0; i < pieces.Length; i++)
        {
            Vector3 dir = Quaternion.AngleAxis(Random.Range(-70f, 70f), Vector3.up) * localAway;
            Vector3 p = homePos[i] + dir * (scatterDistance * Random.Range(0.4f, 1f));
            p.y = groundY;
            landPos[i] = p;
            landRot[i] = Random.rotation;
            arcHeight[i] = scatterHeight * Random.Range(0.6f, 1.2f);
        }

        Current = State.Broken;
        timer = 0f;
    }

    void Update()
    {
        if (Current == State.Ready) return;

        timer += Time.deltaTime;

        if (Current == State.Broken)
        {
            float u = Mathf.Clamp01(timer / scatterTime);
            float e = 1f - (1f - u) * (1f - u);
            for (int i = 0; i < pieces.Length; i++)
            {
                Vector3 p = Vector3.Lerp(homePos[i], landPos[i], e);
                p.y += 4f * arcHeight[i] * u * (1f - u);
                pieces[i].localPosition = p;
                pieces[i].localRotation = Quaternion.Slerp(homeRot[i], landRot[i], e);
            }
            if (timer >= scatterTime + lieTime) { Current = State.Reassembling; timer = 0f; }
        }
        else
        {
            float dur = assembleTime * (1f - stagger);
            bool done = true;
            for (int i = 0; i < pieces.Length; i++)
            {
                float delay = height01[i] * stagger * assembleTime;
                float u = Mathf.Clamp01((timer - delay) / dur);
                if (u < 1f) done = false;
                float e = Mathf.SmoothStep(0f, 1f, u);

                Vector3 p = Vector3.Lerp(landPos[i], homePos[i], e);
                p.y += Mathf.Sin(Mathf.PI * e) * liftHeight;
                pieces[i].localPosition = p;
                pieces[i].localRotation = Quaternion.Slerp(landRot[i], homeRot[i], e);
            }
            if (done) Current = State.Ready;
        }
    }
}
