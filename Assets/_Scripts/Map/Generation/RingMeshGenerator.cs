using UnityEngine;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class RingMeshGenerator : MonoBehaviour
{
    [Header("Ring Dimensions")]
    [SerializeField] private float innerRadius = 10f;
    [SerializeField] private float outerRadius = 20f;
    [SerializeField] private int radialSegments = 64;
    [SerializeField] private int heightSegments = 1;

    [Header("Height Variation")]
    [SerializeField] private bool useHeightVariation = false;
    [SerializeField] private float heightScale = 1f;
    [SerializeField] private float noiseScale = 0.1f;
    [SerializeField] private Vector2 noiseOffset = Vector2.zero;

    [Header("UV Settings")]
    [SerializeField] private float uvTiling = 1f;

    private MeshFilter meshFilter;

    public void GenerateMesh()
    {
        if (meshFilter == null)
            meshFilter = GetComponent<MeshFilter>();

        Mesh mesh = CreateRingMesh();
        mesh.name = $"Ring_{innerRadius}_{outerRadius}";
        
        meshFilter.sharedMesh = mesh;
    }

    private Mesh CreateRingMesh()
    {
        Mesh mesh = new Mesh();

        int vertexCount = (radialSegments + 1) * (heightSegments + 1);
        Vector3[] vertices = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];

        int vertexIndex = 0;
        for (int h = 0; h <= heightSegments; h++)
        {
            float radialLerp = (float)h / heightSegments;

            for (int r = 0; r <= radialSegments; r++)
            {
                float angle = (float)r / radialSegments * Mathf.PI * 2f;
                float radius = Mathf.Lerp(innerRadius, outerRadius, radialLerp);

                float x = Mathf.Cos(angle) * radius;
                float z = Mathf.Sin(angle) * radius;
                float y = 0f;

                if (useHeightVariation)
                {
                    float noiseX = x * noiseScale + noiseOffset.x;
                    float noiseZ = z * noiseScale + noiseOffset.y;
                    y = Mathf.PerlinNoise(noiseX, noiseZ) * heightScale;
                }

                vertices[vertexIndex] = new Vector3(x, y, z);
                uvs[vertexIndex] = new Vector2((float)r / radialSegments * uvTiling, radialLerp * uvTiling);
                normals[vertexIndex] = Vector3.up;

                vertexIndex++;
            }
        }

        int triangleCount = radialSegments * heightSegments * 6;
        int[] triangles = new int[triangleCount];
        int triangleIndex = 0;

        for (int h = 0; h < heightSegments; h++)
        {
            for (int r = 0; r < radialSegments; r++)
            {
                int current = h * (radialSegments + 1) + r;
                int next = current + radialSegments + 1;

                triangles[triangleIndex++] = current;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = current + 1;

                triangles[triangleIndex++] = current + 1;
                triangles[triangleIndex++] = next;
                triangles[triangleIndex++] = next + 1;
            }
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.normals = normals;

        if (useHeightVariation)
        {
            mesh.RecalculateNormals();
        }

        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        return mesh;
    }

    public float InnerRadius => innerRadius;
    public float OuterRadius => outerRadius;
}
