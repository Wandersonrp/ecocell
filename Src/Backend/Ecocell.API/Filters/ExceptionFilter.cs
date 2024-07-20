using Ecocell.Communication.Responses;
using Ecocell.Exception;
using Ecocell.Exception.ExceptionBase;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ecocell.API.Filters;

public class ExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if(context.Exception is EcocellException)
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
        var ecocellException = (EcocellException)context.Exception;

        context.HttpContext.Response.StatusCode = (int)ecocellException.GetStatusCode();

        var responseJson = new ResponseErrorJson(ecocellException.GetErrorMessages());

        context.Result = new ObjectResult(responseJson);
    }

    private static void ThrowUnknowException(ExceptionContext context)
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var responseJson = new ResponseErrorJson(new List<string>
        {
            ResourceErrorMessages.UNKNOWN_ERROR
        });

        context.Result = new ObjectResult(responseJson);
    }
}
