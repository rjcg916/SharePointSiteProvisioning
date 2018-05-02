using Microsoft.SharePoint.Client;
using System;
using System.Xml.Linq;
//using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;

namespace PRFT.SharePoint
{
    class SubSites
    {

        /// <summary>
        /// Creates lists configured in settings.xml pnp:lists.
        /// Will also setup content types.
        /// Can create list items.
        /// </summary>
        /// <param name="clientContext"></param>
        private static void CreateLists(XNamespace _NameSpace, TokenParser _TokenParser, XElement _Branding, ClientContext clientContext, string listSpecifications)
        {
            foreach (var listInstance in _Branding.GetDescendants(listSpecifications, _NameSpace + "ListInstance"))
            {
                clientContext.CreateList(listInstance, _NameSpace, _TokenParser);
            }
        }

        public static void CreateSubSites(XNamespace _NameSpace, TokenParser _TokenParser, XElement _Branding, string siteUrl, System.Net.ICredentials creds)
        {

            Console.WriteLine("Creating Sub-Sites . . . ");

            foreach (var site in _Branding.GetDescendants("sites", "site"))
            {

                string web = site.GetAttributeValue(_TokenParser, "web");
                string title = site.GetAttributeValue(_TokenParser, "title");
                string leafUrl = site.GetAttributeValue(_TokenParser, "leafUrl");
                string description = site.GetAttributeValue(_TokenParser, "description");
                string template = site.GetAttributeValue(_TokenParser, "template");
                int language = site.GetAttributeValue<int>(_TokenParser, "language");
                bool inheritPermissions = Convert.ToBoolean(site.GetAttributeValue(_TokenParser, "inheritpermissions"));
                bool inheritNavigation = Convert.ToBoolean(site.GetAttributeValue(_TokenParser, "inheritnavigation"));
                string lists = site.GetAttributeValue(_TokenParser, "lists");

                //pre-pend site collection path
                if (String.IsNullOrEmpty(web))
                    web = siteUrl;
                else
                    web = siteUrl + "/" + web;

                // create new Sub-site after testing for existence
                Web newWeb = null;
                using (ClientContext clientContext = new ClientContext(web))
                {
                    clientContext.Credentials = creds;
                    clientContext.RequestTimeout = 10 * 60 * 1000;
                    clientContext.Load(clientContext.Web);
                    clientContext.ExecuteQueryRetry();

                    string newWebUrl = clientContext.Web.Url + "/" + leafUrl;
                    if (clientContext.WebExistsFullUrl(newWebUrl))
                    {
                        Console.WriteLine("Sub-Site {0} already exists, skipping provisioning.", leafUrl);
                    }
                    else
                    {
                        Console.WriteLine("Creating Sub-Site {0} . . .", leafUrl);

                        newWeb = clientContext.Web.CreateWeb(title, leafUrl, description, template, language, inheritPermissions, inheritNavigation);

                        clientContext.Load(newWeb, w => w.Lists);
                        clientContext.ExecuteQueryRetry();

                        Microsoft.SharePoint.Client.File welcomePageFile = newWeb.Lists.GetByTitle("Pages").GetItemById(1).File;
                        welcomePageFile.Publish("Initial Publish");
                        clientContext.Load(welcomePageFile);
                        clientContext.ExecuteQueryRetry();

                        if (!inheritPermissions)
                            SitePermissions.ApplyPermissions(clientContext, newWeb);

                    }

                    if (!String.IsNullOrEmpty(lists))
                    {
                        CreateLists(_NameSpace, _TokenParser, _Branding, clientContext, lists);
                    }
                }

            }

        }
    }
}
