using System;
using NUnit.Framework;
using PlanetGen.FieldGen2.Graph;
using PlanetGen.FieldGen2.Graph.Types;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace PlanetGen.FieldGen2.Types
{
    public sealed class FieldData : IDisposable
    {
        public RasterData BaseRasterData { get; }
        public VectorData VectorData { get; }
        public TextureData BaseTextureData { get; }
        public int Size { get; }
        public bool IsValid { get; }

        private bool _disposed = false;

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
                // If texture creation fails, we need to clean up the data we took ownership of
                BaseRasterData.Dispose();
                VectorData.Dispose();
                throw;
            }
        }

        /// <summary>
        /// Create a mutable working copy for terrain deformation
        /// Only copies the scalar data - everything else is shared immutably
        /// </summary>
        public DeformableFieldData CreateDeformableVersion()
        {
            ThrowIfDisposed();
            return new DeformableFieldData(this);
        }

        public void Dispose()
        {
            if (_disposed) return;

            BaseTextureData?.Dispose();
            if(VectorData.IsValid) 
                VectorData.Dispose();
            if (BaseRasterData.IsValid)
                BaseRasterData.Dispose();
            // VectorData?.Dispose();
            // BaseRasterData?.Dispose();

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(FieldData));
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
        // Use fields instead of properties to allow direct modification
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
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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
            ThrowIfDisposed();

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

            if (_modifiedScalarTexture != null)
            {
                _modifiedScalarTexture.Release();
                UnityEngine.Object.DestroyImmediate(_modifiedScalarTexture);
            }

            if (_modifiedScalarField.IsCreated)
                _modifiedScalarField.Dispose();

            _disposed = true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DeformableFieldData));
        }
    }
}