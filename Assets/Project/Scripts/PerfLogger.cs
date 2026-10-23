using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// «амер производительности: F9 запускает запись кадров на заданное врем€,
/// затем пишет медиану, среднее, 1% low и сборки мусора в Console и perf.csv.
public class PerfLogger : MonoBehaviour
{
    [SerializeField] string scenarioName = "Gameplay";
    [SerializeField] float duration = 30f;

    readonly List<float> frames = new List<float>(20000);
    bool recording;
    float elapsed;
    int gcStart;
    long memStart;
    string lastResult = "F9 - старт замера";

    void Awake()
    {
        QualitySettings.vSyncCount = 0;      // VSync выключен по “«
        Application.targetFrameRate = -1;    // без ограничени€ FPS
    }

    void Update()
    {
        if (KeyPressed() && !recording) Begin();
        if (!recording) return;

        frames.Add(Time.unscaledDeltaTime);
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration) Finish();
    }

    static bool KeyPressed()
    {
#if ENABLE_INPUT_SYSTEM
        return Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame;
#else
        return Input.GetKeyDown(KeyCode.F9);
#endif
    }

    void Begin()
    {
        frames.Clear();
        elapsed = 0f;
        gcStart = GC.CollectionCount(0);
        memStart = GC.GetTotalMemory(false);
        recording = true;
    }

    void Finish()
    {
        recording = false;
        frames.Sort();
        float median = frames[frames.Count / 2];
        float p99 = frames[Mathf.Min(frames.Count - 1, (int)(frames.Count * 0.99f))];
        float worst = frames[frames.Count - 1];
        float avgFps = frames.Count / elapsed;
        int gcCount = GC.CollectionCount(0) - gcStart;
        float memDeltaMb = (GC.GetTotalMemory(false) - memStart) / (1024f * 1024f);

        lastResult = $"{scenarioName}: median {1f / median:F0} FPS | avg {avgFps:F0} | " +
                     $"1% low {1f / p99:F0} | min {1f / worst:F0} | GC {gcCount} | mem {memDeltaMb:+0.0;-0.0} MB";
        Debug.Log(lastResult);

        string path = Path.Combine(Application.persistentDataPath, "perf.csv");
        File.AppendAllText(path, lastResult + Environment.NewLine);
    }

    void OnGUI()
    {
        if (recording) return;               // во врем€ замера ничего не рисуем
        GUI.Label(new Rect(10, 10, 900, 25), lastResult);
    }
}