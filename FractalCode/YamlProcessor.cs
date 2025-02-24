using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace FractalCode.Core
{
    public interface IYamlProcessor
    {
        Task<T> DeserializeAsync<T>(string path);
        Task<T> DeserializeAsync<T>(Stream stream);
        Task SerializeAsync<T>(T obj, string path);
        Task<string> SerializeToStringAsync<T>(T obj);
        Task<bool> ValidateAsync(string path);
        Task<IDictionary<string, object>> MergeAsync(string path1, string path2);
    }

    public class YamlProcessor : IYamlProcessor
    {
        private readonly ILogger<YamlProcessor> _logger;
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        public YamlProcessor(ILogger<YamlProcessor> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure YAML deserializer with common settings
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            // Configure YAML serializer with common settings
            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .WithQuotingNecessaryStrings()
                .Build();
        }

        public async Task<T> DeserializeAsync<T>(string path)
        {
            try
            {
                _logger.LogDebug($"Deserializing YAML from path: {path}");

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"YAML file not found: {path}");
                }

                using var stream = File.OpenRead(path);
                return await DeserializeAsync<T>(stream);
            }
            catch (YamlException ex)
            {
                _logger.LogError(ex, $"YAML deserialization error in {path}: {ex.Message}");
                throw new InvalidOperationException($"Invalid YAML format in {path}: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error deserializing YAML from {path}");
                throw;
            }
        }

        public async Task<T> DeserializeAsync<T>(Stream stream)
        {
            try
            {
                _logger.LogDebug("Deserializing YAML from stream");

                using var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();
                return _deserializer.Deserialize<T>(content);
            }
            catch (YamlException ex)
            {
                _logger.LogError(ex, "YAML deserialization error from stream");
                throw new InvalidOperationException($"Invalid YAML format: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deserializing YAML from stream");
                throw;
            }
        }

        public async Task SerializeAsync<T>(T obj, string path)
        {
            try
            {
                _logger.LogDebug($"Serializing object to YAML at path: {path}");

                // Create directory if it doesn't exist
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // Serialize and write
                var yaml = _serializer.Serialize(obj);
                await File.WriteAllTextAsync(path, yaml);
            }
            catch (YamlException ex)
            {
                _logger.LogError(ex, $"YAML serialization error for {path}");
                throw new InvalidOperationException($"Error generating YAML: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error serializing object to {path}");
                throw;
            }
        }

        public async Task<string> SerializeToStringAsync<T>(T obj)
        {
            try
            {
                _logger.LogDebug("Serializing object to YAML string");
                return _serializer.Serialize(obj);
            }
            catch (YamlException ex)
            {
                _logger.LogError(ex, "YAML serialization error");
                throw new InvalidOperationException($"Error generating YAML: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing object to string");
                throw;
            }
        }

        public async Task<bool> ValidateAsync(string path)
        {
            try
            {
                _logger.LogDebug($"Validating YAML at path: {path}");

                if (!File.Exists(path))
                {
                    throw new FileNotFoundException($"YAML file not found: {path}");
                }

                var content = await File.ReadAllTextAsync(path);

                // Attempt to parse as generic object to validate syntax
                _deserializer.Deserialize<object>(content);
                return true;
            }
            catch (YamlException ex)
            {
                _logger.LogWarning(ex, $"YAML validation failed for {path}: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating YAML at {path}");
                throw;
            }
        }

        public async Task<IDictionary<string, object>> MergeAsync(string path1, string path2)
        {
            try
            {
                _logger.LogDebug($"Merging YAML files: {path1} and {path2}");

                // Load both files
                var dict1 = await DeserializeAsync<Dictionary<string, object>>(path1);
                var dict2 = await DeserializeAsync<Dictionary<string, object>>(path2);

                // Perform deep merge
                return MergeDictionaries(dict1, dict2);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error merging YAML files {path1} and {path2}");
                throw;
            }
        }

        private IDictionary<string, object> MergeDictionaries(
            IDictionary<string, object> dict1,
            IDictionary<string, object> dict2)
        {
            var result = new Dictionary<string, object>(dict1);

            foreach (var kvp in dict2)
            {
                if (!result.ContainsKey(kvp.Key))
                {
                    result[kvp.Key] = kvp.Value;
                }
                else if (result[kvp.Key] is IDictionary<string, object> existingDict &&
                         kvp.Value is IDictionary<string, object> newDict)
                {
                    // Recursively merge nested dictionaries
                    result[kvp.Key] = MergeDictionaries(existingDict, newDict);
                }
                else if (result[kvp.Key] is List<object> existingList &&
                         kvp.Value is List<object> newList)
                {
                    // Merge lists, removing duplicates
                    existingList.AddRange(newList);
                    result[kvp.Key] = existingList.Distinct().ToList();
                }
                else
                {
                    // For all other types, newer value overwrites
                    result[kvp.Key] = kvp.Value;
                }
            }

            return result;
        }

        public class ValidationException : Exception
        {
            public ValidationException(string message) : base(message) { }
            public ValidationException(string message, Exception inner) : base(message, inner) { }
        }
    }

    public static class YamlProcessorExtensions
    {
        public static async Task<T> DeserializeFileAsync<T>(this IYamlProcessor processor, string path)
        {
            return await processor.DeserializeAsync<T>(path);
        }

        public static async Task<string> SerializeObjectAsync<T>(this IYamlProcessor processor, T obj)
        {
            return await processor.SerializeToStringAsync(obj);
        }

        public static async Task<bool> IsValidYamlFileAsync(this IYamlProcessor processor, string path)
        {
            return await processor.ValidateAsync(path);
        }
    }
}