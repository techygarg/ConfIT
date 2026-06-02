namespace ConfIT.Server.Dto;

public class TestCase
{
    public List<string> Tags { get; set; }
    public TestMock? Mock { get; set; }
    public TestApi Api { get; set; }

    public TestCase Initialize(string requestFolder, string responseFolder)
    {
        Api.Initialize(requestFolder, responseFolder);
        Mock?.Interactions?.ForEach(interaction => interaction.Initialize(requestFolder, responseFolder));
        return this;
    }
}