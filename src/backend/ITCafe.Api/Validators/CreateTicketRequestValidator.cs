using FluentValidation;
using ITCafe.Api.Dtos.Tickets;

namespace ITCafe.Api.Validators;

public class CreateTicketRequestValidator : AbstractValidator<CreateTicketRequest>
{
    public CreateTicketRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(500);
        RuleFor(x => x.RequestType).NotEmpty();
        RuleFor(x => x.Priority).NotEmpty();
        RuleFor(x => x.Department).NotEmpty();
    }
}
