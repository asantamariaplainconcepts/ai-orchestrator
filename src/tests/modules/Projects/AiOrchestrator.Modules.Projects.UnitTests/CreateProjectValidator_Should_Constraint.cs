using AiOrchestrator.Modules.Projects.Features.Projects.UseCases;
using Shouldly;

namespace AiOrchestrator.Modules.Projects.UnitTests;

public class CreateProjectValidator_Should_Constraint
{
    readonly CreateProject.Validator _validator = new();

    [Fact]
    public void Validator_Should_RejectEmptyName()
    {
        var result = _validator.Validate(new CreateProject.Command(string.Empty));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_Should_RejectNameLongerThanTwoHundredCharacters()
    {
        var result = _validator.Validate(new CreateProject.Command(new string('a', 201)));

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public void Validator_Should_AcceptWellFormedName()
    {
        var result = _validator.Validate(new CreateProject.Command("Phoenix"));

        result.IsValid.ShouldBeTrue();
    }
}
