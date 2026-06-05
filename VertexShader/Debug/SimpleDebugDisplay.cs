using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class SimpleDebugDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI debugText;

    [SerializeField] private float updateInterval = 0.1f;
    private float lastUpdate = 0f;

    private static SimpleDebugDisplay instance;
    private static Dictionary<string, string> slots = new Dictionary<string, string>();

    void Awake()
    {
        instance = this;
    }

    void Start()
    {
        if (debugText == null)
            debugText = GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (debugText == null) return;
        if (Time.time - lastUpdate < updateInterval) return;
        lastUpdate = Time.time;

        string display = $"FPS: {(1f / Time.deltaTime):F0}\n";
        foreach (var kv in slots)
            display += $"{kv.Key}: {kv.Value}\n";

        debugText.text = display;
    }

    public static void Set(string key, string value)
    {
        slots[key] = value;
    }
}