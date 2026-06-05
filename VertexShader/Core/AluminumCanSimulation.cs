using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AluminumCanSimulation : MonoBehaviour
{
    [Header("Mesh (UV Extraction)")]
    [SerializeField] private MeshFilter canMeshFilter;
    [SerializeField] private MeshCollider canMeshCollider;
    [SerializeField] private Mesh originalMeshAsset;
    private Mesh runtimeMesh;
    private Transform meshSpace;

    [Header("Delay After Grab")]
    public float minTimeAfterGrab = 0.15f;
    private float leftGrabTime = -999f;
    private float rightGrabTime = -999f;
    private float leftGrabPinchDist = 0f;
    private float rightGrabPinchDist = 0f;

    [Header("References")]
    public MRTKHandAdapter handAdapter;
    private CanGrabSystem canGrabSystem;
    private CanNormalMapDeformer normalMapDeformer;
    private HandCanContactDetector contactDetector;

    [Header("Deformation Settings")]
    public float curlThreshold = 0.05f;
    public float deformationCooldown = 0.3f;
    private float lastDeformationTime = 0f;

    private bool leftGrabbed = false;
    private bool rightGrabbed = false;

    private float leftGrabThumbCurl = 0f;
    private float leftGrabIndexCurl = 0f;
    private float leftGrabMiddleCurl = 0f;
    private float leftGrabRingCurl = 0f;
    private float leftGrabPinkyCurl = 0f;

    private float rightGrabThumbCurl = 0f;
    private float rightGrabIndexCurl = 0f;
    private float rightGrabMiddleCurl = 0f;
    private float rightGrabRingCurl = 0f;
    private float rightGrabPinkyCurl = 0f;

    private HashSet<int> activeLeftDeformations = new HashSet<int>();
    private HashSet<int> activeRightDeformations = new HashSet<int>();
    private HashSet<int> leftDeformedFingers = new HashSet<int>();
    private HashSet<int> rightDeformedFingers = new HashSet<int>();
    private Dictionary<int, Vector2> lastContactUVs = new Dictionary<int, Vector2>();
    private Dictionary<int, Vector3> lastContactWorldPositions = new Dictionary<int, Vector3>();

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showContactPoints = true;

    void Start()
    {
        InitializeSimulation();
    }

    void InitializeSimulation()
    {
        if (handAdapter == null)
        {
            handAdapter = FindObjectOfType<MRTKHandAdapter>();
            if (handAdapter == null)
            {
                Debug.LogError("[CanSimulation] MRTKHandAdapter not found!");
                enabled = false;
                return;
            }
        }

        canGrabSystem = GetComponent<CanGrabSystem>();
        if (canGrabSystem == null)
        {
            Debug.LogError("[CanSimulation] CanGrabSystem not found!");
            enabled = false;
            return;
        }

        if (canMeshFilter == null) canMeshFilter = GetComponent<MeshFilter>();
        if (canMeshCollider == null) canMeshCollider = GetComponent<MeshCollider>();

        // Runtime mesh used only for UV extraction via MeshCollider
        runtimeMesh = Instantiate(originalMeshAsset != null ? originalMeshAsset : canMeshFilter.sharedMesh);
        runtimeMesh.name = canMeshFilter.sharedMesh.name + "_Runtime";
        if (canMeshCollider != null)
            canMeshCollider.sharedMesh = runtimeMesh;

        contactDetector = new HandCanContactDetector();
        meshSpace = canMeshFilter.transform;

        normalMapDeformer = gameObject.AddComponent<CanNormalMapDeformer>();
        var renderer = GetComponent<Renderer>();
        SimpleDebugDisplay.Set("Renderer", renderer == null ? "NULL" : renderer.name);
        normalMapDeformer.Initialize(renderer);

        Debug.Log("[CanSimulation] Initialized!");
    }

    void Update()
    {
        if (!handAdapter.IsInitialized()) return;

        if (handAdapter.IsLeftHandTracked())
        {
            HandState leftHand = handAdapter.GetLeftHand();
            bool isGrabbed = GetIsGrabbedByLeft();

            if (isGrabbed && !leftGrabbed)
            {
                SaveGrabCurls(leftHand, true);
                leftGrabbed = true;
                leftGrabTime = Time.time;
                leftDeformedFingers.Clear();
            }
            else if (!isGrabbed && leftGrabbed)
            {
                leftGrabbed = false;
                activeLeftDeformations.Clear();
                leftDeformedFingers.Clear();
            }

            if (isGrabbed) ProcessFingerDeformation(leftHand, "Left", true);
        }

        if (handAdapter.IsRightHandTracked())
        {
            HandState rightHand = handAdapter.GetRightHand();
            bool isGrabbed = GetIsGrabbedByRight();

            if (isGrabbed && !rightGrabbed)
            {
                SaveGrabCurls(rightHand, false);
                rightGrabbed = true;
                rightGrabTime = Time.time;
                rightDeformedFingers.Clear();
            }
            else if (!isGrabbed && rightGrabbed)
            {
                rightGrabbed = false;
                activeRightDeformations.Clear();
                rightDeformedFingers.Clear();
            }

            if (isGrabbed) ProcessFingerDeformation(rightHand, "Right", false);
        }
    }

    void SaveGrabCurls(HandState hand, bool isLeftHand)
    {
        if (isLeftHand)
        {
            leftGrabPinchDist = Vector3.Distance(hand.jointPositions[5], hand.jointPositions[10]);
            leftGrabThumbCurl = hand.thumbCurl;
            leftGrabIndexCurl = hand.indexCurl;
            leftGrabMiddleCurl = hand.middleCurl;
            leftGrabRingCurl = hand.ringCurl;
            leftGrabPinkyCurl = hand.pinkyCurl;
        }
        else
        {
            rightGrabPinchDist = Vector3.Distance(hand.jointPositions[5], hand.jointPositions[10]);
            rightGrabThumbCurl = hand.thumbCurl;
            rightGrabIndexCurl = hand.indexCurl;
            rightGrabMiddleCurl = hand.middleCurl;
            rightGrabRingCurl = hand.ringCurl;
            rightGrabPinkyCurl = hand.pinkyCurl;
        }
    }

    void ProcessFingerDeformation(HandState hand, string handName, bool isLeftHand)
    {
        float grabTime = isLeftHand ? leftGrabTime : rightGrabTime;
        if (Time.time - grabTime < minTimeAfterGrab) return;
        if (Time.time - lastDeformationTime < deformationCooldown) return;
        if (!normalMapDeformer.IsReady) return;

        List<HandCanContactDetector.ContactPoint> contacts =
            contactDetector.DetectContacts(hand, meshSpace, runtimeMesh);

        if (contacts.Count == 0) return;

        HashSet<int> activeSet = isLeftHand ? activeLeftDeformations : activeRightDeformations;
        HashSet<int> deformedSet = isLeftHand ? leftDeformedFingers : rightDeformedFingers;

        // Collect thumb and finger contacts separately
        bool hasThumb = false;
        bool hasFingers = false;
        float maxSeverity = 0f;

        foreach (var contact in contacts)
        {
            if (contact.hasValidUV)
                lastContactUVs[contact.jointIndex] = contact.uv;
            lastContactWorldPositions[contact.jointIndex] = contact.canSurfacePoint;

            if (deformedSet.Contains(contact.jointIndex)) continue;
            if (activeSet.Contains(contact.jointIndex)) continue;

            bool shouldDeform = false;
            float severity = 0f;

            // Thumb (joint 5): use pinch distance delta
            if (contact.jointIndex == 5)
            {
                float pinchDist = Vector3.Distance(hand.jointPositions[5], hand.jointPositions[10]);
                float grabPinch = isLeftHand ? leftGrabPinchDist : rightGrabPinchDist;
                float pinchDelta = grabPinch - pinchDist;

                const float thumbPinchThreshold = 0.012f;
                const float thumbPinchFullScale = 0.03f;

                if (pinchDelta > thumbPinchThreshold)
                {
                    severity = Mathf.Clamp01((pinchDelta - thumbPinchThreshold) / thumbPinchFullScale);
                    shouldDeform = true;
                }
            }
            else
            {
                // Other fingers: use curl delta from grab pose
                float currentCurl = GetFingerCurl(hand, contact.jointIndex);
                float grabCurl = GetGrabFingerCurl(contact.jointIndex, isLeftHand);
                float curlIncrease = currentCurl - grabCurl;
                float fingerThreshold = curlThreshold + 0.02f;

                if (grabCurl > 0.7f)
                {
                    if (currentCurl > 0.55f)
                    {
                        severity = Mathf.Clamp01((currentCurl - 0.55f) / 0.4f);
                        severity = Mathf.Lerp(0.15f, 1.0f, severity);
                        shouldDeform = true;
                    }
                }
                else if (curlIncrease > fingerThreshold)
                {
                    float t = (curlIncrease - fingerThreshold) / 0.35f;
                    severity = Mathf.Lerp(0.15f, 1.0f, t);
                    severity = Mathf.Clamp(severity, 0.15f, 1.0f);
                    shouldDeform = true;
                }
            }

            if (shouldDeform)
            {
                deformedSet.Add(contact.jointIndex);
                activeSet.Add(contact.jointIndex);

                // Remap UV.y to normalized height (non-standard UV range on this model)
                const float UV_MIN = -0.233f;
                const float UV_MAX = 0.362f;
                Vector2 uv = GetLastContactUV(contact.jointIndex);
                float height = Mathf.Clamp01((uv.y - UV_MIN) / (UV_MAX - UV_MIN));
                Vector3 worldPos = contact.canSurfacePoint;

                if (contact.jointIndex == 5)
                {
                    normalMapDeformer.RegisterThumb(worldPos, meshSpace, height);
                    hasThumb = true;
                }
                else
                {
                    normalMapDeformer.RegisterFinger(worldPos, meshSpace, height);
                    hasFingers = true;
                }

                maxSeverity = Mathf.Max(maxSeverity, severity);
            }
        }

        // Commit grip if at least one contact triggered deformation
        if ((hasThumb || hasFingers) && maxSeverity > 0f)
        {
            StartCoroutine(CommitAndCleanup(maxSeverity, isLeftHand, activeSet));
            lastDeformationTime = Time.time;
        }

        if (showContactPoints)
        {
            foreach (var contact in contacts)
                Debug.DrawLine(contact.handJointPoint, contact.canSurfacePoint, Color.cyan, 0.01f);
        }
    }

    IEnumerator CommitAndCleanup(float severity, bool isLeftHand, HashSet<int> activeSet)
    {
        SimpleDebugDisplay.Set("Sev", $"{severity:F3}");
        yield return StartCoroutine(normalMapDeformer.CommitGrip(severity));
    }

    public void ResetDeformation()
    {
        activeLeftDeformations.Clear();
        activeRightDeformations.Clear();
        leftDeformedFingers.Clear();
        rightDeformedFingers.Clear();
        leftGrabbed = false;
        rightGrabbed = false;
        lastContactUVs.Clear();
        lastContactWorldPositions.Clear();

        if (normalMapDeformer != null)
            normalMapDeformer.ResetNormalMap();
    }

    float GetFingerCurl(HandState hand, int jointIndex)
    {
        if (jointIndex == 5) return hand.thumbCurl;
        if (jointIndex == 10) return hand.indexCurl;
        if (jointIndex == 15) return hand.middleCurl;
        if (jointIndex == 20) return hand.ringCurl;
        if (jointIndex == 25) return hand.pinkyCurl;
        return 0f;
    }

    float GetGrabFingerCurl(int jointIndex, bool isLeftHand)
    {
        if (isLeftHand)
        {
            if (jointIndex == 5) return leftGrabThumbCurl;
            if (jointIndex == 10) return leftGrabIndexCurl;
            if (jointIndex == 15) return leftGrabMiddleCurl;
            if (jointIndex == 20) return leftGrabRingCurl;
            if (jointIndex == 25) return leftGrabPinkyCurl;
        }
        else
        {
            if (jointIndex == 5) return rightGrabThumbCurl;
            if (jointIndex == 10) return rightGrabIndexCurl;
            if (jointIndex == 15) return rightGrabMiddleCurl;
            if (jointIndex == 20) return rightGrabRingCurl;
            if (jointIndex == 25) return rightGrabPinkyCurl;
        }
        return 0f;
    }

    Vector2 GetLastContactUV(int jointIndex)
    {
        if (lastContactUVs.TryGetValue(jointIndex, out Vector2 uv))
            return uv;
        return new Vector2(0.5f, 0.5f);
    }

    Vector3 GetLastContactWorldPos(int jointIndex)
    {
        if (lastContactWorldPositions.TryGetValue(jointIndex, out Vector3 pos))
            return pos;
        return meshSpace.position;
    }

    bool GetIsGrabbedByLeft()
    {
        if (canGrabSystem == null) return false;
        var field = typeof(CanGrabSystem).GetField("isGrabbedByLeft",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) return (bool)field.GetValue(canGrabSystem);
        return false;
    }

    bool GetIsGrabbedByRight()
    {
        if (canGrabSystem == null) return false;
        var field = typeof(CanGrabSystem).GetField("isGrabbedByRight",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        if (field != null) return (bool)field.GetValue(canGrabSystem);
        return false;
    }

    // Stub for BucklingAnimator compatibility (unused in this version)
    public void OnNewDeformation() { }
}