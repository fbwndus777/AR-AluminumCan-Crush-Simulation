using System.Collections.Generic;
using UnityEngine;

public class HandCanContactDetector
{
    private const float CONTACT_THRESHOLD = 0.03f;

    public struct ContactPoint
    {
        public Vector3 canSurfacePoint;
        public Vector3 handJointPoint;
        public int jointIndex;
        public float penetrationDepth;
        public Vector3 contactNormal;
        public float force;
    }

    public List<ContactPoint> DetectContacts(
        HandState hand,
        Transform canTransform,
        Mesh canMesh)
    {
        List<ContactPoint> contacts = new List<ContactPoint>();

        int[] fingerTips = { 5, 10, 15, 20, 25 };

        Collider canCollider = canTransform.GetComponent<Collider>();

        if (canCollider == null)
        {
            Debug.LogError("Can has no collider");
            return contacts;
        }

        Vector3[] vertices = canMesh.vertices;

        foreach (int jointIdx in fingerTips)
        {
            if (jointIdx >= hand.jointPositions.Length)
                continue;

            Vector3 jointWorldPos = hand.jointPositions[jointIdx];

            Vector3 closestPoint = canCollider.ClosestPoint(jointWorldPos);

            float distance = Vector3.Distance(jointWorldPos, closestPoint);

            if (distance > CONTACT_THRESHOLD)
                continue;

            float penetration = CONTACT_THRESHOLD - distance;

            // Use world-space closest point directly as contact surface point
            ContactPoint contact = new ContactPoint
            {
                canSurfacePoint = closestPoint,
                handJointPoint = jointWorldPos,
                jointIndex = jointIdx,
                penetrationDepth = penetration,
                contactNormal = (closestPoint - jointWorldPos).normalized,
                force = penetration
            };

            contacts.Add(contact);
        }

        return contacts;
    }
}