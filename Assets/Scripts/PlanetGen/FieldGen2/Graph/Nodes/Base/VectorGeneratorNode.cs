// using System.Collections.Generic;
// using Unity.Collections;
// using Unity.Jobs;
// using Unity.Mathematics;
// using UnityEngine;
// using XNode;
//
// namespace PlanetGen.FieldGen2.Graph.Nodes.Base
// {
//     // Vector port wrapper class for compile-time type safety
//     [System.Serializable]
//     public class VectorPort : BaseNode
//     {
//         public override object GetValue(NodePort port) => null;
//     }
//
//     // Interface for vector outputs - polylines in polar coordinates
//     public interface IVectorOutput
//     {
//         JobHandle ScheduleVector(JobHandle dependency, int textureSize,
//             TempBufferManager tempBuffers, ref VectorData outputBuffer);
//     }
//
//     /// <summary>
//     /// Base class for all vector generator nodes.
//     /// These nodes output polylines as arrays of float2 in polar coordinates (angle, radius).
//     /// Angle is in radians [-π, π], radius is normalized [0, 1].
//     /// </summary>
//     public abstract class VectorGeneratorNode : BaseNode, IVectorOutput
//     {
//         [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
//         public VectorPort output;
//
//         [Header("Vector Parameters")]
//         [Tooltip("Maximum number of vertices this generator can produce")]
//         public int maxVertexCount = 256;
//
//         public override object GetValue(NodePort port)
//         {
//             if (port?.fieldName == "output" || port == null)
//             {
//                 return this;
//             }
//             return null;
//         }
//
//         public JobHandle ScheduleVector(JobHandle dependency, int textureSize,
//             TempBufferManager tempBuffers, ref VectorData outputBuffer)
//         {
//             var context = GetContext();
//             return ScheduleVectorGeneration(dependency, textureSize, tempBuffers, ref outputBuffer, context);
//         }
//
//         /// <summary>
//         /// Implement this method to generate your polyline vertices.
//         /// outputBuffer: VectorData to write vertices to (polar coordinates as float2(angle, radius))
//         /// </summary>
//         protected abstract JobHandle ScheduleVectorGeneration(JobHandle dependency, int textureSize,
//             TempBufferManager tempBuffers, ref VectorData outputBuffer, EvaluationContext context);
//     }
//
//     
// }

using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Base
{
    // Vector port wrapper class for compile-time type safety
    [System.Serializable]
    public class VectorPort : BaseNode
    {
        public override object GetValue(NodePort port) => null;
    }

    // Interface for vector outputs - polylines in polar coordinates
    public interface IVectorOutput
    {
        JobHandle ScheduleVector(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer);
    }

    /// <summary>
    /// Base class for all vector generator nodes.
    /// These nodes output polylines as arrays of float2 in polar coordinates (angle, radius).
    /// Angle is in radians [-π, π], radius is normalized [0, 1].
    /// Global contribution mask is automatically applied.
    /// </summary>
    public abstract class VectorGeneratorNode : BaseNode, IVectorOutput
    {
        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort output;

        [Header("Vector Parameters")]
        [Tooltip("Maximum number of vertices this generator can produce")]
        public int maxVertexCount = 256;

        public override object GetValue(NodePort port)
        {
            if (port?.fieldName == "output" || port == null)
            {
                return this;
            }
            return null;
        }

        public JobHandle ScheduleVector(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer)
        {
            var context = GetContext();
            return ScheduleVectorGeneration(dependency, textureSize, tempBuffers, ref outputBuffer, context);
        }

        /// <summary>
        /// Implement this method to generate your polyline vertices.
        /// Global contribution mask is automatically applied via the context.
        /// outputBuffer: VectorData to write vertices to (polar coordinates as float2(angle, radius))
        /// </summary>
        protected abstract JobHandle ScheduleVectorGeneration(JobHandle dependency, int textureSize,
            TempBufferManager tempBuffers, ref VectorData outputBuffer, EvaluationContext context);
    }
}