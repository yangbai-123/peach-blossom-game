using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 程序化花瓣网格：一片有弧度、有尖端的桃花瓣。
///
/// 不用贴图，靠几何本身成形：
///   - 沿长度方向宽度先增后减，形成桃花瓣的轮廓
///   - 沿长度方向略微卷曲（curl），有立体感
///   - 正反两面都有面片，从任何角度看都完整
/// </summary>
public static class PetalMesh
{
    public static Mesh Create(int segments = 16, float curl = 0.20f, float widthScale = 0.66f)
    {
        var mesh = new Mesh { name = "Petal" };

        int ringCount = segments + 1;
        var front = new Vector3[ringCount * 2];
        var uv = new Vector2[ringCount * 2];

        for (int i = 0; i < ringCount; i++)
        {
            float t = i / (float)segments;

            // 宽度：根部窄、中段宽、尖端收成一点
            float width = Mathf.Sin(Mathf.Pow(t, 0.7f) * Mathf.PI) * 0.5f * widthScale;

            // 卷曲：中间鼓起，两端贴合
            float z = Mathf.Sin(t * Mathf.PI) * curl;

            // 尖端稍微翘起
            float y = t + Mathf.Pow(t, 3f) * 0.06f;

            front[i * 2] = new Vector3(-width, y, z);
            front[i * 2 + 1] = new Vector3(width, y, z);
            uv[i * 2] = new Vector2(0f, t);
            uv[i * 2 + 1] = new Vector2(1f, t);
        }

        var vertices = new List<Vector3>(ringCount * 4);
        var uvs = new List<Vector2>(ringCount * 4);
        var triangles = new List<int>(segments * 12);

        // 正面
        vertices.AddRange(front);
        uvs.AddRange(uv);

        for (int i = 0; i < segments; i++)
        {
            int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
            triangles.Add(a); triangles.Add(c); triangles.Add(b);
            triangles.Add(b); triangles.Add(c); triangles.Add(d);
        }

        // 背面：顶点复制一份，绕序反过来
        int offset = vertices.Count;
        vertices.AddRange(front);
        uvs.AddRange(uv);

        for (int i = 0; i < segments; i++)
        {
            int a = offset + i * 2, b = offset + i * 2 + 1;
            int c = offset + (i + 1) * 2, d = offset + (i + 1) * 2 + 1;
            triangles.Add(a); triangles.Add(b); triangles.Add(c);
            triangles.Add(b); triangles.Add(d); triangles.Add(c);
        }

        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }
}
