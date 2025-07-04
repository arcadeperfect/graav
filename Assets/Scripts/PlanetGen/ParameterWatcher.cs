﻿// using System;
// using System.Reflection;
// using UnityEngine;
//
// namespace PlanetGen
// {
//     /// <summary>
//     /// Marks a field as triggering field regeneration when changed
//     /// </summary>
//     [AttributeUsage(AttributeTargets.Field)]
//     public class TriggerFieldRegenAttribute : Attribute { }
//     
//     /// <summary>
//     /// Marks a field as triggering compute regeneration when changed
//     /// </summary>
//     [AttributeUsage(AttributeTargets.Field)]
//     public class TriggerComputeRegenAttribute : Attribute { }
//
//     /// <summary>
//     /// Flags indicating what type of regeneration is needed
//     /// </summary>
//     [Flags]
//     public enum ParameterChangeFlags
//     {
//         None = 0,
//         FieldRegen = 1,
//         ComputeRegen = 2,
//         All = FieldRegen | ComputeRegen
//     }
//
//     /// <summary>
//     /// Watches for changes in marked parameters using reflection and attributes.
//     /// This is a generic system that can be used with any class.
//     /// </summary>
//     public class ParameterWatcher
//     {
//         private readonly object target;
//         private readonly FieldInfo[] fieldRegenFields;
//         private readonly FieldInfo[] computeRegenFields;
//         private readonly object[] lastFieldValues;
//         private readonly object[] lastComputeValues;
//         private readonly bool useApproximateFloatComparison;
//
//         /// <summary>
//         /// Creates a new parameter watcher for the given target object
//         /// </summary>
//         /// <param name="target">The object to watch for parameter changes</param>
//         /// <param name="useApproximateFloatComparison">Whether to use Mathf.Approximately for float comparisons</param>
//         public ParameterWatcher(object target, bool useApproximateFloatComparison = true)
//         {
//             this.target = target ?? throw new ArgumentNullException(nameof(target));
//             this.useApproximateFloatComparison = useApproximateFloatComparison;
//             
//             // Get all fields with our custom attributes
//             var allFields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
//             
//             fieldRegenFields = Array.FindAll(allFields, 
//                 field => field.GetCustomAttribute<TriggerFieldRegenAttribute>() != null);
//             computeRegenFields = Array.FindAll(allFields, 
//                 field => field.GetCustomAttribute<TriggerComputeRegenAttribute>() != null);
//
//             // Initialize cached values
//             lastFieldValues = new object[fieldRegenFields.Length];
//             lastComputeValues = new object[computeRegenFields.Length];
//             
//             CacheCurrentValues();
//
//             // Debug info
//             Debug.Log($"ParameterWatcher initialized for {target.GetType().Name}:");
//             Debug.Log($"  - {fieldRegenFields.Length} field regen parameters");
//             Debug.Log($"  - {computeRegenFields.Length} compute regen parameters");
//         }
//
//         /// <summary>
//         /// Check for parameter changes since last check
//         /// </summary>
//         /// <returns>Flags indicating what types of regeneration are needed</returns>
//         public ParameterChangeFlags CheckForChanges()
//         {
//             var flags = ParameterChangeFlags.None;
//
//             // Check field regen parameters
//             for (int i = 0; i < fieldRegenFields.Length; i++)
//             {
//                 var currentValue = fieldRegenFields[i].GetValue(target);
//                 if (!ValuesEqual(lastFieldValues[i], currentValue))
//                 {
//                     flags |= ParameterChangeFlags.FieldRegen;
//                     lastFieldValues[i] = currentValue;
//                     
//                     // Early exit if we already know both types changed
//                     if (flags == ParameterChangeFlags.All) break;
//                 }
//             }
//
//             // Check compute regen parameters
//             for (int i = 0; i < computeRegenFields.Length; i++)
//             {
//                 var currentValue = computeRegenFields[i].GetValue(target);
//                 if (!ValuesEqual(lastComputeValues[i], currentValue))
//                 {
//                     flags |= ParameterChangeFlags.ComputeRegen;
//                     lastComputeValues[i] = currentValue;
//                     
//                     // Early exit if we already know both types changed
//                     if (flags == ParameterChangeFlags.All) break;
//                 }
//             }
//
//             return flags;
//         }
//
//         /// <summary>
//         /// Force update the cached values without checking for changes
//         /// Useful after manual parameter modifications
//         /// </summary>
//         public void UpdateCache()
//         {
//             CacheCurrentValues();
//         }
//
//         /// <summary>
//         /// Get diagnostic information about watched parameters
//         /// </summary>
//         public string GetDiagnosticInfo()
//         {
//             var info = $"ParameterWatcher for {target.GetType().Name}:\n";
//             
//             info += "Field Regen Parameters:\n";
//             for (int i = 0; i < fieldRegenFields.Length; i++)
//             {
//                 var field = fieldRegenFields[i];
//                 var value = field.GetValue(target);
//                 info += $"  - {field.Name}: {value} ({field.FieldType.Name})\n";
//             }
//             
//             info += "Compute Regen Parameters:\n";
//             for (int i = 0; i < computeRegenFields.Length; i++)
//             {
//                 var field = computeRegenFields[i];
//                 var value = field.GetValue(target);
//                 info += $"  - {field.Name}: {value} ({field.FieldType.Name})\n";
//             }
//             
//             return info;
//         }
//
//         private void CacheCurrentValues()
//         {
//             for (int i = 0; i < fieldRegenFields.Length; i++)
//             {
//                 lastFieldValues[i] = fieldRegenFields[i].GetValue(target);
//             }
//             
//             for (int i = 0; i < computeRegenFields.Length; i++)
//             {
//                 lastComputeValues[i] = computeRegenFields[i].GetValue(target);
//             }
//         }
//
//         private bool ValuesEqual(object a, object b)
//         {
//             if (a == null && b == null) return true;
//             if (a == null || b == null) return false;
//             
//             // Special handling for floats to use approximate comparison
//             if (useApproximateFloatComparison && a is float fa && b is float fb)
//             {
//                 return Mathf.Approximately(fa, fb);
//             }
//             
//             return a.Equals(b);
//         }
//     }
//
//     /// <summary>
//     /// Extension methods for ParameterChangeFlags to make usage more readable
//     /// </summary>
//     public static class ParameterChangeFlagsExtensions
//     {
//         public static bool HasFieldRegen(this ParameterChangeFlags flags)
//         {
//             return flags.HasFlag(ParameterChangeFlags.FieldRegen);
//         }
//         
//         public static bool HasComputeRegen(this ParameterChangeFlags flags)
//         {
//             return flags.HasFlag(ParameterChangeFlags.ComputeRegen);
//         }
//         
//         public static bool HasAnyChanges(this ParameterChangeFlags flags)
//         {
//             return flags != ParameterChangeFlags.None;
//         }
//     }
// }
using System;
using System.Reflection;
using UnityEngine;

namespace PlanetGen
{
    /// <summary>
    /// Marks a field as triggering field regeneration when changed
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TriggerFieldRegenAttribute : Attribute { }
    
    /// <summary>
    /// Marks a field as triggering compute regeneration when changed
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TriggerComputeRegenAttribute : Attribute { }

    /// <summary>
    /// Marks a field as triggering buffer reinitialization when changed
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class TriggerBufferReInitAttribute : Attribute { }

    /// <summary>
    /// Flags indicating what type of regeneration is needed
    /// </summary>
    [Flags]
    public enum ParameterChangeFlags
    {
        None = 0,
        FieldRegen = 1,
        ComputeRegen = 2,
        BufferReInit = 4,
        All = FieldRegen | ComputeRegen | BufferReInit
    }

    /// <summary>
    /// Watches for changes in marked parameters using reflection and attributes.
    /// This is a generic system that can be used with any class.
    /// </summary>
    public class ParameterWatcher
    {
        private readonly object target;
        private readonly FieldInfo[] fieldRegenFields;
        private readonly FieldInfo[] computeRegenFields;
        private readonly FieldInfo[] bufferReInitFields;
        private readonly object[] lastFieldValues;
        private readonly object[] lastComputeValues;
        private readonly object[] lastBufferValues;
        private readonly bool useApproximateFloatComparison;

        /// <summary>
        /// Creates a new parameter watcher for the given target object
        /// </summary>
        /// <param name="target">The object to watch for parameter changes</param>
        /// <param name="useApproximateFloatComparison">Whether to use Mathf.Approximately for float comparisons</param>
        public ParameterWatcher(object target, bool useApproximateFloatComparison = true)
        {
            this.target = target ?? throw new ArgumentNullException(nameof(target));
            this.useApproximateFloatComparison = useApproximateFloatComparison;
            
            // Get all fields with our custom attributes
            var allFields = target.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
            
            fieldRegenFields = Array.FindAll(allFields, 
                field => field.GetCustomAttribute<TriggerFieldRegenAttribute>() != null);
            computeRegenFields = Array.FindAll(allFields, 
                field => field.GetCustomAttribute<TriggerComputeRegenAttribute>() != null);
            bufferReInitFields = Array.FindAll(allFields, 
                field => field.GetCustomAttribute<TriggerBufferReInitAttribute>() != null);

            // Initialize cached values
            lastFieldValues = new object[fieldRegenFields.Length];
            lastComputeValues = new object[computeRegenFields.Length];
            lastBufferValues = new object[bufferReInitFields.Length];
            
            CacheCurrentValues();

            // Debug info
            Debug.Log($"ParameterWatcher initialized for {target.GetType().Name}:");
            Debug.Log($"  - {fieldRegenFields.Length} field regen parameters");
            Debug.Log($"  - {computeRegenFields.Length} compute regen parameters");
            Debug.Log($"  - {bufferReInitFields.Length} buffer reinit parameters");
        }

        /// <summary>
        /// Check for parameter changes since last check
        /// </summary>
        /// <returns>Flags indicating what types of regeneration are needed</returns>
        public ParameterChangeFlags CheckForChanges()
        {
            var flags = ParameterChangeFlags.None;

            // Check field regen parameters
            for (int i = 0; i < fieldRegenFields.Length; i++)
            {
                var currentValue = fieldRegenFields[i].GetValue(target);
                if (!ValuesEqual(lastFieldValues[i], currentValue))
                {
                    flags |= ParameterChangeFlags.FieldRegen;
                    lastFieldValues[i] = currentValue;
                    
                    // Early exit if we already know all types changed
                    if (flags == ParameterChangeFlags.All) break;
                }
            }

            // Check compute regen parameters
            for (int i = 0; i < computeRegenFields.Length; i++)
            {
                var currentValue = computeRegenFields[i].GetValue(target);
                if (!ValuesEqual(lastComputeValues[i], currentValue))
                {
                    flags |= ParameterChangeFlags.ComputeRegen;
                    lastComputeValues[i] = currentValue;
                    
                    // Early exit if we already know all types changed
                    if (flags == ParameterChangeFlags.All) break;
                }
            }

            // Check buffer reinit parameters
            for (int i = 0; i < bufferReInitFields.Length; i++)
            {
                var currentValue = bufferReInitFields[i].GetValue(target);
                if (!ValuesEqual(lastBufferValues[i], currentValue))
                {
                    flags |= ParameterChangeFlags.BufferReInit;
                    lastBufferValues[i] = currentValue;
                    
                    // Early exit if we already know all types changed
                    if (flags == ParameterChangeFlags.All) break;
                }
            }

            return flags;
        }

        /// <summary>
        /// Force update the cached values without checking for changes
        /// Useful after manual parameter modifications
        /// </summary>
        public void UpdateCache()
        {
            CacheCurrentValues();
        }

        /// <summary>
        /// Get diagnostic information about watched parameters
        /// </summary>
        public string GetDiagnosticInfo()
        {
            var info = $"ParameterWatcher for {target.GetType().Name}:\n";
            
            info += "Field Regen Parameters:\n";
            for (int i = 0; i < fieldRegenFields.Length; i++)
            {
                var field = fieldRegenFields[i];
                var value = field.GetValue(target);
                info += $"  - {field.Name}: {value} ({field.FieldType.Name})\n";
            }
            
            info += "Compute Regen Parameters:\n";
            for (int i = 0; i < computeRegenFields.Length; i++)
            {
                var field = computeRegenFields[i];
                var value = field.GetValue(target);
                info += $"  - {field.Name}: {value} ({field.FieldType.Name})\n";
            }
            
            info += "Buffer ReInit Parameters:\n";
            for (int i = 0; i < bufferReInitFields.Length; i++)
            {
                var field = bufferReInitFields[i];
                var value = field.GetValue(target);
                info += $"  - {field.Name}: {value} ({field.FieldType.Name})\n";
            }
            
            return info;
        }

        private void CacheCurrentValues()
        {
            for (int i = 0; i < fieldRegenFields.Length; i++)
            {
                lastFieldValues[i] = fieldRegenFields[i].GetValue(target);
            }
            
            for (int i = 0; i < computeRegenFields.Length; i++)
            {
                lastComputeValues[i] = computeRegenFields[i].GetValue(target);
            }
            
            for (int i = 0; i < bufferReInitFields.Length; i++)
            {
                lastBufferValues[i] = bufferReInitFields[i].GetValue(target);
            }
        }

        private bool ValuesEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            
            // Special handling for floats to use approximate comparison
            if (useApproximateFloatComparison && a is float fa && b is float fb)
            {
                return Mathf.Approximately(fa, fb);
            }
            
            return a.Equals(b);
        }
    }

    /// <summary>
    /// Extension methods for ParameterChangeFlags to make usage more readable
    /// </summary>
    public static class ParameterChangeFlagsExtensions
    {
        public static bool HasFieldRegen(this ParameterChangeFlags flags)
        {
            return flags.HasFlag(ParameterChangeFlags.FieldRegen);
        }
        
        public static bool HasComputeRegen(this ParameterChangeFlags flags)
        {
            return flags.HasFlag(ParameterChangeFlags.ComputeRegen);
        }
        
        public static bool HasBufferReInit(this ParameterChangeFlags flags)
        {
            return flags.HasFlag(ParameterChangeFlags.BufferReInit);
        }
        
        public static bool HasAnyChanges(this ParameterChangeFlags flags)
        {
            return flags != ParameterChangeFlags.None;
        }
    }
}