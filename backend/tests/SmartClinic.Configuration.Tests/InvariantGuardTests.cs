using SmartClinic.Application.Configuration;
using Xunit;

namespace SmartClinic.Configuration.Tests;

public sealed class InvariantGuardTests
{
    [Fact]
    public void Invariant_configuration_is_rejected()
    {
        var guard = new InvariantGuard();

        var definition = new ConfigurationDefinition(
            "test.invariant",
            ConfigurationClassification.Invariant);

        var exception = Assert.Throws<InvalidOperationException>(
            () => guard.EnsureChangeAllowed(definition));

        Assert.Contains("test.invariant", exception.Message);
    }

    [Fact]
    public void Non_invariant_configuration_is_allowed()
    {
        var guard = new InvariantGuard();

        var definition = new ConfigurationDefinition(
            "test.non-invariant",
            ConfigurationClassification.NonInvariant);

        guard.EnsureChangeAllowed(definition);
    }

    [Fact]
    public void Undefined_configuration_classification_is_rejected()
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => new ConfigurationDefinition(
                "test.unclassified",
                (ConfigurationClassification)0));

        Assert.Equal(0, (int)exception.ActualValue!);
    }
}