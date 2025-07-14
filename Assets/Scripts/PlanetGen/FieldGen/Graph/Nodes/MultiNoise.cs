// using System.Collections.Generic;
// using PlanetGen.FieldGen2.Graph.Jobs;
// using PlanetGen.FieldGen2.Graph.Nodes.Base;
// using Unity.Collections;
// using Unity.Jobs;
// using UnityEngine;
// using XNode;
// using Unity.Burst;
// using Unity.Mathematics;
//
// namespace PlanetGen.FieldGen2.Graph.Nodes
// {
//     // PATTERN-BASED NOISE
//     [Node.CreateNodeMenu("Noise/Pattern/Brick")]
//     public class BrickNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 2f)]
//         public float brickWidth = 1f;
//         
//         [Range(0.1f, 2f)]
//         public float brickHeight = 0.5f;
//         
//         [Range(0f, 1f)]
//         public float offset = 0.5f;
//         
//         [Range(0f, 0.1f)]
//         public float mortarThickness = 0.05f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new BrickNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 brickWidth = this.brickWidth,
//                 brickHeight = this.brickHeight,
//                 offset = this.offset,
//                 mortarThickness = this.mortarThickness
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Pattern/Sine Wave")]
//     public class SineWaveNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0f, 360f)]
//         public float direction = 0f;
//         
//         [Range(0.1f, 5f)]
//         public float waveLength = 1f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new SineWaveNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 direction = math.radians(this.direction),
//                 waveLength = this.waveLength
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Pattern/Checkerboard")]
//     public class CheckerboardNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0f, 1f)]
//         public float variation = 0.1f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new CheckerboardNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 variation = this.variation
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Pattern/Radial Rings")]
//     public class RadialRingsNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 5f)]
//         public float ringSpacing = 1f;
//         
//         [Range(0f, 1f)]
//         public float variation = 0.1f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new RadialRingsNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 ringSpacing = this.ringSpacing,
//                 variation = this.variation
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     // ADVANCED CELLULAR TYPES
//     [Node.CreateNodeMenu("Noise/Voronoi/Manhattan Distance")]
//     public class ManhattanVoronoiNode : NoiseGeneratorNode
//     {
//         [Range(0f, 1f)]
//         public float jitter = 0.5f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new ManhattanVoronoiJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 jitter = this.jitter
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Voronoi/Edge Detection")]
//     public class VoronoiEdgeNode : NoiseGeneratorNode
//     {
//         [Range(0f, 1f)]
//         public float jitter = 0.5f;
//         
//         [Range(0.01f, 0.2f)]
//         public float edgeThickness = 0.05f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new VoronoiEdgeJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 jitter = this.jitter,
//                 edgeThickness = this.edgeThickness
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     // TURBULENCE & DISTORTION
//     [Node.CreateNodeMenu("Noise/Turbulence/Ridged")]
//     public class RidgedNoiseNode : NoiseGeneratorNode
//     {
//         [Range(1, 8)]
//         public int octaves = 4;
//         
//         [Range(1f, 4f)]
//         public float lacunarity = 2f;
//         
//         [Range(0.1f, 1f)]
//         public float persistence = 0.5f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new RidgedNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 octaves = this.octaves,
//                 lacunarity = this.lacunarity,
//                 persistence = this.persistence
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Turbulence/Billowy")]
//     public class BillowyNoiseNode : NoiseGeneratorNode
//     {
//         [Range(1, 8)]
//         public int octaves = 4;
//         
//         [Range(1f, 4f)]
//         public float lacunarity = 2f;
//         
//         [Range(0.1f, 1f)]
//         public float persistence = 0.5f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new BillowyNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 octaves = this.octaves,
//                 lacunarity = this.lacunarity,
//                 persistence = this.persistence
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Turbulence/Domain Warp")]
//     public class DomainWarpNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 2f)]
//         public float warpStrength = 0.5f;
//         
//         [Range(0.1f, 5f)]
//         public float warpFrequency = 1f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new DomainWarpNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 warpStrength = this.warpStrength,
//                 warpFrequency = this.warpFrequency
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     // GEOMETRIC PATTERNS
//     [Node.CreateNodeMenu("Noise/Geometric/Spiral")]
//     public class SpiralNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 5f)]
//         public float spiralTightness = 1f;
//         
//         [Range(1, 10)]
//         public int arms = 2;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new SpiralNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 spiralTightness = this.spiralTightness,
//                 arms = this.arms
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Geometric/Maze")]
//     public class MazeNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 0.9f)]
//         public float wallDensity = 0.4f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new MazeNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 wallDensity = this.wallDensity
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     // NATURAL PHENOMENA
//     [Node.CreateNodeMenu("Noise/Natural/Lightning")]
//     public class LightningNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 2f)]
//         public float branchiness = 0.5f;
//         
//         [Range(1, 6)]
//         public int iterations = 3;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new LightningNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 branchiness = this.branchiness,
//                 iterations = this.iterations
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Natural/Crystalline")]
//     public class CrystallineNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 2f)]
//         public float crystalSize = 0.5f;
//         
//         [Range(0.1f, 1f)]
//         public float randomness = 0.3f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new CrystallineNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 crystalSize = this.crystalSize,
//                 randomness = this.randomness
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Natural/Plasma")]
//     public class PlasmaNoiseNode : NoiseGeneratorNode
//     {
//         [Range(1, 8)]
//         public int octaves = 4;
//         
//         [Range(0.1f, 5f)]
//         public float turbulence = 1f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new PlasmaNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 octaves = this.octaves,
//                 turbulence = this.turbulence
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Natural/Fractal Tree")]
//     public class FractalTreeNode : NoiseGeneratorNode
//     {
//         [Range(2, 8)]
//         public int branchLevels = 4;
//         
//         [Range(0.1f, 1.5f)]
//         public float branchAngle = 0.5f;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new FractalTreeJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 branchLevels = this.branchLevels,
//                 branchAngle = this.branchAngle
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Natural/Erosion")]
//     public class ErosionNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.01f, 0.5f)]
//         public float erosionStrength = 0.1f;
//         
//         [Range(1, 10)]
//         public int iterations = 3;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new ErosionNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 erosionStrength = this.erosionStrength,
//                 iterations = this.iterations
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
//
//     [Node.CreateNodeMenu("Noise/Natural/Caustics")]
//     public class CausticsNoiseNode : NoiseGeneratorNode
//     {
//         [Range(0.1f, 2f)]
//         public float refractionStrength = 0.5f;
//         
//         [Range(4, 16)]
//         public int rayCount = 8;
//         
//         protected override JobHandle ScheduleNoiseGeneration(JobHandle dependency, int textureSize,
//             List<NativeArray<float>> tempBuffers, ref NativeArray<float> outputBuffer)
//         {
//             return new CausticsNoiseJob
//             {
//                 outputBuffer = outputBuffer,
//                 textureSize = textureSize,
//                 frequency = this.frequency,
//                 amplitude = this.amplitude,
//                 seed = this.seed,
//                 refractionStrength = this.refractionStrength,
//                 rayCount = this.rayCount
//             }.Schedule(textureSize * textureSize, 64, dependency);
//         }
//     }
// }
//
// namespace PlanetGen.FieldGen2.Graph.Jobs
// {
//     // PATTERN-BASED JOBS
//     [BurstCompile(CompileSynchronously = true)]
//     public struct BrickNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float brickWidth, brickHeight, offset, mortarThickness;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize * frequency;
//             
//             float brickX = uv.x / brickWidth;
//             float brickY = uv.y / brickHeight;
//             
//             // Offset every other row
//             if ((int)math.floor(brickY) % 2 == 1)
//                 brickX += offset;
//             
//             float2 brickUV = new float2(brickX - math.floor(brickX), brickY - math.floor(brickY));
//             
//             // Check if we're in mortar
//             bool inMortar = brickUV.x < mortarThickness || brickUV.x > 1f - mortarThickness ||
//                            brickUV.y < mortarThickness || brickUV.y > 1f - mortarThickness;
//             
//             float baseValue = inMortar ? -0.5f : 0.5f;
//             
//             // Add noise variation
//             float2 noisePos = uv + new float2(seed, seed);
//             float noiseValue = noise.snoise(noisePos) * 0.2f;
//             
//             outputBuffer[index] = (baseValue + noiseValue) * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct SineWaveNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float direction, waveLength;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             
//             // Project UV onto wave direction
//             float2 waveDir = new float2(math.cos(direction), math.sin(direction));
//             float projection = math.dot(uv, waveDir);
//             
//             float waveValue = math.sin(projection * frequency * 2f * math.PI / waveLength + seed);
//             
//             outputBuffer[index] = waveValue * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct CheckerboardNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float variation;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize * frequency;
//             
//             int checkX = (int)math.floor(uv.x);
//             int checkY = (int)math.floor(uv.y);
//             
//             bool isWhite = (checkX + checkY) % 2 == 0;
//             float baseValue = isWhite ? 1f : -1f;
//             
//             // Add variation
//             float2 noisePos = new float2(checkX, checkY) * 0.1f + new float2(seed, seed);
//             float noiseValue = noise.snoise(noisePos) * variation;
//             
//             outputBuffer[index] = (baseValue + noiseValue) * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct RadialRingsNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float ringSpacing, variation;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             float2 center = new float2(0.5f, 0.5f);
//             
//             float distance = math.distance(uv, center) * frequency;
//             float ringValue = math.sin(distance / ringSpacing * 2f * math.PI + seed);
//             
//             // Add variation
//             float2 noisePos = uv * 5f + new float2(seed, seed);
//             float noiseValue = noise.snoise(noisePos) * variation;
//             
//             outputBuffer[index] = (ringValue + noiseValue) * amplitude;
//         }
//     }
//
//     // VORONOI VARIATIONS
//     [BurstCompile(CompileSynchronously = true)]
//     public struct ManhattanVoronoiJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float jitter;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             float2 scaledUV = uv * frequency;
//             
//             int2 baseCell = (int2)math.floor(scaledUV);
//             
//             float minDistance = float.MaxValue;
//             float cellValue = 0f;
//             
//             for (int offsetY = -1; offsetY <= 1; offsetY++)
//             {
//                 for (int offsetX = -1; offsetX <= 1; offsetX++)
//                 {
//                     int2 neighborCell = baseCell + new int2(offsetX, offsetY);
//                     float2 cellCenter = neighborCell + (int2)0.5f;
//                     
//                     float2 noiseCoord = (float2)neighborCell * 0.1f + new float2(seed, seed);
//                     float2 jitterOffset = new float2(
//                         noise.snoise(noiseCoord),
//                         noise.snoise(noiseCoord + new float2(100f, 200f))
//                     ) * jitter * 0.5f;
//                     
//                     float2 cellPoint = cellCenter + jitterOffset;
//                     
//                     // Manhattan distance instead of Euclidean
//                     float distance = math.abs(scaledUV.x - cellPoint.x) + math.abs(scaledUV.y - cellPoint.y);
//                     
//                     if (distance < minDistance)
//                     {
//                         minDistance = distance;
//                         cellValue = noise.snoise(noiseCoord + new float2(500f, 600f));
//                     }
//                 }
//             }
//             
//             outputBuffer[index] = cellValue * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct VoronoiEdgeJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float jitter, edgeThickness;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             float2 scaledUV = uv * frequency;
//             
//             int2 baseCell = (int2)math.floor(scaledUV);
//             
//             float minDistance1 = float.MaxValue;
//             float minDistance2 = float.MaxValue;
//             
//             for (int offsetY = -2; offsetY <= 2; offsetY++)
//             {
//                 for (int offsetX = -2; offsetX <= 2; offsetX++)
//                 {
//                     int2 neighborCell = baseCell + new int2(offsetX, offsetY);
//                     float2 cellCenter = neighborCell + (int2)0.5f;
//                     
//                     float2 noiseCoord = (float2)neighborCell * 0.1f + new float2(seed, seed);
//                     float2 jitterOffset = new float2(
//                         noise.snoise(noiseCoord),
//                         noise.snoise(noiseCoord + new float2(100f, 200f))
//                     ) * jitter * 0.5f;
//                     
//                     float2 cellPoint = cellCenter + jitterOffset;
//                     float distance = math.distance(scaledUV, cellPoint);
//                     
//                     if (distance < minDistance1)
//                     {
//                         minDistance2 = minDistance1;
//                         minDistance1 = distance;
//                     }
//                     else if (distance < minDistance2)
//                     {
//                         minDistance2 = distance;
//                     }
//                 }
//             }
//             
//             float edgeDistance = minDistance2 - minDistance1;
//             float edgeValue = edgeDistance < edgeThickness ? 1f : 0f;
//             
//             outputBuffer[index] = edgeValue * amplitude;
//         }
//     }
//
//     // TURBULENCE JOBS
//     [BurstCompile(CompileSynchronously = true)]
//     public struct RidgedNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public int octaves;
//         [ReadOnly] public float lacunarity, persistence;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 pos = new float2(x, y) / (float)textureSize;
//             
//             float value = 0f;
//             float currentAmplitude = 1f;
//             float currentFrequency = frequency;
//             
//             for (int i = 0; i < octaves; i++)
//             {
//                 float2 noisePos = pos * currentFrequency + new float2(seed, seed);
//                 float noiseValue = noise.snoise(noisePos);
//                 
//                 // Ridge: 1 - abs(noise)
//                 noiseValue = 1f - math.abs(noiseValue);
//                 noiseValue = noiseValue * noiseValue; // Square for sharper ridges
//                 
//                 value += noiseValue * currentAmplitude;
//                 
//                 currentFrequency *= lacunarity;
//                 currentAmplitude *= persistence;
//             }
//             
//             outputBuffer[index] = value * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct BillowyNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public int octaves;
//         [ReadOnly] public float lacunarity, persistence;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 pos = new float2(x, y) / (float)textureSize;
//             
//             float value = 0f;
//             float currentAmplitude = 1f;
//             float currentFrequency = frequency;
//             
//             for (int i = 0; i < octaves; i++)
//             {
//                 float2 noisePos = pos * currentFrequency + new float2(seed, seed);
//                 float noiseValue = noise.snoise(noisePos);
//                 
//                 // Billowy: abs(noise) * 2 - 1
//                 noiseValue = math.abs(noiseValue) * 2f - 1f;
//                 
//                 value += noiseValue * currentAmplitude;
//                 
//                 currentFrequency *= lacunarity;
//                 currentAmplitude *= persistence;
//             }
//             
//             outputBuffer[index] = value * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct DomainWarpNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float warpStrength, warpFrequency;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 pos = new float2(x, y) / (float)textureSize;
//             
//             // Generate warp offsets
//             float2 warpPos = pos * warpFrequency + new float2(seed, seed);
//             float2 warp = new float2(
//                 noise.snoise(warpPos),
//                 noise.snoise(warpPos + new float2(100f, 200f))
//             ) * warpStrength;
//             
//             // Sample noise at warped position
//             float2 warpedPos = pos + warp;
//             float2 noisePos = warpedPos * frequency + new float2(seed, seed);
//             float noiseValue = noise.snoise(noisePos);
//             
//             outputBuffer[index] = noiseValue * amplitude;
//         }
//     }
//
//     // GEOMETRIC PATTERN JOBS
//     [BurstCompile(CompileSynchronously = true)]
//     public struct SpiralNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float spiralTightness;
//         [ReadOnly] public int arms;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             float2 center = new float2(0.5f, 0.5f);
//             float2 fromCenter = uv - center;
//             
//             float radius = math.length(fromCenter);
//             float angle = math.atan2(fromCenter.y, fromCenter.x);
//             
//             // Spiral calculation
//             float spiralAngle = angle + radius * spiralTightness * frequency;
//             float spiralValue = math.sin(spiralAngle * arms + seed);
//             
//             // Fade out from center
//             float falloff = math.smoothstep(0f, 0.5f, radius);
//             
//             outputBuffer[index] = spiralValue * falloff * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct MazeNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float wallDensity;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             float2 pos = uv * frequency;
//             
//             int2 cell = (int2)math.floor(pos);
//             float2 cellUV = pos - cell;
//             
//             // Generate maze pattern using noise
//             float2 noisePos = (float2)cell * 0.1f + new float2(seed, seed);
//             float cellNoise = noise.snoise(noisePos);
//             
//             // Create walls based on cell edges and noise
//             bool isWall = false;
//             
//             // Horizontal walls
//             if (cellUV.y < 0.1f || cellUV.y > 0.9f)
//             {
//                 float horizontalNoise = noise.snoise(noisePos + new float2(0f, 100f));
//                 isWall = horizontalNoise > (1f - wallDensity * 2f);
//             }
//             
//             // Vertical walls
//             if (cellUV.x < 0.1f || cellUV.x > 0.9f)
//             {
//                 float verticalNoise = noise.snoise(noisePos + new float2(200f, 0f));
//                 isWall = isWall || (verticalNoise > (1f - wallDensity * 2f));
//             }
//             
//             float mazeValue = isWall ? 1f : -1f;
//             
//             outputBuffer[index] = mazeValue * amplitude;
//         }
//     }
//
//     // NATURAL PHENOMENA JOBS
//     [BurstCompile(CompileSynchronously = true)]
//     public struct LightningNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float branchiness;
//         [ReadOnly] public int iterations;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             
//             float lightningValue = 0f;
//             
//             // Generate multiple lightning branches
//             for (int i = 0; i < iterations; i++)
//             {
//                 float2 seedOffset = new float2(seed + i * 123.456f, seed + i * 789.123f);
//                 
//                 // Create a path from top to bottom with branching
//                 float pathProgress = uv.y;
//                 
//                 // Main path position at this height
//                 float2 pathPos = new float2(
//                     0.5f + noise.snoise(new float2(pathProgress * frequency + seedOffset.x, seedOffset.y)) * 0.3f,
//                     pathProgress
//                 );
//                 
//                 // Distance to main path
//                 float distanceToPath = math.distance(uv, pathPos);
//                 
//                 // Add branches
//                 for (int branch = 0; branch < 3; branch++)
//                 {
//                     float2 branchSeed = seedOffset + branch * 456.789f;
//                     float branchStart = noise.snoise(branchSeed) * 0.5f + 0.5f;
//                     
//                     if (pathProgress > branchStart)
//                     {
//                         float2 branchDir = new float2(
//                             noise.snoise(branchSeed + new float2(100f, 0f)),
//                             math.abs(noise.snoise(branchSeed + new float2(0f, 100f))) * 0.5f + 0.5f
//                         );
//                         
//                         float2 branchPos = pathPos + branchDir * (pathProgress - branchStart) * branchiness;
//                         float distanceToBranch = math.distance(uv, branchPos);
//                         distanceToPath = math.min(distanceToPath, distanceToBranch);
//                     }
//                 }
//                 
//                 // Convert distance to lightning intensity
//                 float thickness = 0.02f / frequency;
//                 float intensity = math.exp(-distanceToPath / thickness);
//                 lightningValue = math.max(lightningValue, intensity);
//             }
//             
//             outputBuffer[index] = lightningValue * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct CrystallineNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float crystalSize, randomness;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             float2 scaledUV = uv * frequency;
//             
//             int2 baseCell = (int2)math.floor(scaledUV / crystalSize);
//             
//             float minDistance = float.MaxValue;
//             float crystalValue = 0f;
//             
//             // Check surrounding cells for crystal centers
//             for (int offsetY = -1; offsetY <= 1; offsetY++)
//             {
//                 for (int offsetX = -1; offsetX <= 1; offsetX++)
//                 {
//                     int2 neighborCell = baseCell + new int2(offsetX, offsetY);
//                     
//                     // Generate crystal center within this cell
//                     float2 noiseCoord = (float2)neighborCell * 0.1f + new float2(seed, seed);
//                     float2 crystalCenter = (neighborCell + (int2)0.5f) * (int2)crystalSize;
//                     
//                     // Add randomness to crystal position
//                     float2 randomOffset = new float2(
//                         noise.snoise(noiseCoord),
//                         noise.snoise(noiseCoord + new float2(100f, 200f))
//                     ) * randomness * crystalSize * 0.5f;
//                     
//                     crystalCenter += randomOffset;
//                     
//                     float distance = math.distance(scaledUV, crystalCenter);
//                     
//                     if (distance < minDistance)
//                     {
//                         minDistance = distance;
//                         
//                         // Generate crystal properties
//                         float crystalNoise = noise.snoise(noiseCoord + new float2(300f, 400f));
//                         
//                         // Create crystalline facets
//                         float2 toCrystal = scaledUV - crystalCenter;
//                         float angle = math.atan2(toCrystal.y, toCrystal.x);
//                         float facetAngle = math.floor(angle / (math.PI / 3f)) * (math.PI / 3f); // 6 facets
//                         
//                         float facetIntensity = math.cos(angle - facetAngle);
//                         crystalValue = crystalNoise * facetIntensity;
//                     }
//                 }
//             }
//             
//             // Add distance-based falloff
//             float falloff = math.exp(-minDistance / crystalSize);
//             crystalValue *= falloff;
//             
//             outputBuffer[index] = crystalValue * amplitude;
//         }
//     }
//
//     [BurstCompile(CompileSynchronously = true)]
//     public struct PlasmaNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public int octaves;
//         [ReadOnly] public float turbulence;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 pos = new float2(x, y) / (float)textureSize;
//             
//             // Generate plasma using multiple sine waves and noise
//             float plasma = 0f;
//             
//             // Base sine wave patterns
//             float wave1 = math.sin(pos.x * frequency * 10f + seed);
//             float wave2 = math.sin(pos.y * frequency * 10f + seed + 1f);
//             float wave3 = math.sin((pos.x + pos.y) * frequency * 7f + seed + 2f);
//             float wave4 = math.sin(math.sqrt(pos.x * pos.x + pos.y * pos.y) * frequency * 12f + seed + 3f);
//             
//             plasma = (wave1 + wave2 + wave3 + wave4) / 4f;
//             
//             // Add turbulent noise octaves
//             float noiseValue = 0f;
//             float currentAmplitude = turbulence;
//             float currentFrequency = frequency * 2f;
//             
//             for (int i = 0; i < octaves; i++)
//             {
//                 float2 noisePos = pos * currentFrequency + new float2(seed, seed);
//                 noiseValue += noise.snoise(noisePos) * currentAmplitude;
//                 
//                 currentFrequency *= 2f;
//                 currentAmplitude *= 0.5f;
//             }
//             
//             // Combine plasma and noise with interference patterns
//             plasma += noiseValue;
//             plasma = math.sin(plasma * math.PI);
//             
//             outputBuffer[index] = plasma * amplitude;
//         }
//     }
//
//     // FRACTAL PATTERNS
//     [BurstCompile(CompileSynchronously = true)]
//     public struct FractalTreeJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public int branchLevels;
//         [ReadOnly] public float branchAngle;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             
//             float treeValue = 0f;
//             
//             // Start from bottom center
//             float2 startPos = new float2(0.5f, 0.1f);
//             float2 currentPos = startPos;
//             float currentAngle = math.PI * 0.5f; // pointing up
//             float branchLength = 0.3f / frequency;
//             
//             // Generate fractal tree structure
//             for (int level = 0; level < branchLevels; level++)
//             {
//                 int branchesAtLevel = (int)math.pow(2f, level);
//                 
//                 for (int branch = 0; branch < branchesAtLevel; branch++)
//                 {
//                     // Calculate branch parameters
//                     float branchSeed = seed + level * 123.45f + branch * 67.89f;
//                     float angleVariation = noise.snoise(new float2(branchSeed, branchSeed + 100f)) * branchAngle;
//                     float currentBranchAngle = currentAngle + angleVariation;
//                     
//                     // Branch direction
//                     float2 branchDir = new float2(math.cos(currentBranchAngle), math.sin(currentBranchAngle));
//                     float2 branchEnd = currentPos + branchDir * branchLength;
//                     
//                     // Distance to branch line
//                     float distanceToLine = DistanceToLineSegment(uv, currentPos, branchEnd);
//                     float thickness = 0.01f / (level + 1f); // Thinner branches at higher levels
//                     
//                     if (distanceToLine < thickness)
//                     {
//                         treeValue = math.max(treeValue, 1f - distanceToLine / thickness);
//                     }
//                     
//                     // Update for next level
//                     currentPos = branchEnd;
//                 }
//                 
//                 branchLength *= 0.7f; // Shorter branches each level
//                 currentAngle += branchAngle * (level % 2 == 0 ? 1f : -1f); // Alternate sides
//             }
//             
//             outputBuffer[index] = treeValue * amplitude;
//         }
//         
//         private float DistanceToLineSegment(float2 p, float2 a, float2 b)
//         {
//             float2 pa = p - a;
//             float2 ba = b - a;
//             float h = math.clamp(math.dot(pa, ba) / math.dot(ba, ba), 0f, 1f);
//             return math.length(pa - ba * h);
//         }
//     }
//
//     // EROSION SIMULATION
//     [BurstCompile(CompileSynchronously = true)]
//     public struct ErosionNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float erosionStrength;
//         [ReadOnly] public int iterations;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 pos = new float2(x, y) / (float)textureSize;
//             
//             // Start with base terrain
//             float2 noisePos = pos * frequency + new float2(seed, seed);
//             float height = noise.snoise(noisePos);
//             
//             // Simulate erosion by flowing water
//             for (int iter = 0; iter < iterations; iter++)
//             {
//                 // Sample surrounding heights to find slope direction
//                 float2 gradient = new float2(0f, 0f);
//                 float step = 1f / textureSize / frequency;
//                 
//                 // Calculate gradient
//                 float heightRight = noise.snoise(noisePos + new float2(step, 0f));
//                 float heightLeft = noise.snoise(noisePos - new float2(step, 0f));
//                 float heightUp = noise.snoise(noisePos + new float2(0f, step));
//                 float heightDown = noise.snoise(noisePos - new float2(0f, step));
//                 
//                 gradient.x = (heightRight - heightLeft) / (2f * step);
//                 gradient.y = (heightUp - heightDown) / (2f * step);
//                 
//                 // Erode based on slope
//                 float slope = math.length(gradient);
//                 float erosion = slope * erosionStrength * (iter + 1f) / iterations;
//                 
//                 height -= erosion;
//                 
//                 // Move slightly downhill for next iteration
//                 noisePos -= gradient * step * 0.1f;
//             }
//             
//             outputBuffer[index] = height * amplitude;
//         }
//     }
//
//     // CAUSTICS SIMULATION
//     [BurstCompile(CompileSynchronously = true)]
//     public struct CausticsNoiseJob : IJobParallelFor
//     {
//         [WriteOnly] public NativeArray<float> outputBuffer;
//         [ReadOnly] public int textureSize;
//         [ReadOnly] public float frequency, amplitude, seed;
//         [ReadOnly] public float refractionStrength;
//         [ReadOnly] public int rayCount;
//
//         public void Execute(int index)
//         {
//             int x = index % textureSize;
//             int y = index / textureSize;
//             
//             float2 uv = new float2(x, y) / (float)textureSize;
//             
//             float causticIntensity = 0f;
//             
//             // Simulate light rays passing through water surface
//             for (int ray = 0; ray < rayCount; ray++)
//             {
//                 // Generate water surface distortion
//                 float2 surfacePos = uv + new float2(ray * 0.1f, seed);
//                 float2 surfaceNoise = new float2(
//                     noise.snoise(surfacePos * frequency),
//                     noise.snoise(surfacePos * frequency + new float2(100f, 200f))
//                 ) * refractionStrength;
//                 
//                 // Calculate refracted ray direction
//                 float2 rayDirection = math.normalize(new float2(0f, -1f) + surfaceNoise);
//                 
//                 // Find where this ray hits the bottom plane
//                 float rayLength = 1f / math.abs(rayDirection.y);
//                 float2 hitPos = uv + rayDirection * rayLength;
//                 
//                 // Check if this ray contributes to current pixel
//                 float distance = math.distance(uv, hitPos);
//                 if (distance < 0.05f / frequency)
//                 {
//                     causticIntensity += math.exp(-distance * frequency * 20f);
//                 }
//             }
//             
//             // Add time-varying animation (using seed as time substitute)
//             float2 timePos = uv * frequency * 2f + new float2(seed * 2f, seed * 3f);
//             float timeVariation = noise.snoise(timePos) * 0.3f + 0.7f;
//             
//             causticIntensity *= timeVariation;
//             
//             outputBuffer[index] = math.clamp(causticIntensity, 0f, 1f) * amplitude;
//         }
//     }
// }