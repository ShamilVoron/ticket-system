using FluentValidation;
using ITCafe.Api.Dtos.Tickets;

namespace ITCafe.Api.Validators;

public class DelegateRequestValidator : AbstractValidator<DelegateRequest>
{
    public DelegateRequestValidator()
    {
        RuleFor(x => x.DelegatedFrom).NotEmpty();
        RuleFor(x => x.DelegatedTo).NotEmpty();
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}
