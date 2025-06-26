// using UnityEngine;
// using System.Collections.Generic;
//
// namespace PlanetGen
// {
//     /// <summary>
//     /// Type-safe compute kernel identifier
//     /// </summary>
//     public readonly struct ComputeKernelId : System.IEquatable<ComputeKernelId>
//     {
//         public readonly string ShaderPath;
//         public readonly string KernelName;
//         public readonly string DisplayName;
//
//         public ComputeKernelId(string shaderPath, string kernelName, string displayName = null)
//         {
//             ShaderPath = shaderPath;
//             KernelName = kernelName;
//             DisplayName = displayName ?? $"{shaderPath}::{kernelName}";
//         }
//
//         public bool Equals(ComputeKernelId other)
//         {
//             return ShaderPath == other.ShaderPath && KernelName == other.KernelName;
//         }
//
//         public override bool Equals(object obj)
//         {
//             return obj is ComputeKernelId other && Equals(other);
//         }
//
//         public override int GetHashCode()
//         {
//             return System.HashCode.Combine(ShaderPath, KernelName);
//         }
//
//         public override string ToString() => DisplayName;
//
//         public static bool operator ==(ComputeKernelId left, ComputeKernelId right)
//         {
//             return left.Equals(right);
//         }
//
//         public static bool operator !=(ComputeKernelId left, ComputeKernelId right)
//         {
//             return !left.Equals(right);
//         }
//     }
//
//     /// <summary>
//     /// Centralized compute kernel definitions - add new kernels here
//     /// </summary>
//     public static class ComputeKernels
//     {
//         // Marching Squares
//         public static class MarchingSquares
//         {
//             private const string ShaderPath = "Compute/MarchingSquares";
//             
//             public static readonly ComputeKernelId MarchingSquaresKernel = 
//                 new ComputeKernelId(ShaderPath, "MarchingSquares", "Marching Squares");
//         }
//
//         // Jump Flood SDF
//         public static class JumpFloodSDF
//         {
//             private const string ShaderPath = "Compute/JumpFloodSDF";
//             
//             public static readonly ComputeKernelId JumpFlood = 
//                 new ComputeKernelId(ShaderPath, "JumpFlood", "Jump Flood");
//             
//             public static readonly ComputeKernelId UdfFromSegmentsBruteForce = 
//                 new ComputeKernelId(ShaderPath, "UDF_from_segments_bruteForce", "UDF from Segments (Brute Force)");
//             
//             public static readonly ComputeKernelId SeedFromScalarField = 
//                 new ComputeKernelId(ShaderPath, "SeedFromScalarField", "Seed from Scalar Field");
//             
//             public static readonly ComputeKernelId FinalizeSDF = 
//                 new ComputeKernelId(ShaderPath, "FinalizeSDF", "Finalize SDF");
//             
//             public static readonly ComputeKernelId SeedFromSegmentsHQ = 
//                 new ComputeKernelId(ShaderPath, "SeedFromSegmentsHQ", "Seed from Segments (High Quality)");
//         }
//
//         // Domain Warp
//         public static class DomainWarp
//         {
//             private const string ShaderPath = "Compute/pingPong1/domainWarp";
//             
//             public static readonly ComputeKernelId Warp = 
//                 new ComputeKernelId(ShaderPath, "Warp", "Domain Warp");
//         }
//
//         // Gaussian Blur
//         public static class GaussianBlur
//         {
//             private const string ShaderPath = "Compute/pingPong1/gaussianBlur";
//             
//             public static readonly ComputeKernelId GaussianBlurKernel = 
//                 new ComputeKernelId(ShaderPath, "GaussianBlur", "Gaussian Blur");
//         }
//     }
//
//     /// <summary>
//     /// Centralized compute shader provider - purely for getting shaders and kernel indices
//     /// </summary>
//     public static class ComputeShaderProvider
//     {
//         private static readonly Dictionary<string, ComputeShader> _shaderCache = 
//             new Dictionary<string, ComputeShader>();
//         
//         private static readonly Dictionary<ComputeKernelId, int> _kernelCache = 
//             new Dictionary<ComputeKernelId, int>();
//
//         /// <summary>
//         /// Gets a compute shader by path
//         /// </summary>
//         public static ComputeShader GetShader(string shaderPath)
//         {
//             if (string.IsNullOrEmpty(shaderPath))
//             {
//                 Debug.LogError("[ComputeShaderProvider] Shader path cannot be null or empty");
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
//                 Debug.LogError($"[ComputeShaderProvider] Failed to load compute shader at path 'Resources/{shaderPath}.compute'. " +
//                               $"Please ensure the file exists and is properly named.");
//                 return null;
//             }
//
//             // Cache successful load
//             _shaderCache[shaderPath] = shader;
//             return shader;
//         }
//
//         /// <summary>
//         /// Gets a compute shader by kernel ID (convenience method)
//         /// </summary>
//         public static ComputeShader GetShader(ComputeKernelId kernelId)
//         {
//             return GetShader(kernelId.ShaderPath);
//         }
//
//         /// <summary>
//         /// Gets a kernel index by ID
//         /// </summary>
//         public static int GetKernel(ComputeKernelId kernelId)
//         {
//             // Check kernel cache first
//             if (_kernelCache.TryGetValue(kernelId, out int cachedKernelIndex))
//             {
//                 return cachedKernelIndex;
//             }
//
//             // Get the shader
//             ComputeShader shader = GetShader(kernelId.ShaderPath);
//             if (shader == null)
//             {
//                 return -1;
//             }
//
//             // Get kernel index
//             if (!shader.HasKernel(kernelId.KernelName))
//             {
//                 Debug.LogError($"[ComputeShaderProvider] Kernel '{kernelId.KernelName}' not found in " +
//                               $"compute shader '{kernelId.DisplayName}'. Check your .compute file.");
//                 return -1;
//             }
//
//             int kernelIndex = shader.FindKernel(kernelId.KernelName);
//             
//             // Cache successful lookup
//             _kernelCache[kernelId] = kernelIndex;
//             return kernelIndex;
//         }
//
//         /// <summary>
//         /// Gets both shader and kernel index in one call
//         /// </summary>
//         public static (ComputeShader shader, int kernelIndex) GetShaderAndKernel(ComputeKernelId kernelId)
//         {
//             ComputeShader shader = GetShader(kernelId.ShaderPath);
//             int kernelIndex = GetKernel(kernelId);
//             
//             return (shader, kernelIndex);
//         }
//
//         /// <summary>
//         /// Tries to get kernel index without logging errors (useful for optional kernels)
//         /// </summary>
//         public static int TryGetKernel(ComputeKernelId kernelId)
//         {
//             // Check cache first
//             if (_kernelCache.TryGetValue(kernelId, out int cachedKernelIndex))
//             {
//                 return cachedKernelIndex;
//             }
//
//             // Get the shader
//             ComputeShader shader = GetShader(kernelId.ShaderPath);
//             if (shader == null)
//             {
//                 return -1;
//             }
//
//             // Check if kernel exists without logging error
//             if (!shader.HasKernel(kernelId.KernelName))
//             {
//                 return -1;
//             }
//
//             int kernelIndex = shader.FindKernel(kernelId.KernelName);
//             
//             // Cache successful lookup
//             _kernelCache[kernelId] = kernelIndex;
//             return kernelIndex;
//         }
//
//         /// <summary>
//         /// Checks if a kernel exists without trying to load it
//         /// </summary>
//         public static bool HasKernel(ComputeKernelId kernelId)
//         {
//             // Check cache first
//             if (_kernelCache.ContainsKey(kernelId))
//             {
//                 return true;
//             }
//
//             ComputeShader shader = GetShader(kernelId.ShaderPath);
//             return shader != null && shader.HasKernel(kernelId.KernelName);
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
//     }
// }

using UnityEngine;
using System.Collections.Generic;

namespace PlanetGen
{
    /// <summary>
    /// Centralized compute shader provider with IDE-friendly nested structure
    /// </summary>
    public static class CSP
    {
        private static readonly Dictionary<string, ComputeShader> _shaderCache = 
            new Dictionary<string, ComputeShader>();
        
        private static readonly Dictionary<string, int> _kernelCache = 
            new Dictionary<string, int>();

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
            private const string ShaderPath = "Compute/MarchingSquares";
            private const string DisplayName = "Marching Squares";

            public static ComputeShader Get()
            {
                return GetShaderInternal(ShaderPath, DisplayName);
            }

            public static class Kernels
            {
                public static int MarchingSquares => GetKernelInternal(ShaderPath, "MarchingSquares", DisplayName);
            }
        }

        public static class JumpFloodSdf
        {
            private const string ShaderPath = "Compute/JumpFloodSDF";
            private const string DisplayName = "Jump Flood SDF";

            public static ComputeShader Get()
            {
                return GetShaderInternal(ShaderPath, DisplayName);
            }

            public static class Kernels
            {
                public static int JumpFlood => GetKernelInternal(ShaderPath, "JumpFlood", DisplayName);
                public static int UdfFromSegmentsBruteForce => GetKernelInternal(ShaderPath, "UDF_from_segments_bruteForce", DisplayName);
                public static int SeedFromScalarField => GetKernelInternal(ShaderPath, "SeedFromScalarField", DisplayName);
                public static int FinalizeSDF => GetKernelInternal(ShaderPath, "FinalizeSDF", DisplayName);
                public static int SeedFromSegmentsHQ => GetKernelInternal(ShaderPath, "SeedFromSegmentsHQ", DisplayName);
            }
        }

        public static class DomainWarp
        {
            private const string ShaderPath = "Compute/pingPong1/domainWarp";
            private const string DisplayName = "Domain Warp";

            public static ComputeShader Get()
            {
                return GetShaderInternal(ShaderPath, DisplayName);
            }

            public static class Kernels
            {
                public static int Warp => GetKernelInternal(ShaderPath, "Warp", DisplayName);
            }
        }

        public static class GaussianBlur
        {
            private const string ShaderPath = "Compute/pingPong1/gaussianBlur";
            private const string DisplayName = "Gaussian Blur";

            public static ComputeShader Get()
            {
                return GetShaderInternal(ShaderPath, DisplayName);
            }

            public static class Kernels
            {
                public static int GaussianBlur => GetKernelInternal(ShaderPath, "GaussianBlur", DisplayName);
            }
        }

        public static class PreciseDistanceField
        {
            private const string ShaderPath = "Compute/SegmentsToLocalUDF";
            private const string DisplayName = "Segments To Local UDF";

            public static ComputeShader Get()
            {
                return GetShaderInternal(ShaderPath, DisplayName);
            }

            public static class Kernels
            {
                public static int CSMarkTiles  => GetKernelInternal(ShaderPath, "CSMarkTiles", DisplayName);
                public static int CSDistanceField  => GetKernelInternal(ShaderPath, "CSDistanceField", DisplayName);
                
            }
        }
    }
}