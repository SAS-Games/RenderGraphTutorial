using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class LowVertexCube : MonoBehaviour
{
    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = new Mesh();
        meshFilter.mesh = mesh;

        // 1. Define the 8 unique corner vertices
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, -0.5f), // 0
            new Vector3( 0.5f, -0.5f, -0.5f), // 1
            new Vector3( 0.5f,  0.5f, -0.5f), // 2
            new Vector3(-0.5f,  0.5f, -0.5f), // 3
            new Vector3(-0.5f,  0.5f,  0.5f), // 4
            new Vector3( 0.5f,  0.5f,  0.5f), // 5
            new Vector3( 0.5f, -0.5f,  0.5f), // 6
            new Vector3(-0.5f, -0.5f,  0.5f)  // 7
        };

        // 2. Define the 12 triangles (2 per face) using the 8 vertices
        mesh.triangles = new int[]
        {
            0, 2, 1,  0, 3, 2, // Front
            2, 3, 4,  2, 4, 5, // Top
            1, 2, 5,  1, 5, 6, // Right
            0, 7, 4,  0, 4, 3, // Left
            5, 4, 7,  5, 7, 6, // Back
            0, 1, 6,  0, 6, 7  // Bottom
        };

        // 3. Automatically calculate boundaries and lighting
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
    }
}