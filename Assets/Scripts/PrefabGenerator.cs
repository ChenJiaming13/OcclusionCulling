using System.Linq;
using UnityEngine;

public class PrefabGenerator : MonoBehaviour
{
    [SerializeField] private GameObject[] prefabs;
    [SerializeField] private Material[] materials;
    [SerializeField] private Transform parent;
    [SerializeField] private int numSpawned;
    [SerializeField] private Vector3 minScale = Vector3.one;
    [SerializeField] private Vector3 maxScale = Vector3.one;
    [SerializeField] private Bounds spawnBounds;
    [SerializeField] private Vector3 rotationRange = new Vector3(360f, 360f, 360f);
    
    public void SpawnPrefabs()
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            Debug.LogError("Prefab array is empty!");
            return;
        }

        for (var i = 0; i < numSpawned; i++)
        {
            var prefab = prefabs[Random.Range(0, prefabs.Length)];
            var spawnedObject = Instantiate(prefab, parent);
            spawnedObject.transform.localScale = new Vector3(
                Random.Range(minScale.x, maxScale.x),
                Random.Range(minScale.y, maxScale.y),
                Random.Range(minScale.z, maxScale.z)
            );
            spawnedObject.transform.position = new Vector3(
                Random.Range(spawnBounds.min.x, spawnBounds.max.x),
                Random.Range(spawnBounds.min.y, spawnBounds.max.y),
                Random.Range(spawnBounds.min.z, spawnBounds.max.z)
            ) + spawnedObject.transform.localScale * 0.5f;
            // spawnedObject.transform.localRotation = randomizeRotation ? Random.rotation : Quaternion.identity;
            spawnedObject.transform.localRotation = Quaternion.Euler(
                Random.Range(0.0f, 1.0f) * rotationRange.x,
                Random.Range(0.0f, 1.0f) * rotationRange.y,
                Random.Range(0.0f, 1.0f) * rotationRange.z
            );
            if (materials == null || materials.Length == 0) continue;
            var currRenderer = spawnedObject.GetComponent<Renderer>();
            if (currRenderer) currRenderer.material = materials[Random.Range(0, materials.Length)];
        }
    }

    public void ClearPrefabs()
    {
        if (parent == null)
        {
            Debug.LogWarning("Ensure parent is set!");
            return;
        }

        var childObjects = (from Transform child in parent.transform select child.gameObject).ToArray();
        foreach (var childObject in childObjects)
        {
            DestroyImmediate(childObject);
        }
        Debug.Log(childObjects.Length + " prefabs have been deleted!");
    }
    
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        var center = parent == null ? spawnBounds.center : parent.position + spawnBounds.center;
        var size = parent == null ? spawnBounds.size : new Vector3(
            spawnBounds.size.x * parent.localScale.x,
            spawnBounds.size.y * parent.localScale.y,
            spawnBounds.size.z * parent.localScale.z
        );
        Gizmos.DrawWireCube(center, size);
    }
}