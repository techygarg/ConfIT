namespace ConfIT.Server.Dto;

public class TestMock
{
    public List<MockInteraction> Interactions { get; set; } = new();
}

public class MockInteraction : ApiInteraction { }
