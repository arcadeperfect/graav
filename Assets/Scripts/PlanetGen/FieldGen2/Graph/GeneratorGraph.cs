using UnityEngine;
using XNode;

namespace PlanetGen.FieldGen2.Graph
{
    [CreateAssetMenu(fileName = "New Generator Graph", menuName = "PlanetGen/Generator Graph")]
    public class GeneratorGraph: NodeGraph
    {
        // [Range(0, 1)] 
        public float globalContribution = 1f;
        public float seed = 0;

        
        
        public EvaluationContext GetEvaluationContext()
        {
            return new EvaluationContext
            {
                contribution = globalContribution,
                seed = seed,
            };
        } 
    }

    public struct EvaluationContext
    {
        public float contribution;
        public float seed;
    }
}