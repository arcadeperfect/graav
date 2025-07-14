using System;
using NUnit.Framework;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;


using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Types
{
    public class FieldData : IDisposable
    {
        public RasterData BaseRasterData { get; }
        public VectorData VectorData { get; }
        public TextureData BaseTextureData { get; }
        public int Size { get; }
        public bool IsValid { get; }

        private bool _disposed = false;
        private bool _baseRasterDisposed = false;
        private bool _vectorDataDisposed = false;

        public FieldData(int size, RasterData rasterData, VectorData vectorData)
        {
            if (size <= 0) throw new ArgumentException("Size must be positive", nameof(size));
            if (!rasterData.IsValid) throw new ArgumentException("RasterData is invalid", nameof(rasterData));
            if (!vectorData.IsValid) throw new ArgumentException("VectorData is invalid", nameof(vectorData));

            Size = size;
            BaseRasterData = rasterData;
            VectorData = vectorData;

            try
            {
                BaseTextureData = new TextureData(size, rasterData);
                IsValid = true;
            }
            catch
            {
                // If texture creation fails, dispose the data we took ownership of
                DisposeRasterDataSafely(rasterData);
                DisposeVectorDataSafely(vectorData);
                throw;
            }
        }

        /// <summary>
        /// Create a mutable working copy for terrain deformation
        /// </summary>
        public DeformableFieldData CreateDeformableVersion()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FieldData));
            return new DeformableFieldData(this);
        }

        /// <summary>
        /// Safe disposal that handles already-disposed NativeArrays
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;

            // Dispose TextureData first (it doesn't depend on native arrays)
            try
            {
                BaseTextureData?.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing TextureData: {e.Message}");
            }

            // Dispose VectorData safely
            if (!_vectorDataDisposed)
            {
                DisposeVectorDataSafely(VectorData);
                _vectorDataDisposed = true;
            }

            // Dispose RasterData safely  
            if (!_baseRasterDisposed)
            {
                DisposeRasterDataSafely(BaseRasterData);
                _baseRasterDisposed = true;
            }

            _disposed = true;
        }

        /// <summary>
        /// Safe VectorData disposal that checks if NativeArrays are still valid
        /// </summary>
        private void DisposeVectorDataSafely(VectorData vectorData)
        {
            try
            {
                if (vectorData.IsValid)
                {
                    vectorData.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // NativeArrays already disposed - this is fine
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing VectorData: {e.Message}");
            }
        }

        /// <summary>
        /// Safe RasterData disposal that checks if NativeArrays are still valid
        /// </summary>
        private void DisposeRasterDataSafely(RasterData rasterData)
        {
            try
            {
                if (rasterData.IsValid)
                {
                    rasterData.Dispose();
                }
            }
            catch (ObjectDisposedException)
            {
                // NativeArrays already disposed - this is fine
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing RasterData: {e.Message}");
            }
        }
    }


    /// <summary>
    /// Mutable wrapper around FieldData for terrain deformation
    /// Owns only the modified scalar data, shares everything else immutably
    /// </summary>
    public sealed class DeformableFieldData : IDisposable
    {
        private readonly FieldData _baseData;

        // Only these are mutable and owned by this instance
        private NativeArray<float> _modifiedScalarField;
        private RenderTexture _modifiedScalarTexture;

        // Public accessors
        public NativeArray<float> ModifiedScalarField => _modifiedScalarField;
        public RenderTexture ModifiedScalarTexture => _modifiedScalarTexture;

        // Everything else is immutable and shared from base data
        public VectorData VectorData => _baseData.VectorData;
        public RenderTexture ColorTexture => _baseData.BaseTextureData.ColorTex;
        public int Size => _baseData.Size;
        public bool IsValid => _baseData.IsValid && _modifiedScalarField.IsCreated;

        private bool _disposed = false;
        private bool _isDirty = false;

        internal DeformableFieldData(FieldData baseData)
        {
            _baseData = baseData ?? throw new ArgumentNullException(nameof(baseData));

            // Create our own copy of the scalar data for modification
            var originalScalar = baseData.BaseRasterData.Scalar;
            _modifiedScalarField = new NativeArray<float>(originalScalar, Allocator.Persistent);

            // Create our own scalar texture
            _modifiedScalarTexture = new RenderTexture(
                baseData.Size, baseData.Size, 0,
                RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = true
            };
            _modifiedScalarTexture.Create();

            // Initially sync with base data
            SyncTextureFromArray();
        }

        /// <summary>
        /// Apply deformation to the scalar field
        /// Call this for brush operations, explosions, etc.
        /// </summary>
        public void DeformTerrain(int2 center, float radius, float strength, bool additive = true)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DeformableFieldData));

            int centerX = center.x;
            int centerY = center.y;
            int radiusInt = Mathf.CeilToInt(radius);

            for (int y = Mathf.Max(0, centerY - radiusInt); y < Mathf.Min(Size, centerY + radiusInt); y++)
            {
                for (int x = Mathf.Max(0, centerX - radiusInt); x < Mathf.Min(Size, centerX + radiusInt); x++)
                {
                    float distance = math.distance(new float2(x, y), new float2(centerX, centerY));
                    if (distance <= radius)
                    {
                        float falloff = 1.0f - (distance / radius);
                        float effect = strength * falloff;

                        int index = y * Size + x;

                        if (additive)
                            _modifiedScalarField[index] = math.clamp(_modifiedScalarField[index] + effect, 0f, 1f);
                        else
                            _modifiedScalarField[index] = math.clamp(_modifiedScalarField[index] - effect, 0f, 1f);
                    }
                }
            }

            _isDirty = true;
        }

        /// <summary>
        /// Reset to original base data
        /// </summary>
        public void ResetToBase()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DeformableFieldData));

            var originalScalar = _baseData.BaseRasterData.Scalar;
            _modifiedScalarField.CopyFrom(originalScalar);
            _isDirty = true;
        }

        /// <summary>
        /// Update the GPU texture if modifications have been made
        /// Call this before running compute pipeline
        /// </summary>
        public bool SyncTextureIfDirty()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DeformableFieldData));

            if (!_isDirty) return false;

            SyncTextureFromArray();
            _isDirty = false;
            return true;
        }

        private void SyncTextureFromArray()
        {
            // Efficient upload - could be optimized further with compute shader
            var tempTexture = new Texture2D(Size, Size, TextureFormat.RFloat, false);
            tempTexture.SetPixelData(_modifiedScalarField, 0);
            tempTexture.Apply();
            Graphics.Blit(tempTexture, _modifiedScalarTexture);
            UnityEngine.Object.DestroyImmediate(tempTexture);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_modifiedScalarTexture != null)
                {
                    _modifiedScalarTexture.Release();
                    UnityEngine.Object.DestroyImmediate(_modifiedScalarTexture);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing ModifiedScalarTexture: {e.Message}");
            }

            try
            {
                if (_modifiedScalarField.IsCreated)
                    _modifiedScalarField.Dispose();
            }
            catch (Exception e)
            {
                Debug.LogError($"Error disposing ModifiedScalarField: {e.Message}");
            }

            _disposed = true;
        }
    }
}