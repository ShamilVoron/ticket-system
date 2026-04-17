using FluentValidation;
using ITCafe.Api.Dtos.Messenger;

namespace ITCafe.Api.Validators;

public class CreateGroupChatRequestValidator : AbstractValidator<CreateGroupChatRequest>
{
    public CreateGroupChatRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.MemberUserIds).NotNull().NotEmpty();
        RuleForEach(x => x.MemberUserIds).NotEmpty().MaximumLength(128);
    }
}
