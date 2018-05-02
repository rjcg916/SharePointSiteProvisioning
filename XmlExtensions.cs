using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
//using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;

namespace PRFT.SharePoint
{
    public static class XmlExtensions
    {
        /// <summary>
        /// String overload for GetAttributeValue<T>
        /// </summary>
        /// <param name="element">XML element to parse.</param>
        /// <param name="tokenParser"></param>
        /// <param name="name">Attribute name to get as a string.</param>
        /// <returns></returns>
        public static string GetAttributeValue(this XElement element, TokenParser tokenParser, string name)
        {
            return GetAttributeValue<string>(element, tokenParser, name);
        }

        /// <summary>
        /// Returns an attribute value cast as T.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element">XML element to parse.</param>
        /// <param name="tokenParser"></param>
        /// <param name="name">Attribute name to get as T.</param>
        /// <returns></returns>
        public static T GetAttributeValue<T>(this XElement element, TokenParser tokenParser, string name)
        {
            var result = default(T);
            var attribute = element.Attribute(name);

            if (attribute != null)
            {
                var parsedString = tokenParser.ParseString(attribute.Value);

                if (typeof(T).Equals(typeof(int)))
                {
                    int resultInt = default(int);
                    int.TryParse(parsedString, out resultInt);
                    result = (T)Convert.ChangeType(resultInt, typeof(T));
                }
                else if (typeof(T).Equals(typeof(bool)))
                {
                    bool resultBool = default(bool);
                    bool.TryParse(parsedString, out resultBool);
                    result = (T)Convert.ChangeType(resultBool, typeof(T));
                }
                else if (typeof(T).Equals(typeof(Uri)))
                {
                    Uri resultUri = new Uri(parsedString);
                    result = (T)Convert.ChangeType(resultUri, typeof(T));
                }
                else
                {
                    result = (T)Convert.ChangeType(parsedString, typeof(T));
                }
            }

            return result;
        }

        /// <summary>
        /// Returns an attribute value as an enum. 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="element">XML element to parse.</param>
        /// <param name="tokenParser"></param>
        /// <param name="name">Name of the attribute to return as an enum.</param>
        /// <returns></returns>
        public static T GetEnumAttributeValue<T>(this XElement element, TokenParser tokenParser, string name) where T: struct
        {
            var result = default(T);
            var attribute = element.Attribute(name);

            if (attribute != null)
            {
                var parsedString = tokenParser.ParseString(attribute.Value);

                T resultEnum = default(T);
                Enum.TryParse<T>(parsedString, true, out resultEnum);
                result = (T)Convert.ChangeType(resultEnum, typeof(T));
            }

            return result;
        }

        /// <summary>
        /// Get an XML element as a descendant of the parent.
        /// </summary>
        /// <param name="element">XML element to parse.</param>
        /// <param name="descendants">List of descendants to parse. Retuns the final element.</param>
        /// <returns></returns>
        public static IEnumerable<XElement> GetDescendants(this XElement element, params XName[] descendants)
        {
            IEnumerable<XElement> result = new XElement[0];

            if (element != null && descendants.Any())
            {
                var root = element;

                for (int i = 0; i < descendants.Length - 1; i++)
                {
                    root = root.Element(descendants[i]);
                    if (root == null)
                    {
                        break;
                    }
                }

                if (root != null)
                {
                    result = root.Descendants(descendants.Last());
                }
            }

            return result;
        }
    }
}
