using System.Collections;
using UnityEngine;

public class CanResetManager : MonoBehaviour
{
    [Header("Initial State")]
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private Vector3 initialScale;
    private Transform initialParent;

    [Header("References")]
    private Rigidbody canRigidbody;
    private CanGrabSystem canGrabSystem;
    private AluminumCanSimulation canSimulation;

    [Header("Reset Tuning")]
    [Tooltip("Time in seconds to freeze physics during reset")]
    public float freezeSeconds = 0.05f;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private bool isResetting;

    void Start()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        initialScale = transform.localScale;
        initialParent = transform.parent;

        canRigidbody = GetComponent<Rigidbody>();
        canGrabSystem = GetComponent<CanGrabSystem>();
        canSimulation = GetComponent<AluminumCanSimulation>();

        if (showDebugInfo)
            Debug.Log("[CanReset] Initial state saved");
    }

    // Called by UI button onClick
    public void ResetCan()
    {
        if (isResetting) return;
        StartCoroutine(ResetRoutine());
    }

    private IEnumerator ResetRoutine()
    {
        isResetting = true;

        if (showDebugInfo)
            Debug.Log("[CanReset] Reset start");

        // 0) Detach from hand if currently grabbed
        transform.SetParent(initialParent, worldPositionStays: true);

        // 1) Stop physics and temporarily disable it to prevent overwrite during roll
        if (canRigidbody != null)
        {
            canRigidbody.velocity = Vector3.zero;
            canRigidbody.angularVelocity = Vector3.zero;
            canRigidbody.isKinematic = true;
            canRigidbody.useGravity = false;
        }

        // 2) Wait one frame for physics tick and transform to stabilize
        yield return null;

        // 3) Restore position, rotation, scale
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        transform.localScale = initialScale;

        // 4) Reset grab state
        if (canGrabSystem != null)
            ResetGrabStateLegacy();

        // 5) Reset mesh deformation via reflection
        ResetMeshDeformationByReflection();

        // 6) Stabilization delay
        if (freezeSeconds > 0f)
            yield return new WaitForSeconds(freezeSeconds);

        // 7) Re-enable physics
        if (canRigidbody != null)
        {
            canRigidbody.isKinematic = false;
            canRigidbody.useGravity = true;
            canRigidbody.velocity = Vector3.zero;
            canRigidbody.angularVelocity = Vector3.zero;
        }

        if (showDebugInfo)
            Debug.Log("[CanReset] Reset complete");

        isResetting = false;
    }

    private void ResetGrabStateLegacy()
    {
        var type = typeof(CanGrabSystem);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        var leftField = type.GetField("isGrabbedByLeft", flags);
        if (leftField != null) leftField.SetValue(canGrabSystem, false);

        var rightField = type.GetField("isGrabbedByRight", flags);
        if (rightField != null) rightField.SetValue(canGrabSystem, false);

        var leftTimerField = type.GetField("leftGrabTimer", flags);
        if (leftTimerField != null) leftTimerField.SetValue(canGrabSystem, 0f);

        var rightTimerField = type.GetField("rightGrabTimer", flags);
        if (rightTimerField != null) rightTimerField.SetValue(canGrabSystem, 0f);

        var currentHandTransformField = type.GetField("currentHandTransform", flags);
        if (currentHandTransformField != null) currentHandTransformField.SetValue(canGrabSystem, null);
    }

    private void ResetMeshDeformationByReflection()
    {
        if (canSimulation == null) return;

        var simType = typeof(AluminumCanSimulation);
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        var canMeshField = simType.GetField("canMesh", flags);
        if (canMeshField == null) return;

        var canMeshObj = canMeshField.GetValue(canSimulation);
        if (canMeshObj == null) return;

        var cmType = canMeshObj.GetType();

        var buckleRegionsField = cmType.GetField("buckleRegions");
        var originalVertsField = cmType.GetField("originalVertices");
        var currentVertsField = cmType.GetField("currentVertices");
        var meshField = cmType.GetField("mesh");

        // 1) Clear buckle regions
        if (buckleRegionsField != null)
        {
            var listObj = buckleRegionsField.GetValue(canMeshObj);
            if (listObj != null)
            {
                var clearMethod = listObj.GetType().GetMethod("Clear");
                if (clearMethod != null) clearMethod.Invoke(listObj, null);
            }
        }

        // 2) Restore mesh vertices to original
        Mesh m = null;
        if (meshField != null) m = meshField.GetValue(canMeshObj) as Mesh;

        Vector3[] original = null;
        if (originalVertsField != null) original = originalVertsField.GetValue(canMeshObj) as Vector3[];

        if (m != null && original != null && original.Length > 0)
        {
            m.vertices = original;
            m.RecalculateNormals();
            m.RecalculateBounds();

            if (currentVertsField != null)
            {
                var cloned = (Vector3[])original.Clone();
                currentVertsField.SetValue(canMeshObj, cloned);
            }
        }
    }
}