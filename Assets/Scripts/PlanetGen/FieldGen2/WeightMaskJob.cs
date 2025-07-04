using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using static Unity.Mathematics.noise; // Use snoise directly

namespace PlanetGen.FieldGen2
{
    public class WeightMask
    {
        [BurstCompile]
        public struct GenerateNoiseJob : IJobParallelFor
        {
            [ReadOnly] public int textureSize;
            [ReadOnly] public float dominanceNoiseFrequency;
            [ReadOnly] public float2 layerNoiseOffset;
            [WriteOnly] public NativeArray<float> noiseOutput;
            
            public void Execute(int pixelIndex)
            {
                int x = pixelIndex % textureSize;
                int y = pixelIndex / textureSize;

                // Convert pixel position to polar-like coordinates for radial noise generation
                float normalizedX = (float)x / (textureSize - 1) - 0.5f; // Range [-0.5, 0.5]
                float normalizedY = (float)y / (textureSize - 1) - 0.5f; // Range [-0.5, 0.5]

                // Angle from -PI to PI
                float angle_coord = math.atan2(normalizedY, normalizedX);

                // Radius from center, scaled to [0,1] to cover the full circle if texture is square
                float radius_coord = math.sqrt(normalizedX * normalizedX + normalizedY * normalizedY) * 2f;
                radius_coord = math.clamp(radius_coord, 0f, 1f); // Ensure it's within [0,1] for noise

                // Generate noise for this layer
                float2 noiseCoord = new float2(
                    angle_coord * dominanceNoiseFrequency, 
                    radius_coord * dominanceNoiseFrequency
                ) + layerNoiseOffset;
                
                float noiseValue = snoise(noiseCoord) * 0.5f + 0.5f; // Range [0, 1]
                noiseOutput[pixelIndex] = noiseValue;
            }
        }

        [BurstCompile]
        public struct CalculateWeightsJob : IJobParallelFor
        {
            [ReadOnly] public int layerCount;
            [ReadOnly] public int pixelCount;
            [ReadOnly] public float dominanceSharpness;
            [ReadOnly] public NativeArray<float> layerBaseWeights;
            
            // Flattened input: [layer0_allPixels, layer1_allPixels, layer2_allPixels, ...]
            [ReadOnly, NativeDisableParallelForRestriction] 
            public NativeArray<float> flattenedNoiseData;
            
            // Flattened output: [pixel0_layer0, pixel0_layer1, pixel1_layer0, pixel1_layer1, ...]
            [WriteOnly, NativeDisableParallelForRestriction] 
            public NativeArray<float> flattenedWeightData;
            
            public void Execute(int pixelIndex)
            {
                // Find the maximum noise value across all layers for this pixel
                float maxNoiseValue = -float.MaxValue;
                for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
                {
                    // Read from flattened noise: layer data is stored sequentially
                    int noiseIndex = layerIdx * pixelCount + pixelIndex;
                    float noiseValue = flattenedNoiseData[noiseIndex];
                    if (noiseValue > maxNoiseValue)
                    {
                        maxNoiseValue = noiseValue;
                    }
                }

                // Calculate influences for each layer
                var influences = new NativeArray<float>(layerCount, Allocator.Temp);
                float totalUnnormalizedInfluence = 0f;

                for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
                {
                    int noiseIndex = layerIdx * pixelCount + pixelIndex;
                    float currentNoiseValue = flattenedNoiseData[noiseIndex];
                    float dominanceDelta = maxNoiseValue - currentNoiseValue;
                    float layerInfluence = math.exp(-dominanceDelta * dominanceSharpness);

                    influences[layerIdx] = layerInfluence * layerBaseWeights[layerIdx];
                    totalUnnormalizedInfluence += influences[layerIdx];
                }

                // Write to flattened output: [pixel0_layer0, pixel0_layer1, pixel1_layer0, pixel1_layer1, ...]
                int baseOutputIndex = pixelIndex * layerCount;
                
                if (totalUnnormalizedInfluence > 0f)
                {
                    for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
                    {
                        flattenedWeightData[baseOutputIndex + layerIdx] = influences[layerIdx] / totalUnnormalizedInfluence;
                    }
                }
                else
                {
                    float equalWeight = 1f / layerCount;
                    for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
                    {
                        flattenedWeightData[baseOutputIndex + layerIdx] = equalWeight;
                    }
                }

                influences.Dispose();
            }
        }

        public static void GenerateWeightMasks(
            List<GraphLayer> graphLayers,
            int textureSize,
            uint dominanceMasterSeed,
            float dominanceNoiseFrequency,
            float dominanceSharpness)
        {
            // Dispose existing weight masks
            for (int i = 0; i < graphLayers.Count; i++)
            {
                var layer = graphLayers[i];
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }
            }

            if (graphLayers.Count == 0) return;

            int pixelCount = textureSize * textureSize;
            int layerCount = graphLayers.Count;

            // Phase 1: Generate noise for each layer in parallel
            var noiseArrays = new NativeArray<NativeArray<float>>(layerCount, Allocator.TempJob);
            var layerNoiseOffsets = new NativeArray<float2>(layerCount, Allocator.TempJob);
            var layerBaseWeights = new NativeArray<float>(layerCount, Allocator.TempJob);
            
            // Setup layer data
            var masterRandom = new Random(dominanceMasterSeed);
            for (int i = 0; i < layerCount; i++)
            {
                masterRandom.InitState(dominanceMasterSeed + (uint)graphLayers[i].weightSeed * 731 + (uint)i * 127);
                layerNoiseOffsets[i] = masterRandom.NextFloat2() * 1000f;
                layerBaseWeights[i] = graphLayers[i].baseWeight;
                
                // Create noise array for this layer
                noiseArrays[i] = new NativeArray<float>(pixelCount, Allocator.TempJob);
            }

            // Schedule noise generation jobs (one per layer)
            var noiseJobHandles = new NativeArray<JobHandle>(layerCount, Allocator.Temp);
            for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
            {
                var noiseJob = new GenerateNoiseJob
                {
                    textureSize = textureSize,
                    dominanceNoiseFrequency = dominanceNoiseFrequency,
                    layerNoiseOffset = layerNoiseOffsets[layerIdx],
                    noiseOutput = noiseArrays[layerIdx]
                };

                int batchSize = math.max(1, pixelCount / (Unity.Jobs.LowLevel.Unsafe.JobsUtility.MaxJobThreadCount * 4));
                noiseJobHandles[layerIdx] = noiseJob.Schedule(pixelCount, batchSize);
            }

            // Wait for all noise generation to complete
            JobHandle.CompleteAll(noiseJobHandles);
            noiseJobHandles.Dispose();

            // Phase 2: Flatten noise data and calculate weights
            // Create flattened noise array: [layer0_allPixels, layer1_allPixels, layer2_allPixels, ...]
            var flattenedNoiseData = new NativeArray<float>(pixelCount * layerCount, Allocator.TempJob);
            for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
            {
                NativeArray<float>.Copy(
                    noiseArrays[layerIdx], 0, 
                    flattenedNoiseData, layerIdx * pixelCount, 
                    pixelCount
                );
            }

            // Create flattened output for weights: [pixel0_layer0, pixel0_layer1, pixel1_layer0, pixel1_layer1, ...]
            var flattenedWeightData = new NativeArray<float>(pixelCount * layerCount, Allocator.TempJob);
            
            var weightsJob = new CalculateWeightsJob
            {
                layerCount = layerCount,
                pixelCount = pixelCount,
                dominanceSharpness = dominanceSharpness,
                layerBaseWeights = layerBaseWeights,
                flattenedNoiseData = flattenedNoiseData,
                flattenedWeightData = flattenedWeightData
            };

            int weightsBatchSize = math.max(1, pixelCount / (Unity.Jobs.LowLevel.Unsafe.JobsUtility.MaxJobThreadCount * 4));
            JobHandle weightsJobHandle = weightsJob.Schedule(pixelCount, weightsBatchSize);
            weightsJobHandle.Complete();

            // Create final weight masks and extract data from flattened output
            var weightMasks = new NativeArray<NativeArray<float>>(layerCount, Allocator.Temp);
            for (int layerIdx = 0; layerIdx < layerCount; layerIdx++)
            {
                var layerWeightMask = new NativeArray<float>(pixelCount, Allocator.Persistent);
                
                // Extract this layer's data from flattened weight data
                for (int pixelIdx = 0; pixelIdx < pixelCount; pixelIdx++)
                {
                    int flattenedIndex = pixelIdx * layerCount + layerIdx;
                    layerWeightMask[pixelIdx] = flattenedWeightData[flattenedIndex];
                }
                
                weightMasks[layerIdx] = layerWeightMask;
            }

            // Assign weight masks to graph layers
            for (int layerIndex = 0; layerIndex < layerCount; layerIndex++)
            {
                var layer = graphLayers[layerIndex];
                layer.weightMask = weightMasks[layerIndex];
                graphLayers[layerIndex] = layer;
            }

            // Cleanup temporary allocations
            for (int i = 0; i < layerCount; i++)
            {
                noiseArrays[i].Dispose();
            }
            noiseArrays.Dispose();
            layerNoiseOffsets.Dispose();
            layerBaseWeights.Dispose();
            flattenedNoiseData.Dispose();
            flattenedWeightData.Dispose();
            weightMasks.Dispose();
        }
    }
}