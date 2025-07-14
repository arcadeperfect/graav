using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.PlayerLoop;

namespace PlanetGen.Core
{
    /// <summary>
    /// Represents the result of an operation that can succeed or fail
    /// Provides rich error information and prevents silent failures
    /// </summary>
    public readonly struct Result<T>
    {
        public bool IsSuccess { get; }
        public T Value { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        private Result(bool isSuccess, T value, string errorMessage, Exception exception)
        {
            IsSuccess = isSuccess;
            Value = value;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static Result<T> Success(T value) => new(true, value, null, null);
        public static Result<T> Failure(string errorMessage) => new(false, default, errorMessage, null);

        public static Result<T> Failure(string errorMessage, Exception exception) =>
            new(false, default(T), errorMessage, exception);

        public static Result<T> Failure(Exception exception) => new(false, default(T), exception.Message, exception);

        /// <summary>
        /// Transform the value if successful, otherwise propagate the error
        /// </summary>
        public Result<TOut> Map<TOut>(Func<T, TOut> transform)
        {
            return IsSuccess ? Result<TOut>.Success(transform(Value)) : Result<TOut>.Failure(ErrorMessage, Exception);
        }

        /// <summary>
        /// Chain another operation that returns a Result
        /// </summary>
        public Result<TOut> Bind<TOut>(Func<T, Result<TOut>> operation)
        {
            return IsSuccess
                ? operation(Value)
                : Result<TOut>.Failure(ErrorMessage, Exception);
        }

        /// <summary>
        /// Execute an action if successful, return original result
        /// </summary>
        public Result<T> OnSuccess(Action<T> action)
        {
            if (IsSuccess) action(Value);
            return this;
        }

        /// <summary>
        /// Execute an action if successful (ignoring the value), return original result
        /// </summary>
        public Result<T> OnSuccess(Action action)
        {
            if (IsSuccess) action();
            return this;
        }

        /// <summary>
        /// Execute an action if failed, return original result
        /// </summary>
        public Result<T> OnFailure(Action<string> action)
        {
            if (!IsSuccess) action(ErrorMessage);
            return this;
        }

        /// <summary>
        /// Execute an action if failed (ignoring the error message), return original result
        /// </summary>
        public Result<T> OnFailure(Action action)
        {
            if (!IsSuccess) action();
            return this;
        }
    }

    /// <summary>
    /// Result type for operations that don't return a value
    /// </summary>
    public readonly struct Result
    {
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }
        public Exception Exception { get; }

        private Result(bool isSuccess, string errorMessage, Exception exception)
        {
            IsSuccess = isSuccess;
            ErrorMessage = errorMessage;
            Exception = exception;
        }

        public static Result Success() => new(true, null, null);
        public static Result Failure(string errorMessage) => new(false, errorMessage, null);
        public static Result Failure(string errorMessage, Exception exception) => new(false, errorMessage, exception);
        public static Result Failure(Exception exception) => new(false, exception.Message, exception);

        public Result OnSuccess(Action action)
        {
            if (IsSuccess) action();
            return this;
        }

        public Result OnFailure(Action<string> action)
        {
            if (!IsSuccess) action(ErrorMessage);
            return this;
        }
    }

    /// <summary>
    /// Validation result with detailed error information
    /// </summary>
    public class ValidationResult
    {
        public List<string> Errors { get; }
        public List<string> Warnings { get; }
        public bool IsValid => !Errors.Any();

        public bool HasWarnings => Warnings.Any();
        public bool HasErrors => Errors.Any();

        public ValidationResult()
        {
            Errors = new List<string>();
            Warnings = new List<string>();
        }

        public ValidationResult(IEnumerable<string> errors, IEnumerable<string> warnings = null)
        {
            Errors = errors?.ToList() ?? new List<string>();
            Warnings = warnings?.ToList() ?? new List<string>();
        }

        public ValidationResult AddError(string error)
        {
            Errors.Add(error);
            return this;
        }

        public ValidationResult AddWarning(string warning)
        {
            Warnings.Add(warning);
            return this;
        }

        public ValidationResult Merge(ValidationResult other)
        {
            Errors.AddRange(other.Errors);
            Warnings.AddRange(other.Warnings);
            return this;
        }

        public string GetSummary()
        {
            var parts = new List<string>();
            if (Errors.Any())
                parts.Add($"Errors: {string.Join("; ", Errors)}");

            if (Warnings.Any())
                parts.Add($"Warnings: {string.Join("; ", Warnings)}");

            return parts.Any() ? string.Join("; ", parts) : "Valid";
        }
    }

    /// <summary>
    /// Centralized error logging and handling
    /// </summary>
    public static class ErrorHandler
    {
        public static void LogError(string context, string message, Exception exception = null)
        {
            var fullMessage = $"[{context}] {message}";
            if (exception != null)
            {
                Debug.LogError($"{fullMessage}\nException: {exception.Message}\nStack: {exception.StackTrace}");
            }
            else
            {
                Debug.LogError($"{fullMessage}");
            }
        }

        public static void LogWarning(string context, string message)
        {
            Debug.LogWarning($"[{context}] {message}");
        }

        public static void LogValidationResult(string context, ValidationResult validation)
        {
            if (!validation.IsValid)
            {
                LogError(context, $"Validation Failed: {validation.GetSummary()}");
            }

            foreach (var warning in validation.Warnings)
            {
                LogWarning(context, warning);
            }
        }

        /// <summary>
        /// Safely execute an operation and return a Result
        /// </summary>
        public static Result<T> TryExecute<T>(string context, Func<T> operation)
        {
            try
            {
                var result = operation();
                return Result<T>.Success(result);
            }
            catch (Exception e)
            {
                LogError(context, "Operation failed", e);
                return Result<T>.Failure($"Operation failed: {e.Message}", e);
            }
        }

        /// <summary>
        /// Safely execute an operation that doesn't return a value
        /// </summary>
        public static Result TryExecute(string context, Action operation)
        {
            try
            {
                operation();
                return Result.Success();
            }
            catch (Exception e)
            {
                LogError(context, "Operation failed", e);
                return Result.Failure($"Operation failed: {e.Message}", e);
            }
        }
    }
}