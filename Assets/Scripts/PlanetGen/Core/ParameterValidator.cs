using UnityEngine;

namespace PlanetGen.Core
{
    public class ParameterValidator
    {
        private readonly ValidationResult _result = new();
        public static ParameterValidator Create() => new();

        public ParameterValidator ValidatePositive(int value, string paramName)
        {
            if (value <= 0)
                _result.AddError($"{paramName} must be positive, got {value}");

            return this;
        }

        public ParameterValidator ValidatePositive(float value, string paramName)
        {
            if (value <= 0f)
                _result.AddError($"{paramName} must be positive, got {value}");
            return this;
        }

        public ParameterValidator ValidateRange(int value, int min, int max, string paramName)
        {
            if (value < min || value > max)
                _result.AddError($"{paramName} must be between {min} and {max}, got {value}");
            return this;
        }

        public ParameterValidator ValidateRange(float value, float min, float max, string paramName)
        {
            if (value < min || value > max)
                _result.AddError($"{paramName} must be between {min} and {max}, got {value}");
            return this;
        }

        public ParameterValidator ValidatePowerOfTwo(int value, string paramName)
        {
            if (value <= 0 || (value & (value - 1)) != 0)
                _result.AddError($"{paramName} must be a positive power of 2, got {value}");
            return this;
        }

        public ParameterValidator ValidateNotNull<T>(T value, string paramName) where T : class
        {
            if (value == null)
                _result.AddError($"{paramName} cannot be null");
            return this;
        }

        public ParameterValidator ValidateNativeArrayCreated<T>(Unity.Collections.NativeArray<T> array,
            string paramName)
            where T : struct
        {
            if (!array.IsCreated)
                _result.AddError($"{paramName} NativeArray is not created");
            return this;
        }

        public ParameterValidator ValidateTexture(RenderTexture texture, string paramName)
        {
            if (texture == null)
                _result.AddError($"{paramName} texture is null");
            else if (!texture.IsCreated())
                _result.AddError($"{paramName} texture is not created");
            return this;
        }

        public ParameterValidator ValidateShader(ComputeShader shader, string paramName)
        {
            if (shader == null)
                _result.AddError($"{paramName} compute shader is null");
            return this;
        }

        public ParameterValidator ValidateKernel(ComputeShader shader, int kernel, string kernelName)
        {
            if (shader != null && kernel < 0)
                _result.AddError($"Kernel '{kernelName}' not found in shader");
            return this;
        }

        public ParameterValidator ValidateCustom(bool condition, string errorMessage)
        {
            if (!condition)
                _result.AddError(errorMessage);
            return this;
        }

        public ParameterValidator WarnIf(bool condition, string warningMessage)
        {
            if (condition)
                _result.AddWarning(warningMessage);
            return this;
        }

        public ValidationResult Build() => _result;
    }

    /// <summary>
    /// Specific validators for different pipeline components
    /// </summary>
    public static class PipelineValidators
    {
        public static ValidationResult ValidateComputePipelineInit(
            int fieldRes,
            int textureRes,
            int gridRes,
            int maxSegmentsPerCell)
        {
            return ParameterValidator.Create()
                .ValidatePositive(fieldRes, nameof(fieldRes))
                .ValidatePositive(textureRes, nameof(textureRes))
                .ValidatePowerOfTwo(textureRes, nameof(textureRes))
                .ValidatePositive(gridRes, nameof(gridRes))
                .ValidatePositive(maxSegmentsPerCell, nameof(maxSegmentsPerCell))
                // .ValidateCustom(gridRes <= textureRes,
                //     "Grid resolution cannot exceed texture resolution")
                // .ValidateCustom(fieldRes <= textureRes * 4,
                //     "Field width should not exceed 4x texture resolution")
                // .WarnIf(textureRes > 2048,
                //     "Large texture resolutions may impact performance")
                // .WarnIf(maxSegmentsPerCell > 128,
                //     "High segment count per cell may impact performance")
                .Build();
        }

        public static ValidationResult ValidateFieldData(
            int size,
            Unity.Collections.NativeArray<float> scalarData,
            Unity.Collections.NativeArray<Unity.Mathematics.float4> colorData)
        {
            return ParameterValidator.Create()
                .ValidatePositive(size, nameof(size))
                .ValidateNativeArrayCreated(scalarData, nameof(scalarData))
                .ValidateNativeArrayCreated(colorData, nameof(colorData))
                .ValidateCustom(scalarData.Length == size * size,
                    $"Scalar data length ({scalarData.Length}) doesn't match size squared ({size * size})")
                .ValidateCustom(colorData.Length == size * size,
                    $"Color data length ({colorData.Length}) doesn't match size squared ({size * size})")
                .Build();
        }

        public static ValidationResult ValidateDeformationParameters(
            Unity.Mathematics.int2 center,
            float radius,
            float strength,
            int fieldSize)
        {
            return ParameterValidator.Create()
                .ValidateRange(center.x, 0, fieldSize - 1, "center.x")
                .ValidateRange(center.y, 0, fieldSize - 1, "center.y")
                .ValidatePositive(radius, nameof(radius))
                .ValidateRange(strength, -1f, 1f, nameof(strength))
                .WarnIf(radius > fieldSize * 0.5f,
                    "Large deformation radius may affect entire field")
                .Build();
        }
    }
}