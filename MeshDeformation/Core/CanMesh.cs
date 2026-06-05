using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BuckleRegion
{
    public Vector3 epicenterLocal;
    public float radius;
    public float depth;
    public float maxDepth;
    public bool isActive;
    public bool isAnimating;
    public float creationTime;
}

public class CanMesh
{
    public Mesh mesh;
    public Vector3[] originalVertices;
    public Vector3[] currentVertices;

    public VertexPhysicsState[] vertexStates;
    public List<BuckleRegion> buckleRegions = new List<BuckleRegion>();

    // Local center of mesh for stable normal calculation
    private Vector3 localCenter;

    // Accumulation mode:
    // false: recalculate from originalVertices every frame (stable, elastic-like)
    // true : accumulate on currentVertices (more crush feel, may break if excessive)
    public bool accumulateOnCurrent = false;

    [System.Serializable]
    public struct VertexPhysicsState
    {
        public float accumulatedStrain;
        public float plasticDeformation;
        public bool hasYielded;
        public Vector3 displacement;
    }

    public void Initialize(Mesh runtimeMesh)
    {
        mesh = runtimeMesh;

        originalVertices = (Vector3[])mesh.vertices.Clone();
        currentVertices = (Vector3[])originalVertices.Clone();

        vertexStates = new VertexPhysicsState[originalVertices.Length];

        // Compute local center (works even if mesh is not origin-centered)
        Vector3 sum = Vector3.zero;
        for (int i = 0; i < originalVertices.Length; i++) sum += originalVertices[i];
        localCenter = sum / Mathf.Max(1, originalVertices.Length);
    }

    public void ApplyDeformation(Transform canTransform)
    {
        if (mesh == null) return;
        if (originalVertices == null || originalVertices.Length == 0) return;

        float scale = Mathf.Max(1e-5f, canTransform.lossyScale.x);

        // Use currentVertices as base in accumulate mode, otherwise use originalVertices
        Vector3[] baseArray = accumulateOnCurrent ? currentVertices : originalVertices;

        for (int i = 0; i < currentVertices.Length; i++)
        {
            Vector3 baseVertex = baseArray[i];
            Vector3 totalDisplacement = Vector3.zero;

            foreach (var buckle in buckleRegions)
            {
                if (!buckle.isActive) continue;

                float scaledRadius = buckle.radius / scale;
                float scaledDepth = buckle.depth / scale;

                float dist = Vector3.Distance(baseVertex, buckle.epicenterLocal);
                if (dist > scaledRadius * 1.2f) continue;

                float normalizedDist = dist / scaledRadius;
                float influence = Mathf.Exp(-normalizedDist * normalizedDist * 5f);

                // Cylindrical radial normal corrected by local center
                Vector3 radial = baseVertex - localCenter;
                Vector3 normal = new Vector3(radial.x, 0f, radial.z);
                float nMag = normal.magnitude;
                if (nMag > 1e-6f) normal /= nMag;
                else normal = Vector3.right;

                // Displace inward only
                totalDisplacement -= normal * scaledDepth * influence;
            }

            currentVertices[i] = baseVertex + totalDisplacement;
            vertexStates[i].displacement = totalDisplacement;
        }

        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Force vertex move for debug testing
    public void ForceTestVertexMove(int index, Vector3 offset)
    {
        if (mesh == null) return;
        if (currentVertices == null || currentVertices.Length == 0) return;
        if (index < 0 || index >= currentVertices.Length) return;

        if (currentVertices.Length != mesh.vertexCount)
            currentVertices = mesh.vertices;

        currentVertices[index] += offset;

        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    // Reset mesh to original shape (called by ResetManager)
    public void ResetToOriginal()
    {
        if (mesh == null) return;
        if (originalVertices == null || originalVertices.Length == 0) return;

        buckleRegions.Clear();

        currentVertices = (Vector3[])originalVertices.Clone();
        mesh.vertices = currentVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}