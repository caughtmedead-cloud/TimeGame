using UnityEngine;
using CSGOperations;

public class CSGHelper : MonoBehaviour
{
    public GameObject lhs;
    public GameObject rhs;
    public CSG.BooleanOp operation = CSG.BooleanOp.Subtraction;
    public Material material;
    
    [Header("Performance Settings")]
    [Tooltip("Tolerance for plane comparison. Increase for larger meshes. Default is 0.00001")]
    public float epsilon = 0.1f;
    
    [Tooltip("Maximum vertices to process. Set lower for high-poly meshes.")]
    public int maxVertices = 10000;

    public void Perform()
    {
        if (lhs == null || rhs == null)
        {
            Debug.LogError("Both LHS and RHS GameObjects must be assigned!");
            return;
        }

        MeshFilter lhsFilter = lhs.GetComponent<MeshFilter>();
        MeshFilter rhsFilter = rhs.GetComponent<MeshFilter>();

        if (lhsFilter == null || rhsFilter == null || 
            lhsFilter.sharedMesh == null || rhsFilter.sharedMesh == null)
        {
            Debug.LogError("Both GameObjects must have MeshFilter with valid meshes!");
            return;
        }

        int lhsVertCount = lhsFilter.sharedMesh.vertexCount;
        int rhsVertCount = rhsFilter.sharedMesh.vertexCount;
        
        if (lhsVertCount > maxVertices || rhsVertCount > maxVertices)
        {
            Debug.LogError($"Mesh too complex! LHS: {lhsVertCount} verts, RHS: {rhsVertCount} verts. " +
                         $"Max allowed: {maxVertices}. Simplify your mesh or increase maxVertices.");
            return;
        }

        float originalEpsilon = CSG.epsilon;
        CSG.epsilon = epsilon;
        
        try
        {
            Debug.Log($"Starting CSG operation with epsilon: {epsilon}");
            
            Model result = CSG.Perform(operation, lhs, rhs);
            
            if (result != null)
            {
                GameObject composite = new GameObject();
                composite.AddComponent<MeshFilter>().sharedMesh = result.mesh;
                
                if (material != null)
                {
                    result.materials.Add(material);
                }
                
                composite.AddComponent<MeshRenderer>().sharedMaterials = result.materials.ToArray();
                composite.name = operation.ToString() + " Result";
                composite.transform.position = lhs.transform.position;
                
                Debug.Log($"CSG operation completed successfully! Result has {result.mesh.vertexCount} vertices.");
            }
            else
            {
                Debug.LogError("CSG operation returned null result!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"CSG operation failed: {e.Message}\n{e.StackTrace}");
        }
        finally
        {
            CSG.epsilon = originalEpsilon;
        }
    }
}
