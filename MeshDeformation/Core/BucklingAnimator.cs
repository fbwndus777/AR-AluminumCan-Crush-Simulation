using System.Collections;
using UnityEngine;

public class BucklingAnimator : MonoBehaviour
{
    [Header("Animation")]
    public float snapDuration = 0.05f;

    [Tooltip("Deformation depth (meters)")]
    public float targetDepth = 0.003f;

    [Tooltip("Deformation influence radius (meters)")]
    public float buckleRadius = 0.012f;

    [Header("Thumb Tuning")]
    public float thumbDepthMultiplier = 1.0f; // Increase to 1.2~1.6 for stronger thumb deformation

    public IEnumerator AnimateLocalBuckling(
        CanMesh canMesh,
        Vector3 buckleLocationWorld,
        Transform meshSpace,
        float severity,
        bool isThumb)
    {
        Vector3 buckleLocationLocal = meshSpace.InverseTransformPoint(buckleLocationWorld);

        BuckleRegion newBuckle = new BuckleRegion
        {
            epicenterLocal = buckleLocationLocal,
            radius = buckleRadius,
            depth = 0f,
            creationTime = Time.time,
            isActive = true,
            isAnimating = true
        };

        canMesh.buckleRegions.Add(newBuckle);

        float elapsed = 0f;

        float depthMul = isThumb ? thumbDepthMultiplier : 1f;
        float finalDepth = targetDepth * severity * depthMul;

        while (elapsed < snapDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / snapDuration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            newBuckle.depth = finalDepth * eased;
            canMesh.ApplyDeformation(meshSpace);

            yield return null;
        }

        newBuckle.depth = finalDepth;
        newBuckle.isAnimating = false;
        canMesh.ApplyDeformation(meshSpace);
    }
}