using System;
using System.Collections.Generic;
using System.Linq;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Nodes;
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
        private NativeArray<float> finalOutputBuffer;
        private List<NativeArray<float>> tempBuffers;

        [Button("Generate Planet", ButtonSizes.Large)] 
        public void Generate()
        {
            if (isJobRunning)
            {
                return;
            }

            CompileAndRunGraph();
        }

        public void OnValidate()
        {
            print("a");
            if (isJobRunning)
            {
                print("b");
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
            
            finalOutputBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            finalJobHandle = outputNode.Schedule(new JobHandle(), textureSize, tempBuffers, ref finalOutputBuffer);
        }

        void Update()
        {
            if (!isJobRunning) return;

            if (finalJobHandle.IsCompleted)
            {
                finalJobHandle.Complete();

                // Add more detailed debugging
                Debug.Log($"Job completed! Buffer length: {finalOutputBuffer.Length}");
        
                // Sample a few values to see what we actually got
                float minVal = float.MaxValue, maxVal = float.MinValue;
                int zeroCount = 0, oneCount = 0;
        
                for (int i = 0; i < finalOutputBuffer.Length; i++)
                {
                    float val = finalOutputBuffer[i];
                    if (val < minVal) minVal = val;
                    if (val > maxVal) maxVal = val;
                    if (math.abs(val) < 0.001f) zeroCount++;
                    if (math.abs(val - 1.0f) < 0.001f) oneCount++;
                }
        
                Debug.Log($"Value range: {minVal} to {maxVal}");
                Debug.Log($"Zero values: {zeroCount}, One values: {oneCount}");
                Debug.Log($"First few values: {finalOutputBuffer[0]}, {finalOutputBuffer[1]}, {finalOutputBuffer[2]}");

                Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);
                tex.SetPixelData(finalOutputBuffer, 0);
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
            if (isJobRunning)
            {
                finalJobHandle.Complete();
                finalOutputBuffer.Dispose();
            }
        }
    }
}

