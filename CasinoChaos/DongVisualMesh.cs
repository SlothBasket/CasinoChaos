using System.Collections.Generic;
using UnityEngine;

namespace GWYF_CasinoChaos
{
    // Small faceted silhouette, centered below a top-mounted pivot. No collider.
    internal static class DongVisualMesh
    {
        internal static Mesh Create(bool broadTip)
        {
            const int sides = 8;
            float[] heights = { .5f, .46f, .34f, -.20f, -.28f, -.33f, -.40f, -.47f, -.5f };
            float[] radii = broadTip
                ? new[] { .43f, .5f, .42f, .38f, .39f, .47f, .5f, .34f, 0 }
                : new[] { .43f, .5f, .42f, .36f, .35f, .43f, .42f, .27f, 0 };
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uv = new List<Vector2>();
            Vector3 Point(int ring, int side)
            {
                float angle = side * (2 * Mathf.PI / sides);
                return new Vector3(Mathf.Cos(angle) * radii[ring], heights[ring], Mathf.Sin(angle) * radii[ring]);
            }
            void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                int i = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                triangles.Add(i); triangles.Add(i+1); triangles.Add(i+2);
                uv.Add(Vector2.zero); uv.Add(Vector2.zero); uv.Add(Vector2.zero);
            }
            for(int ring=0;ring<heights.Length-1;ring++)
                for(int side=0;side<sides;side++)
                {
                    var a=Point(ring,side); var b=Point(ring,side+1);
                    var c=Point(ring+1,side); var d=Point(ring+1,side+1);
                    Triangle(a,b,c);
                    if(radii[ring+1]>0)Triangle(b,d,c);
                }
            for(int side=0;side<sides;side++)Triangle(new Vector3(0,.5f,0),Point(0,side+1),Point(0,side));
            var mesh=new Mesh {name="CasinoChaosFacetedDong"};
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);
            mesh.RecalculateNormals();mesh.RecalculateBounds();
            return mesh;
        }
    }
}
