namespace NewsletterX.ContractModels.Validators;

using FluentValidation;
using NewsletterX.ContractModels.Requests;

/// <summary>
/// Validator for SubscribeRequest.
/// </summary>
public class SubscribeRequestValidator : AbstractValidator<SubscribeRequest>
{
    public SubscribeRequestValidator()
    {
        RuleFor(x => x.NewsletterType)
            .IsInEnum().WithMessage("Invalid newsletter type.");
    }
}

