using System.Collections.Generic;

namespace ConfIT.Extension
{
    public static class DictionaryExtensions
    {
        public static string DictionaryToString(this Dictionary<string, string> dictionary)
        {
            var output = "{";
            foreach (var (key, value) in dictionary)
                output += key + " : " + value + ", ";

            return output.TrimEnd(',', ' ') + "}";
        }
       
    }
}