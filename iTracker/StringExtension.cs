using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace iTracker
{
    public static class StringExtension
    {
        /// <summary>
        /// Get Strings between Given two string
        /// </summary>
        /// <param name="text"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static string Between(this string text, string left, string right)
        {
            int beginIndex = text.IndexOf(left, StringComparison.Ordinal); // find occurence of left delimiter
            if (beginIndex == -1)
                return string.Empty; // or throw exception?

            beginIndex += left.Length;
            
            int endIndex = text.IndexOf(right, beginIndex, StringComparison.Ordinal); // find occurence of right delimiter
            if (endIndex == -1)
                return string.Empty; // or throw exception?

            return text.Substring(beginIndex, endIndex - beginIndex).Trim();
        }

        /// <summary>
        /// Convert string to int
        /// </summary>
        /// <param name="arg"></param>
        /// <returns></returns>
        public static int ToInt(this string arg)
        {
            if (int.TryParse(arg, out var result))
            {
                return result;
            }
            return 0;
        }

        /// <summary>
        /// Get string after the input string
        /// </summary>
        /// <param name="value"></param>
        /// <param name="a"></param>
        /// <returns></returns>
        public static string After(this string value, string a)
        {
            int posA = value.LastIndexOf(a, StringComparison.Ordinal);
            if (posA == -1)
            {
                return "";
            }
            int adjustedPosA = posA + a.Length;
            if (adjustedPosA >= value.Length)
            {
                return "";
            }
            return value.Substring(adjustedPosA);
        }

        /// <summary>
        /// Get only strings between given two strings 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="left"></param>
        /// <param name="right"></param>
        /// <returns></returns>
        public static string ExtractOnlyStringsBetween(this string value, string left, string right)
        {
            string between = Between(value, left, right);
            return string.Join(null, Regex.Split(between, "[\\d]"));
        }
    }
}
