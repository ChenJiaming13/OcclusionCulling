using System.Linq;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;

[RequireComponent(typeof(Camera))]
public class FrustumCulling : MonoBehaviour
{
    [SerializeField] private int[] resultIndices;
    [SerializeField] private int numResults;
    private Camera _camera;
    private Collider[] _colliders;
    // private Renderer[] _renderers;
    private NativeArray<Plane> _planes;
    private NativeArray<Bounds> _boundsArray;
    private NativeArray<bool> _cullingResults;
    private FrustumCullingJob _frustumCullingJob;
    private JobHandle _frustumCullingJobHandle;
    
    private void Start()
    {
        _camera = GetComponent<Camera>();
        const int numFrustumPlanes = 6;
        _planes = new NativeArray<Plane>(numFrustumPlanes, Allocator.Persistent);
        
        // Only support static objects
        _colliders = FindObjectsByType<Collider>(FindObjectsSortMode.InstanceID)
            .Where(coll => coll.GetComponent<Renderer>() != null).ToArray();
        // _renderers = _colliders.Select(coll => coll.GetComponent<Renderer>()).ToArray();
        var boundsArray = _colliders.Select(coll => coll.bounds).ToArray();
        _boundsArray = new NativeArray<Bounds>(boundsArray, Allocator.Persistent);
        _cullingResults = new NativeArray<bool>(boundsArray.Length, Allocator.Persistent);
        resultIndices = new int[_cullingResults.Length];
    }

    private void Update()
    {
        Profiler.BeginSample("FrustumCulling");
        var planes = GeometryUtility.CalculateFrustumPlanes(_camera);
        for (var i = 0; i < planes.Length; i++)
        {
            _planes[i] = planes[i];
        }

        _frustumCullingJob = new FrustumCullingJob()
        {
            Planes = _planes,
            BoundsArray = _boundsArray,
            CullingResults = _cullingResults
        };
        _frustumCullingJobHandle = _frustumCullingJob.Schedule(_boundsArray.Length, 64);
        _frustumCullingJobHandle.Complete();

        Profiler.BeginSample("Update Renderer");
        // numResults = 0;
        // for (var i = 0; i < _frustumCullingJob.CullingResults.Length; i++)
        // {
        //     // 1.
        //     // _colliders[i].gameObject.SetActive(_frustumCullingJob.CullingResults[i]);
        //     // 2.
        //     // _renderers[i].enabled = _frustumCullingJob.CullingResults[i];
        //     // 3.
        //     // _renderers[i].shadowCastingMode = _frustumCullingJob.CullingResults[i]
        //     //     ? ShadowCastingMode.On
        //     //     : ShadowCastingMode.ShadowsOnly;
        //     if (_frustumCullingJob.CullingResults[i]) resultIndices[numResults++] = i;
        // }
        Profiler.EndSample();
        Profiler.EndSample();
    }

    private void OnDestroy()
    {
        _planes.Dispose();
        _boundsArray.Dispose();
        _cullingResults.Dispose();
    }

    [BurstCompile]
    private struct FrustumCullingJob : IJobParallelFor
    {
        [ReadOnly]
        public NativeArray<Plane> Planes;
        [ReadOnly]
        public NativeArray<Bounds> BoundsArray;
        public NativeArray<bool> CullingResults;
    
        public void Execute(int index)
        {
            var bounds = BoundsArray[index];
            var isVisible = true;
            
            foreach (var plane in Planes)
            {
                var center = bounds.center;
                var extents = bounds.extents;

                var distance = plane.GetDistanceToPoint(center);
                var radius = extents.x * Mathf.Abs(plane.normal.x)
                             + extents.y * Mathf.Abs(plane.normal.y)
                             + extents.z * Mathf.Abs(plane.normal.z);

                if (distance + radius < 0)
                {
                    isVisible = false;
                    break;
                }
            }

            CullingResults[index] = isVisible;
        }
    }
}