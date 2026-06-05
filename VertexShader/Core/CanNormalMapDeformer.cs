using UnityEngine;
using System.Collections;

public class CanNormalMapDeformer : MonoBehaviour
{
    [System.Serializable]
    public struct GripZone
    {
        public float thumbAngle;    // Thumb contact angle
        public float fingerAngle;   // Average finger contact angle
        public float thumbHeight;   // Thumb contact height (normalized)
        public float fingerHeight;  // Finger contact height (normalized)
        public float severity;      // Crush intensity
        public float axialSpread;   // Vertical spread of deformation
        public bool active;
    }

    [Header("Crush Settings")]
    public float maxCrushDepth = 0f;
    public float defaultAxialSpread = 0.18f;
    public float gripMergeThreshold = 0.4f; // Threshold for merging nearby grip zones

    private Material canMaterial;
    private GripZone[] gripZones = new GripZone[4]; // Max 4 grip zones
    private int gripZoneCount = 0;
    private float canMinY, canMaxY, canRadius;

    // Pending thumb/finger data collected in current frame
    private float pendingThumbAngle = 0f;
    private float pendingThumbHeight = 0f;
    private bool hasPendingThumb = false;

    private float[] pendingFingerAngles = new float[4];
    private float[] pendingFingerHeights = new float[4];
    private int pendingFingerCount = 0;

    private static readonly int[] _CrushIDs = new int[8];
    private static readonly int _MaxCrushDepthID = Shader.PropertyToID("_MaxCrushDepth");
    private static readonly int _CanMinYID = Shader.PropertyToID("_CanMinY");
    private static readonly int _CanMaxYID = Shader.PropertyToID("_CanMaxY");

    static CanNormalMapDeformer()
    {
        for (int i = 0; i < 8; i++)
            _CrushIDs[i] = Shader.PropertyToID("_Crush" + i);
    }

    public bool IsReady => canMaterial != null;

    public void Initialize(Renderer canRenderer)
    {
        SimpleDebugDisplay.Set("InitCalled", canRenderer == null ? "NULL" : "OK");
        if (canRenderer == null) return;

        canMaterial = canRenderer.material;

        Shader crushShader = Shader.Find("Custom/CanCrushShader");
        if (crushShader == null)
        {
            SimpleDebugDisplay.Set("Shader", "NOT FOUND");
            return;
        }

        canMaterial.shader = crushShader;
        SimpleDebugDisplay.Set("Shader", canMaterial.shader.name);

        MeshFilter mf = canRenderer.GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Bounds b = mf.sharedMesh.bounds;
            canMinY = b.min.y;
            canMaxY = b.max.y;
            canRadius = Mathf.Max(b.extents.x, b.extents.z);

            canMaterial.SetFloat(_CanMinYID, canMinY);
            canMaterial.SetFloat(_CanMaxYID, canMaxY);

            if (maxCrushDepth <= 0)
                maxCrushDepth = canRadius * 2.8f;

            maxCrushDepth = Mathf.Max(maxCrushDepth, 0.008f);
            canMaterial.SetFloat(_MaxCrushDepthID, maxCrushDepth);
            SimpleDebugDisplay.Set("Depth", $"{maxCrushDepth:F4} r:{canRadius:F4}");
        }

        for (int i = 0; i < 4; i++)
        {
            gripZones[i] = new GripZone
            {
                thumbAngle = 0, fingerAngle = Mathf.PI,
                thumbHeight = 0.5f, fingerHeight = 0.5f,
                severity = 0, axialSpread = defaultAxialSpread, active = false
            };
        }

        ClearShaderZones();
    }

    // Register thumb contact data
    public void RegisterThumb(Vector3 worldPos, Transform canTransform, float height)
    {
        Vector3 local = canTransform.InverseTransformPoint(worldPos);
        pendingThumbAngle = Mathf.Atan2(local.x, local.z);
        pendingThumbHeight = height;
        hasPendingThumb = true;
    }

    // Register finger contact data
    public void RegisterFinger(Vector3 worldPos, Transform canTransform, float height)
    {
        if (pendingFingerCount >= 4) return;
        Vector3 local = canTransform.InverseTransformPoint(worldPos);
        pendingFingerAngles[pendingFingerCount] = Mathf.Atan2(local.x, local.z);
        pendingFingerHeights[pendingFingerCount] = height;
        pendingFingerCount++;
    }

    // Commit collected thumb/finger data as a grip deformation
    public IEnumerator CommitGrip(float severity)
    {
        if (!IsReady) yield break;
        if (!hasPendingThumb && pendingFingerCount == 0)
        {
            ResetPending();
            yield break;
        }

        SimpleDebugDisplay.Set("Commit", $"thumb:{hasPendingThumb} fingers:{pendingFingerCount}");

        float fingerAngle, fingerHeight;

        if (pendingFingerCount > 0)
        {
            // Average finger angles using circular mean
            float sinSum = 0f, cosSum = 0f, heightSum = 0f;
            for (int i = 0; i < pendingFingerCount; i++)
            {
                sinSum += Mathf.Sin(pendingFingerAngles[i]);
                cosSum += Mathf.Cos(pendingFingerAngles[i]);
                heightSum += pendingFingerHeights[i];
            }
            fingerAngle = Mathf.Atan2(sinSum, cosSum);
            fingerHeight = heightSum / pendingFingerCount;
        }
        else
        {
            // No fingers: place opposite to thumb
            fingerAngle = pendingThumbAngle + Mathf.PI;
            fingerHeight = pendingThumbHeight;
        }

        float thumbAngle = hasPendingThumb ? pendingThumbAngle : (fingerAngle + Mathf.PI);
        float thumbHeight = hasPendingThumb ? pendingThumbHeight : fingerHeight;
        float midHeight = (thumbHeight + fingerHeight) * 0.5f;

        // Find existing grip zone or create new one
        int zoneIdx = FindOrCreateGrip(pendingThumbAngle, fingerAngle, midHeight);
        if (zoneIdx < 0)
        {
            ResetPending();
            yield break;
        }

        float startSev = gripZones[zoneIdx].severity;
        float targetSev = Mathf.Clamp01(startSev + severity * 0.9f);

        gripZones[zoneIdx].thumbAngle = pendingThumbAngle;
        gripZones[zoneIdx].fingerAngle = fingerAngle;
        gripZones[zoneIdx].thumbHeight = pendingThumbHeight;
        gripZones[zoneIdx].fingerHeight = fingerHeight;
        gripZones[zoneIdx].axialSpread = Mathf.Lerp(defaultAxialSpread, defaultAxialSpread * 1.8f, targetSev);

        ResetPending();

        // Animate severity with ease-out cubic
        float snapDuration = 0.05f;
        float elapsed = 0f;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / snapDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);
            gripZones[zoneIdx].severity = Mathf.Lerp(startSev, targetSev, eased);
            PushGripZonesToShader();
            yield return null;
        }

        gripZones[zoneIdx].severity = targetSev;
        PushGripZonesToShader();
    }

    private int FindOrCreateGrip(float thumbAngle, float fingerAngle, float height)
    {
        float bestDist = float.MaxValue;
        int bestIdx = -1;

        for (int i = 0; i < gripZoneCount; i++)
        {
            if (!gripZones[i].active) continue;
            float tDiff = Mathf.Abs(Mathf.DeltaAngle(
                gripZones[i].thumbAngle * Mathf.Rad2Deg,
                thumbAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            float hDiff = Mathf.Abs(gripZones[i].thumbHeight - height);
            float dist = tDiff + hDiff;
            if (dist < bestDist) { bestDist = dist; bestIdx = i; }
        }

        if (bestIdx >= 0 && bestDist < gripMergeThreshold)
            return bestIdx;

        if (gripZoneCount < 4)
        {
            int idx = gripZoneCount;
            gripZones[idx] = new GripZone
            {
                thumbAngle = thumbAngle, fingerAngle = fingerAngle,
                thumbHeight = height, fingerHeight = height,
                severity = 0f, axialSpread = defaultAxialSpread, active = true
            };
            gripZoneCount++;
            return idx;
        }

        // Replace weakest zone if all slots are full
        float minSev = float.MaxValue;
        int weakest = 0;
        for (int i = 0; i < 4; i++)
            if (gripZones[i].severity < minSev) { minSev = gripZones[i].severity; weakest = i; }

        gripZones[weakest] = new GripZone
        {
            thumbAngle = thumbAngle, fingerAngle = fingerAngle,
            thumbHeight = height, fingerHeight = height,
            severity = 0f, axialSpread = defaultAxialSpread, active = true
        };
        return weakest;
    }

    private void PushGripZonesToShader()
    {
        if (canMaterial == null) return;

        // Each grip zone uses 2 shader slots:
        // _Crush(2i): thumb side, _Crush(2i+1): finger side
        for (int i = 0; i < 4; i++)
        {
            int thumbSlot = i * 2;
            int fingerSlot = i * 2 + 1;

            if (gripZones[i].active && gripZones[i].severity > 0.001f)
            {
                canMaterial.SetVector(_CrushIDs[thumbSlot], new Vector4(
                    gripZones[i].thumbAngle,
                    gripZones[i].thumbHeight,
                    gripZones[i].severity,
                    gripZones[i].axialSpread
                ));
                canMaterial.SetVector(_CrushIDs[fingerSlot], new Vector4(
                    gripZones[i].fingerAngle,
                    gripZones[i].fingerHeight,
                    gripZones[i].severity,
                    gripZones[i].axialSpread
                ));
            }
            else
            {
                canMaterial.SetVector(_CrushIDs[thumbSlot], Vector4.zero);
                canMaterial.SetVector(_CrushIDs[fingerSlot], Vector4.zero);
            }
        }

        SimpleDebugDisplay.Set("Grip0", gripZones[0].active ?
            $"t:{gripZones[0].thumbAngle:F1} f:{gripZones[0].fingerAngle:F1} s:{gripZones[0].severity:F2}" : "inactive");
    }

    private void ClearShaderZones()
    {
        for (int i = 0; i < 8; i++)
            canMaterial.SetVector(_CrushIDs[i], Vector4.zero);
    }

    private void ResetPending()
    {
        hasPendingThumb = false;
        pendingFingerCount = 0;
    }

    public void ResetNormalMap()
    {
        for (int i = 0; i < 4; i++)
        {
            gripZones[i].severity = 0f;
            gripZones[i].active = false;
        }
        gripZoneCount = 0;
        ClearShaderZones();
        ResetPending();
    }

    void OnDestroy() { }
}