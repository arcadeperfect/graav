// using UnityEngine;
// using XNode;
//
// namespace PlanetGen.FieldGen2.Graph
// {
//     [CreateAssetMenu(fileName = "New Generator Graph", menuName = "PlanetGen/Generator Graph")]
//     public class GeneratorGraph: NodeGraph
//     {
//         // [Range(0, 1)] 
//         public float globalContribution = 1f;
//         public float seed = 0;
//
//         
//         
//         public EvaluationContext GetEvaluationContext()
//         {
//             return new EvaluationContext
//             {
//                 contribution = globalContribution,
//                 seed = seed,
//             };
//         } 
//     }
//
//     public struct EvaluationContext
//     {
//         public float contribution;
//         public float seed;
//     }
// }

using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph
{
    [CreateAssetMenu(fileName = "New Generator Graph", menuName = "PlanetGen/Generator Graph")]
    public class GeneratorGraph : NodeGraph
    {
        public float globalContribution = 1f;
        public float gobalSeed = 0;

        // External input data that can be injected into the graph
        private VectorData externalVectorInput;
        private RasterData externalRasterInput;
        private NativeArray<float> externalMaskInput;
        private bool hasVectorInput = false;
        private bool hasMaskInput = false;
        private bool hasRasterInput = false;
        private int currentTextureSize = 512;

        public EvaluationContext GetEvaluationContext()
        {
            return new EvaluationContext
            {
                contribution = globalContribution,
                seed = gobalSeed,
                globalContributionMask = hasMaskInput ? externalMaskInput : default,
                hasGlobalMask = hasMaskInput,
                textureSize = currentTextureSize
            };
        }

        /// <summary>
        /// Set external VectorData input for the graph
        /// </summary>
        public void SetVectorInput(VectorData vectorData)
        {
            externalVectorInput = vectorData;
            hasVectorInput = true;
        }

        /// <summary>
        /// Set external mask/weight texture input for the graph
        /// </summary>
        public void SetMaskInput(NativeArray<float> maskData, int textureSize)
        {
            externalMaskInput = maskData;
            hasMaskInput = true;
            currentTextureSize = textureSize;
        }

        public void SetRasterInput(RasterData rasterData)
        {
            externalRasterInput = rasterData;
            hasRasterInput = true;
        }
        
        public void SetSeed(float seed)
        {
            this.gobalSeed = seed;
        }

        /// <summary>
        /// Get the external VectorData if available
        /// </summary>
        public bool TryGetVectorInput(out VectorData vectorData)
        {
            vectorData = externalVectorInput;
            return hasVectorInput;
        }
        
        public bool TryGetRasterInput(out RasterData rasterData)
        {
            rasterData = externalRasterInput;
            return hasRasterInput;
        }

        /// <summary>
        /// Get the external mask data if available
        /// </summary>
        public bool TryGetMaskInput(out NativeArray<float> maskData)
        {
            maskData = externalMaskInput;
            return hasMaskInput;
        }

        /// <summary>
        /// Clear external inputs (call when graph evaluation is complete)
        /// </summary>
        public void ClearExternalInputs()
        {
            hasVectorInput = false;
            hasMaskInput = false;
            hasRasterInput = false;
            // Note: Don't dispose the arrays here - they're owned by the orchestrator
        }

        /// <summary>
        /// Check if the graph has the required external inputs
        /// </summary>
        public bool HasRequiredInputs(bool requireVector = false, bool requireMask = false)
        {
            return (!requireVector || hasVectorInput) && (!requireMask || hasMaskInput);
        }
    }


    public struct EvaluationContext
    {
        public float contribution;
        public float seed;
        public NativeArray<float> globalContributionMask;
        public bool hasGlobalMask;
        public int textureSize;
    }
}