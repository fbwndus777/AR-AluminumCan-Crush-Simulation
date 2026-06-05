using UnityEngine;
using TMPro;

public class SimpleDebugDisplay : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TextMeshProUGUI debugText;
    [SerializeField] private AluminumCanSimulation canSimulation;
    [SerializeField] private float updateInterval = 0.1f;
    private float lastUpdate = 0f;
    private static SimpleDebugDisplay instance;

    private static System.Collections.Generic.Dictionary<string, string> slots
        = new System.Collections.Generic.Dictionary<string, string>();

    void Awake() { instance = this; }

    void Start()
    {
        if (debugText == null)
            debugText = GetComponent<TextMeshProUGUI>();

        if (canSimulation == null)
            canSimulation = FindObjectOfType<AluminumCanSimulation>();
    }

    void Update()
    {
        if (debugText == null) return;
        if (Time.time - lastUpdate < updateInterval) return;
        lastUpdate = Time.time;

        string display = $"FPS: {(1f / Time.deltaTime):F0}\n";

        // Mesh deformation status
        if (canSimulation != null)
        {
            var type = typeof(AluminumCanSimulation);

            var meshField = type.GetField("canMesh",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            if (meshField != null)
            {
                var canMesh = meshField.GetValue(canSimulation);
                if (canMesh != null)
                {
                    // Buckle region count
                    var buckleField = canMesh.GetType().GetField("buckleRegions");
                    var buckles = (System.Collections.IList)buckleField.GetValue(canMesh);
                    display += $"<color=yellow>Buckles: {buckles.Count}</color>\n";

                    // Latest buckle info
                    if (buckles.Count > 0)
                    {
                        var b = buckles[buckles.Count - 1];
                        float d = (float)b.GetType().GetField("depth").GetValue(b);
                        float r = (float)b.GetType().GetField("radius").GetValue(b);
                        Vector3 ep = (Vector3)b.GetType().GetField("epicenterLocal").GetValue(b);
                        bool active = (bool)b.GetType().GetField("isActive").GetValue(b);

                        display += $"depth={d * 1000f:F3}mm\n";
                        display += $"radius={r * 1000f:F1}mm\n";
                        display += $"active={active}\n";
                        display += $"ep=({ep.x:F3},{ep.y:F3},{ep.z:F3})\n";
                    }

                    // Max vertex displacement
                    var origField = canMesh.GetType().GetField("originalVertices");
                    var currField = canMesh.GetType().GetField("currentVertices");

                    if (origField != null && currField != null)
                    {
                        var orig = (Vector3[])origField.GetValue(canMesh);
                        var curr = (Vector3[])currField.GetValue(canMesh);

                        if (orig != null && curr != null && orig.Length > 0)
                        {
                            float maxMove = 0f;
                            for (int i = 0; i < orig.Length; i++)
                            {
                                float move = Vector3.Distance(orig[i], curr[i]);
                                if (move > maxMove) maxMove = move;
                            }

                            display += $"<color={(maxMove > 0.0001f ? "green" : "red")}>MaxMove: {maxMove * 1000f:F4}mm</color>\n";
                        }
                    }
                }
            }

            // Local scale
            display += $"lossyScale: {canSimulation.transform.lossyScale.x:F5}\n";
        }

        // Custom key-value slots
        foreach (var kv in slots)
            display += $"{kv.Key}: {kv.Value}\n";

        debugText.text = display;
    }

    public static void Set(string key, string value)
    {
        slots[key] = value;
    }
}