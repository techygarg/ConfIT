using System;
using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using User.Api.Error.Exceptions;
using static System.String;

namespace User.Api.Error
{
    public class UserExceptionFilterAttribute : ExceptionFilterAttribute
    {
        public override void OnException(ExceptionContext context)
        {
            HandleException(context);
            base.OnException(context);
        }

        protected virtual void HandleException(ExceptionContext exceptionContext)
        {
            var exception = exceptionContext.Exception;

            var (httpCode, errorCode, message) = exception switch
            {
                ArgumentException _ => (HttpStatusCode.BadRequest, HttpStatusCode.BadRequest.ToString(), Empty),
                NotFoundException _ => (HttpStatusCode.NotFound, HttpStatusCode.NotFound.ToString(), Empty),
                BadRequestException _ => (HttpStatusCode.BadRequest, HttpStatusCode.BadRequest.ToString(), Empty),
                _ => (HttpStatusCode.InternalServerError, HttpStatusCode.InternalServerError.ToString(),
                    "Something went wrong")
            };

            WriteError(exceptionContext, CreateErrorResponse(httpCode, errorCode, message, exception));
        }

        private static ErrorResponse CreateErrorResponse(HttpStatusCode httpCode, string errorCode, string message,
            Exception exception)
        {
            var error = new ErrorResponse
            {
                Error = new ErrorDescription
                {
                    Status = httpCode,
                    Code = errorCode,
                    Description = IsNullOrWhiteSpace(message) ? exception.Message : message
                }
            };
            return error;
        }

        protected virtual void WriteError(ExceptionContext exceptionContext, ErrorResponse error)
        {
            exceptionContext.Result = new ObjectResult(error)
            {
                StatusCode = (int?)error.Error.Status
            };
        }
    }
}