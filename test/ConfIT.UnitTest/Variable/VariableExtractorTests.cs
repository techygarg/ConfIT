namespace ConfIT.UnitTest.Variable
{
    public class VariableExtractorTests
    {
        private static HttpResponseMessage CreateResponse(
            HttpStatusCode statusCode,
            string bodyJson,
            Dictionary<string, string>? headers = null)
        {
            var response = new HttpResponseMessage(statusCode);
            response.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
            if (headers != null)
                foreach (var (key, value) in headers)
                    response.Headers.TryAddWithoutValidation(key, value);
            return response;
        }

        [Fact]
        public void Extract_BodyField_SetsVariableInStore()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.Created, """{"id":"abc-123","name":"Alice"}""");
            var body = JToken.Parse("""{"id":"abc-123","name":"Alice"}""");

            // When
            VariableExtractor.Extract("ShouldCreateUser", response, body,
                new Dictionary<string, string> { { "userId", "$.body.id" } }, store);

            // Then
            store.Resolve("userId").Value<string>().Should().Be("abc-123");
        }

        [Fact]
        public void Extract_NestedBodyField_SetsVariable()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.OK, """{"address":{"city":"London"}}""");
            var body = JToken.Parse("""{"address":{"city":"London"}}""");

            // When
            VariableExtractor.Extract("ShouldGetUser", response, body,
                new Dictionary<string, string> { { "city", "$.body.address.city" } }, store);

            // Then
            store.Resolve("city").Value<string>().Should().Be("London");
        }

        [Fact]
        public void Extract_ResponseHeader_SetsVariable()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.Created, "{}",
                new Dictionary<string, string> { { "X-Request-Id", "req-abc" } });
            var body = JToken.Parse("{}");

            // When — header keys are stored lowercase; use bracket notation for names containing dashes
            VariableExtractor.Extract("ShouldCreateUser", response, body,
                new Dictionary<string, string> { { "requestId", "$.headers['x-request-id']" } }, store);

            // Then
            store.Resolve("requestId").Value<string>().Should().Be("req-abc");
        }

        [Fact]
        public void Extract_StatusCode_SetsNumericVariable()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.Created, "{}");
            var body = JToken.Parse("{}");

            // When
            VariableExtractor.Extract("ShouldCreateUser", response, body,
                new Dictionary<string, string> { { "status", "$.statusCode" } }, store);

            // Then
            store.Resolve("status").Value<int>().Should().Be(201);
        }

        [Fact]
        public void Extract_NullSpec_DoesNothing()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.OK, "{}");
            var body = JToken.Parse("{}");

            // When
            var act = () => VariableExtractor.Extract("ShouldGetUser", response, body, null, store);

            // Then
            act.Should().NotThrow();
        }

        [Fact]
        public void Extract_EmptySpec_DoesNothing()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.OK, "{}");
            var body = JToken.Parse("{}");

            // When
            var act = () => VariableExtractor.Extract("ShouldGetUser", response, body,
                new Dictionary<string, string>(), store);

            // Then
            act.Should().NotThrow();
        }

        [Fact]
        public void Extract_PathMatchesNothing_ThrowsInvalidOperationException()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.OK, """{"name":"Alice"}""");
            var body = JToken.Parse("""{"name":"Alice"}""");

            // When
            var act = () => VariableExtractor.Extract("ShouldGetUser", response, body,
                new Dictionary<string, string> { { "userId", "$.body.nonExistentField" } }, store);

            // Then
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*nonExistentField*userId*");
        }

        [Fact]
        public void Extract_NumberType_PreservesType()
        {
            // Given
            var store = new VariableStore();
            var response = CreateResponse(HttpStatusCode.OK, """{"score":42}""");
            var body = JToken.Parse("""{"score":42}""");

            // When
            VariableExtractor.Extract("ShouldGetUser", response, body,
                new Dictionary<string, string> { { "score", "$.body.score" } }, store);

            // Then
            var result = store.Resolve("score");
            result.Type.Should().Be(JTokenType.Integer);
            result.Value<int>().Should().Be(42);
        }
    }
}
