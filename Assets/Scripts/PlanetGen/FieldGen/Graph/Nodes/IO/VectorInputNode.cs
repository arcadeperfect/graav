using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.IO
{
    /// <summary>
    /// Input node that provides external VectorData to the graph
    /// </summary>
    [Node.CreateNodeMenu("IO/Vector Input")]
    public class VectorInputNode : BaseNode, IVectorOutput
    {
        [Output(ShowBackingValue.Never, ConnectionType.Override, TypeConstraint.Strict)]
        public VectorPort output;

        [Header("Vector Input")] [Tooltip("Receives external VectorData from the orchestrator")]
        public bool showInfo = true;

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
            // Get external vector input from the graph
            if (graph is GeneratorGraph generatorGraph &&
                generatorGraph.TryGetVectorInput(out VectorData externalVector))
            {
                // Copy external vector data to output buffer
                if (externalVector.IsValid && outputBuffer.IsValid)
                {
                    int copyCount = math.min(externalVector.Count, outputBuffer.Vertices.Length);
                    for (int i = 0; i < copyCount; i++)
                    {
                        outputBuffer.Vertices[i] = externalVector.Vertices[i];
                        if (externalVector.VertexWeights.IsCreated && outputBuffer.VertexWeights.IsCreated)
                            outputBuffer.VertexWeights[i] = externalVector.VertexWeights[i];
                        if (externalVector.VertexColors.IsCreated && outputBuffer.VertexColors.IsCreated)
                            outputBuffer.VertexColors[i] = externalVector.VertexColors[i];
                    }

                    outputBuffer.SetVertexCount(copyCount);
                }
                else
                {
                    Debug.LogError("VectorInputNode: Invalid vector data buffers");
                    outputBuffer.SetVertexCount(0);
                }
            }
            else
            {
                Debug.LogError("VectorInputNode: No external vector input available");
                outputBuffer.SetVertexCount(0);
            }

            return dependency;
        }
    }

}