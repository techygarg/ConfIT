using HotChocolate;
using HotChocolate.Execution;
using User.Api.Error.Exceptions;

namespace User.Api.GraphQL
{
    public class UserErrorFilter : IErrorFilter
    {
        public IError OnError(IError error)
        {
            if (error.Exception is null)
                return error;

            var code = error.Exception switch
            {
                NotFoundException => "NOT_FOUND",
                BadRequestException => "BAD_REQUEST",
                _ => "INTERNAL_SERVER_ERROR"
            };

            return error.WithMessage(error.Exception.Message).SetExtension("code", code);
        }
    }
}
