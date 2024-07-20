using Ecocell.Exception;
using Ecocell.Exception.ExceptionBase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ecocell.API.Filters;

public class ExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if(context.Exception is NotFoundException)
        {
            HandleProjectException(context);
        }
        else
        {
            ThrowUnknowException(context);
        }
    }

    private static void HandleProjectException(ExceptionContext context)
    {
        if(context.Exception is EcocellException)
        {
            var ecocellException = (EcocellException)context.Exception;

            context.HttpContext.Response.StatusCode = (int)ecocellException.GetStatusCode();
            context.Result = new ObjectResult(ecocellException.Message);
        }
    }

    private static void ThrowUnknowException(ExceptionContext context)
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Result = new ObjectResult(ResourceErrorMessages.UNKNOWN_ERROR);
    }
}
