namespace Raft {
    using System.Runtime.InteropServices;
    using Walker.Data.Geometry.Speed.Plane;
    using Walker.Data.Geometry.Speed.Space;

    [StructLayout(LayoutKind.Sequential)]
    public struct Vertex {
        public Vector3F pos, norm, col;
        //public Vector2F tex;

        public Vertex(Vector3F p, Vector3F n, Vector3F col) {
            this.pos = p;
            this.norm = n;
            this.col = col;
        }

        public Vertex(float px, float py, float pz,
                      float nx, float ny, float nz,
                      float cx, float cy, float cz) {
            this.pos = new Vector3F(px, py, pz);
            this.norm = new Vector3F(nx, ny, nz);
            this.col = new Vector3F(cx, cy, cz);
            //this.tex = new Vector2F(tx, ty);
        }
    }

    public class Primitive {

        public Vertex[] verts;
        public int[] indices;

        public Primitive(Vertex[] verts, int[] indices) {
            this.verts = verts;
            this.indices = indices;
        }

        public static Primitive Box(float width, float height, float depth) {
            float w2 = 0.5f * width;
            float h2 = 0.5f * height;
            float d2 = 0.5f * depth;

            Vertex[] vertices = {
                // Fill in the front face vertex data.
                new Vertex(-w2, +h2, -d2, +0, +0, -1, +1, +0, 0),
                new Vertex(-w2, -h2, -d2, +0, +0, -1, +1, +0, 0),
                new Vertex(+w2, -h2, -d2, +0, +0, -1, +1, +0, 0),
                new Vertex(+w2, +h2, -d2, +0, +0, -1, +1, +0, 0),
                // Fill in the back face vertex data.
                new Vertex(-w2, +h2, +d2, +0, +0, +1, +1, +.5f, 0),
                new Vertex(+w2, +h2, +d2, +0, +0, +1, +1, +.5f, 0),
                new Vertex(+w2, -h2, +d2, +0, +0, +1, +1, +.5f, 0),
                new Vertex(-w2, -h2, +d2, +0, +0, +1, +1, +.5f, 0),
                // Fill in the top face vertex data.
                new Vertex(-w2, -h2, -d2, +0, +1, +0, +0, +0, 1),
                new Vertex(-w2, -h2, +d2, +0, +1, +0, +0, +0, 1),
                new Vertex(+w2, -h2, +d2, +0, +1, +0, +0, +0, 1),
                new Vertex(+w2, -h2, -d2, +0, +1, +0, +0, +0, 1),
                // Fill in the bottom face vertex data.
                new Vertex(-w2, +h2, -d2, +0, -1, +0, +.5f, +0, 1),
                new Vertex(+w2, +h2, -d2, +0, -1, +0, +.5f, +0, 1),
                new Vertex(+w2, +h2, +d2, +0, -1, +0, +.5f, +0, 1),
                new Vertex(-w2, +h2, +d2, +0, -1, +0, +.5f, +0, 1),
                // Fill in the left face vertex data.
                new Vertex(-w2, +h2, +d2, -1, +0, +0, +0, +1, 0),
                new Vertex(-w2, -h2, +d2, -1, +0, +0, +0, +1, 0),
                new Vertex(-w2, -h2, -d2, -1, +0, +0, +0, +1, 0),
                new Vertex(-w2, +h2, -d2, -1, +0, +0, +0, +1, 0),
                // Fill in the right face vertex data.
                new Vertex(+w2, +h2, -d2, +1, +0, +0, +0, +1, .5f),
                new Vertex(+w2, -h2, -d2, +1, +0, +0, +0, +1, .5f),
                new Vertex(+w2, -h2, +d2, +1, +0, +0, +0, +1, .5f),
                new Vertex(+w2, +h2, +d2, +1, +0, +0, +0, +1, .5f)
            };

            int[] indices = {
                // Fill in the front face index data.
                0, 1, 2, 0, 2, 3,
                // Fill in the back face index data.
                4, 5, 6, 4, 6, 7,
                // Fill in the top face index data.
                8, 9, 10, 8, 10, 11,
                // Fill in the bottom face index data.
                12, 13, 14, 12, 14, 15,
                // Fill in the left face index data
                16, 17, 18, 16, 18, 19,
                // Fill in the right face index data
                20, 21, 22, 20, 22, 23
            };

            return new Primitive(vertices, indices);
        }

    }


}