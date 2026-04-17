using FluentValidation;
using ITCafe.Api.Dtos.Messenger;

namespace ITCafe.Api.Validators;

public class UpdateGroupChatRequestValidator : AbstractValidator<UpdateGroupChatRequest>
{
    public UpdateGroupChatRequestValidator()
    {
        RuleFor(x => x.Title!)
            .MinimumLength(1)
            .MaximumLength(120)
            .When(x => x.Title != null);
    }
}
