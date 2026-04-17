using FluentValidation;
using ITCafe.Api.Dtos.Messenger;

namespace ITCafe.Api.Validators;

public class PostChatMessageRequestValidator : AbstractValidator<PostChatMessageRequest>
{
    public PostChatMessageRequestValidator()
    {
        RuleFor(x => x.Body).MaximumLength(8000);
        RuleFor(x => x.AttachmentUrl).MaximumLength(512).When(x => !string.IsNullOrWhiteSpace(x.AttachmentUrl));
        RuleFor(x => x.AttachmentMimeType).MaximumLength(128).When(x => !string.IsNullOrWhiteSpace(x.AttachmentMimeType));
        RuleFor(x => x.AttachmentFileName).MaximumLength(260).When(x => !string.IsNullOrWhiteSpace(x.AttachmentFileName));
        RuleFor(x => x)
            .Must(x =>
                (!string.IsNullOrWhiteSpace(x.Body?.Trim())) ||
                !string.IsNullOrWhiteSpace(x.AttachmentUrl))
            .WithMessage("Нужен текст сообщения или вложение.");
    }
}
