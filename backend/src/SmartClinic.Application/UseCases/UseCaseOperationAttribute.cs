namespace SmartClinic.Application.UseCases;

/// <summary>Classifies a public application use case for audit coverage checks.</summary>
public enum UseCaseOperationKind
{
    ReadOnly,
    Mutating
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class UseCaseOperationAttribute : Attribute
{
    public UseCaseOperationAttribute(UseCaseOperationKind kind)
    {
        Kind = kind;
    }

    public UseCaseOperationKind Kind { get; }
}
