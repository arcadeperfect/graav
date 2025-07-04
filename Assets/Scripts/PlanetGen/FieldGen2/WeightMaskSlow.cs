using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.noise; // Use snoise directly

namespace PlanetGen.FieldGen2
{
    public class WeightMaskSlow
    {
        public static void GenerateWeightMasks(
            List<GraphLayer> graphLayers,
            int textureSize,
            uint dominanceMasterSeed,
            float dominanceNoiseFrequency,
            float dominanceSharpness)
        {
            for (int i = 0; i < graphLayers.Count; i++)
            {
                var layer = graphLayers[i];
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }
            }

            if (graphLayers.Count == 0) return;

            var newLayerWeightMasks = new NativeArray<NativeArray<float>>(graphLayers.Count, Allocator.Temp);
            for (int i = 0; i < graphLayers.Count; i++)
            {
                newLayerWeightMasks[i] = new NativeArray<float>(textureSize * textureSize, Allocator.Persistent);
            }

            var layerNoiseOffsets = new NativeArray<float2>(graphLayers.Count, Allocator.Temp);
            var masterRandom = new Random(dominanceMasterSeed);
            for (int i = 0; i < graphLayers.Count; i++)
            {
                masterRandom.InitState(dominanceMasterSeed + (uint)graphLayers[i].weightSeed * 731 + (uint)i * 127);
                layerNoiseOffsets[i] = masterRandom.NextFloat2() * 1000f;
            }

            for (int i = 0; i < textureSize * textureSize; i++)
            {
                int x = i % textureSize;
                int y = i / textureSize;

                // --- MODIFIED: Convert pixel position to polar-like coordinates for radial noise generation ---
                // Center the coordinates so atan2 and length behave correctly for a circle centered at 0,0
                float normalizedX = (float)x / (textureSize - 1) - 0.5f; // Range [-0.5, 0.5]
                float normalizedY = (float)y / (textureSize - 1) - 0.5f; // Range [-0.5, 0.5]

                // Angle from -PI to PI
                float angle_coord = math.atan2(normalizedY, normalizedX);

                // Radius from center, scaled to [0,1] to cover the full circle if texture is square
                float radius_coord = math.sqrt(normalizedX * normalizedX + normalizedY * normalizedY) * 2f;
                radius_coord = math.clamp(radius_coord, 0f, 1f); // Ensure it's within [0,1] for noise

                var unnormalizedInfluences = new NativeArray<float>(graphLayers.Count, Allocator.Temp);
                float maxNoiseValue = -float.MaxValue;

                for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                {
                    // Use transformed polar coordinates for snoise
                    // The frequencies now apply to angular and radial dimensions, creating radial patterns
                    float2 noiseCoord =
                        new float2(angle_coord * dominanceNoiseFrequency, radius_coord * dominanceNoiseFrequency) +
                        layerNoiseOffsets[layerIdx];
                    float noiseValue = snoise(noiseCoord) * 0.5f + 0.5f; // Range [0, 1]

                    unnormalizedInfluences[layerIdx] = noiseValue;
                    if (noiseValue > maxNoiseValue)
                    {
                        maxNoiseValue = noiseValue;
                    }
                }

                float totalUnnormalizedInfluence = 0f;

                for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                {
                    float currentNoiseValue = unnormalizedInfluences[layerIdx];
                    float dominanceDelta = maxNoiseValue - currentNoiseValue;

                    float layerInfluence = math.exp(-dominanceDelta * dominanceSharpness);

                    unnormalizedInfluences[layerIdx] = layerInfluence * graphLayers[layerIdx].baseWeight;
                    totalUnnormalizedInfluence += unnormalizedInfluences[layerIdx];
                }

                if (totalUnnormalizedInfluence > 0f)
                {
                    for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                    {
                        NativeArray<float> currentLayerMask = newLayerWeightMasks[layerIdx];
                        currentLayerMask[i] = unnormalizedInfluences[layerIdx] / totalUnnormalizedInfluence;
                        newLayerWeightMasks[layerIdx] = currentLayerMask;
                    }
                }
                else
                {
                    float equalWeight = 1f / graphLayers.Count;
                    for (int layerIdx = 0; layerIdx < graphLayers.Count; layerIdx++)
                    {
                        NativeArray<float> currentLayerMask = newLayerWeightMasks[layerIdx];
                        currentLayerMask[i] = equalWeight;
                        newLayerWeightMasks[layerIdx] = currentLayerMask;
                    }
                }

                unnormalizedInfluences.Dispose();
            }

            layerNoiseOffsets.Dispose();

            for (int layerIndex = 0; layerIndex < graphLayers.Count; layerIndex++)
            {
                var layer = graphLayers[layerIndex];
                if (layer.weightMask.IsCreated)
                {
                    layer.weightMask.Dispose();
                }

                layer.weightMask = newLayerWeightMasks[layerIndex];
                graphLayers[layerIndex] = layer;
            }

            newLayerWeightMasks.Dispose();
        }
    }
}