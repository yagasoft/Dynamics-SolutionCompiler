namespace DataverseSolutionCompiler.Domain.Capabilities;

public sealed record CapabilityDescriptor(
    CapabilityKind Kind,
    string Name,
    string Description,
    CapabilityReadiness Readiness,
    IReadOnlyList<string> RepresentativeFamilies,
    IReadOnlyList<string> KnownBoundaries);
