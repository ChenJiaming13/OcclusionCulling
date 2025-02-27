using System.Linq;
using UnityEngine;

public class StatModelInfo : MonoBehaviour
{
    [SerializeField] private int numVertices;
    [SerializeField] private int numTriangles;
    
    public void StatVertAndTriInfo()
    {
        var meshFilters = GetComponentsInChildren<MeshFilter>().ToList();
        numVertices = 0;
        numTriangles = 0;
        foreach (var meshFilter in meshFilters.Where(meshFilter => meshFilter.gameObject.activeSelf))
        {
            Debug.Log(meshFilter.gameObject.name);
            numVertices += meshFilter.sharedMesh.vertexCount;
            numTriangles += meshFilter.sharedMesh.triangles.Length / 3;
        }
    }
}