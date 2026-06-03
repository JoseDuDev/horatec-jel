using Horafy.Shared;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace Horafy.API.Controllers.Base;

/// <summary>
/// Controller base que encapsula o ISender do MediatR e o mapeamento
/// de Result para IActionResult, evitando repetição nos controllers filhos.
/// </summary>
[ApiController]
[Route("api/v{version:apiVersion}/[controller]")]
[Produces("application/json")]
public abstract class ApiControllerBase(ISender sender) : ControllerBase
{
    protected readonly ISender Sender = sender;

    /// <summary>
    /// Mapeia um Result para IActionResult seguindo as convenções HTTP.
    /// </summary>
    protected IActionResult ToActionResult(Result result) =>
        result.IsSuccess
            ? Ok()
            : result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(ToProblem(result.Error)),
                ErrorType.Validation => BadRequest(ToProblem(result.Error)),
                ErrorType.Conflict => Conflict(ToProblem(result.Error)),
                ErrorType.Unauthorized => Unauthorized(ToProblem(result.Error)),
                _ => StatusCode(StatusCodes.Status500InternalServerError, ToProblem(result.Error))
            };

    protected IActionResult ToActionResult<T>(Result<T> result) =>
        result.IsSuccess
            ? Ok(result.Value)
            : result.Error.Type switch
            {
                ErrorType.NotFound => NotFound(ToProblem(result.Error)),
                ErrorType.Validation => BadRequest(ToProblem(result.Error)),
                ErrorType.Conflict => Conflict(ToProblem(result.Error)),
                ErrorType.Unauthorized => Unauthorized(ToProblem(result.Error)),
                _ => StatusCode(StatusCodes.Status500InternalServerError, ToProblem(result.Error))
            };

    protected IActionResult ToCreatedResult<T>(Result<T> result, string routeName, object routeValues) =>
        result.IsSuccess
            ? CreatedAtRoute(routeName, routeValues, result.Value)
            : ToActionResult(result);

    private ProblemDetails ToProblem(Error error) => new()
    {
        Title = error.Code,
        Detail = error.Description,
        Status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError
        },
        Instance = HttpContext.Request.Path,
        Extensions = { ["traceId"] = HttpContext.TraceIdentifier }
    };
}
