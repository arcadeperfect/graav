using Unity.Jobs;
using Unity.Collections;
using XNode;

namespace PlanetGen.FieldGen2.Graph
{
    public abstract class BaseNode: Node
    {
        // required, but unused due to our graph compiler architecture
        public abstract override object GetValue(NodePort port);
        /// <summary>
        /// 
        /// </summary>
        /// <param name="dependency">The JobHandle from the previous node in the chain.</param>
        /// <param name="textureSize">The size of the output texture.</param>
        /// <param name="outputBuffer">The NativeArray to write the result into.</param>
        /// <returns>A new JobHandle that includes this node's job.</returns>
        public abstract JobHandle Schedule(JobHandle dependency, int textureSize, ref NativeArray<float> outputBuffer);        
    }
}