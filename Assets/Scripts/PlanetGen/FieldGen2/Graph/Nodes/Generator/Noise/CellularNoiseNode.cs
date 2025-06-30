using System.Collections.Generic;
using PlanetGen.FieldGen2.Graph.Jobs;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using Unity.Collections;
using Unity.Jobs;

namespace PlanetGen.FieldGen2.Graph.Nodes.Generator.Noise
{
    [CreateNodeMenu("Generators/Noise/Cellular Noise")]
    public class CellularNoiseNode : NoiseGeneratorNode
    {
        protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
            List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
        {
            var noiseJob = new CellularNoiseJob
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