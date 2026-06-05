// Scripts/Test/HandTrackingDebugger.cs (새 파일)
using UnityEngine;

/// <summary>
/// 손 추적 원시 데이터 확인용
/// </summary>
public class HandTrackingDebugger : MonoBehaviour
{
    public bool showLogs = true;

    void Update()
    {
        if (!showLogs) return;

        // 1. 손 오브젝트 찾기
        GameObject leftHand = GameObject.Find("openxr_left_hand(Clone)");
        GameObject rightHand = GameObject.Find("openxr_right_hand(Clone)");

        if (Time.frameCount % 60 == 0) // 1초마다
        {
            Debug.Log("=== Hand Tracking Debug ===");

            // 왼손
            if (leftHand != null)
            {
                Debug.Log($"✓ Left hand exists: {leftHand.name}");
                Debug.Log($"  Active: {leftHand.activeInHierarchy}");
                Debug.Log($"  Position: {leftHand.transform.position}");

                // Armature 찾기
                Transform armature = leftHand.transform.Find("Armature");
                if (armature != null)
                {
                    Debug.Log($"  ✓ Armature found");
                    Debug.Log($"    Armature position: {armature.position}");

                    // 검지 끝 찾기
                    Transform indexTip = FindChildRecursive(armature, "index_tip");
                    if (indexTip != null)
                    {
                        Debug.Log($"    ✓ index_tip found at: {indexTip.position}");

                        // 손바닥과의 거리
                        float dist = Vector3.Distance(armature.position, indexTip.position);
                        Debug.Log($"    Distance palm→tip: {dist * 100f:F2}cm");
                    }
                    else
                    {
                        Debug.LogWarning("    ✗ index_tip NOT found!");

                        // 모든 자식 나열
                        Debug.Log("    Available children:");
                        foreach (Transform child in armature.GetComponentsInChildren<Transform>())
                        {
                            if (child != armature)
                            {
                                Debug.Log($"      - {child.name}");
                            }
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("  ✗ Armature NOT found!");

                    // 직계 자식 나열
                    Debug.Log("  Direct children of left hand:");
                    foreach (Transform child in leftHand.transform)
                    {
                        Debug.Log($"    - {child.name}");
                    }
                }
            }
            else
            {
                Debug.LogWarning("✗ Left hand NOT found in scene!");
            }

            Debug.Log("======================");
        }
    }

    Transform FindChildRecursive(Transform parent, string name)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>())
        {
            if (child.name.ToLower() == name.ToLower())
            {
                return child;
            }
        }
        return null;
    }
}
