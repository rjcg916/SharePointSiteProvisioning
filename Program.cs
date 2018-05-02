using System;
using System.Security;
using System.Linq;
using System.Xml.Linq;
using System.Net;
using System.Configuration;
using System.IO;
using System.Collections.Generic;
using Microsoft.SharePoint.Client;
using Microsoft.SharePoint.Client.Publishing;
using OfficeDevPnP.Core.Enums;
using OfficeDevPnP.Core.Framework.Provisioning.Model;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;
using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers.CustomTokenDefinitions;
using PRFT.SharePoint.Extensions;
using PRFT.SharePoint.PnP.Entities;

namespace PRFT.SharePoint
{

    enum Mode { activate, deactivate, activateIncremental, debug, invalid, export }

    class Program
    {
        internal static char[] _TrimChars = new char[] { '/' };
        internal static TokenParser _TokenParser;
        internal static XNamespace _NameSpace;
        internal static XElement _Branding;

        static void Main(string[] args)
        {
            var mode = GetMode(args);

            try
            {
                //check to ensure there's at least one argument
                if (mode == Mode.invalid || args.Length > 2)
                {
                    DisplayUsage();
                    return;
                }

                if (mode == Mode.debug)
                {
                    //if we're in debug, to to the project directory and read the Branding files and settings.xml from there
                    var dir = System.IO.Directory.GetCurrentDirectory();
                    dir = dir.Substring(0, dir.IndexOf("\\bin"));
                    System.IO.Directory.SetCurrentDirectory(dir);
                }



                //code used to get the application to work for SP ADFS
                var siteUrl = String.Concat(GetConfigurationValue("url").TrimEnd(_TrimChars), "/", GetConfigurationValue("site").TrimEnd(_TrimChars));
                if (args.Length > 1 && args[1].Equals("online", StringComparison.OrdinalIgnoreCase))
                {
                    OfficeDevPnP.Core.AuthenticationManager am = new OfficeDevPnP.Core.AuthenticationManager();
                    using (ClientContext clientContext = am.GetADFSUserNameMixedAuthenticatedContext(siteUrl, GetConfigurationValue("username"), GetConfigurationValue("password"),
                        GetConfigurationValue("domain"), GetConfigurationValue("adfsserver"), GetConfigurationValue("adfsurn")))
                    {
                        Execute(siteUrl, clientContext, mode);
                    }
                }
                else
                {
                    using (ClientContext clientContext = new ClientContext(siteUrl))
                    {
                        clientContext.Credentials = new NetworkCredential(GetConfigurationValue("username"), GetConfigurationValue("password"), GetConfigurationValue("domain"));
                        Execute(siteUrl, clientContext, mode);
                    }
                }

                Console.WriteLine("Done!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Critical Error: {0}", ex.Message);
            }

            if (mode != Mode.debug)
            {
                Console.ReadLine();
            }
            else
            {
                Console.WriteLine("Closing in 2 seconds...");
                System.Threading.Thread.Sleep(2000);
            }
        }

        private static void Execute(String siteUrl, ClientContext clientContext, Mode mode)
        {
            var lastTimeRun = DateTime.MinValue;
            if (mode == Mode.debug || mode == Mode.activateIncremental)
            {
                lastTimeRun = GetLastRun();
            }

            clientContext.Load(clientContext.Web);
            clientContext.ExecuteQueryRetry();

            var doc = XDocument.Load("settings.xml");
            if (!doc.Root.HasAttributes || !doc.Root.FirstAttribute.IsNamespaceDeclaration)
            {
                Console.WriteLine("PNP namespace missing from the root element.");
                return;
            }

            _NameSpace = (XNamespace)doc.Root.FirstAttribute.Value;
            _Branding = doc.Element("branding");

            //Create token parser and add custom tokens
            _TokenParser = new TokenParser(clientContext.Web, new ProvisioningTemplate());
            _TokenParser.AddToken(new SiteCollectionUrlToken(clientContext.Web));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "Main Search"));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "Local People Results", new Guid("b09a7990-05ea-4af9-81ef-edfab16c4e31")));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "SPFarmId"));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "SPSiteId"));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "SPSiteSubscriptionId"));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "SPWebId"));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "DatabaseId"));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "Local SharePoint", new Guid("fa947043-6046-4f97-9714-40d4c113963d")));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "OpenSearch", new Guid("3a17e140-1574-4093-bad6-e19cdf1c0121")));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "People Search Results", new Guid("e4bcc058-f133-4425-8ffc-1d70596ffd33")));
            _TokenParser.AddToken(new CustomGuidTokenDefinition(clientContext.Web, "Blank", new Guid("00000000-0000-0000-0000-000000000000")));
            foreach (var template in Enum.GetValues(typeof(ListTemplateType)).Cast<ListTemplateType>())
            {
                _TokenParser.AddToken(new ListTypeTokenDefinition(clientContext.Web, template.ToString(), (int)template));
            }

            switch (mode)
            {
                case Mode.export:
                    ExportSearch(clientContext);
                    break;
                case Mode.activate:
                case Mode.activateIncremental:
                case Mode.debug:

                    // create managed metadata here
                    //        string termGroupName = GetConfigurationValue("termGroupName");
                    //        TermSets.CreateTermSets( _TokenParser, _Branding, clientContext, termGroupName); 
                    

                   //                    CreateSiteColumns(clientContext);
                                       CreateContentTypes(clientContext);
                    //                    UploadFiles(clientContext, lastTimeRun);
                    //                    SetSearchConfiguration(clientContext);
                    //                    UploadMasterPages(clientContext, lastTimeRun);
                    //                    UploadPageLayouts(clientContext, lastTimeRun);

                    //                           SitePermissions.CreatePermissions(clientContext);
                    //                                          SubSites.CreateSubSites(_NameSpace, _TokenParser, _Branding, siteUrl, clientContext.Credentials);
                    //                    CreatePages(clientContext);
                    //                    AddNavigationNodes(clientContext);
                    //                    UpdateDeviceChannels(clientContext);
                    //                    CreateImageRenditions(clientContext);
                    //                    SaveTimeStamp();

                    break;
                case Mode.deactivate:
                    RemoveFiles(clientContext);
                    RemoveMasterPages(clientContext);
                    RemovePageLayouts(clientContext);
                    RemoveLists(clientContext);
                    break;
            }
        }



   


        /// <summary>
        /// Gets a configuration value from app.config
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private static string GetConfigurationValue(string key)
        {

            return ConfigurationManager.AppSettings[key];
        }

        #region "export search functions"

        /// <summary>
        /// Exports the search configuration for the site configured in app.config
        /// </summary>
        /// <param name="clientContext"></param>
        private static void ExportSearch(ClientContext clientContext)
        {
            clientContext.ExportSearchSettings(Path.Combine(Environment.ExpandEnvironmentVariables("%userprofile%"), "Documents\\SearchSettings.txt"),
                Microsoft.SharePoint.Client.Search.Administration.SearchObjectLevel.SPSite);
        }

        #endregion

        #region "activate branding functions"

        /// <summary>
        /// Creates site columns that are configured in sitecolumns.xml
        /// </summary>
        /// <param name="clientContext"></param>
        private static void CreateSiteColumns(ClientContext clientContext)
        {

            var xd = XDocument.Load("SiteColumns.xml");

            // Perform the action field creation
            clientContext.Web.CreateFieldsFromXML(xd, _TokenParser);
        }

        /// <summary>
        /// Creates content types that are configured in settings.xml pnp:ContentTypes.
        /// Will just add new content types. Will not delete or modify existing content types.
        /// </summary>
        /// <param name="clientContext"></param>
        private static void CreateContentTypes(ClientContext clientContext)
        {
            foreach (var contentType in _Branding.GetDescendants(_NameSpace + "ContentTypes", _NameSpace + "ContentType"))
            {
                try
                {

                    Console.WriteLine("{0} content type processing.", contentType.GetAttributeValue(_TokenParser, "Name"));

                    var doc = XDocument.Parse(contentType.ToString());
                    doc.Root.Name = _NameSpace + doc.Root.Name.LocalName;

                    clientContext.Web.CreateContentTypeFromXML(doc);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            clientContext.ExecuteQueryRetry();
        }

        /// <summary>
        /// Uploads files configured in settings.xml files
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="lastRun"></param>
        private static void UploadFiles(ClientContext clientContext, DateTime lastRun)
        {
            foreach (var file in _Branding.GetDescendants("files", "file"))
            {
                var name = file.GetAttributeValue(_TokenParser, "name");
                var folder = file.GetAttributeValue(_TokenParser, "folder").TrimEnd(_TrimChars);
                var path = file.GetAttributeValue(_TokenParser, "path").TrimEnd(_TrimChars);

                //get the last modified time of the file
                var fileLastUpdated = System.IO.File.GetLastWriteTime(System.IO.Path.Combine("Branding\\Files\\", name));

                if (fileLastUpdated > lastRun)
                {
                    clientContext.UploadFile(name, folder, path);
                }

            }
        }

        /// <summary>
        /// Uploads master pages configured in settings.xml masterpages into the master page gallery.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="lastRun"></param>
        private static void UploadMasterPages(ClientContext clientContext, DateTime lastRun)
        {
            foreach (var masterpage in _Branding.GetDescendants("masterpages", "masterpage"))
            {
                var name = masterpage.GetAttributeValue(_TokenParser, "name");
                var folder = masterpage.GetAttributeValue(_TokenParser, "folder").TrimEnd(new char[] { '/' });
                var description = masterpage.GetAttributeValue(_TokenParser, "description");
                var setAsMaster = masterpage.GetAttributeValue<bool>(_TokenParser, "setAsMaster");
                var setAsSystemMaster = masterpage.GetAttributeValue<bool>(_TokenParser, "setAsSystemMaster");

                //get the last modified time of the file
                var fileLastUpdated = System.IO.File.GetLastWriteTime(System.IO.Path.Combine("Branding\\MasterPages\\", name));

                if (fileLastUpdated > lastRun)
                {
                    Console.WriteLine("Uploading master page {0} to {1}", name, folder);

                    clientContext.Web.DeployMasterPage(string.Concat("Branding\\MasterPages\\", name), name, description, "15", string.Empty, folder);
                    if (setAsMaster)
                    {
                        Console.WriteLine("Applying master page {0}", name);
                        clientContext.Web.SetCustomMasterPageByName(name);
                    }
                    if (setAsSystemMaster)
                    {
                        Console.WriteLine("Applying system master page {0}", name);
                        clientContext.Web.SetMasterPageByName(name);
                    }
                }
            }
        }

        /// <summary>
        /// Uploads page layouts configured in settings.xml htmlPagelayouts into the master page gallery.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="lastRun"></param>
        private static void UploadPageLayouts(ClientContext clientContext, DateTime lastRun)
        {
            foreach (var pagelayout in _Branding.GetDescendants("htmlPagelayouts", "htmlPagelayout"))
            {
                var name = System.IO.Path.GetFileName(pagelayout.GetAttributeValue(_TokenParser, "sourceFilePath"));
                var title = pagelayout.GetAttributeValue(_TokenParser, "title").TrimEnd(_TrimChars);
                var description = pagelayout.GetAttributeValue(_TokenParser, "description");
                var contentTypeId = pagelayout.GetAttributeValue(_TokenParser, "associatedContentTypeID");
                var folderHierarchy = pagelayout.GetAttributeValue(_TokenParser, "folderHierarchy");

                //get the last modified time of the file
                var fileLastUpdated = System.IO.File.GetLastWriteTime(name);

                if (fileLastUpdated > lastRun)
                {
                    BrandingHelper.UploadPageLayout(clientContext, name, folderHierarchy, title, contentTypeId);
                }
            }
        }



        /// <summary>
        /// Creates publishing pages configured in settings.xml pnp:PublishingPages.
        /// The publishings pages can be deployed to any web of the site configured in app.config.
        /// The page can be set as the welcome page.
        /// </summary>
        /// <param name="clientContext"></param>
        private static void CreatePages(ClientContext clientContext)
        {

            Console.WriteLine("Adding Pages . . . ");

            foreach (var publishingPage in _Branding.GetDescendants(_NameSpace + "PublishingPages", _NameSpace + "PublishingPage"))
            {
                var webUrl = publishingPage.GetAttributeValue(_TokenParser, "webUrl");
                var name = publishingPage.GetAttributeValue(_TokenParser, "name");
                var title = publishingPage.GetAttributeValue(_TokenParser, "title");
                var layout = publishingPage.GetAttributeValue(_TokenParser, "layout");
                var overwrite = publishingPage.GetAttributeValue<bool>(_TokenParser, "overwrite");
                var isWelcomePage = publishingPage.GetAttributeValue<bool>(_TokenParser, "isWelcomePage");

                Console.WriteLine("Adding Page {0} to Site {1}", name, webUrl);
                clientContext.Web.AddPublishingPage(name, layout, title, overwrite, isWelcomePage, webUrl);
            }
        }

    

    /// <summary>
    /// Uploads the search configuration configured in settings.xml pnp:SearchSettings for the site configured in app.config.
    /// </summary>
    /// <param name="clientContext"></param>
    private static void SetSearchConfiguration(ClientContext clientContext)
    {
        try
        {
            Console.WriteLine("Search settings processing.");

            var searchSettings = _Branding.Element(_NameSpace + "SearchSettings");
            if (searchSettings != null)
            {
                var xml = Convert.ToString(searchSettings.FirstNode);
                var parsedXml = _TokenParser.ParseString(xml.ParseSearchTokens(), new string[] { "~sitecollection", "~site" });
                clientContext.Site.SetSearchConfiguration(parsedXml);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
        }
    }

    /// <summary>
    /// Adds navigation nodes to search configured in settings.xml pnp:NavigationNodes.
    /// These nodes are added to all webs under the site configured in app.config.
    /// </summary>
    /// <param name="clientContext"></param>
    private static void AddNavigationNodes(ClientContext clientContext)
    {
        var navigationNodes = new List<NavigationNodeEntity>();
        foreach (var node in _Branding.GetDescendants(_NameSpace + "NavigationNodes", _NameSpace + "NavigationNode"))
        {
            var title = node.GetAttributeValue(_TokenParser, "Title");
            var url = node.GetAttributeValue<Uri>(_TokenParser, "Url");
            var parentTitle = node.GetAttributeValue(_TokenParser, "ParentTitle");
            var type = node.GetEnumAttributeValue<NavigationType>(_TokenParser, "Type");
            var external = node.GetAttributeValue<bool>(_TokenParser, "External");

            navigationNodes.Add(new NavigationNodeEntity()
            {
                Title = title,
                Uri = url,
                ParentTitle = parentTitle,
                NavigationType = type,
                IsExternal = external
            });
        }

        if (navigationNodes.Any())
        {
            ProcessWebNavigationNodes(clientContext, clientContext.Site.RootWeb, navigationNodes);
        }
    }

    /// <summary>
    /// Method used to parse the navigation nodes for a web and all the subwebs.
    /// </summary>
    /// <param name="clientContext"></param>
    /// <param name="webs"></param>
    /// <param name="navigationNodes">Navigation nodes to add.</param>
    private static void ProcessWebNavigationNodes(ClientContext clientContext, WebCollection webs, List<PnP.Entities.NavigationNodeEntity> navigationNodes)
    {
        clientContext.Load(webs);
        clientContext.ExecuteQueryRetry();

        foreach (var subWeb in webs)
        {
            ProcessWebNavigationNodes(clientContext, subWeb, navigationNodes);
        }
    }

    /// <summary>
    /// Processes the navigation nodes for a web.
    /// </summary>
    /// <param name="clientContext"></param>
    /// <param name="web"></param>
    /// <param name="navigationNodes">Navigation nodes to add.</param>
    private static void ProcessWebNavigationNodes(ClientContext clientContext, Web web, List<PnP.Entities.NavigationNodeEntity> navigationNodes)
    {
        clientContext.Load(web, s => s.ServerRelativeUrl, s => s.Navigation, s => s.Webs);
        clientContext.ExecuteQueryRetry();

        Console.WriteLine("Adding navigation nodes to {0}", web.ServerRelativeUrl);

        var typesToDelete = navigationNodes.Select(i => i.NavigationType).Distinct();
        foreach (var type in typesToDelete)
        {
            web.DeleteAllNavigationNodes(type);
        }

        foreach (var node in navigationNodes)
        {
            web.AddNavigationNode(node.Title, node.Uri, node.ParentTitle, node.NavigationType, node.IsExternal);
        }

        if (web.Webs.Any())
        {
            ProcessWebNavigationNodes(clientContext, web.Webs, navigationNodes);
        }
    }

    /// <summary>
    /// Updates device channels configured in settings.xml pnp:DeviceChannels for the site configured in app.config.
    /// Existing device channels with the same Channel Alias will be deleted first.
    /// </summary>
    /// <param name="clientContext"></param>
    private static void UpdateDeviceChannels(ClientContext clientContext)
    {
        clientContext.UpdateDeviceChannels(_Branding, _NameSpace, _TokenParser);
    }

    /// <summary>
    /// Updates image renditions configured in settings.xml pnp:ImageRenditions for the site configured in app.config.
    /// Deletes existing image renditions with the same name and different attributes first.
    /// </summary>
    /// <param name="clientContext"></param>
    private static void CreateImageRenditions(ClientContext clientContext)
    {
        var imageRenditions = SiteImageRenditions.GetRenditions(clientContext);
        clientContext.ExecuteQueryRetry();

        foreach (var node in _Branding.GetDescendants(_NameSpace + "ImageRenditions", _NameSpace + "ImageRendition"))
        {
            ImageRendition rendition = new ImageRendition()
            {
                Name = node.GetAttributeValue(_TokenParser, "Name"),
                Width = node.GetAttributeValue<int>(_TokenParser, "Width"),
                Height = node.GetAttributeValue<int>(_TokenParser, "Height")
            };

            var existing = imageRenditions.FirstOrDefault(i => i.Name.Equals(rendition.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null && (existing.Width != rendition.Width || existing.Height != rendition.Height))
            {
                imageRenditions.Remove(existing);
                imageRenditions.Add(rendition);
            }
            else if (existing == null)
            {
                imageRenditions.Add(rendition);
            }
        }

        SiteImageRenditions.SetRenditions(clientContext, imageRenditions);

        clientContext.ExecuteQueryRetry();
    }

    #endregion

    /// <summary>
    /// These functions are depreciated and not used.
    /// </summary>
    #region "deactivate _Branding functions"

    [Obsolete]
    private static void RemoveFiles(ClientContext clientContext)
    {
        var name = "";
        var folder = "";
        var path = "";
        foreach (var file in _Branding.GetDescendants("files", "file"))
        {
            name = file.GetAttributeValue(_TokenParser, "name");
            folder = file.GetAttributeValue(_TokenParser, "folder").TrimEnd(_TrimChars);
            path = file.GetAttributeValue(_TokenParser, "path").TrimEnd(_TrimChars);

            clientContext.RemoveFile(name, folder, path);
        }
        clientContext.RemoveFolder(folder, path);
    }

    [Obsolete]
    private static void RemoveMasterPages(ClientContext clientContext)
    {
        var name = "";
        var folder = "";
        foreach (var masterpage in _Branding.GetDescendants("masterpages", "masterpage"))
        {
            name = masterpage.GetAttributeValue(_TokenParser, "name");
            folder = masterpage.GetAttributeValue(_TokenParser, "folder").TrimEnd(new char[] { '/' });

            clientContext.RemoveMasterPage(name, folder);
        }
        clientContext.RemoveFolder(folder, "_catalogs/masterpage");
    }

    [Obsolete]
    private static void RemovePageLayouts(ClientContext clientContext)
    {
        foreach (var pagelayout in _Branding.GetDescendants("pagelayouts", "pagelayout"))
        {
            var name = pagelayout.GetAttributeValue(_TokenParser, "name");
            var folder = pagelayout.GetAttributeValue(_TokenParser, "folder").TrimEnd(_TrimChars);
            var publishingAssociatedContentType = pagelayout.GetAttributeValue(_TokenParser, "publishingAssociatedContentType");
            var title = pagelayout.GetAttributeValue(_TokenParser, "title");

            clientContext.RemovePageLayout(name, folder);
        }
    }

    [Obsolete]
    private static void RemoveLists(ClientContext clientContext)
    {
        foreach (var listInstance in _Branding.GetDescendants(_NameSpace + "lists", _NameSpace + "ListInstance"))
        {
            var title = listInstance.GetAttributeValue(_TokenParser, "Title");

            clientContext.RemoveList(title);
        }
    }

    #endregion

    #region "helper functions"

    [Obsolete]
    static SecureString GetPassword()
    {
        SecureString sStrPwd = new SecureString();

        try
        {
            Console.Write("SharePoint Password: ");

            for (ConsoleKeyInfo keyInfo = Console.ReadKey(true); keyInfo.Key != ConsoleKey.Enter; keyInfo = Console.ReadKey(true))
            {
                if (keyInfo.Key == ConsoleKey.Backspace)
                {
                    if (sStrPwd.Length > 0)
                    {
                        sStrPwd.RemoveAt(sStrPwd.Length - 1);
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                        Console.Write(" ");
                        Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
                    }
                }
                else if (keyInfo.Key != ConsoleKey.Enter)
                {
                    Console.Write("*");
                    sStrPwd.AppendChar(keyInfo.KeyChar);
                }

            }
            Console.WriteLine("");
        }
        catch (Exception e)
        {
            sStrPwd = null;
            Console.WriteLine(e.Message);
        }

        return sStrPwd;
    }

    [Obsolete]
    static string GetUserName()
    {
        string strUserName = string.Empty;
        try
        {
            Console.Write("SharePoint Username: ");
            strUserName = Console.ReadLine();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            strUserName = string.Empty;
        }
        return strUserName;
    }

    static void DisplayUsage()
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("Please specify 'activate' or 'deactivate' and optionally 'online'");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("Example 1 (SharePoint Online): \n Program.exe activate online");
        Console.WriteLine("Example 2 (SharePoint Online):  \n Program.exe deactivate online");
        Console.WriteLine("Example 3 (SharePoint On-premises):  \n Program.exe activate");
        Console.WriteLine("Example 4 (SharePoint On-premises):  \n Program.exe deactivate");
        Console.WriteLine("Example 5 (SharePoint Online):  \n Program.exe activateIncremental online");
        Console.WriteLine("Example 6 (SharePoint On-premises):  \n Program.exe activateIncremental");
        Console.ResetColor();
        Console.ReadLine();
    }

    static void SaveTimeStamp()
    {
        try
        {
            var timeStampFile = "lastrun.log";
            if (!System.IO.File.Exists(timeStampFile))
            {
                using (System.IO.File.Create(timeStampFile)) { }
            }

            System.IO.File.WriteAllLines(timeStampFile, new string[] { DateTime.Now.ToString() });
        }
        catch (Exception ex)
        {

        }
    }

    static DateTime GetLastRun()
    {
        var value = DateTime.MinValue;

        var timeStampFile = "lastrun.log";
        if (System.IO.File.Exists(timeStampFile))
        {
            var lines = System.IO.File.ReadAllLines(timeStampFile);
            if (lines.Length > 0)
            {
                var line1 = lines[0];

                DateTime.TryParse(line1, out value);
            }
        }

        return value;
    }

    static Mode GetMode(string[] args)
    {
        var result = Mode.invalid;

        if (args.Any())
        {
            var mode = args.First();
            Enum.TryParse<Mode>(mode, out result);
        }

        return result;
    }

    #endregion
}
}
