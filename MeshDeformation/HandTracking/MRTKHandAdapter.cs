// Scripts/HandTracking/MRTKHandAdapter.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MRTKHandAdapter : MonoBehaviour
{
    [Header("Hand Tracking (Auto-detected)")]
    private Transform leftHandRoot;
    private Transform rightHandRoot;

    [Header("Hand States")]
    private HandState leftHandState = new HandState();
    private HandState rightHandState = new HandState();

    [Header("Detection Settings")]
    public float detectionRetryInterval = 0.5f;

    [Header("Debug")]
    public bool showDebugInfo = true;
    public bool drawJointGizmos = false;

    private Transform[] leftJointTransforms;
    private Transform[] rightJointTransforms;

    private bool isDetecting = false;

    void Start()
    {
        leftHandState.jointPositions = new Vector3[26];
        rightHandState.jointPositions = new Vector3[26];

        Debug.Log("[MRTKHandAdapter] Waiting for hand objects to spawn...");
        StartCoroutine(DetectHandObjects());
    }

    IEnumerator DetectHandObjects()
    {
        isDetecting = true;

        while (leftHandRoot == null || rightHandRoot == null)
        {
            if (leftHandRoot == null)
            {
                GameObject leftHandObj = GameObject.Find("openxr_left_hand(Clone)");
                if (leftHandObj != null)
                {
                    leftHandRoot = leftHandObj.transform;
                    leftJointTransforms = CacheJointTransforms(leftHandRoot);
                    Debug.Log("[MRTKHandAdapter] Left hand detected!");
                }
            }

            if (rightHandRoot == null)
            {
                GameObject rightHandObj = GameObject.Find("openxr_right_hand(Clone)");
                if (rightHandObj != null)
                {
                    rightHandRoot = rightHandObj.transform;
                    rightJointTransforms = CacheJointTransforms(rightHandRoot);
                    Debug.Log("[MRTKHandAdapter] Right hand detected!");
                }
            }

            if (leftHandRoot != null && rightHandRoot != null)
            {
                Debug.Log("[MRTKHandAdapter] Both hands ready!");
                isDetecting = false;
                yield break;
            }

            yield return new WaitForSeconds(detectionRetryInterval);
        }

        isDetecting = false;
    }

    Transform[] CacheJointTransforms(Transform handRoot)
    {
        Transform[] joints = new Transform[26];

        Transform armature = handRoot.Find("Armature");
        if (armature == null)
        {
            Debug.LogWarning($"[MRTKHandAdapter] Armature not found under {handRoot.name}");
            return joints;
        }

        Debug.Log($"[MRTKHandAdapter] Found Armature under {handRoot.name}");

        // Collect all joint transforms
        Dictionary<string, Transform> jointDict = new Dictionary<string, Transform>();
        foreach (Transform child in armature.GetComponentsInChildren<Transform>())
        {
            string cleanName = child.name.ToLower().Replace("_end", "");
            if (!jointDict.ContainsKey(cleanName))
            {
                jointDict[cleanName] = child;
            }
        }

        // Index mapping
        string[] jointNames = new string[]
        {
            "armature",            // 0 - palm
            "armature",            // 1 - wrist
            "thumb_metacarpal",    // 2
            "thumb_proximal",      // 3
            "thumb_distal",        // 4
            "thumb_tip",           // 5
            "index_metacarpal",    // 6
            "index_proximal",      // 7
            "index_intermediate",  // 8
            "index_distal",        // 9
            "index_tip",           // 10
            "middle_metacarpal",   // 11
            "middle_proximal",     // 12
            "middle_intermediate", // 13
            "middle_distal",       // 14
            "middle_tip",          // 15
            "ring_metacarpal",     // 16
            "ring_proximal",       // 17
            "ring_intermediate",   // 18
            "ring_distal",         // 19
            "ring_tip",            // 20
            "little_metacarpal",   // 21
            "little_proximal",     // 22
            "little_intermediate", // 23
            "little_distal",       // 24
            "little_tip"           // 25
        };

        int foundCount = 0;
        for (int i = 0; i < jointNames.Length; i++)
        {
            if (jointDict.TryGetValue(jointNames[i], out Transform joint))
            {
                joints[i] = joint;
                foundCount++;
            }
        }

        Debug.Log($"[MRTKHandAdapter] Found {foundCount}/{jointNames.Length} joints in {handRoot.name}");

        return joints;
    }

    void Update()
    {
        if (isDetecting) return;

        if (leftHandRoot != null && leftHandRoot.gameObject.activeInHierarchy)
        {
            UpdateHandStateFromTransforms(leftJointTransforms, ref leftHandState, "Left");
        }

        if (rightHandRoot != null && rightHandRoot.gameObject.activeInHierarchy)
        {
            UpdateHandStateFromTransforms(rightJointTransforms, ref rightHandState, "Right");
        }
    }

    void UpdateHandStateFromTransforms(Transform[] joints, ref HandState state, string handName)
    {
        if (joints == null) return;

        int validJoints = 0;

        for (int i = 0; i < joints.Length; i++)
        {
            if (joints[i] != null)
            {
                state.jointPositions[i] = joints[i].position;
                validJoints++;
            }
        }

        // Palm data
        if (joints[0] != null)
        {
            state.palmCenter = joints[0].position;
            state.palmNormal = joints[0].forward;
            state.palmRotation = joints[0].rotation;
        }

        // Calculate finger curl values
        state.thumbCurl = CalculateFingerCurlSimple(joints, 5);
        state.indexCurl = CalculateFingerCurlSimple(joints, 10);
        state.middleCurl = CalculateFingerCurlSimple(joints, 15);
        state.ringCurl = CalculateFingerCurlSimple(joints, 20);
        state.pinkyCurl = CalculateFingerCurlSimple(joints, 25);

        // Calculate grip type and strength
        GripPatternRecognizer recognizer = new GripPatternRecognizer();
        state.currentGrip = recognizer.ClassifyGrip(state);
        state.gripStrength = recognizer.CalculateGripStrength(state);

        // Debug log every 10 frames
        if (showDebugInfo && Time.frameCount % 10 == 0)
        {
            Debug.Log($"[{handName}] T:{state.thumbCurl:F2} I:{state.indexCurl:F2} M:{state.middleCurl:F2} R:{state.ringCurl:F2} P:{state.pinkyCurl:F2} Grip:{state.currentGrip}");
        }
    }

    float CalculateFingerCurlSimple(Transform[] joints, int tipIdx)
    {
        if (tipIdx >= joints.Length || joints[tipIdx] == null || joints[0] == null)
        {
            return 0f;
        }

        Vector3 palmPos = joints[0].position;
        Vector3 tipPos = joints[tipIdx].position;

        float distance = Vector3.Distance(palmPos, tipPos);

        // Based on actual XREAL hand tracking measurements
        float maxDist = 0.18f;  // 18cm (fully extended)
        float minDist = 0.08f;  // 8cm (fully curled)

        float curl = 1f - Mathf.Clamp01((distance - minDist) / (maxDist - minDist));

        return curl;
    }

    public HandState GetLeftHand() => leftHandState;
    public HandState GetRightHand() => rightHandState;

    public bool IsLeftHandTracked()
    {
        return leftHandRoot != null &&
               leftHandRoot.gameObject.activeInHierarchy &&
               leftJointTransforms != null;
    }

    public bool IsRightHandTracked()
    {
        return rightHandRoot != null &&
               rightHandRoot.gameObject.activeInHierarchy &&
               rightJointTransforms != null;
    }

    public bool IsInitialized()
    {
        return !isDetecting &&
               (leftHandRoot != null || rightHandRoot != null);
    }

    public Transform GetLeftHandTransform()
    {
        if (leftHandRoot != null)
            return leftHandRoot;
        return null;
    }

    public Transform GetRightHandTransform()
    {
        if (rightHandRoot != null)
            return rightHandRoot;
        return null;
    }

    void OnDrawGizmos()
    {
        if (!drawJointGizmos) return;

        if (leftJointTransforms != null && leftHandRoot != null && leftHandRoot.gameObject.activeInHierarchy)
        {
            DrawHandJointsGizmos(leftJointTransforms, Color.cyan);
        }

        if (rightJointTransforms != null && rightHandRoot != null && rightHandRoot.gameObject.activeInHierarchy)
        {
            DrawHandJointsGizmos(rightJointTransforms, Color.magenta);
        }
    }

    void DrawHandJointsGizmos(Transform[] joints, Color color)
    {
        Gizmos.color = color;
        foreach (Transform joint in joints)
        {
            if (joint != null)
            {
                Gizmos.DrawSphere(joint.position, 0.005f);
            }
        }

        if (joints[0] != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(joints[0].position, 0.02f);
        }
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}