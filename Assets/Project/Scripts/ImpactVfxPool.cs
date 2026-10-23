using UnityEngine;

/// <summary>
/// Пул эффектов контакта: фиксированное число экземпляров создаётся один раз,
/// дальше они переиспользуются по кругу. Нет Instantiate/Destroy во время игры
/// и нет бесконечного роста числа объектов.
/// </summary>
public class ImpactVfxPool : MonoBehaviour
{
    [SerializeField] GameObject prefab;
    [SerializeField] int poolSize = 6;
    [SerializeField] float lifetime = 1.5f;      // через сколько секунд экземпляр выключается

    GameObject[] items;
    ParticleSystem[][] systems;
    float[] releaseTime;
    int next;

    void Awake()
    {
        items = new GameObject[poolSize];
        systems = new ParticleSystem[poolSize][];
        releaseTime = new float[poolSize];

        for (int i = 0; i < poolSize; i++)
        {
            items[i] = Instantiate(prefab, transform);
            items[i].name = prefab.name + "_Pooled_" + i;
            systems[i] = items[i].GetComponentsInChildren<ParticleSystem>(true);
            items[i].SetActive(false);
        }
    }

    public void Play(Vector3 position, Quaternion rotation)
    {
        int i = next;
        next = (next + 1) % poolSize;

        GameObject item = items[i];
        item.SetActive(false);
        item.transform.SetPositionAndRotation(position, rotation);
        item.SetActive(true);

        var list = systems[i];
        for (int s = 0; s < list.Length; s++)
        {
            list[s].Clear(true);
            list[s].Play(true);
        }
        releaseTime[i] = Time.time + lifetime;
    }

    void Update()
    {
        for (int i = 0; i < poolSize; i++)
        {
            if (items[i].activeSelf && Time.time >= releaseTime[i])
                items[i].SetActive(false);
        }
    }
}
