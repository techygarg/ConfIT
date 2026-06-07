namespace ConfIT.Model;

public class TestCase
{
    public List<string>? Depends { get; set; }
    public List<string>? Tags { get; set; }
    public TestMock? Mock { get; set; }
    public TestApi Api { get; set; }
}
