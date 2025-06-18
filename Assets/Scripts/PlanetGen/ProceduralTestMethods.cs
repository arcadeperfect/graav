// using Unity.Mathematics;
// using UnityEngine;
//
//
// namespace PlanetGen
// {
//     public class ProceduralTestMethods
//     {
//         void GenerateAndRenderTestQuads(int fieldWidth)
//         {
//             int quad_count = 10;
//
//             // vertexCount = quad_count * 6;
//             // vertexBuffer = new ComputeBuffer(60, sizeof(float) * 3);
//             // vertexColorBuffer = new ComputeBuffer(60, sizeof(float) * 4);
//
//
//             int max_segments = (fieldWidth - 1) * (fieldWidth - 1) * 4;
//             vertexBuffer = new ComputeBuffer(max_segments * 6, sizeof(float) * 3);
//             vertexColorBuffer = new ComputeBuffer(max_segments * 6, sizeof(float) * 4);
//
//             // int z = 60;
//             // vertexBuffer = new ComputeBuffer(z, sizeof(float) * 3);
//             // vertexColorBuffer = new ComputeBuffer(z, sizeof(float) * 4);
//
//
//             vertexColorBuffer.SetData(GenerateDummyColors(quad_count));
//
//
//             var quads = GenerateDummyQuads(quad_count);
//             vertexBuffer.SetData(quads);
//
//             material.SetBuffer("VertexBuffer", vertexBuffer);
//             material.SetBuffer("VertexColorBuffer", vertexColorBuffer);
//         }
//
//         Vector4[] GenerateDummySegments()
//         {
//             Vector4[] segments = new Vector4[4];
//             segments[0] = new Vector4(-0.5f, 0, 0.5f, 0);
//             segments[1] = new Vector4(0.5f, 0, 0, 0.5f);
//             segments[2] = new Vector4(0, 0.5f, -0.75f, 0.75f);
//             segments[3] = new Vector4(-0.75f, 0.75f, -0.75f, -0.75f);
//             return segments;
//         }
//
//
//         Vector3[] GenerateDummyQuads(int count)
//         {
//             Vector3[] verts = new Vector3[count * 6]; // 6 vertices per quad (2 triangles)
//             System.Random rand = new System.Random();
//
//             for (int i = 0; i < count; i++)
//             {
//                 // Generate random quad corners
//                 float minX = (float)(rand.NextDouble() * 2 - 1); // -1 to 1
//                 float maxX = (float)(rand.NextDouble() * 2 - 1);
//                 float minY = (float)(rand.NextDouble() * 2 - 1);
//                 float maxY = (float)(rand.NextDouble() * 2 - 1);
//                 float z = 0f;
//
//                 // Ensure min/max are correct
//                 if (minX > maxX)
//                 {
//                     float temp = minX;
//                     minX = maxX;
//                     maxX = temp;
//                 }
//
//                 if (minY > maxY)
//                 {
//                     float temp = minY;
//                     minY = maxY;
//                     maxY = temp;
//                 }
//
//                 // Define quad corners
//                 Vector3 bl = new Vector3(minX, minY, z); // bottom-left
//                 Vector3 tl = new Vector3(minX, maxY, z); // top-left
//                 Vector3 br = new Vector3(maxX, minY, z); // bottom-right
//                 Vector3 tr = new Vector3(maxX, maxY, z); // top-right
//
//                 // First triangle (bl, tl, br)
//                 int baseIndex = i * 6;
//                 verts[baseIndex] = bl;
//                 verts[baseIndex + 1] = tl;
//                 verts[baseIndex + 2] = br;
//
//                 // Second triangle (br, tl, tr)
//                 verts[baseIndex + 3] = br;
//                 verts[baseIndex + 4] = tl;
//                 verts[baseIndex + 5] = tr;
//             }
//
//             return verts;
//         }
//
//         Vector4[] GenerateDummyColors(int vertCount)
//         {
//             // count = count * 6;
//             Vector4[] colors = new Vector4[vertCount];
//             for (int i = 0; i < vertCount; i++)
//             {
//                 colors[i] = RandomColor();
//             }
//
//             return colors;
//         }
//     }
// }