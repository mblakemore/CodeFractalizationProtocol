using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FractalCode.CLI
{
    /// <summary>
    /// Represents a CLI command in the FractalCode system
    /// </summary>
    public class Command
    {
        private readonly List<Command> _subcommands = new();
        private readonly List<Option> _options = new();
        private readonly List<Argument> _arguments = new();

        /// <summary>
        /// Name of the command
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Description of what the command does
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Handler for command execution
        /// </summary>
        public ICommandHandler Handler { get; set; }

        /// <summary>
        /// Parent command, if this is a subcommand
        /// </summary>
        public Command Parent { get; private set; }

        /// <summary>
        /// Collection of subcommands
        /// </summary>
        public IReadOnlyList<Command> Subcommands => _subcommands;

        /// <summary>
        /// Collection of command options
        /// </summary>
        public IReadOnlyList<Option> Options => _options;

        /// <summary>
        /// Collection of command arguments
        /// </summary>
        public IReadOnlyList<Argument> Arguments => _arguments;

        /// <summary>
        /// Initializes a new command
        /// </summary>
        /// <param name="name">Command name</param>
        /// <param name="description">Command description</param>
        public Command(string name, string description)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
        }

        /// <summary>
        /// Adds a subcommand
        /// </summary>
        /// <param name="command">Command to add</param>
        public void AddCommand(Command command)
        {
            if (command == null)
                throw new ArgumentNullException(nameof(command));

            command.Parent = this;
            _subcommands.Add(command);
        }

        /// <summary>
        /// Adds an option to the command
        /// </summary>
        /// <param name="option">Option to add</param>
        public void AddOption(Option option)
        {
            if (option == null)
                throw new ArgumentNullException(nameof(option));

            _options.Add(option);
        }

        /// <summary>
        /// Adds an argument to the command
        /// </summary>
        /// <param name="argument">Argument to add</param>
        public void AddArgument(Argument argument)
        {
            if (argument == null)
                throw new ArgumentNullException(nameof(argument));

            _arguments.Add(argument);
        }

        /// <summary>
        /// Gets the full path of the command including parent commands
        /// </summary>
        public string GetCommandPath()
        {
            var path = new List<string> { Name };
            var current = Parent;

            while (current != null)
            {
                path.Insert(0, current.Name);
                current = current.Parent;
            }

            return string.Join(" ", path);
        }

        /// <summary>
        /// Validates the command configuration
        /// </summary>
        /// <returns>True if valid, false otherwise</returns>
        public bool Validate()
        {
            // Check for handler if no subcommands
            if (_subcommands.Count == 0 && Handler == null)
                return false;

            // Validate subcommands
            foreach (var cmd in _subcommands)
            {
                if (!cmd.Validate())
                    return false;
            }

            // Validate options
            foreach (var opt in _options)
            {
                if (!opt.Validate())
                    return false;
            }

            // Validate arguments
            foreach (var arg in _arguments)
            {
                if (!arg.Validate())
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Executes the command with the given arguments
        /// </summary>
        /// <param name="args">Command arguments</param>
        /// <returns>Task representing command execution</returns>
        public async Task<int> ExecuteAsync(string[] args)
        {
            try
            {
                if (Handler == null)
                    throw new InvalidOperationException($"No handler defined for command: {Name}");

                return await Handler.InvokeAsync(args);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error executing command {Name}: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Gets help information for the command
        /// </summary>
        public virtual string GetHelpText()
        {
            var help = new List<string>
            {
                $"Command: {GetCommandPath()}",
                $"Description: {Description}",
                ""
            };

            if (_arguments.Count > 0)
            {
                help.Add("Arguments:");
                foreach (var arg in _arguments)
                {
                    help.Add($"  {arg.Name}: {arg.Description}");
                }
                help.Add("");
            }

            if (_options.Count > 0)
            {
                help.Add("Options:");
                foreach (var opt in _options)
                {
                    help.Add($"  --{opt.Name}: {opt.Description}");
                }
                help.Add("");
            }

            if (_subcommands.Count > 0)
            {
                help.Add("Subcommands:");
                foreach (var cmd in _subcommands)
                {
                    help.Add($"  {cmd.Name}: {cmd.Description}");
                }
            }

            return string.Join(Environment.NewLine, help);
        }
    }

    /// <summary>
    /// Represents a command option
    /// </summary>
    public class Option
    {
        /// <summary>
        /// Option name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Option description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Whether the option is required
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Default value for the option
        /// </summary>
        public object DefaultValue { get; }

        /// <summary>
        /// Option type
        /// </summary>
        public Type OptionType { get; }

        public Option(string name, string description, bool isRequired = false, object defaultValue = null, Type optionType = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            IsRequired = isRequired;
            DefaultValue = defaultValue;
            OptionType = optionType ?? typeof(string);
        }

        /// <summary>
        /// Validates the option configuration
        /// </summary>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return false;

            if (IsRequired && DefaultValue != null)
                return false;

            if (DefaultValue != null && !OptionType.IsInstanceOfType(DefaultValue))
                return false;

            return true;
        }
    }

    /// <summary>
    /// Represents a command argument
    /// </summary>
    public class Argument
    {
        /// <summary>
        /// Argument name
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Argument description
        /// </summary>
        public string Description { get; }

        /// <summary>
        /// Whether the argument is required
        /// </summary>
        public bool IsRequired { get; }

        /// <summary>
        /// Default value for the argument
        /// </summary>
        public object DefaultValue { get; }

        /// <summary>
        /// Argument type
        /// </summary>
        public Type ArgumentType { get; }

        public Argument(string name, string description, bool isRequired = true, object defaultValue = null, Type argumentType = null)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Description = description ?? throw new ArgumentNullException(nameof(description));
            IsRequired = isRequired;
            DefaultValue = defaultValue;
            ArgumentType = argumentType ?? typeof(string);
        }

        /// <summary>
        /// Validates the argument configuration
        /// </summary>
        public bool Validate()
        {
            if (string.IsNullOrWhiteSpace(Name))
                return false;

            if (IsRequired && DefaultValue != null)
                return false;

            if (DefaultValue != null && !ArgumentType.IsInstanceOfType(DefaultValue))
                return false;

            return true;
        }
    }
}