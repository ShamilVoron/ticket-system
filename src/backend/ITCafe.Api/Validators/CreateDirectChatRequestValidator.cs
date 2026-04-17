using FluentValidation;
using ITCafe.Api.Dtos.Messenger;

namespace ITCafe.Api.Validators;

public class CreateDirectChatRequestValidator : AbstractValidator<CreateDirectChatRequest>
{
    public CreateDirectChatRequestValidator()
    {
        RuleFor(x => x.OtherUserId).NotEmpty().MaximumLength(128);
    }
}
