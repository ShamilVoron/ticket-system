using FluentValidation;
using ITCafe.Api.Dtos.Employees;

namespace ITCafe.Api.Validators;

public class CreateEmployeeAccountDtoValidator : AbstractValidator<CreateEmployeeAccountDto>
{
    public CreateEmployeeAccountDtoValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(4);
        RuleFor(x => x.Role).NotEmpty();
    }
}
