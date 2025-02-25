using System;
using System.Runtime.Serialization;

namespace FractalCode.Core.Exceptions
{
    /// <summary>
    /// Exception thrown when a contract violation occurs in the FractalCode system.
    /// 
    /// Contract violations may include:
    /// - Interface contract violations (incorrect inputs/outputs)
    /// - Behavioral contract violations (improper operation sequences)
    /// - Resource contract violations (resource misuse)
    /// - Flexibility boundary violations (exceeding adaptation limits)
    /// </summary>
    [Serializable]
    public class ContractViolationException : Exception
    {
        /// <summary>
        /// The type of contract that was violated
        /// </summary>
        public string ContractType { get; }

        /// <summary>
        /// The specific violation that occurred
        /// </summary>
        public string ViolationType { get; }

        /// <summary>
        /// The component where the violation occurred
        /// </summary>
        public string ComponentId { get; }

        /// <summary>
        /// Initializes a new instance of the ContractViolationException class
        /// </summary>
        public ContractViolationException() : base() { }

        /// <summary>
        /// Initializes a new instance of the ContractViolationException class with a specified error message
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        public ContractViolationException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the ContractViolationException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public ContractViolationException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Initializes a new instance of the ContractViolationException class with detailed contract violation information
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="contractType">The type of contract that was violated (interface, behavioral, resource)</param>
        /// <param name="violationType">The specific violation that occurred</param>
        /// <param name="componentId">The component where the violation occurred</param>
        public ContractViolationException(string message, string contractType, string violationType, string componentId)
            : base(message)
        {
            ContractType = contractType;
            ViolationType = violationType;
            ComponentId = componentId;
        }

        /// <summary>
        /// Initializes a new instance of the ContractViolationException class with serialized data
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        protected ContractViolationException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ContractType = info.GetString(nameof(ContractType));
            ViolationType = info.GetString(nameof(ViolationType));
            ComponentId = info.GetString(nameof(ComponentId));
        }

        /// <summary>
        /// Sets the SerializationInfo with information about the exception
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(ContractType), ContractType);
            info.AddValue(nameof(ViolationType), ViolationType);
            info.AddValue(nameof(ComponentId), ComponentId);

            base.GetObjectData(info, context);
        }
    }

    /// <summary>
    /// Exception thrown when a resource-related error occurs in the FractalCode system.
    /// 
    /// Resource exceptions may include:
    /// - Resource allocation failures
    /// - Resource contention issues
    /// - Resource cleanup failures
    /// - Resource adaptation failures
    /// - Resource boundary violations
    /// </summary>
    [Serializable]
    public class ResourceException : Exception
    {
        /// <summary>
        /// The type of resource that encountered an error
        /// </summary>
        public string ResourceType { get; }

        /// <summary>
        /// The specific error that occurred
        /// </summary>
        public string ErrorType { get; }

        /// <summary>
        /// The resource identifier
        /// </summary>
        public string ResourceId { get; }

        /// <summary>
        /// Resource state at the time of the exception
        /// </summary>
        public object ResourceState { get; }

        /// <summary>
        /// Initializes a new instance of the ResourceException class
        /// </summary>
        public ResourceException() : base() { }

        /// <summary>
        /// Initializes a new instance of the ResourceException class with a specified error message
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        public ResourceException(string message) : base(message) { }

        /// <summary>
        /// Initializes a new instance of the ResourceException class with a specified error message
        /// and a reference to the inner exception that is the cause of this exception
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="inner">The exception that is the cause of the current exception</param>
        public ResourceException(string message, Exception inner) : base(message, inner) { }

        /// <summary>
        /// Initializes a new instance of the ResourceException class with detailed resource error information
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception</param>
        /// <param name="resourceType">The type of resource that encountered an error</param>
        /// <param name="errorType">The specific error that occurred</param>
        /// <param name="resourceId">The resource identifier</param>
        /// <param name="resourceState">Resource state at the time of the exception</param>
        public ResourceException(string message, string resourceType, string errorType, string resourceId, object resourceState = null)
            : base(message)
        {
            ResourceType = resourceType;
            ErrorType = errorType;
            ResourceId = resourceId;
            ResourceState = resourceState;
        }

        /// <summary>
        /// Initializes a new instance of the ResourceException class with serialized data
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        protected ResourceException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
            ResourceType = info.GetString(nameof(ResourceType));
            ErrorType = info.GetString(nameof(ErrorType));
            ResourceId = info.GetString(nameof(ResourceId));
            ResourceState = info.GetValue(nameof(ResourceState), typeof(object));
        }

        /// <summary>
        /// Sets the SerializationInfo with information about the exception
        /// </summary>
        /// <param name="info">The SerializationInfo that holds the serialized object data</param>
        /// <param name="context">The StreamingContext that contains contextual information about the source or destination</param>
        public override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            if (info == null)
                throw new ArgumentNullException(nameof(info));

            info.AddValue(nameof(ResourceType), ResourceType);
            info.AddValue(nameof(ErrorType), ErrorType);
            info.AddValue(nameof(ResourceId), ResourceId);
            info.AddValue(nameof(ResourceState), ResourceState);

            base.GetObjectData(info, context);
        }
    }
}