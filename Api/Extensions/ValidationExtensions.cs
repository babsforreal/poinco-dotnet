using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;

namespace Api.Extensions;

public static class ValidationExtensions
{
    // Factorise le ValidationResult -> ModelState -> ValidationProblem répété
    // dans chaque Create(). Retourne null quand la validation passe.
    public static ActionResult? ToProblem(this ValidationResult result, ControllerBase controller)
    {
        if (result.IsValid) return null;

        foreach (var error in result.Errors)
            controller.ModelState.AddModelError(error.PropertyName, error.ErrorMessage);
        return controller.ValidationProblem(controller.ModelState);
    }
}
