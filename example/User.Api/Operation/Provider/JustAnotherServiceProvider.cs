using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace User.Api.Operation.Provider
{
    public class JustAnotherServiceProvider : IJustAnotherServiceProvider
    {
        private readonly JustAnotherServiceProviderSettings _settings;
        private readonly HttpClient _clinet;

        public JustAnotherServiceProvider(JustAnotherServiceProviderSettings settings)
        {
            _settings = settings;
            _clinet = new HttpClient();
        }

        public async Task<EmailResponse> VerifyByGet(string email)
        {
            var response = await _clinet.GetAsync($"{_settings.Url}/api/demo/{email}");
            return JsonConvert.DeserializeObject<EmailResponse>(await response.Content.ReadAsStringAsync());
        }

        public async Task<EmailResponse> VerifyByPost(string email)
        {
            var response = await _clinet.PostAsync($"{_settings.Url}/api/demo", JsonContent.Create(new { email }));
            return JsonConvert.DeserializeObject<EmailResponse>(await response.Content.ReadAsStringAsync());
        }

        public async Task<EmailResponse> VerifyByGetQuery(string email)
        {
            var response = await _clinet.GetAsync($"{_settings.Url}/api/demo?email={email}");
            return JsonConvert.DeserializeObject<EmailResponse>(await response.Content.ReadAsStringAsync());
        }
    }
}