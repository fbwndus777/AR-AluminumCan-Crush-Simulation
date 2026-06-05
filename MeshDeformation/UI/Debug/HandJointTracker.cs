using UnityEngine;
using UnityEngine.XR;
using MixedReality.Toolkit;
using MixedReality.Toolkit.Subsystems;
using MixedReality.Toolkit.Input;
using System.Collections;

/// <summary>
/// MRTK의 HandsAggregatorSubsystem을 사용하여 손 관절을 추적
/// 런타임에 자동으로 손 오브젝트를 찾아서 연결
/// </summary>
public class HandJointTracker : MonoBehaviour
{
    [SerializeField] private XRNode handNode = XRNode.LeftHand;

    /// <summary>
    /// 외부에서 접근 가능한 HandNode 프로퍼티
    /// </summary>
    public XRNode HandNode => handNode;

    [Header("Joint Transforms - 자동으로 연결됨")]
    public Transform wrist;
    public Transform thumb_tip;
    public Transform thumb_proximal;
    public Transform index_tip;
    public Transform index_proximal;
    public Transform middle_tip;
    public Transform middle_proximal;
    public Transform ring_tip;
    public Transform ring_proximal;
    public Transform pinky_tip;
    public Transform pinky_proximal;

    [Header("Virtual Tracking (손이 없을 때)")]
    public bool useVirtualTracking = true;
    private GameObject virtualJointsRoot;

    private HandsAggregatorSubsystem handsSubsystem;
    private bool jointsConnected = false;

    void Start()
    {
        StartCoroutine(InitializeWhenReady());
    }

    IEnumerator InitializeWhenReady()
    {
        // HandsAggregatorSubsystem이 준비될 때까지 대기
        int attempts = 0;
        while (handsSubsystem == null && attempts < 20)
        {
            handsSubsystem = XRSubsystemHelpers.GetFirstRunningSubsystem<HandsAggregatorSubsystem>();
            attempts++;
            yield return new WaitForSeconds(0.5f);
        }

        if (handsSubsystem == null)
        {
            Debug.LogError("[HandJointTracker] HandsAggregatorSubsystem not found after " + attempts + " attempts!");
            yield break;
        }

        Debug.Log("[HandJointTracker] HandsAggregatorSubsystem found for " + handNode);

        // 손 오브젝트가 Scene에 나타날 때까지 대기 (더 길게)
        yield return new WaitForSeconds(3f);

        // 손 오브젝트 찾기 시도 (더 적극적으로)
        for (int i = 0; i < 5; i++)
        {
            TryConnectToHandObject();
            if (jointsConnected)
            {
                Debug.Log("[HandJointTracker] Successfully connected on attempt " + (i + 1));
                yield break;
            }
            yield return new WaitForSeconds(1f);
        }

        // 못 찾으면 Virtual Tracking 사용
        if (!jointsConnected && useVirtualTracking)
        {
            Debug.LogWarning("[HandJointTracker] Using virtual tracking for " + handNode);
            CreateVirtualJoints();
        }
        else if (!jointsConnected)
        {
            Debug.LogError("[HandJointTracker] Failed to connect hand joints for " + handNode + "!");
        }
    }

    /// <summary>
    /// Scene에 있는 손 오브젝트를 찾아서 연결 시도
    /// </summary>
    void TryConnectToHandObject()
    {
        string handName = handNode == XRNode.LeftHand ? "left" : "right";

        // Scene의 모든 GameObject 검색
        GameObject[] allObjects = FindObjectsOfType<GameObject>();

        Debug.Log("[HandJointTracker] Searching for " + handName + " hand in " + allObjects.Length + " objects...");

        foreach (GameObject obj in allObjects)
        {
            string objNameLower = obj.name.ToLower();

            // 손 오브젝트 찾기
            if (objNameLower.Contains("hand") && objNameLower.Contains(handName))
            {
                Debug.Log("[HandJointTracker] Found potential hand object: " + obj.name);

                // Armature 찾기
                Transform armature = obj.transform.Find("Armature");
                if (armature != null)
                {
                    Debug.Log("[HandJointTracker] Found Armature in " + obj.name);
                    ConnectJointsFromArmature(armature);
                    if (jointsConnected)
                    {
                        Debug.Log("<color=green>[HandJointTracker] Successfully connected to hand object: " + obj.name + "</color>");
                        return;
                    }
                }
                else
                {
                    Debug.LogWarning("[HandJointTracker] No Armature found in " + obj.name);
                }
            }
        }

        Debug.LogWarning("[HandJointTracker] Could not find hand object for " + handNode);
    }

    /// <summary>
    /// Armature에서 관절들을 찾아 연결
    /// </summary>
    void ConnectJointsFromArmature(Transform root)
    {
        wrist = root;

        Debug.Log("[HandJointTracker] Searching for finger joints in: " + root.name);

        // 모든 자식 순회
        foreach (Transform child in root.GetComponentsInChildren<Transform>())
        {
            string name = child.name.ToLower();

            // 엄지
            if (name.Contains("thumb") && (name.Contains("distal") || name.Contains("tip")))
            {
                thumb_tip = child;
                Debug.Log("  Found thumb: " + child.name);
            }
            else if (name.Contains("thumb") && name.Contains("proximal"))
            {
                thumb_proximal = child;
            }
            // 검지
            else if (name.Contains("index") && (name.Contains("distal") || name.Contains("tip")))
            {
                index_tip = child;
                Debug.Log("  Found index: " + child.name);
            }
            else if (name.Contains("index") && name.Contains("proximal"))
            {
                index_proximal = child;
            }
            // 중지
            else if (name.Contains("middle") && (name.Contains("distal") || name.Contains("tip")))
            {
                middle_tip = child;
                Debug.Log("  Found middle: " + child.name);
            }
            else if (name.Contains("middle") && name.Contains("proximal"))
            {
                middle_proximal = child;
            }
            // 약지
            else if (name.Contains("ring") && (name.Contains("distal") || name.Contains("tip")))
            {
                ring_tip = child;
                Debug.Log("  Found ring: " + child.name);
            }
            else if (name.Contains("ring") && name.Contains("proximal"))
            {
                ring_proximal = child;
            }
            // 새끼
            else if ((name.Contains("little") || name.Contains("pinky")) && (name.Contains("distal") || name.Contains("tip")))
            {
                pinky_tip = child;
                Debug.Log("  Found pinky: " + child.name);
            }
            else if ((name.Contains("little") || name.Contains("pinky")) && name.Contains("proximal"))
            {
                pinky_proximal = child;
            }
        }

        jointsConnected = (thumb_tip != null && index_tip != null && middle_tip != null);

        Debug.Log("[HandJointTracker] Connected: " + jointsConnected);
        Debug.Log("  Wrist: " + (wrist != null ? wrist.name : "NULL"));
        Debug.Log("  Thumb: " + (thumb_tip != null ? thumb_tip.name : "NULL"));
        Debug.Log("  Index: " + (index_tip != null ? index_tip.name : "NULL"));
        Debug.Log("  Middle: " + (middle_tip != null ? middle_tip.name : "NULL"));
        Debug.Log("  Ring: " + (ring_tip != null ? ring_tip.name : "NULL"));
        Debug.Log("  Pinky: " + (pinky_tip != null ? pinky_tip.name : "NULL"));
    }

    /// <summary>
    /// Virtual Joint 생성 (손 오브젝트가 없을 때 대안)
    /// </summary>
    void CreateVirtualJoints()
    {
        virtualJointsRoot = new GameObject("VirtualHand_" + handNode);
        virtualJointsRoot.transform.SetParent(transform);

        wrist = CreateVirtualJoint("Wrist");
        thumb_tip = CreateVirtualJoint("Thumb_Tip");
        thumb_proximal = CreateVirtualJoint("Thumb_Proximal");
        index_tip = CreateVirtualJoint("Index_Tip");
        index_proximal = CreateVirtualJoint("Index_Proximal");
        middle_tip = CreateVirtualJoint("Middle_Tip");
        middle_proximal = CreateVirtualJoint("Middle_Proximal");
        ring_tip = CreateVirtualJoint("Ring_Tip");
        ring_proximal = CreateVirtualJoint("Ring_Proximal");
        pinky_tip = CreateVirtualJoint("Pinky_Tip");
        pinky_proximal = CreateVirtualJoint("Pinky_Proximal");

        jointsConnected = true;
        Debug.Log("[HandJointTracker] Created virtual joints for " + handNode);
    }

    Transform CreateVirtualJoint(string jointName)
    {
        GameObject jointObj = new GameObject(handNode + "_" + jointName);
        jointObj.transform.SetParent(virtualJointsRoot.transform);
        return jointObj.transform;
    }

    void Update()
    {
        if (handsSubsystem == null || !jointsConnected) return;

        // Virtual Tracking을 사용하는 경우에만 위치 업데이트
        if (virtualJointsRoot != null)
        {
            UpdateJoint(TrackedHandJoint.Wrist, wrist);
            UpdateJoint(TrackedHandJoint.ThumbTip, thumb_tip);
            UpdateJoint(TrackedHandJoint.ThumbProximal, thumb_proximal);
            UpdateJoint(TrackedHandJoint.IndexTip, index_tip);
            UpdateJoint(TrackedHandJoint.IndexProximal, index_proximal);
            UpdateJoint(TrackedHandJoint.MiddleTip, middle_tip);
            UpdateJoint(TrackedHandJoint.MiddleProximal, middle_proximal);
            UpdateJoint(TrackedHandJoint.RingTip, ring_tip);
            UpdateJoint(TrackedHandJoint.RingProximal, ring_proximal);
            UpdateJoint(TrackedHandJoint.LittleTip, pinky_tip);
            UpdateJoint(TrackedHandJoint.LittleProximal, pinky_proximal);
        }
    }

    void UpdateJoint(TrackedHandJoint jointType, Transform jointTransform)
    {
        if (jointTransform == null) return;

        if (handsSubsystem.TryGetJoint(jointType, handNode, out HandJointPose pose))
        {
            jointTransform.position = pose.Position;
            jointTransform.rotation = pose.Rotation;
        }
    }

    void OnDrawGizmos()
    {
        if (!jointsConnected) return;

        Gizmos.color = handNode == XRNode.LeftHand ? Color.cyan : Color.magenta;

        Transform[] joints = { wrist, thumb_tip, index_tip, middle_tip, ring_tip, pinky_tip };
        foreach (var joint in joints)
        {
            if (joint != null)
            {
                Gizmos.DrawSphere(joint.position, 0.01f);
            }
        }

        // 손가락 선 그리기
        Gizmos.color = Color.yellow;
        if (wrist != null && index_tip != null)
            Gizmos.DrawLine(wrist.position, index_tip.position);
    }
}
