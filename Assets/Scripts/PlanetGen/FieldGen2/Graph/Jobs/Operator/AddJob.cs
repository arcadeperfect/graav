using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;

namespace PlanetGen.FieldGen2.Graph.Jobs
{
    [BurstCompile(CompileSynchronously = true)]
    public struct AddJob: IJobParallelFor
    {
        [ReadOnly] public NativeArray<float> InputA;
        [ReadOnly] public NativeArray<float> InputB;

        [WriteOnly] public NativeArray<float> Output;

        public void Execute(int i)
        {
            Output[i] = InputA[i] + InputB[i];
        }
    }
}