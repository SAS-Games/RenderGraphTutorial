using UnityEngine;

[ExecuteAlways]
public class NormalVisualizer : MonoBehaviour
{
    private void OnDrawGizmos()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();

        if (!meshFilter)
            return;

        Mesh mesh = meshFilter.sharedMesh;

        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 worldPos = transform.TransformPoint(vertices[i]);
            Vector3 worldNormal = transform.TransformDirection(normals[i]);

            Gizmos.color = Color.green;

            Gizmos.DrawLine(worldPos, worldPos + worldNormal * 0.25f);
        }
    }
}