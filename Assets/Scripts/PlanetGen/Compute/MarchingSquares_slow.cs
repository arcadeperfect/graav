// using System.Collections.Generic;
// using PlanetGen.FieldGen2.Types;
// using Unity.Mathematics;
// using UnityEngine;
// // using PlanetGen.FieldGen;
//
// namespace PlanetGen.FieldGen2
// {
//     /// <summary>
//     /// Provides a CPU implementation of the Marching Squares algorithm that is designed to
//     /// produce identical results to the corresponding GPU compute shader version.
//     /// It processes a scalar field to generate line segments representing the iso-surface.
//     /// </summary>
//     public static class MarchingSquaresCPU_Slow
//     {
//         // A struct to hold the start and end colors of a line segment, mirroring the GPU version.
//         public struct SegmentColor
//         {
//             public Color color1;
//             public Color color2;
//         }
//
//         /// <summary>
//         /// Generates line segments from scalar field data using the Marching Squares algorithm.
//         /// </summary>
//         /// <param name="fieldData">The input field data containing scalar values and colors.</param>
//         /// <param name="isoValue">The threshold value to generate the iso-surface contour.</param>
//         /// <returns>A tuple containing a list of segment positions (Vector4) and a list of segment colors (two per segment).</returns>
//         public static (List<Vector4> segments, List<SegmentColor> segmentColors) GenerateSegments(FieldData fieldData, float isoValue)
//         {
//             var segments = new List<Vector4>();
//             var segmentColors = new List<SegmentColor>();
//             int width = fieldData.Size;
//             int height = fieldData.Size; // Assuming square texture
//
//             for (int x = 0; x < width - 1; x++)
//             {
//                 for (int y = 0; y < height - 1; y++)
//                 {
//                     // Sample the scalar values at the four corners of the cell.
//                     // float v00 = fieldData.GetScalarValue(x, y);         // bottom-left
//                     // float v10 = fieldData.GetScalarValue(x + 1, y);     // bottom-right
//                     // float v11 = fieldData.GetScalarValue(x + 1, y + 1); // top-right
//                     // float v01 = fieldData.GetScalarValue(x, y + 1);     // top-left
//
//                     float v00 = fieldData.BaseRasterData.GetScalarAt(x, y);
//                     float v10 = fieldData.BaseRasterData.GetScalarAt(x + 1, y);
//                     float v11 = fieldData.BaseRasterData.GetScalarAt(x + 1, y + 1);
//                     float v01 = fieldData.BaseRasterData.GetScalarAt(x, y + 1)
// ;
//                     // Sample the colors at the four corners of the cell.
//                     // float4 c00_f4 = fieldData.GetColorValue(x, y);
//                     // float4 c10_f4 = fieldData.GetColorValue(x + 1, y);
//                     // float4 c11_f4 = fieldData.GetColorValue(x + 1, y + 1);
//                     // float4 c01_f4 = fieldData.GetColorValue(x, y + 1);
//                     
//                     float4 c00_f4 = fieldData.BaseRasterData.GetColorAt(x, y);
//                     float4 c10_f4 = fieldData.BaseRasterData.GetColorAt(x + 1, y);
//                     float4 c11_f4 = fieldData.BaseRasterData.GetColorAt(x + 1, y + 1);
//                     float4 c01_f4 = fieldData.BaseRasterData.GetColorAt(x, y + 1);
//                     
//                     
//                     Color c00 = new Color(c00_f4.x, c00_f4.y, c00_f4.z, c00_f4.w);
//                     Color c10 = new Color(c10_f4.x, c10_f4.y, c10_f4.z, c10_f4.w);
//                     Color c11 = new Color(c11_f4.x, c11_f4.y, c11_f4.z, c11_f4.w);
//                     Color c01 = new Color(c01_f4.x, c01_f4.y, c01_f4.z, c01_f4.w);
//
//                     // Determine the case index based on which corners are inside the iso-surface.
//                     int caseIndex = 0;
//                     if (v00 > isoValue) caseIndex |= 1; // bottom-left
//                     if (v10 > isoValue) caseIndex |= 2; // bottom-right
//                     if (v11 > isoValue) caseIndex |= 4; // top-right
//                     if (v01 > isoValue) caseIndex |= 8; // top-left
//
//                     // If all corners are inside or all are outside, there is no line segment.
//                     if (caseIndex == 0 || caseIndex == 15)
//                     {
//                         continue;
//                     }
//
//                     // Calculate the interpolated vertex positions on each of the four cell edges.
//                     Vector2 edge0 = InterpolateEdgeNormalized(new Vector2Int(x, y), new Vector2Int(x + 1, y), v00, v10, isoValue, width, height); // bottom
//                     Vector2 edge1 = InterpolateEdgeNormalized(new Vector2Int(x + 1, y), new Vector2Int(x + 1, y + 1), v10, v11, isoValue, width, height); // right
//                     Vector2 edge2 = InterpolateEdgeNormalized(new Vector2Int(x, y + 1), new Vector2Int(x + 1, y + 1), v01, v11, isoValue, width, height); // top
//                     Vector2 edge3 = InterpolateEdgeNormalized(new Vector2Int(x, y), new Vector2Int(x, y + 1), v00, v01, isoValue, width, height); // left
//
//                     // Calculate the interpolated colors at each of the new vertices.
//                     Color color0 = InterpolateColor(c00, c10, v00, v10, isoValue); // bottom
//                     Color color1 = InterpolateColor(c10, c11, v10, v11, isoValue); // right
//                     Color color2 = InterpolateColor(c01, c11, v01, v11, isoValue); // top
//                     Color color3 = InterpolateColor(c00, c01, v00, v01, isoValue); // left
//
//                     // Use the case index to determine which line segments to generate.
//                     switch (caseIndex)
//                     {
//                         case 1: case 14:
//                             AddSegment(segments, segmentColors, edge3, edge0, color3, color0);
//                             break;
//                         case 2: case 13:
//                             AddSegment(segments, segmentColors, edge0, edge1, color0, color1);
//                             break;
//                         case 3: case 12:
//                             AddSegment(segments, segmentColors, edge3, edge1, color3, color1);
//                             break;
//                         case 4: case 11:
//                             AddSegment(segments, segmentColors, edge1, edge2, color1, color2);
//                             break;
//                         case 5: // Saddle point
//                             AddSegment(segments, segmentColors, edge3, edge0, color3, color0);
//                             AddSegment(segments, segmentColors, edge1, edge2, color1, color2);
//                             break;
//                         case 6: case 9:
//                             AddSegment(segments, segmentColors, edge0, edge2, color0, color2);
//                             break;
//                         case 7: case 8:
//                             AddSegment(segments, segmentColors, edge2, edge3, color2, color3);
//                             break;
//                         case 10: // Saddle point
//                             AddSegment(segments, segmentColors, edge0, edge1, color0, color1);
//                             AddSegment(segments, segmentColors, edge2, edge3, color2, color3);
//                             break;
//                     }
//                 }
//             }
//
//             return (segments, segmentColors);
//         }
//
//         private static void AddSegment(List<Vector4> segments, List<SegmentColor> colors, Vector2 start, Vector2 end, Color color1, Color color2)
//         {
//             segments.Add(new Vector4(start.x, start.y, end.x, end.y));
//             colors.Add(new SegmentColor { color1 = color1, color2 = color2 });
//         }
//
//         // Replicates the HLSL shader's edge interpolation.
//         private static Vector2 InterpolateEdgeNormalized(Vector2Int p1, Vector2Int p2, float v1, float v2, float iso, int width, int height)
//         {
//             if (Mathf.Abs(v1 - v2) < 0.00001f)
//             {
//                 return TextureToNormalized(p1, width, height);
//             }
//
//             float t = (iso - v1) / (v2 - v1);
//             Vector2 interpTexCoord = Vector2.Lerp(p1, p2, t);
//
//             return TextureToNormalized(interpTexCoord, width, height);
//         }
//
//         // Replicates the HLSL shader's color interpolation.
//         private static Color InterpolateColor(Color c1, Color c2, float v1, float v2, float iso)
//         {
//             if (Mathf.Abs(v1 - v2) < 0.00001f)
//             {
//                 return c1;
//             }
//             float t = (iso - v1) / (v2 - v1);
//             return Color.Lerp(c1, c2, t);
//         }
//
//         // Converts texture coordinates to normalized [-1, 1] space.
//         private static Vector2 TextureToNormalized(Vector2 texCoord, int width, int height)
//         {
//             float normX = (texCoord.x / width) * 2.0f - 1.0f;
//             float normY = (texCoord.y / height) * 2.0f - 1.0f;
//             return new Vector2(normX, normY);
//         }
//     }
// }
