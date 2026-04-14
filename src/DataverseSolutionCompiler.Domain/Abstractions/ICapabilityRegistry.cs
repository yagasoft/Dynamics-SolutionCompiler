using DataverseSolutionCompiler.Domain.Capabilities;

namespace DataverseSolutionCompiler.Domain.Abstractions;

public interface ICapabilityRegistry
{
    IReadOnlyCollection<CapabilityDescriptor> GetAll();

    bool TryGet(CapabilityKind kind, out CapabilityDescriptor descriptor);

    bool TryGet(string capabilityName, out CapabilityDescriptor descriptor);
}
