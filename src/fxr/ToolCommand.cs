using System.Threading.Tasks;

using Mono.Options;

namespace fxr
{
    internal abstract class ToolCommand
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract void AddOptions(OptionSet options);
        public abstract void Execute();
    }
}
