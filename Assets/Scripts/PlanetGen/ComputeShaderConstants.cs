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
        
        public static class JumpFloodCompute
        {
            public const string Path = "Compute/JumpFloodSDF";
            public static class Kernels
            {
                public const string JumpFlood = "JumpFlood";
                public const string SeedFromSegments = "SeedGeneration";
                public const string FinalizeSDF = "FinalizeSDF";
                public const string SeedFromScalarField = "SeedFromScalarField";
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
    }
}