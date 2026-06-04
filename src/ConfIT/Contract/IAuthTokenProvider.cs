namespace ConfIT.Contract;

public interface IAuthTokenProvider
{
    string HeaderKey() => "Authorization";
    string Token();
}