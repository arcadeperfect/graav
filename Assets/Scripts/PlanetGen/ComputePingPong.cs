// // // using System;
// // // using System.Collections.Generic;
// // // using System.Linq;
// // // using UnityEditor;
// // // using UnityEngine;
// // // using Object = UnityEngine.Object;
// // //
// // // namespace PlanetGen
// // // {
// // //     public class ComputePingPong : IDisposable
// // //
// // //     {
// // //         private List<ComputeShaderContainer> shaderList;
// // //         private BufferSet ping;
// // //         private BufferSet pong;
// // //         public BufferSet Result { get; private set; }
// // //
// // //         public Dictionary<String, (ComputeBufferType, int)>
// // //             buffers = new Dictionary<string, (ComputeBufferType, int)>();
// // //
// // //         public Dictionary<String, RenderTextureFormat> textures = new Dictionary<string, RenderTextureFormat>();
// // //
// // //         private bool BuffersInitialized;
// // //         private class ComputeShaderContainer
// // //         {
// // //             public ComputeShader Shader { get; }
// // //             private string KernelName { get; }
// // //             public int Iterations { get; }
// // //             public int Kernel { get; }
// // //             public Dictionary<string, int> IntParams;
// // //             public Dictionary<string, float> FloatParams;
// // //
// // //             
// // //
// // //             public ComputeShaderContainer(
// // //                 ComputeShader shader,
// // //                 string kernelName, 
// // //                 int iterations = 1,
// // //                 Dictionary<string, float> floatParams = null,
// // //                 Dictionary<string, int> intParams = null)
// // //             {
// // //                 Shader = shader;
// // //                 this.KernelName = kernelName;
// // //                 this.Iterations = iterations;
// // //                 Kernel = Shader.FindKernel(kernelName);
// // //                 IntParams = intParams ?? new Dictionary<string, int>();
// // //                 FloatParams = floatParams ?? new Dictionary<string, float>();
// // //             }
// // //         }
// // //
// // //         private int width;
// // //
// // //         public struct BufferSet
// // //         {
// // //             public Dictionary<string, ComputeBuffer> Buffers { get; set; }
// // //             public Dictionary<string, RenderTexture> Textures { get; set; }
// // //
// // //             public static BufferSet Create()
// // //             {
// // //                 return new BufferSet
// // //                 {
// // //                     Buffers = new Dictionary<string, ComputeBuffer>(),
// // //                     Textures = new Dictionary<string, RenderTexture>()
// // //                 };
// // //             }
// // //
// // //             public void Release()
// // //             {
// // //                 if (Buffers != null)
// // //                 {
// // //                     foreach (var buffer in Buffers.Values)
// // //                     {
// // //                         if (buffer != null && buffer.IsValid())
// // //                         {
// // //                             buffer.Release();
// // //                         }
// // //                     }
// // //
// // //                     Buffers.Clear();
// // //                 }
// // //
// // //                 if (Textures != null)
// // //                 {
// // //                     foreach (var texture in Textures.Values)
// // //                     {
// // //                         if (texture != null && texture.IsCreated())
// // //                         {
// // //                             texture.Release();
// // //                         }
// // //                     }
// // //
// // //                     Textures.Clear();
// // //                 }
// // //             }
// // //         }
// // //
// // //         public void AddBuffer(string name, ComputeBufferType type, int stride)
// // //         {
// // //             buffers[name] = (type, stride);
// // //         }
// // //
// // //         public void AddTexture(string name, RenderTextureFormat format)
// // //         {
// // //             textures[name] = format;
// // //         }
// // //
// // //         public void AddShader(ComputeShader shader, string kernelName, int iterations = 1, Dictionary<string, float> floatParams = null, Dictionary<string, int> intParams = null)
// // //         {
// // //             if (shaderList == null)
// // //             {
// // //                 shaderList = new List<ComputeShaderContainer>();
// // //             }
// // //
// // //             shaderList.Add(new ComputeShaderContainer(shader, kernelName, iterations, floatParams, intParams));
// // //         }
// // //
// // //
// // //
// // //         public void InitBuffers(int newWidth)
// // //         {
// // //             this.width = newWidth;
// // //
// // //             ping.Release();
// // //             pong.Release();
// // //
// // //             ping = BufferSet.Create();
// // //             pong = BufferSet.Create();
// // //
// // //             foreach (var buffer in buffers)
// // //             {
// // //                 var type = buffer.Value.Item1;
// // //                 var stride = buffer.Value.Item2;
// // //                 var pingBuffer = new ComputeBuffer(newWidth, stride, type);
// // //                 var pongBuffer = new ComputeBuffer(newWidth, stride, type);
// // //                 ping.Buffers[buffer.Key] = pingBuffer;
// // //                 pong.Buffers[buffer.Key] = pongBuffer;
// // //             }
// // //
// // //             foreach (var texture in textures)
// // //             {
// // //                 var format = texture.Value;
// // //                 var pingTexture = new RenderTexture(newWidth, newWidth, 0, format)
// // //                 {
// // //                     enableRandomWrite = true,
// // //                     filterMode = FilterMode.Point
// // //                 };
// // //                 pingTexture.Create();
// // //
// // //                 var pongTexture = new RenderTexture(newWidth, newWidth, 0, format)
// // //                 {
// // //                     enableRandomWrite = true,
// // //                     filterMode = FilterMode.Point
// // //                 };
// // //                 pongTexture.Create();
// // //
// // //                 ping.Textures[texture.Key] = pingTexture;
// // //                 pong.Textures[texture.Key] = pongTexture;
// // //             }
// // //             
// // //             BuffersInitialized = true;
// // //         }
// // //
// // //
// // //         public void Execute(BufferSet source)
// // //         {
// // //             if (!BuffersInitialized)
// // //             {
// // //                 Debug.LogError("Buffers not initialized. Call InitBuffers first.");
// // //                 return;
// // //             }
// // //             
// // //             if (shaderList == null || shaderList.Count == 0)
// // //             {
// // //                 Debug.LogError("No shaders added to ComputePingPong.");
// // //                 return;
// // //             }
// // //
// // //             if (source.Buffers == null || source.Textures == null)
// // //             {
// // //                 Debug.LogError("Source BufferSet is not properly initialized.");
// // //                 return;
// // //             }
// // //
// // //
// // //             // --- Phase 1: The Handoff Dispatch ---
// // //
// // //             // Get the first shader and kernel
// // //             var firstContainer = shaderList[0];
// // //             var firstShader = firstContainer.Shader;
// // //             var firstKernel = firstContainer.Kernel;
// // //
// // //             foreach (var bufferInfo in source.Buffers)
// // //             {
// // //                 var name = bufferInfo.Key + "_in";
// // //                 var buffer = bufferInfo.Value;
// // //                 firstShader.SetBuffer(firstKernel, name, buffer);
// // //             }
// // //
// // //             foreach (var bufferInfo in ping.Buffers)
// // //             {
// // //                 var name = bufferInfo.Key + "_out";
// // //                 var buffer = bufferInfo.Value;
// // //                 firstShader.SetBuffer(firstKernel, name, buffer);
// // //             }
// // //
// // //             foreach (var textureInfo in source.Textures)
// // //             {
// // //                 var name = textureInfo.Key + "_in";
// // //                 var texture = textureInfo.Value;
// // //                 firstShader.SetTexture(firstKernel, name, texture);
// // //             }
// // //
// // //             foreach (var textureInfo in ping.Textures)
// // //             {
// // //                 var name = textureInfo.Key + "_out";
// // //                 var texture = textureInfo.Value;
// // //                 firstShader.SetTexture(firstKernel, name, texture);
// // //             }
// // //
// // //             foreach (var intParam in firstContainer.IntParams)
// // //             {
// // //                 firstShader.SetInt(intParam.Key, intParam.Value);
// // //             }
// // //
// // //             foreach (var floatParam in firstContainer.FloatParams)
// // //             {
// // //                 firstShader.SetFloat(floatParam.Key, floatParam.Value);
// // //             }
// // //
// // //             int threadGroupsX = Mathf.CeilToInt(width / 8f);
// // //             int threadGroupsY = Mathf.CeilToInt(width / 8f);
// // //             firstShader.Dispatch(firstKernel, threadGroupsX, threadGroupsY, 1);
// // //
// // //             // The result of the first step is now in `ping`.
// // //             // Check if we are already done.
// // //             int totalDispatches = shaderList.Sum(s => s.Iterations);
// // //             if (totalDispatches == 1)
// // //             {
// // //                 Result = ping;
// // //                 return;
// // //             }
// // //
// // //             // --- Phase 2: The Internal Ping-Pong Loop ---
// // //
// // //             // The subsequent dispatches will happen between ping and pong.
// // //             BufferSet currentRead = ping;
// // //             BufferSet currentWrite = pong;
// // //
// // //             int dispatchCounter = 1;
// // //             foreach (var container in shaderList)
// // //             {
// // //                 for (int j = 0; j < container.Iterations; j++)
// // //                 {
// // //                     // Skip the very first dispatch, which we already did.
// // //                     if (dispatchCounter == 1)
// // //                     {
// // //                         dispatchCounter++;
// // //                         continue;
// // //                     }
// // //
// // //                     var shader = container.Shader;
// // //                     var kernel = container.Kernel;
// // //                     foreach (var bufferInfo in currentRead.Buffers)
// // //                     {
// // //                         var name = bufferInfo.Key + "_in";
// // //                         var buffer = bufferInfo.Value;
// // //                         shader.SetBuffer(kernel, name, buffer);
// // //                     }
// // //
// // //                     foreach (var bufferInfo in currentWrite.Buffers)
// // //                     {
// // //                         var name = bufferInfo.Key + "_out";
// // //                         var buffer = bufferInfo.Value;
// // //                         shader.SetBuffer(kernel, name, buffer);
// // //                     }
// // //
// // //                     foreach (var textureInfo in currentRead.Textures)
// // //                     {
// // //                         var name = textureInfo.Key + "_in";
// // //                         var texture = textureInfo.Value;
// // //                         shader.SetTexture(kernel, name, texture);
// // //                     }
// // //
// // //                     foreach (var textureInfo in currentWrite.Textures)
// // //                     {
// // //                         var name = textureInfo.Key + "_out";
// // //                         var texture = textureInfo.Value;
// // //                         shader.SetTexture(kernel, name, texture);
// // //                     }
// // //
// // //                     foreach (var intParam in container.IntParams)
// // //                     {
// // //                         shader.SetInt(intParam.Key, intParam.Value);
// // //                     }
// // //
// // //                     foreach (var floatParam in container.FloatParams)
// // //                     {
// // //                         shader.SetFloat(floatParam.Key, floatParam.Value);
// // //                     }
// // //
// // //                     shader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
// // //
// // //                     // Now we swap our internal buffers for the next iteration.
// // //                     (currentRead, currentWrite) = (currentWrite, currentRead);
// // //
// // //                     dispatchCounter++;
// // //                 }
// // //             }
// // //
// // //             // The final result is in the last buffer we read from (after the final swap).
// // //             Result = currentRead;
// // //             
// // //         }
// // //
// // //         public void Release()
// // //         {
// // //             ping.Release();
// // //             pong.Release();
// // //         }
// // //
// // //         public void Dispose()
// // //         {
// // //             Release();
// // //         }
// // //     }
// // // }
// //
// // using System;
// // using System.Collections.Generic;
// // using System.Linq;
// // using UnityEngine;
// //
// // namespace PlanetGen
// // {
// //     public class ComputePingPong : IDisposable
// //     {
// //         // Parameter reference delegates
// //         public delegate float FloatParamProvider();
// //         public delegate int IntParamProvider();
// //         
// //         private List<ComputeShaderContainer> shaderList;
// //         private BufferSet ping;
// //         private BufferSet pong;
// //         public BufferSet Result { get; private set; }
// //
// //         public Dictionary<String, (ComputeBufferType, int)> buffers = new Dictionary<string, (ComputeBufferType, int)>();
// //         public Dictionary<String, RenderTextureFormat> textures = new Dictionary<string, RenderTextureFormat>();
// //
// //         private bool BuffersInitialized;
// //         
// //         private class ComputeShaderContainer
// //         {
// //             public ComputeShader Shader { get; }
// //             private string KernelName { get; }
// //             public int Iterations { get; }
// //             public int Kernel { get; }
// //             
// //             // // Static parameters (set once)
// //             // public Dictionary<string, int> StaticIntParams;
// //             // public Dictionary<string, float> StaticFloatParams;
// //             
// //             // Dynamic parameter providers (updated each frame)
// //             public Dictionary<string, IntParamProvider> DynamicIntParams;
// //             public Dictionary<string, FloatParamProvider> DynamicFloatParams;
// //
// //             public ComputeShaderContainer(
// //                 ComputeShader shader,
// //                 string kernelName, 
// //                 int iterations = 1,
// //                 // Dictionary<string, float> staticFloatParams = null,
// //                 // Dictionary<string, int> staticIntParams = null,
// //                 Dictionary<string, FloatParamProvider> dynamicFloatParams = null,
// //                 Dictionary<string, IntParamProvider> dynamicIntParams = null)
// //             {
// //                 Shader = shader;
// //                 this.KernelName = kernelName;
// //                 this.Iterations = iterations;
// //                 Kernel = Shader.FindKernel(kernelName);
// //                 
// //                 // StaticIntParams = staticIntParams ?? new Dictionary<string, int>();
// //                 // StaticFloatParams = staticFloatParams ?? new Dictionary<string, float>();
// //                 DynamicIntParams = dynamicIntParams ?? new Dictionary<string, IntParamProvider>();
// //                 DynamicFloatParams = dynamicFloatParams ?? new Dictionary<string, FloatParamProvider>();
// //             }
// //             
// //             public void SetCurrentParameters()
// //             {
// //                 // // Set static parameters
// //                 // foreach (var intParam in StaticIntParams)
// //                 // {
// //                 //     Shader.SetInt(intParam.Key, intParam.Value);
// //                 // }
// //                 //
// //                 // foreach (var floatParam in StaticFloatParams)
// //                 // {
// //                 //     Shader.SetFloat(floatParam.Key, floatParam.Value);
// //                 // }
// //                 
// //                 // Set dynamic parameters (get current values)
// //                 foreach (var intParam in DynamicIntParams)
// //                 {
// //                     Shader.SetInt(intParam.Key, intParam.Value());
// //                 }
// //                 
// //                 foreach (var floatParam in DynamicFloatParams)
// //                 {
// //                     Shader.SetFloat(floatParam.Key, floatParam.Value());
// //                 }
// //             }
// //         }
// //
// //         private int width;
// //
// //         public struct BufferSet
// //         {
// //             public Dictionary<string, ComputeBuffer> Buffers { get; set; }
// //             public Dictionary<string, RenderTexture> Textures { get; set; }
// //
// //             public static BufferSet Create()
// //             {
// //                 return new BufferSet
// //                 {
// //                     Buffers = new Dictionary<string, ComputeBuffer>(),
// //                     Textures = new Dictionary<string, RenderTexture>()
// //                 };
// //             }
// //
// //             public void Release()
// //             {
// //                 if (Buffers != null)
// //                 {
// //                     foreach (var buffer in Buffers.Values)
// //                     {
// //                         if (buffer != null && buffer.IsValid())
// //                         {
// //                             buffer.Release();
// //                         }
// //                     }
// //                     Buffers.Clear();
// //                 }
// //
// //                 if (Textures != null)
// //                 {
// //                     foreach (var texture in Textures.Values)
// //                     {
// //                         if (texture != null && texture.IsCreated())
// //                         {
// //                             texture.Release();
// //                         }
// //                     }
// //                     Textures.Clear();
// //                 }
// //             }
// //         }
// //
// //         public void AddBuffer(string name, ComputeBufferType type, int stride)
// //         {
// //             buffers[name] = (type, stride);
// //         }
// //
// //         public void AddTexture(string name, RenderTextureFormat format)
// //         {
// //             textures[name] = format;
// //         }
// //
// //         // Original method for backward compatibility
// //         // public void AddShader(ComputeShader shader, string kernelName, int iterations = 1, 
// //         //     Dictionary<string, float> floatParams = null, Dictionary<string, int> intParams = null)
// //         // {
// //         //     if (shaderList == null)
// //         //     {
// //         //         shaderList = new List<ComputeShaderContainer>();
// //         //     }
// //         //
// //         //     shaderList.Add(new ComputeShaderContainer(shader, kernelName, iterations, floatParams, intParams));
// //         // }
// //         
// //         // New method with parameter providers
// //         public void AddShader(ComputeShader shader, string kernelName, int iterations = 1,
// //             Dictionary<string, FloatParamProvider> floatParams = null,
// //             Dictionary<string, IntParamProvider> intParams = null)
// //         {
// //             if (shaderList == null)
// //             {
// //                 shaderList = new List<ComputeShaderContainer>();
// //             }
// //
// //             shaderList.Add(new ComputeShaderContainer(shader, kernelName, iterations, 
// //                 floatParams, intParams));
// //         }
// //         
// //         public void InitBuffers(int newWidth)
// //         {
// //             this.width = newWidth;
// //
// //             ping.Release();
// //             pong.Release();
// //
// //             ping = BufferSet.Create();
// //             pong = BufferSet.Create();
// //
// //             foreach (var buffer in buffers)
// //             {
// //                 var type = buffer.Value.Item1;
// //                 var stride = buffer.Value.Item2;
// //                 var pingBuffer = new ComputeBuffer(newWidth, stride, type);
// //                 var pongBuffer = new ComputeBuffer(newWidth, stride, type);
// //                 ping.Buffers[buffer.Key] = pingBuffer;
// //                 pong.Buffers[buffer.Key] = pongBuffer;
// //             }
// //
// //             foreach (var texture in textures)
// //             {
// //                 var format = texture.Value;
// //                 var pingTexture = new RenderTexture(newWidth, newWidth, 0, format)
// //                 {
// //                     enableRandomWrite = true,
// //                     filterMode = FilterMode.Point
// //                 };
// //                 pingTexture.Create();
// //
// //                 var pongTexture = new RenderTexture(newWidth, newWidth, 0, format)
// //                 {
// //                     enableRandomWrite = true,
// //                     filterMode = FilterMode.Point
// //                 };
// //                 pongTexture.Create();
// //
// //                 ping.Textures[texture.Key] = pingTexture;
// //                 pong.Textures[texture.Key] = pongTexture;
// //             }
// //             
// //             BuffersInitialized = true;
// //         }
// //
// //         public void Execute(BufferSet source)
// //         {
// //             if (!BuffersInitialized)
// //             {
// //                 Debug.LogError("Buffers not initialized. Call InitBuffers first.");
// //                 return;
// //             }
// //             
// //             if (shaderList == null || shaderList.Count == 0)
// //             {
// //                 Debug.LogError("No shaders added to ComputePingPong.");
// //                 return;
// //             }
// //
// //             if (source.Buffers == null || source.Textures == null)
// //             {
// //                 Debug.LogError("Source BufferSet is not properly initialized.");
// //                 return;
// //             }
// //
// //             // --- Phase 1: The Handoff Dispatch ---
// //             var firstContainer = shaderList[0];
// //             var firstShader = firstContainer.Shader;
// //             var firstKernel = firstContainer.Kernel;
// //
// //             foreach (var bufferInfo in source.Buffers)
// //             {
// //                 var name = bufferInfo.Key + "_in";
// //                 var buffer = bufferInfo.Value;
// //                 firstShader.SetBuffer(firstKernel, name, buffer);
// //             }
// //
// //             foreach (var bufferInfo in ping.Buffers)
// //             {
// //                 var name = bufferInfo.Key + "_out";
// //                 var buffer = bufferInfo.Value;
// //                 firstShader.SetBuffer(firstKernel, name, buffer);
// //             }
// //
// //             foreach (var textureInfo in source.Textures)
// //             {
// //                 var name = textureInfo.Key + "_in";
// //                 var texture = textureInfo.Value;
// //                 firstShader.SetTexture(firstKernel, name, texture);
// //             }
// //
// //             foreach (var textureInfo in ping.Textures)
// //             {
// //                 var name = textureInfo.Key + "_out";
// //                 var texture = textureInfo.Value;
// //                 firstShader.SetTexture(firstKernel, name, texture);
// //             }
// //
// //             // Set parameters using the new system
// //             firstContainer.SetCurrentParameters();
// //
// //             int threadGroupsX = Mathf.CeilToInt(width / 8f);
// //             int threadGroupsY = Mathf.CeilToInt(width / 8f);
// //             firstShader.Dispatch(firstKernel, threadGroupsX, threadGroupsY, 1);
// //
// //             int totalDispatches = shaderList.Sum(s => s.Iterations);
// //             if (totalDispatches == 1)
// //             {
// //                 Result = ping;
// //                 return;
// //             }
// //
// //             // --- Phase 2: The Internal Ping-Pong Loop ---
// //             BufferSet currentRead = ping;
// //             BufferSet currentWrite = pong;
// //
// //             int dispatchCounter = 1;
// //             foreach (var container in shaderList)
// //             {
// //                 for (int j = 0; j < container.Iterations; j++)
// //                 {
// //                     if (dispatchCounter == 1)
// //                     {
// //                         dispatchCounter++;
// //                         continue;
// //                     }
// //
// //                     var shader = container.Shader;
// //                     var kernel = container.Kernel;
// //                     
// //                     foreach (var bufferInfo in currentRead.Buffers)
// //                     {
// //                         var name = bufferInfo.Key + "_in";
// //                         var buffer = bufferInfo.Value;
// //                         shader.SetBuffer(kernel, name, buffer);
// //                     }
// //
// //                     foreach (var bufferInfo in currentWrite.Buffers)
// //                     {
// //                         var name = bufferInfo.Key + "_out";
// //                         var buffer = bufferInfo.Value;
// //                         shader.SetBuffer(kernel, name, buffer);
// //                     }
// //
// //                     foreach (var textureInfo in currentRead.Textures)
// //                     {
// //                         var name = textureInfo.Key + "_in";
// //                         var texture = textureInfo.Value;
// //                         shader.SetTexture(kernel, name, texture);
// //                     }
// //
// //                     foreach (var textureInfo in currentWrite.Textures)
// //                     {
// //                         var name = textureInfo.Key + "_out";
// //                         var texture = textureInfo.Value;
// //                         shader.SetTexture(kernel, name, texture);
// //                     }
// //
// //                     // Set parameters using the new system
// //                     container.SetCurrentParameters();
// //
// //                     shader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);
// //
// //                     (currentRead, currentWrite) = (currentWrite, currentRead);
// //                     dispatchCounter++;
// //                 }
// //             }
// //
// //             Result = currentRead;
// //         }
// //
// //         public void Release()
// //         {
// //             ping.Release();
// //             pong.Release();
// //         }
// //
// //         public void Dispose()
// //         {
// //             Release();
// //         }
// //     }
// // }
//
// using System;
// using System.Collections.Generic;
// using UnityEngine;
//
// namespace PlanetGen
// {
//     // Resource specification - what resources you need and their properties
//     public class ComputeResourceSpec
//     {
//         public Dictionary<string, (ComputeBufferType type, int stride)> Buffers { get; } = new();
//         public Dictionary<string, RenderTextureFormat> Textures { get; } = new();
//
//         public ComputeResourceSpec AddBuffer(string name, ComputeBufferType type, int stride)
//         {
//             Buffers[name] = (type, stride);
//             return this;
//         }
//
//         public ComputeResourceSpec AddTexture(string name, RenderTextureFormat format)
//         {
//             Textures[name] = format;
//             return this;
//         }
//     }
//
//     // Shader step configuration
//     public class ComputeStep
//     {
//         public ComputeShader Shader { get; set; }
//         public string KernelName { get; set; }
//         public Func<int> IterationsProvider { get; set; } = () => 1;
//         public int Iterations => IterationsProvider();
//         public Dictionary<string, Func<float>> FloatParams { get; set; } = new();
//         public Dictionary<string, Func<int>> IntParams { get; set; } = new();
//
//         public ComputeStep(ComputeShader shader, string kernelName)
//         {
//             Shader = shader;
//             KernelName = kernelName;
//         }
//
//         public ComputeStep WithIterations(Func<int> iterationsProvider)
//         {
//             IterationsProvider = iterationsProvider;
//             return this;
//         }
//
//         public ComputeStep WithFloatParam(string name, Func<float> provider)
//         {
//             FloatParams[name] = provider;
//             return this;
//         }
//
//         public ComputeStep WithIntParam(string name, Func<int> provider)
//         {
//             IntParams[name] = provider;
//             return this;
//         }
//     }
//
//     // Resource collection that can be used as input or output
//     public class ComputeResources : IDisposable
//     {
//         public Dictionary<string, ComputeBuffer> Buffers { get; } = new();
//         public Dictionary<string, RenderTexture> Textures { get; } = new();
//
//         public void Release()
//         {
//             foreach (var buffer in Buffers.Values)
//                 buffer?.Release();
//             foreach (var texture in Textures.Values)
//                 texture?.Release();
//
//             Buffers.Clear();
//             Textures.Clear();
//         }
//
//         public void Dispose() => Release();
//     }
//
//     // Main pipeline class with fluent interface
//     public class PingPongPipeline : IDisposable
//     {
//         private ComputeResourceSpec resourceSpec;
//         private List<ComputeStep> steps = new();
//         private ComputeResources pingResources;
//         private ComputeResources pongResources;
//         private bool initialized = false;
//         private int currentWidth;
//
//         public ComputeResources Result { get; private set; }
//
//         // Fluent configuration
//         public PingPongPipeline WithResources(Action<ComputeResourceSpec> configure)
//         {
//             resourceSpec = new ComputeResourceSpec();
//             configure(resourceSpec);
//             return this;
//         }
//
//         public PingPongPipeline AddStep(ComputeShader shader, string kernelName, Action<ComputeStep> configure = null)
//         {
//             var step = new ComputeStep(shader, kernelName);
//             configure?.Invoke(step);
//             steps.Add(step);
//             return this;
//         }
//
//         // Initialize with a width - creates all internal buffers
//         public void Initialize(int width)
//         {
//             if (resourceSpec == null)
//                 throw new InvalidOperationException("Must configure resources before initializing");
//
//             currentWidth = width;
//
//             // Clean up existing resources
//             pingResources?.Dispose();
//             pongResources?.Dispose();
//
//             // Create ping-pong resources
//             pingResources = CreateResourceSet(width);
//             pongResources = CreateResourceSet(width);
//
//             initialized = true;
//         }
//
//         // Execute with input resources - can be external or from previous pipeline
//         public ComputeResources Execute(ComputeResources input)
//         {
//             if (!initialized)
//                 throw new InvalidOperationException("Pipeline not initialized");
//
//             if (steps.Count == 0)
//             {
//                 Debug.LogWarning("No compute steps configured");
//                 return input;
//             }
//
//             // First step: input -> ping
//             ExecuteStep(steps[0], input, pingResources, 0);
//
//             if (steps.Count == 1 && steps[0].Iterations == 1)
//             {
//                 Result = pingResources;
//                 return Result;
//             }
//
//             // Subsequent steps: ping-pong between internal resources
//             var currentRead = pingResources;
//             var currentWrite = pongResources;
//             int totalIterations = 0;
//
//             foreach (var step in steps)
//             {
//                 for (int i = 0; i < step.Iterations; i++)
//                 {
//                     // Skip first iteration if it's the very first step (already done)
//                     if (totalIterations == 0)
//                     {
//                         totalIterations++;
//                         continue;
//                     }
//
//                     ExecuteStep(step, currentRead, currentWrite, totalIterations);
//                     (currentRead, currentWrite) = (currentWrite, currentRead);
//                     totalIterations++;
//                 }
//             }
//
//             Result = currentRead;
//             return Result;
//         }
//
//         // Convenience method to create input from external textures/buffers
//         public ComputeResources CreateInput(Dictionary<string, Texture> textures = null,
//             Dictionary<string, ComputeBuffer> buffers = null)
//         {
//             var input = new ComputeResources();
//
//             if (textures != null)
//             {
//                 foreach (var kvp in textures)
//                     input.Textures[kvp.Key] = kvp.Value as RenderTexture;
//             }
//
//             if (buffers != null)
//             {
//                 foreach (var kvp in buffers)
//                     input.Buffers[kvp.Key] = kvp.Value;
//             }
//
//             return input;
//         }
//
//         private ComputeResources CreateResourceSet(int width)
//         {
//             var resources = new ComputeResources();
//
//             foreach (var buffer in resourceSpec.Buffers)
//             {
//                 var (type, stride) = buffer.Value;
//                 resources.Buffers[buffer.Key] = new ComputeBuffer(width, stride, type);
//             }
//
//             foreach (var texture in resourceSpec.Textures)
//             {
//                 var format = texture.Value;
//                 var renderTexture = new RenderTexture(width, width, 0, format)
//                 {
//                     enableRandomWrite = true,
//                     filterMode = FilterMode.Point
//                 };
//                 renderTexture.Create();
//                 resources.Textures[texture.Key] = renderTexture;
//             }
//
//             return resources;
//         }
//
//         private void ExecuteStep(ComputeStep step, ComputeResources input, ComputeResources output, int iteration)
//         {
//             var kernel = step.Shader.FindKernel(step.KernelName);
//
//             // Bind input resources
//             foreach (var buffer in input.Buffers)
//                 step.Shader.SetBuffer(kernel, buffer.Key + "_in", buffer.Value);
//
//             foreach (var texture in input.Textures)
//                 step.Shader.SetTexture(kernel, texture.Key + "_in", texture.Value);
//
//             // Bind output resources
//             foreach (var buffer in output.Buffers)
//                 step.Shader.SetBuffer(kernel, buffer.Key + "_out", buffer.Value);
//
//             foreach (var texture in output.Textures)
//                 step.Shader.SetTexture(kernel, texture.Key + "_out", texture.Value);
//
//             // Set parameters
//             foreach (var param in step.FloatParams)
//                 step.Shader.SetFloat(param.Key, param.Value());
//
//             foreach (var param in step.IntParams)
//                 step.Shader.SetInt(param.Key, param.Value());
//
//             // Dispatch
//             int threadGroups = Mathf.CeilToInt(currentWidth / 8f);
//             step.Shader.Dispatch(kernel, threadGroups, threadGroups, 1);
//         }
//
//         public void Dispose()
//         {
//             pingResources?.Dispose();
//             pongResources?.Dispose();
//             Result?.Dispose();
//         }
//     }
// }
// //     // Usage example - much cleaner!
// //     public class ExampleUsage : MonoBehaviour
// //     {
// //         [SerializeField] private float domainWarp = 1f;
// //         [SerializeField] private float domainWarpScale = 1f;
// //         [SerializeField] private int domainWarpIterations = 3;
// //         
// //         private ComputePipeline pipeline;
// //         
// //         void Start()
// //         {
// //             // Configure pipeline once
// //             pipeline = new ComputePipeline()
// //                 .WithResources(spec => spec
// //                     .AddTexture("field", RenderTextureFormat.ARGBFloat)
// //                     .AddTexture("noise", RenderTextureFormat.RFloat))
// //                 .AddStep(Resources.Load<ComputeShader>("Compute/NoiseGen"), "GenerateNoise")
// //                 .AddStep(Resources.Load<ComputeShader>("Compute/DomainWarp"), "Warp", step => step
// //                     .WithIterations(() => domainWarpIterations)  // Dynamic iterations
// //                     .WithFloatParam("amplitude", () => domainWarp)
// //                     .WithFloatParam("frequency", () => domainWarpScale));
// //
// //             pipeline.Initialize(512);
// //         }
// //
// //         void Update()
// //         {
// //             // Create input from your existing field texture
// //             var input = pipeline.CreateInput(
// //                 textures: new Dictionary<string, Texture> { ["field"] = myFieldTexture }
// //             );
// //
// //             // Execute pipeline - parameters are evaluated fresh each time
// //             var result = pipeline.Execute(input);
// //             
// //             // Use result
// //             myRenderer.material.SetTexture("_MainTex", result.Textures["field"]);
// //         }
// //
// //         void OnDestroy()
// //         {
// //             pipeline?.Dispose();
// //         }
// //     }
// // }

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PlanetGen
{
    // Resource specification - what resources you need and their properties
    public class ComputeResourceSpec
    {
        public Dictionary<string, (ComputeBufferType type, int stride)> Buffers { get; } = new();
        public Dictionary<string, RenderTextureFormat> Textures { get; } = new();

        public ComputeResourceSpec AddBuffer(string name, ComputeBufferType type, int stride)
        {
            Buffers[name] = (type, stride);
            return this;
        }

        public ComputeResourceSpec AddTexture(string name, RenderTextureFormat format)
        {
            Textures[name] = format;
            return this;
        }
    }

    // Shader step configuration
    public class ComputeStep
    {
        public ComputeShader Shader { get; set; }
        // public string KernelName { get; set; }
        public int KernelId { get; set; }
        public Func<int> IterationsProvider { get; set; } = () => 1;
        public Dictionary<string, Func<float>> FloatParams { get; set; } = new();
        public Dictionary<string, Func<int>> IntParams { get; set; } = new();

        // Helper property for when iterations are accessed
        public int Iterations => IterationsProvider();

        public ComputeStep(ComputeShader shader, int kernel)
        {
            Shader = shader;
            KernelId = kernel;
        }

        public ComputeStep WithIterations(int iterations)
        {
            IterationsProvider = () => iterations;
            return this;
        }

        public ComputeStep WithIterations(Func<int> iterationsProvider)
        {
            IterationsProvider = iterationsProvider;
            return this;
        }

        public ComputeStep WithFloatParam(string name, Func<float> provider)
        {
            FloatParams[name] = provider;
            return this;
        }

        public ComputeStep WithIntParam(string name, Func<int> provider)
        {
            IntParams[name] = provider;
            return this;
        }
    }

    // Resource collection that can be used as input or output
    public class ComputeResources : IDisposable
    {
        public Dictionary<string, ComputeBuffer> Buffers { get; } = new();
        public Dictionary<string, RenderTexture> Textures { get; } = new();

        public void Release()
        {
            foreach (var buffer in Buffers.Values)
                buffer?.Release();
            foreach (var texture in Textures.Values)
                texture?.Release();

            Buffers.Clear();
            Textures.Clear();
        }

        public void Dispose() => Release();
    }

    // Main pipeline class with fluent interface
    public class PingPongPipeline : IDisposable
    {
        private ComputeResourceSpec resourceSpec;
        private List<ComputeStep> steps = new();
        private ComputeResources pingResources;
        private ComputeResources pongResources;
        private bool initialized = false;
        private int currentWidth;

        // public ComputeResources Output { get; private set; }
        
        public ComputeResources Result { get; private set; }

        // Fluent configuration
        public PingPongPipeline WithResources(Action<ComputeResourceSpec> configure)
        {
            resourceSpec = new ComputeResourceSpec();
            configure(resourceSpec);
            return this;
        }

        public PingPongPipeline AddStep(ComputeShader shader, int kernelID, Action<ComputeStep> configure = null)
        {
            var step = new ComputeStep(shader, kernelID);
            configure?.Invoke(step);
            steps.Add(step);
            return this;
        }

        // Initialize with a width - creates all internal buffers
        public void Init(int width)
        {
            if (resourceSpec == null)
                throw new InvalidOperationException("Must configure resources before initializing");

            currentWidth = width;

            // Clean up existing resources
            pingResources?.Dispose();
            pongResources?.Dispose();

            // Create ping-pong resources
            pingResources = CreateResourceSet(width);
            pongResources = CreateResourceSet(width);

            initialized = true;
            // return Output;
        }

        // Execute with input resources - can be external or from previous pipeline
        public ComputeResources Dispatch(ComputeResources input)
        {
            if (!initialized)
                throw new InvalidOperationException("Pipeline not initialized");

            if (steps.Count == 0)
            {
                Debug.LogWarning("No compute steps configured");
                return input;
            }

            // First step: input -> ping
            ExecuteStep(steps[0], input, pingResources, 0);

            if (steps.Count == 1 && steps[0].Iterations == 1)
            {
                Result = pingResources;
                return Result;
            }

            // Subsequent steps: ping-pong between internal resources
            var currentRead = pingResources;
            var currentWrite = pongResources;
            int totalIterations = 0;

            foreach (var step in steps)
            {
                for (int i = 0; i < step.Iterations; i++)
                {
                    // Skip first iteration if it's the very first step (already done)
                    if (totalIterations == 0)
                    {
                        totalIterations++;
                        continue;
                    }

                    ExecuteStep(step, currentRead, currentWrite, totalIterations);
                    (currentRead, currentWrite) = (currentWrite, currentRead);
                    totalIterations++;
                }
            }
            Result = currentRead;
            return Result;
        }

        // Convenience method to create input from external textures/buffers
        public ComputeResources CreateInput(Dictionary<string, Texture> textures = null,
            Dictionary<string, ComputeBuffer> buffers = null)
        {
            var input = new ComputeResources();

            if (textures != null)
            {
                foreach (var kvp in textures)
                    input.Textures[kvp.Key] = kvp.Value as RenderTexture;
            }

            if (buffers != null)
            {
                foreach (var kvp in buffers)
                    input.Buffers[kvp.Key] = kvp.Value;
            }

            return input;
        }

        private ComputeResources CreateResourceSet(int width)
        {
            var resources = new ComputeResources();

            foreach (var buffer in resourceSpec.Buffers)
            {
                var (type, stride) = buffer.Value;
                resources.Buffers[buffer.Key] = new ComputeBuffer(width, stride, type);
            }

            foreach (var texture in resourceSpec.Textures)
            {
                var format = texture.Value;
                var renderTexture = new RenderTexture(width, width, 0, format)
                {
                    enableRandomWrite = true,
                    // filterMode = FilterMode.Point
                    filterMode = FilterMode.Trilinear
                };
                renderTexture.Create();
                resources.Textures[texture.Key] = renderTexture;
            }

            return resources;
        }

        private void ExecuteStep(ComputeStep step, ComputeResources input, ComputeResources output, int iteration)
        {
            // Debug.Log(step.Shader);
            
            
            // var kernel = step.Shader.FindKernel(step.KernelName);
            var kernel = step.KernelId;
            
            // Bind input resources
            foreach (var buffer in input.Buffers)
                step.Shader.SetBuffer(kernel, buffer.Key + "_in", buffer.Value);

            foreach (var texture in input.Textures)
                step.Shader.SetTexture(kernel, texture.Key + "_in", texture.Value);

            // Bind output resources
            foreach (var buffer in output.Buffers)
                step.Shader.SetBuffer(kernel, buffer.Key + "_out", buffer.Value);

            foreach (var texture in output.Textures)
                step.Shader.SetTexture(kernel, texture.Key + "_out", texture.Value);

            // Set parameters
            foreach (var param in step.FloatParams)
                step.Shader.SetFloat(param.Key, param.Value());

            foreach (var param in step.IntParams)
                step.Shader.SetInt(param.Key, param.Value());

            // Dispatch
            int threadGroups = Mathf.CeilToInt(currentWidth / 8f);
            step.Shader.Dispatch(kernel, threadGroups, threadGroups, 1);
        }

        public void Dispose()
        {
            pingResources?.Dispose();
            pongResources?.Dispose();
            Result?.Dispose();
        }
    }
}