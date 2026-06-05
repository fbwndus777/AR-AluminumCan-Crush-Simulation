using UnityEngine;
using UnityEngine.XR;

public class CanGrabSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MRTKHandAdapter handAdapter;
    [SerializeField] private Rigidbody canRigidbody;

    [Header("Grab Settings")]
    public float fingerContactDistance = 0.06f;
    public int minContactFingers = 2;
    public float grabHoldTime = 0.12f;
    public float palmDistanceLimit = 0.12f;

    [Header("Optimization")]
    [Tooltip("Frame skip interval (2 = every 2 frames)")]
    public int frameSkip = 2;
    [Tooltip("Skip check if hand is farther than this distance (meters)")]
    public float handDistanceCheckThreshold = 0.5f;
    [Tooltip("Reduce update frequency when not grabbed")]
    public bool optimizeWhenNotGrabbed = true;

    [Header("Debug")]
    public bool showDebugInfo = true;

    private bool isGrabbedByLeft = false;
    private bool isGrabbedByRight = false;

    private float leftGrabTimer = 0f;
    private float rightGrabTimer = 0f;

    private Transform originalParent;
    private Transform currentHandTransform;

    // Optimization variables
    private int updateCounter = 0;
    private bool isAnyHandNearby = false;

    [Header("Debug Info")]
    public Vector3 debugThumbPos;
    public Vector3 debugIndexPos;
    public Vector3 debugFingerCenter;
    public Vector3 debugOriginalCanPos;
    public Vector3 debugNewCanPos;
    public float debugThumbIndexDist;

    [Header("Top Grab Debug")]
    public Vector3 debugMiddleMCP;
    public Vector3 debugCanTop;
    public float debugMCPToTopDist;
    public int debugSideContactCount;
    public bool debugIsTopGrab;

    void Start()
    {
        if (handAdapter == null)
            handAdapter = FindObjectOfType<MRTKHandAdapter>();

        if (canRigidbody == null)
            canRigidbody = GetComponent<Rigidbody>();

        originalParent = transform.parent;

        if (canRigidbody != null)
        {
            canRigidbody.useGravity = true;
            canRigidbody.mass = 0.05f;
        }
    }

    void Update()
    {
        if (!handAdapter.IsInitialized()) return;

        // Optimization 1: Frame skip
        updateCounter++;
        bool shouldUpdate = (isGrabbedByLeft || isGrabbedByRight)
            ? true
            : (updateCounter % frameSkip == 0);

        if (!shouldUpdate) return;

        // Optimization 2: Quick hand distance check
        if (optimizeWhenNotGrabbed && !isGrabbedByLeft && !isGrabbedByRight)
        {
            isAnyHandNearby = CheckIfAnyHandNearby();
            if (!isAnyHandNearby) return;
        }

        HandState leftHand = handAdapter.GetLeftHand();
        HandState rightHand = handAdapter.GetRightHand();

        if (handAdapter.IsLeftHandTracked())
        {
            UpdateGrabForHand(leftHand, ref isGrabbedByLeft, "Left", ref leftGrabTimer);
        }
        else if (isGrabbedByLeft)
        {
            ReleaseGrab(ref isGrabbedByLeft, "Left (tracking lost)");
            leftGrabTimer = 0f;
        }

        if (handAdapter.IsRightHandTracked())
        {
            UpdateGrabForHand(rightHand, ref isGrabbedByRight, "Right", ref rightGrabTimer);
        }
        else if (isGrabbedByRight)
        {
            ReleaseGrab(ref isGrabbedByRight, "Right (tracking lost)");
            rightGrabTimer = 0f;
        }
    }

    // Quick distance check to skip processing when hand is far away
    bool CheckIfAnyHandNearby()
    {
        Vector3 canPos = transform.position;

        if (handAdapter.IsLeftHandTracked())
        {
            HandState leftHand = handAdapter.GetLeftHand();
            float leftDist = Vector3.Distance(leftHand.palmCenter, canPos);
            if (leftDist < handDistanceCheckThreshold)
                return true;
        }

        if (handAdapter.IsRightHandTracked())
        {
            HandState rightHand = handAdapter.GetRightHand();
            float rightDist = Vector3.Distance(rightHand.palmCenter, canPos);
            if (rightDist < handDistanceCheckThreshold)
                return true;
        }

        return false;
    }

    void UpdateGrabForHand(HandState hand, ref bool isGrabbed, string handName, ref float grabTimer)
    {
        // Quick rejection before full check
        if (!isGrabbed)
        {
            int quickContactCheck = CountContactingFingers(hand);
            if (quickContactCheck < minContactFingers)
            {
                grabTimer = 0f;
                return;
            }
        }

        int contactingFingers = CountContactingFingers(hand);

        GripPatternRecognizer recognizer = new GripPatternRecognizer();
        bool hasHoldingPose = recognizer.IsCanHoldingPose(hand);

        float minFingerDist = GetMinFingerDistance(hand);
        bool fingersNear = minFingerDist < 0.15f;

        float palmDistance = Vector3.Distance(hand.palmCenter, transform.position);
        bool palmNear = palmDistance < palmDistanceLimit;

        // Check for top grab
        bool isTopGrab = CheckTopGrab(hand, out Vector3 middleMCP);

        if (!isGrabbed)
        {
            GripPatternRecognizer recognizer2 = new GripPatternRecognizer();
            bool isPowerGrip = recognizer2.ClassifyGrip(hand) == GripType.PowerGrip;

            bool canGrab =
                (contactingFingers >= minContactFingers &&
                 hasHoldingPose &&
                 fingersNear &&
                 palmNear) ||
                isTopGrab ||
                isPowerGrip; // PowerGrip always triggers grab

            if (canGrab)
            {
                grabTimer += Time.deltaTime;

                if (grabTimer >= grabHoldTime)
                {
                    isGrabbed = true;
                    grabTimer = 0f;

                    // Disable physics and attach to hand
                    canRigidbody.isKinematic = true;
                    canRigidbody.velocity = Vector3.zero;
                    canRigidbody.angularVelocity = Vector3.zero;

                    Transform handTransform = GetHandTransform(handName);
                    if (handTransform != null)
                    {
                        currentHandTransform = handTransform;

                        if (isTopGrab) AdjustCanPositionForTopGrab(middleMCP);
                        else AdjustCanPositionBetweenFingers(hand);

                        transform.SetParent(handTransform, worldPositionStays: true);

                        if (showDebugInfo)
                        {
                            string grabType = isTopGrab ? "TOP" : "SIDE";
                            Debug.Log($"[CanGrab] {handName} GRABBED ({grabType})!");
                        }
                    }
                }
            }
            else
            {
                grabTimer = 0f;
            }
        }
        else
        {
            bool fingersOpen =
                hand.indexCurl < 0.30f &&
                hand.middleCurl < 0.30f &&
                hand.ringCurl < 0.40f;

            bool tooFarAway = minFingerDist > 0.50f;

            if (fingersOpen || tooFarAway)
            {
                ReleaseGrab(ref isGrabbed, handName);
                grabTimer = 0f;
            }
        }
    }

    Transform GetHandTransform(string handName)
    {
        return handName == "Left"
            ? handAdapter.GetLeftHandTransform()
            : handAdapter.GetRightHandTransform();
    }

    void ReleaseGrab(ref bool isGrabbed, string handName)
    {
        isGrabbed = false;

        // Detach from hand
        transform.SetParent(originalParent, worldPositionStays: true);
        currentHandTransform = null;

        // Re-enable physics
        canRigidbody.isKinematic = false;
        canRigidbody.useGravity = true;
        canRigidbody.velocity = Vector3.zero;
        canRigidbody.angularVelocity = Vector3.zero;

        if (showDebugInfo)
            Debug.Log($"[CanGrab] {handName} RELEASED!");
    }

    int CountContactingFingers(HandState hand)
    {
        int count = 0;
        int[] fingerTips = new int[] { 5, 10, 15, 20, 25 };

        // Calculate can Y bounds for vertical range check
        Collider col = GetComponent<Collider>();
        float canMinY = col != null ? col.bounds.min.y : transform.position.y - 0.1f;
        float canMaxY = col != null ? col.bounds.max.y : transform.position.y + 0.1f;

        foreach (int tipIdx in fingerTips)
        {
            if (tipIdx >= hand.jointPositions.Length) continue;

            Vector3 tipPos = hand.jointPositions[tipIdx];
            float distance = Vector3.Distance(tipPos, transform.position);

            bool withinXZ = distance < fingerContactDistance;
            bool withinY = tipPos.y >= canMinY - 0.02f && tipPos.y <= canMaxY + 0.02f;

            if (withinXZ && withinY)
                count++;
        }
        return count;
    }

    float GetMinFingerDistance(HandState hand)
    {
        float minDist = float.MaxValue;
        int[] fingerTips = new int[] { 5, 10, 15, 20, 25 };

        foreach (int tipIdx in fingerTips)
        {
            if (tipIdx >= hand.jointPositions.Length) continue;

            Vector3 tipPos = hand.jointPositions[tipIdx];
            float dist = Vector3.Distance(tipPos, transform.position);

            if (dist < minDist)
                minDist = dist;
        }

        return minDist;
    }

    Vector3 GetContactingFingersCenter(HandState hand)
    {
        Vector3 sum = Vector3.zero;
        int count = 0;
        int[] fingerTips = new int[] { 5, 10, 15, 20, 25 };

        foreach (int tipIdx in fingerTips)
        {
            Vector3 tipPos = hand.jointPositions[tipIdx];
            float distance = Vector3.Distance(tipPos, transform.position);

            if (distance < fingerContactDistance)
            {
                sum += tipPos;
                count++;
            }
        }

        return count > 0 ? sum / count : hand.palmCenter;
    }

    void AdjustCanPositionBetweenFingers(HandState hand)
    {
        Vector3 thumbPos = hand.jointPositions[5];
        Vector3 indexPos = hand.jointPositions[10];

        Vector3 fingerCenter = (thumbPos + indexPos) / 2f;
        float originalHeight = transform.position.y;

        Vector3 newPosition = new Vector3(
            fingerCenter.x,
            originalHeight,
            fingerCenter.z
        );

        debugThumbPos = thumbPos;
        debugIndexPos = indexPos;
        debugFingerCenter = fingerCenter;
        debugOriginalCanPos = transform.position;
        debugNewCanPos = newPosition;
        debugThumbIndexDist = Vector3.Distance(thumbPos, indexPos);

        transform.position = newPosition;

        if (showDebugInfo)
        {
            Debug.Log($"[CanGrab] Thumb: {thumbPos}, Index: {indexPos}");
            Debug.Log($"[CanGrab] Center: {fingerCenter}, Height maintained: {originalHeight}");
            Debug.Log($"[CanGrab] Thumb-Index distance: {debugThumbIndexDist * 100f:F1}cm");
        }
    }

    bool CheckTopGrab(HandState hand, out Vector3 middleMCP)
    {
        middleMCP = hand.jointPositions[13]; // Middle finger MCP

        Bounds canBounds = GetComponent<Collider>().bounds;
        Vector3 canTop = new Vector3(transform.position.x, canBounds.max.y, transform.position.z);

        float mcpToTopDist = Vector3.Distance(middleMCP, canTop);
        bool mcpNearTop = mcpToTopDist < 0.05f;

        int sideContactCount = CountSideTopContacts(hand);
        bool enoughSideContacts = sideContactCount >= 4;

        debugMiddleMCP = middleMCP;
        debugCanTop = canTop;
        debugMCPToTopDist = mcpToTopDist;
        debugSideContactCount = sideContactCount;
        debugIsTopGrab = mcpNearTop && enoughSideContacts;

        return mcpNearTop && enoughSideContacts;
    }

    int CountSideTopContacts(HandState hand)
    {
        int count = 0;
        int[] fingerTips = new int[] { 10, 15, 20, 25 };

        Bounds canBounds = GetComponent<Collider>().bounds;
        float canTopY = canBounds.max.y;
        float canBottomY = canBounds.min.y;
        float canHeight = canTopY - canBottomY;
        float canRadius = canBounds.extents.x;

        float upperThreshold = canTopY - (canHeight * 0.2f);

        foreach (int tipIdx in fingerTips)
        {
            if (tipIdx >= hand.jointPositions.Length) continue;

            Vector3 tipPos = hand.jointPositions[tipIdx];

            Vector3 tipPosXZ = new Vector3(tipPos.x, transform.position.y, tipPos.z);
            Vector3 canCenterXZ = new Vector3(transform.position.x, transform.position.y, transform.position.z);

            float distFromCenter = Vector3.Distance(tipPosXZ, canCenterXZ);

            bool nearSide = Mathf.Abs(distFromCenter - canRadius) < 0.02f;
            bool inUpperPart = tipPos.y > upperThreshold;

            if (nearSide && inUpperPart)
                count++;
        }

        return count;
    }

    void AdjustCanPositionForTopGrab(Vector3 middleMCP)
    {
        float originalHeight = transform.position.y;

        Vector3 newPosition = new Vector3(
            middleMCP.x,
            originalHeight,
            middleMCP.z
        );

        debugOriginalCanPos = transform.position;
        debugNewCanPos = newPosition;

        transform.position = newPosition;

        if (showDebugInfo)
            Debug.Log($"[CanGrab] TOP GRAB! Middle MCP: {middleMCP}, New pos: {newPosition}");
    }
}