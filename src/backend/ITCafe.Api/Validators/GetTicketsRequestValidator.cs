using FluentValidation;
using ITCafe.Api.Dtos.Tickets;

namespace ITCafe.Api.Validators;

public class GetTicketsRequestValidator : AbstractValidator<GetTicketsRequest>
{
    public GetTicketsRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
