using System.Linq;
using PlanetGen.FieldGen2.Graph;
using Sirenix.OdinInspector;
using Unity.Collections;
using Unity.Jobs;
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

        [Button("Generate Planet", ButtonSizes.Large)] 
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

            finalOutputBuffer = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            finalJobHandle = outputNode.Schedule(new JobHandle(), textureSize, ref finalOutputBuffer);
        }

        void Update()
        {
            if (!isJobRunning) return;

            if (finalJobHandle.IsCompleted)
            {
                finalJobHandle.Complete();

                Debug.Log($"Generated Data Sample. First pixel: {finalOutputBuffer[0]}, Middle pixel: {finalOutputBuffer[finalOutputBuffer.Length / 2]}");

                
                Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RFloat, false);
                tex.SetPixelData(finalOutputBuffer, 0);
                tex.Apply();

                if (outputRenderer != null)
                {
                    if (outputRenderer.material.mainTexture != null) {
                        Destroy(outputRenderer.material.mainTexture);
                    }
                    outputRenderer.material.mainTexture = tex;

                    // outputRenderer.sharedMaterial.mainTexture = tex;
                }

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

