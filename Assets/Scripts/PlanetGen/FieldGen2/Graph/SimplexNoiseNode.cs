using Unity.Collections;
using Unity.Jobs;
using XNode;

namespace PlanetGen.FieldGen2.Graph
{
    [Node.CreateNodeMenuAttribute("Generators/Simplex Noise")]

    public class SimplexNoiseNode: BaseNode
    {
        // [Output] public BaseNode output;
        [Output(backingValue = ShowBackingValue.Never)] public BaseNode output;

        public float frequency = 10f;
        public float amplitude = 1f;
        
        public override object GetValue(NodePort port)
        {
            return this;
        }
        
        public override JobHandle Schedule(JobHandle dependency, int textureSize, ref NativeArray<float> outputBuffer)
        {
            UnityEngine.Debug.Log($"Scheduling SimplexNoiseJob -> Freq: {this.frequency}, Amp: {this.amplitude}");

            var noiseJob = new SimplexNoiseJob
            {
                outputBuffer = outputBuffer,
                textureSize = textureSize,
                frequency = this.frequency,
                amplitude = this.amplitude,
                seed = 12345
            };

            return noiseJob.Schedule(textureSize * textureSize, 64, dependency);
        }
    }
}