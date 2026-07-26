using AiOrchestrator.Modules.Backlog.Domain;
using Shouldly;

namespace AiOrchestrator.Modules.Backlog.UnitTests;

public class Connector_Should_Constraint
{
    static readonly DateTimeOffset Now = new(2026, 7, 26, 12, 0, 0, TimeSpan.Zero);

    static Connector Configured() =>
        Connector.Create(Guid.NewGuid(), BacklogVendor.GitHub, "acme", "portal", "acme-pat");

    [Fact]
    public void RecordFailure_Should_KeepTheReasonSoEmptyAndBrokenDiffer()
    {
        var connector = Configured();

        connector.RecordFailure(Now, "the vendor rejected the credential");

        connector.LastFailure.ShouldBe("the vendor rejected the credential");
        connector.LastFailureAt.ShouldBe(Now);
    }

    [Fact]
    public void RecordSuccess_Should_ClearAPreviousFailure()
    {
        var connector = Configured();
        connector.RecordFailure(Now, "boom");

        connector.RecordSuccess(Now.AddMinutes(1));

        connector.LastFailure.ShouldBeNull();
        connector.LastFailureAt.ShouldBeNull();
        connector.LastSyncedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void Reconfigure_Should_ForgetEverythingAboutTheOldRepository()
    {
        var connector = Configured();
        connector.RecordSuccess(Now);
        connector.RecordFailure(Now, "boom");

        connector.Reconfigure(BacklogVendor.GitHub, "acme", "different-repo", "other-pat");

        // Sync state described a repository we are no longer pointing at.
        connector.Repository.ShouldBe("different-repo");
        connector.SecretName.ShouldBe("other-pat");
        connector.LastSyncedAt.ShouldBeNull();
        connector.LastFailure.ShouldBeNull();
    }

    [Fact]
    public void Create_Should_StoreOnlyTheSecretName()
    {
        var connector = Configured();

        // BR-010: the name, never the value. Nothing on the aggregate can hold a token.
        connector.SecretName.ShouldBe("acme-pat");
        typeof(Connector)
            .GetProperties()
            .ShouldNotContain(property => property.Name.Contains("Token"));
    }
}
