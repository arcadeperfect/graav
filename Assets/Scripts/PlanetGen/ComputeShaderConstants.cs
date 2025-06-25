// using UnityEngine;
//
// namespace PlanetGen
// {
//     public static class ComputeShaderConstants
//     {
//
//         
//         public static class MarchingSquaresCompute
//         {
//             public const string Path = "Compute/MarchingSquares";
//             public struct Kernels
//             {
//                 public const string MarchingSquares = "MarchingSquares";
//             }
//         }
//         
//         // public static class JumpFloodCompute
//         // {
//         //     public const string Path = "Compute/JumpFloodSDF";
//         //     public static class Kernels
//         //     {
//         //         public const string JumpFlood = "JumpFlood";
//         //         public const string SeedFromSegments = "SeedFromSegments";
//         //         public const string FinalizeSDF = "FinalizeSDF";
//         //         public const string SeedFromScalarField = "SeedFromScalarField";
//         //     }
//         // }
//         public static class JumpFloodCompute
//         {
//             // Assumes the shader file is named "JumpFlood.compute" in a Resources folder
//             // public const string Path = "Compute/JumpFlood"; 
//             
//             
//             public static ComputeShader Get()
//             {
//                 var shader = Resources.Load<ComputeShader>("Compute/JumpFloodSDF");
//                 if (shader == null)
//                 {
//                     Debug.LogError("JumpFlood compute shader not found in Resources/Compute folder.");
//                 }
//
//                 return shader;
//             }
//             public static class Kernels
//             {
//                 public const string JumpFlood = "JumpFlood";
//                 public const string SeedFromSegments = "SeedFromSegments";
//                 public const string SeedFromScalarField = "SeedFromScalarField";
//                 public const string FinalizeSDF = "FinalizeSDF";
//                 
//             }
//         }
//         
//         public static class PingPongDomainWarpCompute
//         {
//             public const string Path = "Compute/pingPong1/domainWarp";
//             public static class Kernels
//             {
//                 public const string Warp = "Warp";
//             }
//         }
//         
//         public static class GaussianBlurCompute
//         {
//             public const string Path = "Compute/PingPng1/GaussianBlur";
//             public static class Kernels
//             {
//                 public const string GaussianBlur = "GaussianBlur";
//             }
//
//             public static ComputeShader Get()
//             {
//                 return Resources.Load<ComputeShader>("Compute/pingPong1/gaussianBlur");
//             }
//             public static int GetKernel()
//             {
//                 return Get().FindKernel(GaussianBlurCompute.Kernels.GaussianBlur);
//             }
//         }
//     }
// }

using UnityEngine;
using System.Collections.Generic;

namespace PlanetGen
{
    /// <summary>
    /// Centralized compute shader provider with error handling and caching
    /// </summary>
    public static class ComputeShaderProvider
    {
        private static readonly Dictionary<string, ComputeShader> _shaderCache = new Dictionary<string, ComputeShader>();
        
        /// <summary>
        /// Loads a compute shader from Resources with error handling and caching
        /// </summary>
        /// <param name="path">Path to the shader in Resources folder (without .compute extension)</param>
        /// <param name="shaderName">Human-readable name for error messages</param>
        /// <returns>ComputeShader or null if not found</returns>
        public static ComputeShader LoadShader(string path, string shaderName = null)
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError($"[ComputeShaderProvider] Path cannot be null or empty for shader: {shaderName ?? "Unknown"}");
                return null;
            }

            // Check cache first
            if (_shaderCache.TryGetValue(path, out ComputeShader cachedShader))
            {
                return cachedShader;
            }

            // Load from Resources
            ComputeShader shader = Resources.Load<ComputeShader>(path);
            
            if (shader == null)
            {
                string displayName = shaderName ?? path;
                Debug.LogError($"[ComputeShaderProvider] Failed to load compute shader '{displayName}' at path 'Resources/{path}.compute'. " +
                              $"Please ensure the file exists and is properly named.");
                return null;
            }

            // Cache successful loads
            _shaderCache[path] = shader;
            return shader;
        }

        /// <summary>
        /// Gets a kernel index from a compute shader with error handling
        /// </summary>
        /// <param name="shader">The compute shader</param>
        /// <param name="kernelName">Name of the kernel</param>
        /// <param name="shaderName">Human-readable shader name for error messages</param>
        /// <returns>Kernel index or -1 if not found</returns>
        public static int GetKernel(ComputeShader shader, string kernelName, string shaderName = null)
        {
            if (shader == null)
            {
                Debug.LogError($"[ComputeShaderProvider] Cannot get kernel '{kernelName}' from null shader: {shaderName ?? "Unknown"}");
                return -1;
            }

            if (string.IsNullOrEmpty(kernelName))
            {
                Debug.LogError($"[ComputeShaderProvider] Kernel name cannot be null or empty for shader: {shaderName ?? shader.name}");
                return -1;
            }

            if (!shader.HasKernel(kernelName))
            {
                string displayName = shaderName ?? shader.name;
                Debug.LogError($"[ComputeShaderProvider] Kernel '{kernelName}' not found in compute shader '{displayName}'. " +
                              $"Available kernels might include different names - check your .compute file.");
                return -1;
            }

            return shader.FindKernel(kernelName);
        }

        /// <summary>
        /// Clears the shader cache (useful for development/testing)
        /// </summary>
        public static void ClearCache()
        {
            _shaderCache.Clear();
        }
    }

    public static class ComputeShaderConstants
    {
        public static class MarchingSquaresCompute
        {
            public const string Path = "Compute/MarchingSquares";
            public const string DisplayName = "Marching Squares";
            
            public static List<string> KernelsList = new List<string>
            {
                "MarchingSquares"
            };
            
            public static class Kernels
            {
                public const string MarchingSquares = "MarchingSquares";
            }

            public static ComputeShader GetShader()
            {
                return ComputeShaderProvider.LoadShader(Path, DisplayName);
            }

            public static int GetKernel(string kernelName)
            {
                var shader = GetShader();
                return ComputeShaderProvider.GetKernel(shader, kernelName, DisplayName);
            }

            // Convenience methods for common kernels
            public static int GetMarchingSquaresKernel()
            {
                return GetKernel(Kernels.MarchingSquares);
            }
        }

        public static class JumpFloodCompute
        {
            public const string Path = "Compute/JumpFloodSDF";
            public const string DisplayName = "Jump Flood SDF";
            
            public static class Kernels
            {
                public const string JumpFlood = "JumpFlood";
                public const string BruteForceUDF = "BruteForceUDF";
                public const string SeedFromScalarField = "SeedFromScalarField";
                public const string FinalizeSDF = "FinalizeSDF";
            }

            public static ComputeShader GetShader()
            {
                return ComputeShaderProvider.LoadShader(Path, DisplayName);
            }

            public static int GetKernel(string kernelName)
            {
                var shader = GetShader();
                return ComputeShaderProvider.GetKernel(shader, kernelName, DisplayName);
            }

            // Convenience methods for common kernels
            public static int GetJumpFloodKernel()
            {
                return GetKernel(Kernels.JumpFlood);
            }

            public static int GetSeedFromSegmentsKernel()
            {
                return GetKernel(Kernels.BruteForceUDF);
            }

            public static int GetSeedFromScalarFieldKernel()
            {
                return GetKernel(Kernels.SeedFromScalarField);
            }

            public static int GetFinalizeSdfKernel()
            {
                return GetKernel(Kernels.FinalizeSDF);
            }
        }

        public static class PingPongDomainWarpCompute
        {
            public const string Path = "Compute/pingPong1/domainWarp";
            public const string DisplayName = "Ping Pong Domain Warp";
            
            public static class Kernels
            {
                public const string Warp = "Warp";
            }

            public static ComputeShader GetShader()
            {
                return ComputeShaderProvider.LoadShader(Path, DisplayName);
            }

            public static int GetKernel(string kernelName)
            {
                var shader = GetShader();
                return ComputeShaderProvider.GetKernel(shader, kernelName, DisplayName);
            }

            // Convenience method for common kernel
            public static int GetWarpKernel()
            {
                return GetKernel(Kernels.Warp);
            }
        }

        public static class GaussianBlurCompute
        {
            // Fixed path inconsistency (was "PingPng1" vs "pingPong1")
            public const string Path = "Compute/pingPong1/gaussianBlur";
            public const string DisplayName = "Gaussian Blur";
            
            public static class Kernels
            {
                public const string GaussianBlur = "GaussianBlur";
            }

            public static ComputeShader GetShader()
            {
                return ComputeShaderProvider.LoadShader(Path, DisplayName);
            }

            public static int GetKernel(string kernelName = null)
            {
                var shader = GetShader();
                string kernel = kernelName ?? Kernels.GaussianBlur;
                return ComputeShaderProvider.GetKernel(shader, kernel, DisplayName);
            }

            // Convenience method for the most common use case
            public static int GetDefaultKernel()
            {
                return GetKernel(Kernels.GaussianBlur);
            }
        }
    }

    /// <summary>
    /// Extension methods for easier compute shader usage
    /// </summary>
    public static class ComputeShaderExtensions
    {
        /// <summary>
        /// Safely dispatch a compute shader with error checking
        /// </summary>
        public static bool SafeDispatch(this ComputeShader shader, int kernelIndex, int groupsX, int groupsY, int groupsZ, string shaderName = null)
        {
            if (shader == null)
            {
                Debug.LogError($"[ComputeShaderExtensions] Cannot dispatch null shader: {shaderName ?? "Unknown"}");
                return false;
            }

            if (kernelIndex < 0)
            {
                Debug.LogError($"[ComputeShaderExtensions] Invalid kernel index {kernelIndex} for shader: {shaderName ?? shader.name}");
                return false;
            }

            try
            {
                shader.Dispatch(kernelIndex, groupsX, groupsY, groupsZ);
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ComputeShaderExtensions] Failed to dispatch shader '{shaderName ?? shader.name}': {e.Message}");
                return false;
            }
        }
    }
}