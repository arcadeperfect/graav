using System.Collections.Generic;
using UnityEngine;

namespace PlanetGen.Compute
{
    /// <summary>
    /// Centralized compute shader provider with IDE-friendly nested structure
    /// </summary>
    public static class CSP
    {
        // ===== SHADER PATHS =====
        // All shader paths are defined here for easy maintenance
        private const string MARCHING_SQUARES_PATH = "Compute/MarchingSquares";
        private const string JUMP_FLOOD_SDF_PATH = "Compute/JumpFloodSDF";
        private const string DOMAIN_WARP_PATH = "Compute/pingPong1/domainWarp";
        private const string GAUSSIAN_BLUR_PATH = "Compute/pingPong1/gaussianBlur";
        private const string UDF_FROM_SEGMENTS_PATH = "Compute/UdfFromSegments";
        
        // ===== DISPLAY NAMES =====
        private const string MARCHING_SQUARES_DISPLAY = "Marching Squares";
        private const string JUMP_FLOOD_SDF_DISPLAY = "Jump Flood SDF";
        private const string DOMAIN_WARP_DISPLAY = "Domain Warp";
        private const string GAUSSIAN_BLUR_DISPLAY = "Gaussian Blur";
        private const string UDF_FROM_SEGMENTS_DISPLAY = "Segments To Local UDF";

        private static readonly Dictionary<string, ComputeShader> _shaderCache = 
            new Dictionary<string, ComputeShader>();
        
        private static readonly Dictionary<string, int> _kernelCache = 
            new Dictionary<string, int>();

        // ReSharper disable Unity.PerformanceAnalysis
        /// <summary>
        /// Internal method to get shader by path with caching
        /// </summary>
        private static ComputeShader GetShaderInternal(string shaderPath, string displayName)
        {
            if (string.IsNullOrEmpty(shaderPath))
            {
                Debug.LogError($"[ComputeShaderProvider] Shader path cannot be null or empty for {displayName}");
                return null;
            }

            // Check cache first
            if (_shaderCache.TryGetValue(shaderPath, out ComputeShader cachedShader))
            {
                return cachedShader;
            }

            // Load from Resources
            ComputeShader shader = Resources.Load<ComputeShader>(shaderPath);
            
            if (shader == null)
            {
                Debug.LogError($"[ComputeShaderProvider] Failed to load compute shader '{displayName}' " +
                              $"at path 'Resources/{shaderPath}.compute'. Please ensure the file exists and is properly named.");
                return null;
            }

            // Cache successful load
            _shaderCache[shaderPath] = shader;
            return shader;
        }

        /// <summary>
        /// Internal method to get kernel index with caching
        /// </summary>
        private static int GetKernelInternal(string shaderPath, string kernelName, string displayName)
        {
            string cacheKey = $"{shaderPath}::{kernelName}";
            
            // Check kernel cache first
            if (_kernelCache.TryGetValue(cacheKey, out int cachedKernelIndex))
            {
                return cachedKernelIndex;
            }

            // Get the shader
            ComputeShader shader = GetShaderInternal(shaderPath, displayName);
            if (shader == null)
            {
                return -1;
            }

            // Get kernel index
            if (!shader.HasKernel(kernelName))
            {
                Debug.LogError($"[ComputeShaderProvider] Kernel '{kernelName}' not found in " +
                              $"compute shader '{displayName}'. Check your .compute file.");
                return -1;
            }

            int kernelIndex = shader.FindKernel(kernelName);
            
            // Cache successful lookup
            _kernelCache[cacheKey] = kernelIndex;
            return kernelIndex;
        }

        /// <summary>
        /// Clears all caches (useful for development/testing)
        /// </summary>
        public static void ClearCache()
        {
            _shaderCache.Clear();
            _kernelCache.Clear();
        }

        // ===== SHADER DEFINITIONS =====
        // Add new shaders here as nested classes

        public static class MarchingSquares
        {
            public static ComputeShader Get()
            {
                return GetShaderInternal(MARCHING_SQUARES_PATH, MARCHING_SQUARES_DISPLAY);
            }

            public static class Kernels
            {
                public static int MarchingSquares => GetKernelInternal(MARCHING_SQUARES_PATH, "MarchingSquares", MARCHING_SQUARES_DISPLAY);
            }
        }

        public static class JumpFloodSdf
        {
            public static ComputeShader Get()
            {
                return GetShaderInternal(JUMP_FLOOD_SDF_PATH, JUMP_FLOOD_SDF_DISPLAY);
            }

            public static class Kernels
            {
                public static int JumpFlood => GetKernelInternal(JUMP_FLOOD_SDF_PATH, "JumpFlood", JUMP_FLOOD_SDF_DISPLAY);
                public static int UdfFromSegmentsBruteForce => GetKernelInternal(JUMP_FLOOD_SDF_PATH, "UDF_from_segments_bruteForce", JUMP_FLOOD_SDF_DISPLAY);
                public static int SeedFromScalarField => GetKernelInternal(JUMP_FLOOD_SDF_PATH, "SeedFromScalarField", JUMP_FLOOD_SDF_DISPLAY);
                public static int FinalizeSDF => GetKernelInternal(JUMP_FLOOD_SDF_PATH, "FinalizeSDF", JUMP_FLOOD_SDF_DISPLAY);
                public static int SeedFromSegmentsHQ => GetKernelInternal(JUMP_FLOOD_SDF_PATH, "SeedFromSegmentsHQ", JUMP_FLOOD_SDF_DISPLAY);
            }
        }

        public static class DomainWarp
        {
            public static ComputeShader GetShader()
            {
                return GetShaderInternal(DOMAIN_WARP_PATH, DOMAIN_WARP_DISPLAY);
            }

            public static class Kernels
            {
                public static int Warp => GetKernelInternal(DOMAIN_WARP_PATH, "Warp", DOMAIN_WARP_DISPLAY);
            }
        }

        public static class GaussianBlur
        {
            public static ComputeShader GetShader()
            {
                return GetShaderInternal(GAUSSIAN_BLUR_PATH, GAUSSIAN_BLUR_DISPLAY);
            }

            public static class Kernels
            {
                public static int GaussianBlur => GetKernelInternal(GAUSSIAN_BLUR_PATH, "GaussianBlur", GAUSSIAN_BLUR_DISPLAY);
            }
        }

        public static class UdfFromSegments
        {
            public static ComputeShader GetShader()
            {
                return GetShaderInternal(UDF_FROM_SEGMENTS_PATH, UDF_FROM_SEGMENTS_DISPLAY);
            }

            public static class Kernels
            {

            }
        }
    }
}

// using System.Collections.Generic;
// using UnityEngine;
//
// namespace PlanetGen.Compute
// {
//     /// <summary>
//     /// Centralized compute shader provider with IDE-friendly nested structure
//     /// </summary>
//     public static class CSP
//     {
//         private static readonly Dictionary<string, ComputeShader> _shaderCache = 
//             new Dictionary<string, ComputeShader>();
//         
//         private static readonly Dictionary<string, int> _kernelCache = 
//             new Dictionary<string, int>();
//
//         // ReSharper disable Unity.PerformanceAnalysis
//         /// <summary>
//         /// Internal method to get shader by path with caching
//         /// </summary>
//         private static ComputeShader GetShaderInternal(string shaderPath, string displayName)
//         {
//             if (string.IsNullOrEmpty(shaderPath))
//             {
//                 Debug.LogError($"[ComputeShaderProvider] Shader path cannot be null or empty for {displayName}");
//                 return null;
//             }
//
//             // Check cache first
//             if (_shaderCache.TryGetValue(shaderPath, out ComputeShader cachedShader))
//             {
//                 return cachedShader;
//             }
//
//             // Load from Resources
//             ComputeShader shader = Resources.Load<ComputeShader>(shaderPath);
//             
//             if (shader == null)
//             {
//                 Debug.LogError($"[ComputeShaderProvider] Failed to load compute shader '{displayName}' " +
//                               $"at path 'Resources/{shaderPath}.compute'. Please ensure the file exists and is properly named.");
//                 return null;
//             }
//
//             // Cache successful load
//             _shaderCache[shaderPath] = shader;
//             return shader;
//         }
//
//         /// <summary>
//         /// Internal method to get kernel index with caching
//         /// </summary>
//         private static int GetKernelInternal(string shaderPath, string kernelName, string displayName)
//         {
//             string cacheKey = $"{shaderPath}::{kernelName}";
//             
//             // Check kernel cache first
//             if (_kernelCache.TryGetValue(cacheKey, out int cachedKernelIndex))
//             {
//                 return cachedKernelIndex;
//             }
//
//             // Get the shader
//             ComputeShader shader = GetShaderInternal(shaderPath, displayName);
//             if (shader == null)
//             {
//                 return -1;
//             }
//
//             // Get kernel index
//             if (!shader.HasKernel(kernelName))
//             {
//                 Debug.LogError($"[ComputeShaderProvider] Kernel '{kernelName}' not found in " +
//                               $"compute shader '{displayName}'. Check your .compute file.");
//                 return -1;
//             }
//
//             int kernelIndex = shader.FindKernel(kernelName);
//             
//             // Cache successful lookup
//             _kernelCache[cacheKey] = kernelIndex;
//             return kernelIndex;
//         }
//
//         /// <summary>
//         /// Clears all caches (useful for development/testing)
//         /// </summary>
//         public static void ClearCache()
//         {
//             _shaderCache.Clear();
//             _kernelCache.Clear();
//         }
//
//         // ===== SHADER DEFINITIONS =====
//         // Add new shaders here as nested classes
//
//         public static class MarchingSquares
//         {
//             private const string ShaderPath = "Compute/MarchingSquares";
//             private const string DisplayName = "Marching Squares";
//
//             public static ComputeShader Get()
//             {
//                 return GetShaderInternal(ShaderPath, DisplayName);
//             }
//
//             public static class Kernels
//             {
//                 public static int MarchingSquares => GetKernelInternal(ShaderPath, "MarchingSquares", DisplayName);
//             }
//         }
//
//         public static class JumpFloodSdf
//         {
//             private const string ShaderPath = "Compute/JumpFloodSDF";
//             private const string DisplayName = "Jump Flood SDF";
//
//             public static ComputeShader Get()
//             {
//                 return GetShaderInternal(ShaderPath, DisplayName);
//             }
//
//             public static class Kernels
//             {
//                 public static int JumpFlood => GetKernelInternal(ShaderPath, "JumpFlood", DisplayName);
//                 public static int UdfFromSegmentsBruteForce => GetKernelInternal(ShaderPath, "UDF_from_segments_bruteForce", DisplayName);
//                 public static int SeedFromScalarField => GetKernelInternal(ShaderPath, "SeedFromScalarField", DisplayName);
//                 public static int FinalizeSDF => GetKernelInternal(ShaderPath, "FinalizeSDF", DisplayName);
//                 public static int SeedFromSegmentsHQ => GetKernelInternal(ShaderPath, "SeedFromSegmentsHQ", DisplayName);
//             }
//         }
//
//         public static class DomainWarp
//         {
//             private const string ShaderPath = "Compute/pingPong1/domainWarp";
//             private const string DisplayName = "Domain Warp";
//
//             public static ComputeShader GetShader()
//             {
//                 return GetShaderInternal(ShaderPath, DisplayName);
//             }
//
//             public static class Kernels
//             {
//                 public static int Warp => GetKernelInternal(ShaderPath, "Warp", DisplayName);
//             }
//         }
//
//         public static class GaussianBlur
//         {
//             private const string ShaderPath = "Compute/pingPong1/gaussianBlur";
//             private const string DisplayName = "Gaussian Blur";
//
//             public static ComputeShader GetShader()
//             {
//                 return GetShaderInternal(ShaderPath, DisplayName);
//             }
//
//             public static class Kernels
//             {
//                 public static int GaussianBlur => GetKernelInternal(ShaderPath, "GaussianBlur", DisplayName);
//             }
//         }
//
//         public static class UdfFromSegments
//         {
//             private const string ShaderPath = "Compute/UdfFromSegments";
//             private const string DisplayName = "Segments To Local UDF";
//
//             public static ComputeShader GetShader()
//             {
//                 return GetShaderInternal(ShaderPath, DisplayName);
//             }
//
//             public static class Kernels
//             {
//
//             }
//         }
//     }
// }