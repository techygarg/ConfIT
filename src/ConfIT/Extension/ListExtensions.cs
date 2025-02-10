using System.Collections.Generic;

namespace ConfIT.Extension
{
    public static class ListExtensions
    {
        public static string ListToString(this List<string> list)
        {
            var output = "{";
            foreach (var item in list)
                output += item + ", ";

            return output.TrimEnd(',', ' ') + "}";
        }
       
    }
}