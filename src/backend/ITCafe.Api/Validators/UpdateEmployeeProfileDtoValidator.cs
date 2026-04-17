using FluentValidation;
using ITCafe.Api.Dtos.Employees;

namespace ITCafe.Api.Validators;

public class UpdateEmployeeProfileDtoValidator : AbstractValidator<UpdateEmployeeProfileDto>
{
    public UpdateEmployeeProfileDtoValidator()
    {
        When(x => !string.IsNullOrWhiteSpace(x.FullName), () =>
        {
            RuleFor(x => x.FullName).MaximumLength(200);
        });
    }
}
