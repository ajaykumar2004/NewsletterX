namespace NewsletterX.ContractModels.Validators;

using FluentValidation;
using NewsletterX.ContractModels.Requests;
using NewsletterX.Types.Constants;

/// <summary>
/// Validator for RegisterUserRequest.
/// </summary>
/// <remarks>
/// FLUENT VALIDATION BENEFITS:
/// ──────────────────────────
/// - Testable: Validators are classes, can unit test validation logic
/// - Reusable: Can compose validators, reuse rules
/// - Expressive: Rules read like natural language
/// - Async: Supports async validation (e.g., check if email exists)
/// 
/// VALIDATION vs BUSINESS RULES:
/// - Validation: Structural correctness (format, length, required)
/// - Business Rules: Domain logic (can't subscribe twice, user must exist)
/// 
/// Validation happens in the API layer (before Provider).
/// Business rules happen in the Provider layer.
/// </remarks>
public class RegisterUserRequestValidator : AbstractValidator<RegisterUserRequest>
{
    public RegisterUserRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("Invalid email format.")
            .MaximumLength(320).WithMessage("Email cannot exceed 320 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(AppConstants.MinPasswordLength)
                .WithMessage($"Password must be at least {AppConstants.MinPasswordLength} characters.")
            .MaximumLength(128).WithMessage("Password cannot exceed 128 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one digit.")
            .Matches(@"[\W_]").WithMessage("Password must contain at least one special character.");

        RuleFor(x => x.DisplayName)
            .MaximumLength(100).WithMessage("Display name cannot exceed 100 characters.")
            .When(x => !string.IsNullOrEmpty(x.DisplayName));
    }
}

