using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Taxonomy;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PRFT.SharePoint.Extensions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Overload for ParseDateTokens and ParseIdTokens.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <returns></returns>
        public static string ParseSearchTokens(this string str)
        {
            return str.ParseGuidTokens().ParseDateTokens().ParseIdTokens();
        }

        /// <summary>
        /// Parse all {guid} patterns and replace with a new GUID.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <returns></returns>
        public static string ParseGuidTokens(this string str)
        {
            var pattern = @"{guid}";
            return str.ParseMatches(pattern, delegate (Capture capture, string parsedString)
            {
                return parsedString.Remove(capture.Index, capture.Length).Insert(capture.Index, Guid.NewGuid().ToString());
            });
        }

        /// <summary>
        /// Parse all {date} tokens and replace with the current UTC time.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <returns></returns>
        public static string ParseDateTokens(this string str)
        {
            //2015-10-09T14:10:41.307
            const string format = "yyyy-MM-ddTHH:mm:ss";
            var pattern = @"{date}";
            return str.ParseMatches(pattern, delegate (Capture capture, string parsedString)
            {
                return parsedString.Remove(capture.Index, capture.Length).Insert(capture.Index, DateTime.UtcNow.ToString(format, CultureInfo.InvariantCulture));
            });
        }

        /// <summary>
        /// Parse all {id} tokenss and replace with a random Int32 up to the maximum value allowed.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <returns></returns>
        public static string ParseIdTokens(this string str)
        {
            var random = new Random();
            var pattern = @"{id}";
            return str.ParseMatches(pattern, delegate (Capture capture, string parsedString)
            {
                return parsedString.Remove(capture.Index, capture.Length).Insert(capture.Index, random.Next(Int32.MaxValue).ToString());
            });
        }

        /// <summary>
        /// Parse {anchorid:*} tokens and replace with the id from the termstore.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <param name="context"></param>
        /// <returns></returns>
        public static string ParseAnchorTermTokens(this string str, ClientRuntimeContext context)
        {
            TaxonomySession session = TaxonomySession.GetTaxonomySession(context);

            var termStore = session.GetDefaultSiteCollectionTermStore();
            context.Load(termStore);
            context.ExecuteQueryRetry();

            if (!termStore.ServerObjectIsNull.Value)
            {
                var pattern = @"{anchorid:[^}]*}";
                str = str.ParseMatches(pattern, delegate (Capture capture, string parsedString)
                {
                    string[] properties = Regex.Replace(capture.Value, @"[{}]", string.Empty).Split(':');

                    var group = termStore.Groups.GetByName(properties[1]);
                    var termset = group.TermSets.GetByName(properties[2]);
                    var term = termset.Terms.GetByName(properties[properties.Length - 1]);
                    context.Load(term, t => t.Id);
                    context.ExecuteQueryRetry();

                    return parsedString.Remove(capture.Index, capture.Length).Insert(capture.Index, term.Id.ToString());
                });
            }

            return str;
        }

        /// <summary>
        /// Parses matches for a given string, pattern, and delegate function.
        /// </summary>
        /// <param name="str">String to parse.</param>
        /// <param name="pattern">Pattern to look for.</param>
        /// <param name="matchItems">Function to execute on any matches.</param>
        /// <returns></returns>
        private static string ParseMatches(this string str, string pattern, Func<Capture, string, string> matchItems)
        {
            Match match = Regex.Match(str, pattern, RegexOptions.IgnoreCase);
            while (match.Success)
            {
                var capture = match.Groups[0].Captures[0];
                try
                {
                    str = matchItems(capture, str);
                    match = Regex.Match(str, pattern);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Unable to parse Token '{0}'.", capture.Value);
                    throw ex;
                }
            }

            return str;
        }
    }
}