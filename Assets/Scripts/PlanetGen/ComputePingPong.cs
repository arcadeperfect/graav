using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PlanetGen
{
    public class ComputePingPong : IDisposable

    {
        private List<ComputeShaderContainer> shaderList;
        private BufferSet ping;
        private BufferSet pong;
        public BufferSet Result { get; private set; }

        public Dictionary<String, (ComputeBufferType, int)>
            buffers = new Dictionary<string, (ComputeBufferType, int)>();

        public Dictionary<String, RenderTextureFormat> textures = new Dictionary<string, RenderTextureFormat>();

        private bool BuffersInitialized;
        private class ComputeShaderContainer
        {
            public ComputeShader Shader { get; }
            private string KernelName { get; }
            public int Iterations { get; }
            public int Kernel { get; }
            public Dictionary<string, int> IntParams;
            public Dictionary<string, float> FloatParams;

            

            public ComputeShaderContainer(
                ComputeShader shader,
                string kernelName, 
                int iterations = 1,
                Dictionary<string, float> floatParams = null,
                Dictionary<string, int> intParams = null)
            {
                Shader = shader;
                this.KernelName = kernelName;
                this.Iterations = iterations;
                Kernel = Shader.FindKernel(kernelName);
                IntParams = intParams ?? new Dictionary<string, int>();
                FloatParams = floatParams ?? new Dictionary<string, float>();
            }
        }

        private int width;

        public struct BufferSet
        {
            public Dictionary<string, ComputeBuffer> Buffers { get; set; }
            public Dictionary<string, RenderTexture> Textures { get; set; }

            public static BufferSet Create()
            {
                return new BufferSet
                {
                    Buffers = new Dictionary<string, ComputeBuffer>(),
                    Textures = new Dictionary<string, RenderTexture>()
                };
            }

            public void Release()
            {
                if (Buffers != null)
                {
                    foreach (var buffer in Buffers.Values)
                    {
                        if (buffer != null && buffer.IsValid())
                        {
                            buffer.Release();
                        }
                    }

                    Buffers.Clear();
                }

                if (Textures != null)
                {
                    foreach (var texture in Textures.Values)
                    {
                        if (texture != null && texture.IsCreated())
                        {
                            texture.Release();
                        }
                    }

                    Textures.Clear();
                }
            }
        }

        public void AddBuffer(string name, ComputeBufferType type, int stride)
        {
            buffers[name] = (type, stride);
        }

        public void AddTexture(string name, RenderTextureFormat format)
        {
            textures[name] = format;
        }

        public void AddShader(ComputeShader shader, string kernelName, int iterations = 1, Dictionary<string, float> floatParams = null, Dictionary<string, int> intParams = null)
        {
            if (shaderList == null)
            {
                shaderList = new List<ComputeShaderContainer>();
            }

            shaderList.Add(new ComputeShaderContainer(shader, kernelName, iterations, floatParams, intParams));
        }



        public void InitBuffers(int newWidth)
        {
            this.width = newWidth;

            ping.Release();
            pong.Release();

            ping = BufferSet.Create();
            pong = BufferSet.Create();

            foreach (var buffer in buffers)
            {
                var type = buffer.Value.Item1;
                var stride = buffer.Value.Item2;
                var pingBuffer = new ComputeBuffer(newWidth, stride, type);
                var pongBuffer = new ComputeBuffer(newWidth, stride, type);
                ping.Buffers[buffer.Key] = pingBuffer;
                pong.Buffers[buffer.Key] = pongBuffer;
            }

            foreach (var texture in textures)
            {
                var format = texture.Value;
                var pingTexture = new RenderTexture(newWidth, newWidth, 0, format)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Point
                };
                pingTexture.Create();

                var pongTexture = new RenderTexture(newWidth, newWidth, 0, format)
                {
                    enableRandomWrite = true,
                    filterMode = FilterMode.Point
                };
                pongTexture.Create();

                ping.Textures[texture.Key] = pingTexture;
                pong.Textures[texture.Key] = pongTexture;
            }
            
            BuffersInitialized = true;
        }


        public void Execute(BufferSet source)
        {
            if (!BuffersInitialized)
            {
                Debug.LogError("Buffers not initialized. Call InitBuffers first.");
                return;
            }
            
            if (shaderList == null || shaderList.Count == 0)
            {
                Debug.LogError("No shaders added to ComputePingPong.");
                return;
            }

            if (source.Buffers == null || source.Textures == null)
            {
                Debug.LogError("Source BufferSet is not properly initialized.");
                return;
            }


            // --- Phase 1: The Handoff Dispatch ---

            // Get the first shader and kernel
            var firstContainer = shaderList[0];
            var firstShader = firstContainer.Shader;
            var firstKernel = firstContainer.Kernel;

            foreach (var bufferInfo in source.Buffers)
            {
                var name = bufferInfo.Key + "_in";
                var buffer = bufferInfo.Value;
                firstShader.SetBuffer(firstKernel, name, buffer);
            }

            foreach (var bufferInfo in ping.Buffers)
            {
                var name = bufferInfo.Key + "_out";
                var buffer = bufferInfo.Value;
                firstShader.SetBuffer(firstKernel, name, buffer);
            }

            foreach (var textureInfo in source.Textures)
            {
                var name = textureInfo.Key + "_in";
                var texture = textureInfo.Value;
                firstShader.SetTexture(firstKernel, name, texture);
            }

            foreach (var textureInfo in ping.Textures)
            {
                var name = textureInfo.Key + "_out";
                var texture = textureInfo.Value;
                firstShader.SetTexture(firstKernel, name, texture);
            }

            foreach (var intParam in firstContainer.IntParams)
            {
                firstShader.SetInt(intParam.Key, intParam.Value);
            }

            foreach (var floatParam in firstContainer.FloatParams)
            {
                firstShader.SetFloat(floatParam.Key, floatParam.Value);
            }

            int threadGroupsX = Mathf.CeilToInt(width / 8f);
            int threadGroupsY = Mathf.CeilToInt(width / 8f);
            firstShader.Dispatch(firstKernel, threadGroupsX, threadGroupsY, 1);

            // The result of the first step is now in `ping`.
            // Check if we are already done.
            int totalDispatches = shaderList.Sum(s => s.Iterations);
            if (totalDispatches == 1)
            {
                Result = ping;
                return;
            }

            // --- Phase 2: The Internal Ping-Pong Loop ---

            // The subsequent dispatches will happen between ping and pong.
            BufferSet currentRead = ping;
            BufferSet currentWrite = pong;

            int dispatchCounter = 1;
            foreach (var container in shaderList)
            {
                for (int j = 0; j < container.Iterations; j++)
                {
                    // Skip the very first dispatch, which we already did.
                    if (dispatchCounter == 1)
                    {
                        dispatchCounter++;
                        continue;
                    }

                    var shader = container.Shader;
                    var kernel = container.Kernel;
                    foreach (var bufferInfo in currentRead.Buffers)
                    {
                        var name = bufferInfo.Key + "_in";
                        var buffer = bufferInfo.Value;
                        shader.SetBuffer(kernel, name, buffer);
                    }

                    foreach (var bufferInfo in currentWrite.Buffers)
                    {
                        var name = bufferInfo.Key + "_out";
                        var buffer = bufferInfo.Value;
                        shader.SetBuffer(kernel, name, buffer);
                    }

                    foreach (var textureInfo in currentRead.Textures)
                    {
                        var name = textureInfo.Key + "_in";
                        var texture = textureInfo.Value;
                        shader.SetTexture(kernel, name, texture);
                    }

                    foreach (var textureInfo in currentWrite.Textures)
                    {
                        var name = textureInfo.Key + "_out";
                        var texture = textureInfo.Value;
                        shader.SetTexture(kernel, name, texture);
                    }

                    foreach (var intParam in container.IntParams)
                    {
                        shader.SetInt(intParam.Key, intParam.Value);
                    }

                    foreach (var floatParam in container.FloatParams)
                    {
                        shader.SetFloat(floatParam.Key, floatParam.Value);
                    }

                    shader.Dispatch(kernel, threadGroupsX, threadGroupsY, 1);

                    // Now we swap our internal buffers for the next iteration.
                    (currentRead, currentWrite) = (currentWrite, currentRead);

                    dispatchCounter++;
                }
            }

            // The final result is in the last buffer we read from (after the final swap).
            Result = currentRead;
            
        }

        public void Release()
        {
            ping.Release();
            pong.Release();
        }

        public void Dispose()
        {
            Release();
        }
    }
}