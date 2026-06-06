using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ConfIT.Runner.Mock;

internal static class BuilderExtension
{
    public static IRequestBuilder WithBodyIfProvided(this IRequestBuilder builder, JToken body)
    {
        if (body != null && body.Type != JTokenType.Null)
            builder.WithBody(new JsonMatcher(MatchBehaviour.AcceptOnMatch, body, true, true));

        return builder;
    }

    public static IRequestBuilder WithQueryParams(this IRequestBuilder builder, Dictionary<string, string> queryParams)
    {
        if (queryParams is { Count: > 0 })
            foreach (var (key, value) in queryParams)
                builder.WithParam(key.Trim(), new ExactMatcher(value.Trim()));

        return builder;
    }

    public static IRequestBuilder WithHeaders(this IRequestBuilder builder, Dictionary<string, string> headers)
    {
        if (headers is { Count: > 0 })
            foreach (var (key, value) in headers)
                builder.WithHeader(key.Trim(), new ExactMatcher(value.Trim()));

        return builder;
    }

    public static IResponseBuilder WithBodyIfProvided(this IResponseBuilder builder, JToken body)
    {
        if (body != null && body.Type != JTokenType.Null)
            builder.WithBody(body.ToString());

        return builder;
    }

    public static IResponseBuilder WithHeadersIfProvided(this IResponseBuilder builder,
        Dictionary<string, string> headers)
    {
        if (headers is { Count: > 0 })
            foreach (var (key, value) in headers)
                builder.WithHeader(key.Trim(), value.Trim());

        return builder;
    }
}
