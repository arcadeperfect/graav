using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes;
using PlanetGen.FieldGen2.Graph.Nodes.Base;
using PlanetGen.FieldGen2.Graph.Nodes.Outputs;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2
{
    public class FieldGen2 : MonoBehaviour
    {
        [Header("Config")] public GeneratorGraph graph;
        public int textureSize = 512;

        [Header("Output")] public Renderer outputRenderer;

        private bool isJobRunning = false;
        private JobHandle finalJobHandle;
        private PlanetData finalOutputBuffer;
        private List<NativeArray<float>> tempBuffers;

        [Button("Generate Planet", ButtonSizes.Large)]
        public void GeneratePlanet()
        {
            regenQueued = true;
        }

        private bool regenQueued = false;
        
        public void Start()
        {
            BaseNode.OnAnyNodeChanged += OnNodeParameterChanged;
        }
        
        void OnNodeParameterChanged()
        {
            regenQueued = true;

        }
        
        public void Generate()
        {
            if (isJobRunning)
            {
                return;
            }

            CompileAndRunGraph();
        }


        private void CompileAndRunGraph()
        {
            if (graph == null)
            {
                Debug.LogError("Graph is null");
                return;
            }
            
            OutputNode outputNode = graph.nodes.OfType<OutputNode>().FirstOrDefault();
            if (outputNode == null)
            {
                Debug.LogError("Output node is null");
                return;
            }
            
            
            isJobRunning = true; 
            
            tempBuffers = new List<NativeArray<float>>();
            
            finalOutputBuffer = new PlanetData(textureSize);
            // finalJobHandle = outputNode.Schedule(new JobHandle(), textureSize, tempBuffers, ref finalOutputBuffer);
            finalJobHandle = outputNode.SchedulePlanetData(new JobHandle(), textureSize, tempBuffers, ref finalOutputBuffer);
        }

        void Update()
        {
            // First, check if we need to kick off a new generation.
            // We do this at the top of Update to be very responsive.
            if (regenQueued && !isJobRunning)
            {
                regenQueued = false; // Reset the flag
                Generate(); // Start the generation immediately
            }

            // Your existing logic for checking completion remains the same.
            if (!isJobRunning) return;

            if (finalJobHandle.IsCompleted)
            {
                finalJobHandle.Complete();
                
                // float minVal = float.MaxValue, maxVal = float.MinValue;
                // int zeroCount = 0, oneCount = 0;
        
                // for (int i = 0; i < finalOutputBuffer.Length; i++)
                // {
                //     float val = finalOutputBuffer[i];
                //     if (val < minVal) minVal = val;
                //     if (val > maxVal) maxVal = val;
                //     if (math.abs(val) < 0.001f) zeroCount++;
                //     if (math.abs(val - 1.0f) < 0.001f) oneCount++;
                // }

                Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);
                tex.SetPixelData(finalOutputBuffer.Scalar, 0);
                tex.Apply();

                if (outputRenderer != null)
                {
                    if (outputRenderer.material.mainTexture != null) {
                        Destroy(outputRenderer.material.mainTexture);
                    }
                    outputRenderer.material.mainTexture = tex;
                }

                // Clean up temp buffers
                foreach (var buffer in tempBuffers)
                {
                    if (buffer.IsCreated)
                        buffer.Dispose();
                }
                tempBuffers.Clear();

                finalOutputBuffer.Dispose();
                isJobRunning = false;
            }
        }

        void OnDestroy()
        {
            BaseNode.OnAnyNodeChanged  -= OnNodeParameterChanged;
            
            if (isJobRunning)
            {
                finalJobHandle.Complete();

                foreach (var buffer in tempBuffers)
                {
                    if(buffer.IsCreated)
                        buffer.Dispose();
                }
                tempBuffers.Clear();
                finalOutputBuffer.Dispose();
                isJobRunning = false;
            }

            if (outputRenderer != null && outputRenderer.material != null &&
                outputRenderer.material.mainTexture != null)
            {
                // Important: check if the texture is a RenderTexture or Texture2D
                if (outputRenderer.material.mainTexture is Texture2D)
                {
                    Destroy(outputRenderer.material.mainTexture);
                }
            }
        }
    }
}

