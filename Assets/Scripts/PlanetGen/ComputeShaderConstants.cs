using UnityEngine;

namespace PlanetGen
{
    public static class ComputeShaderConstants
    {

        
        public static class MarchingSquaresCompute
        {
            public const string Path = "Compute/MarchingSquares";
            public struct Kernels
            {
                public const string MarchingSquares = "MarchingSquares";
            }
        }
        
        // public static class JumpFloodCompute
        // {
        //     public const string Path = "Compute/JumpFloodSDF";
        //     public static class Kernels
        //     {
        //         public const string JumpFlood = "JumpFlood";
        //         public const string SeedFromSegments = "SeedFromSegments";
        //         public const string FinalizeSDF = "FinalizeSDF";
        //         public const string SeedFromScalarField = "SeedFromScalarField";
        //     }
        // }
        public static class JumpFloodCompute
        {
            // Assumes the shader file is named "JumpFlood.compute" in a Resources folder
            // public const string Path = "Compute/JumpFlood"; 
            
            
            public static ComputeShader Get()
            {
                var shader = Resources.Load<ComputeShader>("Compute/JumpFloodSDF");
                if (shader == null)
                {
                    Debug.LogError("JumpFlood compute shader not found in Resources/Compute folder.");
                }

                return shader;
            }
            public static class Kernels
            {
                public const string JumpFlood = "JumpFlood";
                public const string SeedFromSegments = "SeedFromSegments";
                public const string SeedFromScalarField = "SeedFromScalarField";
                public const string FinalizeSDF = "FinalizeSDF";
                public const string BuildSegmentGrid = "BuildSegmentGrid"; // The new grid kernel
                public const string RefineSDF = "RefineSDF";             // For the next step
                public const string SeedFromSegments_SP = "SeedFromSegments_SP";
                public const string FinalizeSDF_FromDense = "FinalizeSDF_FromDense";
            }
        }
        
        public static class PingPongDomainWarpCompute
        {
            public const string Path = "Compute/pingPong1/domainWarp";
            public static class Kernels
            {
                public const string Warp = "Warp";
            }
        }
        
        public static class GaussianBlurCompute
        {
            public const string Path = "Compute/PingPng1/GaussianBlur";
            public static class Kernels
            {
                public const string GaussianBlur = "GaussianBlur";
            }

            public static ComputeShader Get()
            {
                return Resources.Load<ComputeShader>("Compute/pingPong1/gaussianBlur");
            }
            public static int GetKernel()
            {
                return Get().FindKernel(GaussianBlurCompute.Kernels.GaussianBlur);
            }
        }
    }
}