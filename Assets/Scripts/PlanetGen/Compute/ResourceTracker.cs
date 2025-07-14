using System;
using System.Collections.Generic;
using UnityEngine;

namespace PlanetGen.Compute
{
    public sealed class ResourceTracker
    {
        private readonly List<IDisposable> _managedResources = new();
        private readonly List<RenderTexture> _renderTextures = new();
        private readonly List<ComputeBuffer> _computeBuffers = new();
        private bool _disposed = false;


        public T Track<T>(T resource) where T : IDisposable
        {
            if (resource == null) return resource;

            ThrowIfDisposed();
            _managedResources.Add(resource);
            return resource;
        }

        public RenderTexture TrackTexture(RenderTexture texture)
        {
            if (texture == null) return texture;
            
            ThrowIfDisposed();
            _renderTextures.Add(texture);
            return texture;
        }

        public ComputeBuffer TrackBuffer(ComputeBuffer buffer)
        {
            if(buffer == null) return buffer;
            ThrowIfDisposed();
            _computeBuffers.Add(buffer);
            return buffer;
        }

        public RenderTexture CreateTexture(int width, int height, int depth, RenderTextureFormat format,
            bool enableRandomWrite = true, FilterMode filterMode = FilterMode.Bilinear)
        {
            ThrowIfDisposed();
            var texture = new RenderTexture(width, height, depth, format, RenderTextureReadWrite.Linear)
            {
                enableRandomWrite = enableRandomWrite, 
                filterMode = filterMode
            };
            if (!texture.Create())
            {
                texture.Release();
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"Failed to create RenderTexture of size {width}x{height}x{depth} with format {format}");
            }

            return TrackTexture(texture);
        }

        public ComputeBuffer CreateBuffer(int count, int stride, ComputeBufferType type = ComputeBufferType.Default)
        {
            ThrowIfDisposed();
            
            if(count <= 0) throw new ArgumentException("Buffer count must be positive", nameof(count));
            if(stride <= 0) throw new ArgumentException("Buffer stride must be positive", nameof(stride));
            
            var buffer = new ComputeBuffer(count, stride, type);
            return TrackBuffer(buffer);
        }

        public T Untrack<T>(T resource) where T : class
        {
            if (resource == null) return resource;
            _managedResources.Remove(resource as IDisposable);
            _renderTextures.Remove(resource as RenderTexture);
            _computeBuffers.Remove(resource as ComputeBuffer);
            
            return resource;
        }

        public (int disposables, int textures, int buffers) GetResourceCounts()
        {
            return (_managedResources.Count, _computeBuffers.Count, _renderTextures.Count);
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            foreach (var buffer in _computeBuffers)
            {
                try
                {
                    buffer?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error disposing ComputeBuffer: {e.Message}");
                }
            }
            
            foreach (var texture in _renderTextures)
            {
                try
                {
                    if (texture != null)
                    {
                        texture.Release();
                        UnityEngine.Object.DestroyImmediate(texture);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error disposing RenderTexture: {e.Message}");
                }
            }
            
            foreach (var resource in _managedResources)
            {
                try
                {
                    resource?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error disposing managed resource: {e.Message}");
                }
            }
            
            _computeBuffers.Clear();
            _renderTextures.Clear();
            _managedResources.Clear();
            _disposed = true;
        }
        
        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(ResourceTracker));
        }
    }
}