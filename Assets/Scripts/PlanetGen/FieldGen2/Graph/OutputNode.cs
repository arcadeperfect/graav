using PlanetGen.FieldGen2.Graph;
using Unity.Collections;
using Unity.Jobs;
using XNode;

[Node.CreateNodeMenuAttribute("Output/Final Output")]
public class OutputNode : BaseNode
{
    [Input(backingValue = ShowBackingValue.Never)] public BaseNode input;

    /// <summary>
    /// This is a key part of the fix. We now properly participate in the GetValue chain
    /// by getting the value from our input node and passing it through. xNode uses this
    /// internally to resolve the graph connections correctly.
    /// </summary>
    public override object GetValue(NodePort port)
    {
        // Ask our input port "who is connected to you?" and return that node.
        return GetInputValue<BaseNode>("input");
    }
    
    // The output node doesn't schedule its own job. Instead, it finds its
    // input node and starts the scheduling chain from there.
    public override JobHandle Schedule(JobHandle dependency, int textureSize, ref NativeArray<float> outputBuffer)
    {
        // Get the node connected to our input port using xNode's robust GetInputValue method.
        // This works reliably now because our GetValue() methods are implemented correctly.
        BaseNode inputNode = GetInputValue<BaseNode>("input");

        // If a node is connected, schedule its job.
        if (inputNode != null)
        {
            UnityEngine.Debug.Log($"OutputNode found connected node: {inputNode.name}. Starting schedule chain.");
            return inputNode.Schedule(dependency, textureSize, ref outputBuffer);
        }

        // If nothing is connected, log a warning and return the dependency handle.
        UnityEngine.Debug.LogWarning("OutputNode is not connected to anything!");
        return dependency;
    }
}