using System.Collections.Generic;
using UnityEngine;

public class HandCanContactDetector
{
    private const float CONTACT_THRESHOLD = 0.03f; // 3cm

    public struct ContactPoint
    {
        public Vector3 canSurfacePoint;   // World space surface point (mesh vertex based)
        public Vector3 handJointPoint;    // World space joint position
        public int jointIndex;
        public float penetrationDepth;
        public Vector3 contactNormal;
        public float force;

        // UV data for normal map deformation
        public Vector2 uv;                // Texture coordinate at contact point (0~1)
        public bool hasValidUV;           // Whether UV extraction succeeded
    }

    public List<ContactPoint> DetectContacts(
        HandState hand,
        Transform canTransform,
        Mesh canMesh)
    {
        List<ContactPoint> contacts = new List<ContactPoint>();
        int[] fingerTips = new int[] { 5, 10, 15, 20, 25 };

        Collider canCollider = canTransform.GetComponent<Collider>();
        if (canCollider == null)
        {
            Debug.LogError("[ContactDetector] Can has no Collider!");
            return contacts;
        }

        // MeshCollider required for UV extraction
        MeshCollider meshCollider = canCollider as MeshCollider;
        bool canExtractUV = (meshCollider != null);

        Vector3[] vertices = canMesh.vertices;
        Vector2[] uvs = canMesh.uv;

        foreach (int jointIdx in fingerTips)
        {
            if (jointIdx >= hand.jointPositions.Length)
                continue;

            Vector3 jointWorldPos = hand.jointPositions[jointIdx];

            // 1) Collider-based contact detection
            Vector3 closestPoint = canCollider.ClosestPoint(jointWorldPos);
            float distance = Vector3.Distance(jointWorldPos, closestPoint);

            if (distance > CONTACT_THRESHOLD)
                continue;

            float penetration = CONTACT_THRESHOLD - distance;

            // 2) Convert collider point to can local space
            Vector3 localHit = canTransform.InverseTransformPoint(closestPoint);

            // 3) Find closest mesh vertex
            int closestVertex = FindClosestVertex(localHit, vertices);
            Vector3 epicenterLocal = vertices[closestVertex];
            Vector3 epicenterWorld = canTransform.TransformPoint(epicenterLocal);

            // 4) Extract UV
            Vector2 contactUV = Vector2.zero;
            bool hasUV = false;

            if (canExtractUV)
            {
                // Method 1: Accurate UV via raycast
                hasUV = TryGetUVFromRaycast(jointWorldPos, canTransform, meshCollider, out contactUV);
            }

            if (!hasUV && uvs != null && uvs.Length > closestVertex)
            {
                // Method 2: Fallback to closest vertex UV
                contactUV = uvs[closestVertex];
                hasUV = true;
            }

            // 5) Build contact point
            ContactPoint contact = new ContactPoint
            {
                canSurfacePoint = epicenterWorld,
                handJointPoint = jointWorldPos,
                jointIndex = jointIdx,
                penetrationDepth = penetration,
                contactNormal = (epicenterWorld - jointWorldPos).normalized,
                force = penetration,
                uv = contactUV,
                hasValidUV = hasUV
            };

            contacts.Add(contact);

            if (hasUV)
            {
                Debug.Log($"[ContactDetector] Joint {jointIdx} UV({contactUV.x:F3}, {contactUV.y:F3})");
            }
        }

        return contacts;
    }

    /// <summary>
    /// Extract accurate UV coordinates via raycast
    /// </summary>
    private bool TryGetUVFromRaycast(
        Vector3 jointWorldPos,
        Transform canTransform,
        MeshCollider meshCollider,
        out Vector2 uv)
    {
        uv = Vector2.zero;

        // Ray from finger toward can center
        Vector3 toCenter = (canTransform.position - jointWorldPos).normalized;
        Ray ray = new Ray(jointWorldPos, toCenter);

        RaycastHit hit;
        if (meshCollider.Raycast(ray, out hit, 0.15f))
        {
            uv = hit.textureCoord;
            return true;
        }

        // Try reverse direction (finger may already be inside)
        Ray reverseRay = new Ray(jointWorldPos, -toCenter);
        if (meshCollider.Raycast(reverseRay, out hit, 0.15f))
        {
            uv = hit.textureCoord;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Find the closest vertex to a local space point
    /// </summary>
    private int FindClosestVertex(Vector3 localPoint, Vector3[] vertices)
    {
        int closest = 0;
        float minDist = float.MaxValue;

        for (int i = 0; i < vertices.Length; i++)
        {
            float d = (vertices[i] - localPoint).sqrMagnitude;
            if (d < minDist)
            {
                minDist = d;
                closest = i;
            }
        }

        return closest;
    }
}