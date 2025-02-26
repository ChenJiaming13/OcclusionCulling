using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

public class TestSimd : MonoBehaviour
{
    [ContextMenu("Test")]
    private void Test()
    {
        
        // var a = new v128(1.0f, 2.0f, 3.0f, 4.0f);
        // var b = new v128(5.0f, 6.0f, 7.0f, 8.0f);
        // var c = X86.Sse.mul_ps(a, b); // 5 12 21 32
        // var d = X86.Sse.set1_ps(123.0f); // 123 123 123 123
        // var e = X86.Sse.shuffle_ps(a, b, 0x44); // 1 2 5 6
        // var f = X86.Sse.shuffle_ps(a, b, 0xEE); // 3 4 7 8
        // var g = X86.Sse.shuffle_ps(a, b, 0x88); // 1 3 5 7
        // var h = X86.Sse.shuffle_ps(a, b, 0xDD); // 2 4 6 8
    }

    [BurstCompile]
    private static void SortVertices(ref float4x3 vtxX, ref float4x3 vtxY)
    {
        // Rotate the triangle in the winding order until v0 is the vertex with lowest Y value
        for (var i = 0; i < 2; i++)
        {
            var ey1 = vtxY[1] - vtxY[0];
            var ey2 = vtxY[2] - vtxY[0];

            // 生成交换掩码：当ey1或ey2为负，或ey2等于0时交换
            var swapMask = (ey1 < 0) | (ey2 < 0) | (ey2 == 0);

            // 使用掩码交换X坐标
            var sX = math.select(vtxX[2], vtxX[0], swapMask);
            vtxX[0] = math.select(vtxX[0], vtxX[1], swapMask);
            vtxX[1] = math.select(vtxX[1], vtxX[2], swapMask);
            vtxX[2] = sX;

            // 使用掩码交换Y坐标
            var sY = math.select(vtxY[2], vtxY[0], swapMask);
            vtxY[0] = math.select(vtxY[0], vtxY[1], swapMask);
            vtxY[1] = math.select(vtxY[1], vtxY[2], swapMask);
            vtxY[2] = sY;
        }
    }
    
    [BurstCompile]
    private static float3 Barycentric(float2 a, float2 b, float2 c, float2 p)
    {
        var v0 = b - a;
        var v1 = c - a;
        var v2 = p - a;
        var d00 = math.dot(v0, v0);
        var d01 = math.dot(v0, v1);
        var d11 = math.dot(v1, v1);
        var d20 = math.dot(v2, v0);
        var d21 = math.dot(v2, v1);
        var denominator = d00 * d11 - d01 * d01;
        var v = (d11 * d20 - d01 * d21) / denominator;
        var w = (d00 * d21 - d01 * d20) / denominator;
        var u = 1.0f - v - w;
        return new float3(u, v, w);
    }
}