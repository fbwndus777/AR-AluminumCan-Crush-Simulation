using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class AluminumCanSimulation : MonoBehaviour
{
    [Header("Mesh (Required)")]
    [SerializeField] private MeshFilter canMeshFilter;
    [SerializeField] private MeshCollider canMeshCollider; // Assign if available

    private Mesh runtimeMesh; // Instantiated runtime mesh
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
    private BucklingAnimator bucklingAnimator;

    [Header("Deformation Settings")]
    public float curlThreshold = 0.05f;
    public float deformationCooldown = 0.5f;
    private float lastDeformationTime = 0f;

    private CanMesh canMesh;
    private HandCanContactDetector contactDetector;

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

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool showContactPoints = true;

    void Start()
    {
        InitializeSimulation();
    }

    void InitializeSimulation()
    {
        // =============================
        // 0) HandAdapter / GrabSystem
        // =============================
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

        bucklingAnimator = GetComponent<BucklingAnimator>();
        if (bucklingAnimator == null)
            bucklingAnimator = gameObject.AddComponent<BucklingAnimator>();

        // =============================
        // 1) Cache MeshFilter / MeshCollider
        //    (force assignment here even if set in Inspector)
        // =============================
        canMeshFilter = GetComponent<MeshFilter>();
        if (canMeshFilter == null)
        {
            Debug.LogError("[CanSimulation] MeshFilter not found!");
            enabled = false;
            return;
        }

        MeshCollider col = GetComponent<MeshCollider>(); // may or may not exist

        // =============================
        // 2) Create unique runtimeMesh per can instance
        //    Clone sharedMesh (asset) via Instantiate
        // =============================
        Mesh source = canMeshFilter.sharedMesh;
        if (source == null)
        {
            Debug.LogError("[CanSimulation] MeshFilter.sharedMesh is null!");
            enabled = false;
            return;
        }

        runtimeMesh = Instantiate(source);
        runtimeMesh.name = source.name + "_Runtime_" + GetInstanceID();
        runtimeMesh.MarkDynamic();

        // Assign runtimeMesh to renderer
        canMeshFilter.sharedMesh = runtimeMesh;

        // Assign runtimeMesh to collider if present
        if (col != null)
            col.sharedMesh = runtimeMesh;

        // =============================
        // 3) Initialize CanMesh with runtimeMesh
        // =============================
        canMesh = new CanMesh();
        canMesh.Initialize(runtimeMesh);

        contactDetector = new HandCanContactDetector();

        meshSpace = canMeshFilter.transform;

        // =============================
        // 4) Debug: verify each can instance has unique mesh
        // =============================
        Debug.Log($"[CanSimulation] Initialized! name={name} meshID={runtimeMesh.GetInstanceID()}");
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

        // Apply mesh deformation every frame
        if (canMesh != null)
            canMesh.ApplyDeformation(meshSpace);
    }

    void SaveGrabCurls(HandState hand, bool isLeftHand)
    {
        if (isLeftHand)
            leftGrabPinchDist = Vector3.Distance(hand.jointPositions[5], hand.jointPositions[10]);
        else
            rightGrabPinchDist = Vector3.Distance(hand.jointPositions[5], hand.jointPositions[10]);

        if (isLeftHand)
        {
            leftGrabThumbCurl = hand.thumbCurl;
            leftGrabIndexCurl = hand.indexCurl;
            leftGrabMiddleCurl = hand.middleCurl;
            leftGrabRingCurl = hand.ringCurl;
            leftGrabPinkyCurl = hand.pinkyCurl;
        }
        else
        {
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

        if (Time.time - lastDeformationTime < deformationCooldown)
            return;

        // Detect contacts in meshSpace local coordinates
        List<HandCanContactDetector.ContactPoint> contacts =
            contactDetector.DetectContacts(hand, meshSpace, canMesh.mesh);

        if (contacts.Count == 0) return;

        bool anyDeformation = false;
        HashSet<int> activeSet = isLeftHand ? activeLeftDeformations : activeRightDeformations;
        HashSet<int> deformedSet = isLeftHand ? leftDeformedFingers : rightDeformedFingers;

        foreach (var contact in contacts)
        {
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

                if (curlIncrease > fingerThreshold)
                {
                    float t = (curlIncrease - fingerThreshold) / 0.35f;
                    severity = Mathf.Lerp(0.15f, 0.75f, t);
                    severity = Mathf.Clamp(severity, 0.15f, 0.75f);
                    shouldDeform = true;
                }
            }

            if (shouldDeform)
            {
                deformedSet.Add(contact.jointIndex);
                activeSet.Add(contact.jointIndex);

                StartCoroutine(AnimateWithCleanup(
                    contact.canSurfacePoint,
                    severity,
                    contact.jointIndex,
                    isLeftHand,
                    handName
                ));

                anyDeformation = true;
            }
        }

        if (anyDeformation)
            lastDeformationTime = Time.time;

        if (showContactPoints)
        {
            foreach (var contact in contacts)
                Debug.DrawLine(contact.handJointPoint, contact.canSurfacePoint, Color.cyan, 0.01f);
        }
    }

    IEnumerator AnimateWithCleanup(Vector3 position, float severity, int jointIndex, bool isLeftHand, string handName)
    {
        bool isThumb = (jointIndex == 5);

        yield return StartCoroutine(bucklingAnimator.AnimateLocalBuckling(
            canMesh,
            position,
            meshSpace,
            severity,
            isThumb
        ));

        HashSet<int> activeSet = isLeftHand ? activeLeftDeformations : activeRightDeformations;
        activeSet.Remove(jointIndex);
    }

    public void ResetDeformation()
    {
        if (canMesh == null) return;

        // Clear all buckle regions
        if (canMesh.buckleRegions != null)
            canMesh.buckleRegions.Clear();

        // Ensure runtime mesh exists
        if (canMeshFilter == null) canMeshFilter = GetComponent<MeshFilter>();
        if (runtimeMesh == null)
        {
            runtimeMesh = Instantiate(canMeshFilter.sharedMesh);
            runtimeMesh.name = canMeshFilter.sharedMesh.name + "_Runtime";
            runtimeMesh.MarkDynamic();
            canMeshFilter.mesh = runtimeMesh;

            if (canMeshCollider == null) canMeshCollider = GetComponent<MeshCollider>();
            if (canMeshCollider != null) canMeshCollider.sharedMesh = runtimeMesh;

            canMesh.Initialize(runtimeMesh);
        }

        // Restore original vertices
        if (canMesh.originalVertices != null && canMesh.originalVertices.Length > 0)
        {
            runtimeMesh.vertices = canMesh.originalVertices;
            runtimeMesh.RecalculateNormals();
            runtimeMesh.RecalculateBounds();
            canMesh.currentVertices = (Vector3[])canMesh.originalVertices.Clone();
        }

        activeLeftDeformations.Clear();
        activeRightDeformations.Clear();
        leftDeformedFingers.Clear();
        rightDeformedFingers.Clear();

        leftGrabbed = false;
        rightGrabbed = false;
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
}