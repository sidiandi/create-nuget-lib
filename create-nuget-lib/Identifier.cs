using Amg.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace gt
{
    class Identifier
    {
        public static string CsharpClassName(string text)
        {
            var p = Regex.Split(text, @"\W");
            return p.Select(FirstUpperThenLower).Join(String.Empty);
        }

        public static string FirstUpperThenLower(string text)
        {
            var c = Math.Min(text.Length, 1);
            return text.Substring(0, c).ToUpper()
                + text.Substring(c, Math.Max(text.Length - c, 0));
        }
    }
}
