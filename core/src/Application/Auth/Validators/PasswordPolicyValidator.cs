namespace Nona.Application.Auth.Validators;

public static class PasswordPolicyValidator
{
    public const int MinimumLength = 8;

    public static IRuleBuilderOptions<T, string> PasswordPolicy<T>(
        this IRuleBuilderInitial<T, string> ruleBuilder)
    {
        return ruleBuilder
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Password is required")
            .MinimumLength(MinimumLength).WithMessage($"Password must be at least {MinimumLength} characters long")
            .Matches("[A-Z]").WithMessage("Password must contain at least one uppercase letter")
            .Matches("[0-9]").WithMessage("Password must contain at least one number")
            .Matches("[^A-Za-z0-9]").WithMessage("Password must contain at least one special character");
    }
}
