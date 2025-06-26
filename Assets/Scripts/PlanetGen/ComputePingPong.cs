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
        // ReSharper disable Unity.PerformanceAnalysis
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