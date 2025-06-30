using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs;
using PlanetGen.FieldGen2.Graph.Jobs.Generator.Noise;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;
using XNode;

namespace PlanetGen.FieldGen2.Graph.Nodes.Generator.Noise
{
    [Node.CreateNodeMenu("Generators/Noise/Simplex Noise")]
    public class SimplexNoiseNode : NoiseGeneratorNode
    {
        // Inherits frequency, amplitude, and seed from base class
        // Can add additional parameters specific to Simplex noise here if needed

        protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
            List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
        {
            var noiseJob = new SimplexNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = this.seed
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}