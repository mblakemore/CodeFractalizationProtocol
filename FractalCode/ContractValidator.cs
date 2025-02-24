using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using FractalCode.Core.Models;

namespace FractalCode.Contracts
{
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
    }

    public interface IContractValidator
    {
        Task<ValidationResult> ValidateContractsAsync(string path);
        Task<ValidationResult> ValidateContractAsync(string contractPath, string contractType);
        Task<ValidationResult> ValidateContractEvolutionAsync(string oldContractPath, string newContractPath);
    }

    public class ContractValidator : IContractValidator
    {
        private readonly ILogger<ContractValidator> _logger;
        private readonly IDeserializer _deserializer;
        private readonly ISerializer _serializer;

        private static readonly HashSet<string> ValidContractTypes = new()
        {
            "interface",
            "behavior",
            "resource"
        };

        public ContractValidator(ILogger<ContractValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Configure YAML serializer/deserializer
            _deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            _serializer = new SerializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();
        }

        public async Task<ValidationResult> ValidateContractsAsync(string path)
        {
            try
            {
                _logger.LogInformation($"Validating contracts in path: {path}");

                var result = new ValidationResult { IsValid = true };

                if (File.Exists(path))
                {
                    // Single contract file validation
                    var contractType = DetermineContractType(path);
                    var contractResult = await ValidateContractAsync(path, contractType);
                    MergeValidationResults(result, contractResult);
                }
                else if (Directory.Exists(path))
                {
                    // Directory of contracts validation
                    foreach (var contractFile in Directory.GetFiles(path, "*.yaml", SearchOption.AllDirectories))
                    {
                        var contractType = DetermineContractType(contractFile);
                        var contractResult = await ValidateContractAsync(contractFile, contractType);
                        MergeValidationResults(result, contractResult);
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Contract path not found: {path}");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating contracts: {ex.Message}");
                throw;
            }
        }

        public async Task<ValidationResult> ValidateContractAsync(string contractPath, string contractType)
        {
            try
            {
                _logger.LogInformation($"Validating contract: {contractPath} of type: {contractType}");

                var result = new ValidationResult { IsValid = true };

                // Read and parse contract
                var contractContent = await File.ReadAllTextAsync(contractPath);
                var contract = _deserializer.Deserialize<dynamic>(contractContent);

                // Validate based on contract type
                switch (contractType.ToLower())
                {
                    case "interface":
                        ValidateInterfaceContract(contract, result);
                        break;

                    case "behavior":
                        ValidateBehaviorContract(contract, result);
                        break;

                    case "resource":
                        ValidateResourceContract(contract, result);
                        break;

                    default:
                        result.Errors.Add($"Unknown contract type: {contractType}");
                        result.IsValid = false;
                        break;
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating contract {contractPath}: {ex.Message}");
                throw;
            }
        }

        public async Task<ValidationResult> ValidateContractEvolutionAsync(string oldContractPath, string newContractPath)
        {
            try
            {
                _logger.LogInformation($"Validating contract evolution from {oldContractPath} to {newContractPath}");

                var result = new ValidationResult { IsValid = true };

                // Read and parse contracts
                var oldContractContent = await File.ReadAllTextAsync(oldContractPath);
                var newContractContent = await File.ReadAllTextAsync(newContractPath);

                var oldContract = _deserializer.Deserialize<dynamic>(oldContractContent);
                var newContract = _deserializer.Deserialize<dynamic>(newContractContent);

                // Validate version evolution
                ValidateVersionEvolution(oldContract, newContract, result);

                // Validate compatibility
                ValidateCompatibility(oldContract, newContract, result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating contract evolution: {ex.Message}");
                throw;
            }
        }

        private void ValidateInterfaceContract(dynamic contract, ValidationResult result)
        {
            // Validate required fields
            if (contract.name == null)
            {
                result.Errors.Add("Interface contract must have a name");
                result.IsValid = false;
            }

            if (contract.version == null)
            {
                result.Errors.Add("Interface contract must have a version");
                result.IsValid = false;
            }

            // Validate inputs
            if (contract.inputs != null)
            {
                foreach (var input in contract.inputs)
                {
                    if (input.name == null || input.type == null)
                    {
                        result.Errors.Add("Interface inputs must have name and type");
                        result.IsValid = false;
                    }
                }
            }

            // Validate outputs
            if (contract.outputs != null)
            {
                foreach (var output in contract.outputs)
                {
                    if (output.name == null || output.type == null)
                    {
                        result.Errors.Add("Interface outputs must have name and type");
                        result.IsValid = false;
                    }
                }
            }

            // Validate extension points
            if (contract.extensionPoints != null && !(contract.extensionPoints is List<object>))
            {
                result.Errors.Add("Extension points must be a list");
                result.IsValid = false;
            }
        }

        private void ValidateBehaviorContract(dynamic contract, ValidationResult result)
        {
            // Validate required fields
            if (contract.name == null)
            {
                result.Errors.Add("Behavior contract must have a name");
                result.IsValid = false;
            }

            if (contract.version == null)
            {
                result.Errors.Add("Behavior contract must have a version");
                result.IsValid = false;
            }

            // Validate operations
            if (contract.operations != null)
            {
                foreach (var operation in contract.operations)
                {
                    if (operation.name == null || operation.description == null)
                    {
                        result.Errors.Add("Operations must have name and description");
                        result.IsValid = false;
                    }
                }
            }

            // Validate concurrency rules
            if (contract.concurrencyRules != null)
            {
                foreach (var rule in contract.concurrencyRules)
                {
                    if (rule.type == null || rule.description == null)
                    {
                        result.Errors.Add("Concurrency rules must have type and description");
                        result.IsValid = false;
                    }
                }
            }

            // Validate performance constraints
            if (contract.performanceConstraints != null)
            {
                foreach (var constraint in contract.performanceConstraints)
                {
                    if (constraint.metric == null || constraint.threshold == null)
                    {
                        result.Errors.Add("Performance constraints must have metric and threshold");
                        result.IsValid = false;
                    }
                }
            }
        }

        private void ValidateResourceContract(dynamic contract, ValidationResult result)
        {
            // Validate required fields
            if (contract.name == null)
            {
                result.Errors.Add("Resource contract must have a name");
                result.IsValid = false;
            }

            if (contract.version == null)
            {
                result.Errors.Add("Resource contract must have a version");
                result.IsValid = false;
            }

            // Validate resource requirements
            if (contract.resourceRequirements != null)
            {
                foreach (var requirement in contract.resourceRequirements)
                {
                    if (requirement.type == null || requirement.specification == null)
                    {
                        result.Errors.Add("Resource requirements must have type and specification");
                        result.IsValid = false;
                    }
                }
            }

            // Validate access patterns
            if (contract.accessPatterns != null)
            {
                foreach (var pattern in contract.accessPatterns)
                {
                    if (pattern.type == null || pattern.description == null)
                    {
                        result.Errors.Add("Access patterns must have type and description");
                        result.IsValid = false;
                    }
                }
            }

            // Validate scaling rules
            if (contract.scalingRules != null)
            {
                foreach (var rule in contract.scalingRules)
                {
                    if (rule.trigger == null || rule.action == null)
                    {
                        result.Errors.Add("Scaling rules must have trigger and action");
                        result.IsValid = false;
                    }
                }
            }
        }

        private void ValidateVersionEvolution(dynamic oldContract, dynamic newContract, ValidationResult result)
        {
            // Parse versions using separate checks
            bool oldVersionValid = Version.TryParse(oldContract.version.ToString(), out Version oldVersion);
            bool newVersionValid = Version.TryParse(newContract.version.ToString(), out Version newVersion);

            if (!oldVersionValid || !newVersionValid)
            {
                result.Errors.Add("Invalid version format");
                result.IsValid = false;
                return;
            }

            // Verify version increment
            if (newVersion <= oldVersion)
            {
                result.Errors.Add("New version must be greater than old version");
                result.IsValid = false;
            }

            // Check for major version changes
            if (newVersion.Major > oldVersion.Major)
            {
                result.Warnings.Add("Major version change detected - breaking changes expected");
            }
        }

        private void ValidateCompatibility(dynamic oldContract, dynamic newContract, ValidationResult result)
        {
            // Compare contract names
            if (oldContract.name.ToString() != newContract.name.ToString())
            {
                result.Errors.Add("Contract names must match");
                result.IsValid = false;
            }

            // For interface contracts, check input/output compatibility
            if (oldContract.inputs != null && newContract.inputs != null)
            {
                ValidateInterfaceCompatibility(oldContract, newContract, result);
            }

            // For behavior contracts, check operation compatibility
            if (oldContract.operations != null && newContract.operations != null)
            {
                ValidateBehaviorCompatibility(oldContract, newContract, result);
            }

            // For resource contracts, check requirement compatibility
            if (oldContract.resourceRequirements != null && newContract.resourceRequirements != null)
            {
                ValidateResourceCompatibility(oldContract, newContract, result);
            }
        }

        private void ValidateInterfaceCompatibility(dynamic oldContract, dynamic newContract, ValidationResult result)
        {
            // Check for removed inputs
            var oldInputs = ((List<object>)oldContract.inputs).Select(i => ((dynamic)i).name.ToString());
            var newInputs = ((List<object>)newContract.inputs).Select(i => ((dynamic)i).name.ToString());

            var removedInputs = oldInputs.Except(newInputs);
            if (removedInputs.Any())
            {
                result.Errors.Add($"Breaking change: Removed inputs: {string.Join(", ", removedInputs)}");
                result.IsValid = false;
            }

            // Check for required outputs that were removed
            var oldOutputs = ((List<object>)oldContract.outputs).Select(o => ((dynamic)o).name.ToString());
            var newOutputs = ((List<object>)newContract.outputs).Select(o => ((dynamic)o).name.ToString());

            var removedOutputs = oldOutputs.Except(newOutputs);
            if (removedOutputs.Any())
            {
                result.Warnings.Add($"Potentially breaking change: Removed outputs: {string.Join(", ", removedOutputs)}");
            }
        }

        private void ValidateBehaviorCompatibility(dynamic oldContract, dynamic newContract, ValidationResult result)
        {
            // Check for removed operations
            var oldOps = ((List<object>)oldContract.operations).Select(o => ((dynamic)o).name.ToString());
            var newOps = ((List<object>)newContract.operations).Select(o => ((dynamic)o).name.ToString());

            var removedOps = oldOps.Except(newOps);
            if (removedOps.Any())
            {
                result.Errors.Add($"Breaking change: Removed operations: {string.Join(", ", removedOps)}");
                result.IsValid = false;
            }

            // Check for modified operation signatures
            foreach (var operation in oldContract.operations)
            {
                var oldOp = (dynamic)operation;
                var newOp = ((List<object>)newContract.operations)
                    .Cast<dynamic>()
                    .FirstOrDefault(o => o.name.ToString() == oldOp.name.ToString());

                if (newOp != null && !AreOperationSignaturesCompatible(oldOp, newOp))
                {
                    result.Errors.Add($"Breaking change: Modified operation signature: {oldOp.name}");
                    result.IsValid = false;
                }
            }
        }

        private void ValidateResourceCompatibility(dynamic oldContract, dynamic newContract, ValidationResult result)
        {
            // Check for increased resource requirements
            foreach (var requirement in newContract.resourceRequirements)
            {
                var newReq = (dynamic)requirement;
                var oldReq = ((List<object>)oldContract.resourceRequirements)
                    .Cast<dynamic>()
                    .FirstOrDefault(r => r.type.ToString() == newReq.type.ToString());

                if (oldReq != null && HasIncreasedRequirements(oldReq, newReq))
                {
                    result.Warnings.Add($"Increased resource requirements for: {newReq.type}");
                }
            }
        }

        private string DetermineContractType(string contractPath)
        {
            var fileName = Path.GetFileNameWithoutExtension(contractPath).ToLower();

            foreach (var type in ValidContractTypes)
            {
                if (fileName.Contains(type))
                {
                    return type;
                }
            }

            throw new InvalidOperationException($"Unable to determine contract type from filename: {fileName}");
        }

        private void MergeValidationResults(ValidationResult target, ValidationResult source)
        {
            target.IsValid &= source.IsValid;
            target.Errors.AddRange(source.Errors);
            target.Warnings.AddRange(source.Warnings);
        }

        private bool AreOperationSignaturesCompatible(dynamic oldOp, dynamic newOp)
        {
            try
            {
                // Check input parameters
                if (oldOp.parameters != null)
                {
                    var oldParams = ((List<object>)oldOp.parameters).Cast<dynamic>();
                    var newParams = ((List<object>)newOp.parameters).Cast<dynamic>();

                    foreach (var oldParam in oldParams)
                    {
                        var newParam = newParams.FirstOrDefault(p => p.name.ToString() == oldParam.name.ToString());

                        // Parameter was removed or type changed
                        if (newParam == null || newParam.type.ToString() != oldParam.type.ToString())
                        {
                            return false;
                        }

                        // Required parameter became optional
                        if (oldParam.required != null && newParam.required != null &&
                            oldParam.required == true && newParam.required == false)
                        {
                            return true; // This is a compatible change
                        }
                    }
                }

                // Check return type
                if (oldOp.returnType != null && newOp.returnType != null &&
                    oldOp.returnType.ToString() != newOp.returnType.ToString())
                {
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing operation signatures");
                return false;
            }
        }

        private bool HasIncreasedRequirements(dynamic oldReq, dynamic newReq)
        {
            try
            {
                if (oldReq.specification == null || newReq.specification == null)
                {
                    return false;
                }

                // Initialize variables with defaults
                decimal oldValue = 0;
                decimal newValue = 0;
                bool oldParsed = decimal.TryParse(oldReq.specification.ToString(), out oldValue);
                bool newParsed = decimal.TryParse(newReq.specification.ToString(), out newValue);

                // Compare numeric values if both parsed successfully
                if (oldParsed && newParsed)
                {
                    return newValue > oldValue;
                }

                // Compare string specifications
                return !string.Equals(
                    oldReq.specification.ToString(),
                    newReq.specification.ToString(),
                    StringComparison.OrdinalIgnoreCase
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error comparing resource requirements");
                return true; // Assume increased requirements on error for safety
            }
        }

        private async Task ValidateContractSyntaxAsync(string contractPath)
        {
            try
            {
                var content = await File.ReadAllTextAsync(contractPath);
                _deserializer.Deserialize<dynamic>(content);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Invalid YAML syntax in contract {contractPath}: {ex.Message}", ex);
            }
        }
    }
}