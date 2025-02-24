using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;

namespace FractalCode.CLI
{
    /// <summary>
    /// Interface for command handlers in the CLI system
    /// </summary>
    public interface ICommandHandler
    {
        /// <summary>
        /// Invokes the command handler with the given arguments
        /// </summary>
        /// <param name="args">Command line arguments</param>
        /// <returns>Exit code for the command</returns>
        Task<int> InvokeAsync(string[] args);

        /// <summary>
        /// Gets the parameter requirements for the handler
        /// </summary>
        /// <returns>Dictionary of parameter names and types</returns>
        IDictionary<string, Type> GetParameterRequirements();

        /// <summary>
        /// Validates the arguments against the parameter requirements
        /// </summary>
        /// <param name="args">Arguments to validate</param>
        /// <returns>True if valid, false otherwise</returns>
        bool ValidateArguments(string[] args);
    }

    /// <summary>
    /// Base class for implementing command handlers
    /// </summary>
    public abstract class CommandHandlerBase : ICommandHandler
    {
        protected readonly IServiceProvider ServiceProvider;
        private readonly IDictionary<string, Type> _parameterRequirements;

        protected CommandHandlerBase(IServiceProvider serviceProvider, IDictionary<string, Type> parameterRequirements)
        {
            ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _parameterRequirements = parameterRequirements ?? throw new ArgumentNullException(nameof(parameterRequirements));
        }

        public abstract Task<int> InvokeAsync(string[] args);

        public IDictionary<string, Type> GetParameterRequirements() => _parameterRequirements;

        public virtual bool ValidateArguments(string[] args)
        {
            if (args == null)
                return false;

            // Check required parameter count
            var requiredParams = _parameterRequirements.Count;
            if (args.Length < requiredParams)
                return false;

            // Validate parameter types
            for (int i = 0; i < requiredParams; i++)
            {
                var paramType = _parameterRequirements.ElementAt(i).Value;
                if (!TryParseArgument(args[i], paramType, out _))
                    return false;
            }

            return true;
        }

        protected T GetService<T>() where T : class
        {
            return ServiceProvider.GetRequiredService<T>();
        }

        protected bool TryParseArgument(string arg, Type targetType, out object result)
        {
            try
            {
                if (targetType == typeof(string))
                {
                    result = arg;
                    return true;
                }

                if (targetType == typeof(int) && int.TryParse(arg, out var intValue))
                {
                    result = intValue;
                    return true;
                }

                if (targetType == typeof(bool) && bool.TryParse(arg, out var boolValue))
                {
                    result = boolValue;
                    return true;
                }

                // Add more type parsing as needed

                result = null;
                return false;
            }
            catch
            {
                result = null;
                return false;
            }
        }
    }

    /// <summary>
    /// Factory for creating command handlers with type-safe parameters
    /// </summary>
    public static class CommandHandler
    {
        public static ICommandHandler Create<T1>(Func<T1, Task<int>> handler, IServiceProvider serviceProvider)
        {
            return new DelegateCommandHandler<T1>(handler, serviceProvider);
        }

        public static ICommandHandler Create<T1, T2>(Func<T1, T2, Task<int>> handler, IServiceProvider serviceProvider)
        {
            return new DelegateCommandHandler<T1, T2>(handler, serviceProvider);
        }

        public static ICommandHandler Create<T1, T2, T3>(Func<T1, T2, T3, Task<int>> handler, IServiceProvider serviceProvider)
        {
            return new DelegateCommandHandler<T1, T2, T3>(handler, serviceProvider);
        }

        // Add more overloads as needed for different parameter counts
    }

    /// <summary>
    /// Command handler implementation for single parameter
    /// </summary>
    internal class DelegateCommandHandler<T1> : CommandHandlerBase
    {
        private readonly Func<T1, Task<int>> _handler;

        public DelegateCommandHandler(Func<T1, Task<int>> handler, IServiceProvider serviceProvider)
            : base(serviceProvider, new Dictionary<string, Type> { { "param1", typeof(T1) } })
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override async Task<int> InvokeAsync(string[] args)
        {
            if (!ValidateArguments(args))
                throw new ArgumentException("Invalid arguments for command");

            if (!TryParseArgument(args[0], typeof(T1), out var param1))
                throw new ArgumentException($"Cannot parse argument as {typeof(T1).Name}");

            return await _handler((T1)param1);
        }
    }

    /// <summary>
    /// Command handler implementation for two parameters
    /// </summary>
    internal class DelegateCommandHandler<T1, T2> : CommandHandlerBase
    {
        private readonly Func<T1, T2, Task<int>> _handler;

        public DelegateCommandHandler(Func<T1, T2, Task<int>> handler, IServiceProvider serviceProvider)
            : base(serviceProvider, new Dictionary<string, Type>
            {
                { "param1", typeof(T1) },
                { "param2", typeof(T2) }
            })
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override async Task<int> InvokeAsync(string[] args)
        {
            if (!ValidateArguments(args))
                throw new ArgumentException("Invalid arguments for command");

            if (!TryParseArgument(args[0], typeof(T1), out var param1) ||
                !TryParseArgument(args[1], typeof(T2), out var param2))
                throw new ArgumentException("Cannot parse arguments");

            return await _handler((T1)param1, (T2)param2);
        }
    }

    /// <summary>
    /// Command handler implementation for three parameters
    /// </summary>
    internal class DelegateCommandHandler<T1, T2, T3> : CommandHandlerBase
    {
        private readonly Func<T1, T2, T3, Task<int>> _handler;

        public DelegateCommandHandler(Func<T1, T2, T3, Task<int>> handler, IServiceProvider serviceProvider)
            : base(serviceProvider, new Dictionary<string, Type>
            {
                { "param1", typeof(T1) },
                { "param2", typeof(T2) },
                { "param3", typeof(T3) }
            })
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
        }

        public override async Task<int> InvokeAsync(string[] args)
        {
            if (!ValidateArguments(args))
                throw new ArgumentException("Invalid arguments for command");

            if (!TryParseArgument(args[0], typeof(T1), out var param1) ||
                !TryParseArgument(args[1], typeof(T2), out var param2) ||
                !TryParseArgument(args[2], typeof(T3), out var param3))
                throw new ArgumentException("Cannot parse arguments");

            return await _handler((T1)param1, (T2)param2, (T3)param3);
        }
    }
}