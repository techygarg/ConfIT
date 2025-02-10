using Newtonsoft.Json;

namespace User.Api.Error
{
    public class ErrorResponse
    {
        public ErrorDescription Error { get; set; }

        public override string ToString() => 
            JsonConvert.SerializeObject(this);
    }
}