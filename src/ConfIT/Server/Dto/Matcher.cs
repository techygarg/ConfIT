using System.Collections.Generic;

namespace ConfIT.Server.Dto
{
    public class Matcher
    {
        public List<string> Ignore { get; set; }
        public Dictionary<string, string> Pattern { get; set; }
    }
}