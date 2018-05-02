using Microsoft.SharePoint.Client;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Xml.Linq;
using Microsoft.SharePoint.Client.Taxonomy;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;
//using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;
using System.Text.RegularExpressions;

namespace PRFT.SharePoint
{
    static class BrandingHelper
    {
        #region "activate branding functions"

        /// <summary>
        /// Uploads a file to a specific folder in SharePoint.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="name"></param>
        /// <param name="folder"></param>
        /// <param name="path"></param>
        public static void UploadFile(this ClientContext clientContext, string name, string folder, string path)
        {
            name = name.Replace("\\", "/");
            var web = clientContext.Web;
            var filePath = web.ServerRelativeUrl.TrimEnd(Program._TrimChars) + "/" + path + "/";

            Console.WriteLine("Uploading file {0} to {1}{2}", name, filePath, folder);
            EnsureFolders(web, filePath, folder, name);
            CheckOutFile(web, name, filePath, folder);
            var uploadFile = AddFile(web.Url, web, "Branding\\Files\\", name, filePath, folder);
            CheckInPublishAndApproveFile(uploadFile);
        }

        /// <summary>
        /// Takes a list XML configuration and creates the list in SharePoint.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="listInstance"></param>
        /// <param name="ns"></param>
        /// <param name="tokenParser"></param>
        public static void CreateList(this ClientContext clientContext, XElement listInstance, XNamespace ns, TokenParser tokenParser)
        {
            try
            {
                var title = listInstance.GetAttributeValue(tokenParser, "Title");
                var subWebUrl = listInstance.GetAttributeValue(tokenParser, "WebUrl");

                var subWeb = clientContext.Web;
                if (!string.IsNullOrEmpty(subWebUrl))
                {
                    subWeb = clientContext.Site.OpenWebFromFullUrl(subWebUrl);
                }

                var list = subWeb.GetListByTitle(title);
                if (list == null)
                {
                    Console.WriteLine("Creating list {0}.", title);

                    var templateType = listInstance.GetAttributeValue(tokenParser, "TemplateType");
                    var type = ListTemplateType.GenericList;
                    Enum.TryParse<ListTemplateType>(templateType, out type);

                    var trimmedTitle = Regex.Replace(title, @"\s+", ""); // Remove spaces so that the list URL has no spaces
                    list = subWeb.CreateList(type, trimmedTitle, false);
                    list.Title = title; // Update the list title since the URL has already been set in SharePoint
                    list.Update();

                    var removeExistingContentTypes = listInstance.GetAttributeValue(tokenParser, "RemoveExistingContentTypes");
                    var remove = false;
                    Boolean.TryParse(removeExistingContentTypes, out remove);

                    var contentTypeBindings = listInstance.GetDescendants(ns + "ContentTypeBindings", ns + "ContentTypeBinding");
                    var dataRows = listInstance.GetDescendants(ns + "DataRows", ns + "DataRow");
                    var indexedColumns = listInstance.GetDescendants(ns + "IndexedColumns", ns + "IndexedColumn");

                    AddContentTypesToList(clientContext.Web, list, contentTypeBindings, tokenParser, remove);
                    AddItemsToList(subWeb, list, dataRows, tokenParser);
                    AddIndexedColumnsToList(subWeb, list, indexedColumns, tokenParser);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        
        /// <summary>
        /// Removes a list from SharePoint by title.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="title"></param>
        [Obsolete]
        public static void RemoveList(this ClientContext clientContext, string title)
        {
            try
            {
                var web = clientContext.Web;

                var list = web.GetListByTitle(title);
                if (list != null)
                {
                    list.DeleteObject();
                    clientContext.ExecuteQueryRetry();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Adds content types to a given list.
        /// </summary>
        /// <param name="sourceWeb">Web where the content types exist.</param>
        /// <param name="list">List to update.</param>
        /// <param name="contentTypeBindings">XML content type configuration.</param>
        /// <param name="tokenParser"></param>
        /// <param name="removeExistingContentTypes">Whether to remove existing content types on the list.</param>
        private static void AddContentTypesToList(Web sourceWeb, List list, IEnumerable<XElement> contentTypeBindings, TokenParser tokenParser, bool removeExistingContentTypes)
        {
            list.EnsureProperty(i => i.ContentTypes);
            var existingContentTypes = list.ContentTypes.Select(i => i.Name).ToList();

            foreach (var contentTypeBinding in contentTypeBindings)
            {
                var contentTypeId = contentTypeBinding.GetAttributeValue(tokenParser, "ContentTypeID");
                var isDefault = bool.Parse(contentTypeBinding.GetAttributeValue(tokenParser, "Default"));

                var contentType = sourceWeb.GetContentTypeById(contentTypeId);

                list.AddContentTypeToList(contentType, isDefault);
            }

            if (removeExistingContentTypes)
            {
                foreach (var contentType in existingContentTypes)
                {
                    list.RemoveContentTypeByName(contentType);
                }
            }
        }

        /// <summary>
        /// Add list items to a list.
        /// </summary>
        /// <param name="web">Web where the list exists.</param>
        /// <param name="list">List to add items to.</param>
        /// <param name="dataRows">XML configration for items to add.</param>
        /// <param name="tokenParser"></param>
        private static void AddItemsToList(this Web web, List list, IEnumerable<XElement> dataRows, TokenParser tokenParser)
        {
            // Retrieve the fields' types from the list
            FieldCollection fields = list.Fields;
            web.Context.Load(fields, fs => fs.Include(f => f.InternalName, f => f.FieldTypeKind));
            web.Context.ExecuteQueryRetry();

            foreach (var dataRow in dataRows)
            {
                try
                {
                    var listitemCI = new ListItemCreationInformation();
                    var listitem = list.AddItem(listitemCI);

                    foreach (var dataValue in dataRow.Attributes())
                    {
                        Field dataField = fields.FirstOrDefault(
                            f => f.InternalName == dataValue.Name.LocalName);

                        if (dataField != null)
                        {
                            String fieldValue = tokenParser.ParseString(dataValue.Value);

                            switch (dataField.FieldTypeKind)
                            {
                                case FieldType.Geolocation:
                                    // FieldGeolocationValue - Expected format: Altitude,Latitude,Longitude,Measure
                                    var geolocationArray = fieldValue.Split(',');
                                    if (geolocationArray.Length == 4)
                                    {
                                        var geolocationValue = new FieldGeolocationValue
                                        {
                                            Altitude = Double.Parse(geolocationArray[0]),
                                            Latitude = Double.Parse(geolocationArray[1]),
                                            Longitude = Double.Parse(geolocationArray[2]),
                                            Measure = Double.Parse(geolocationArray[3]),
                                        };
                                        listitem[dataValue.Name.LocalName] = geolocationValue;
                                    }
                                    else
                                    {
                                        listitem[dataValue.Name.LocalName] = fieldValue;
                                    }
                                    break;
                                case FieldType.Lookup:
                                    // FieldLookupValue - Expected format: LookupID
                                    var lookupValue = new FieldLookupValue
                                    {
                                        LookupId = Int32.Parse(fieldValue),
                                    };
                                    listitem[dataValue.Name.LocalName] = lookupValue;
                                    break;
                                case FieldType.URL:
                                    // FieldUrlValue - Expected format: URL,Description
                                    var urlArray = fieldValue.Split(',');
                                    var linkValue = new FieldUrlValue();
                                    if (urlArray.Length == 2)
                                    {
                                        linkValue.Url = urlArray[0];
                                        linkValue.Description = urlArray[1];
                                    }
                                    else
                                    {
                                        linkValue.Url = urlArray[0];
                                        linkValue.Description = urlArray[0];
                                    }
                                    listitem[dataValue.Name.LocalName] = linkValue;
                                    break;
                                case FieldType.User:
                                    // FieldUserValue - Expected format: loginName
                                    var user = web.EnsureUser(fieldValue);
                                    web.Context.Load(user);
                                    web.Context.ExecuteQueryRetry();

                                    if (user != null)
                                    {
                                        var userValue = new FieldUserValue
                                        {
                                            LookupId = user.Id,
                                        };
                                        listitem[dataValue.Name.LocalName] = userValue;
                                    }
                                    else
                                    {
                                        listitem[dataValue.Name.LocalName] = fieldValue;
                                    }
                                    break;
                                case FieldType.Invalid:
                                    if (dataField.GetType() == typeof(Microsoft.SharePoint.Client.Taxonomy.TaxonomyField))
                                    {
                                        var txField = web.Context.CastTo<TaxonomyField>(dataField);
                                        web.Context.Load(txField, tx => tx.TermSetId);
                                        web.Context.ExecuteQueryRetry();

                                        var ts = TaxonomySession.GetTaxonomySession(web.Context);
                                        var termSet = ts.GetDefaultSiteCollectionTermStore().GetTermSet(txField.TermSetId);
                                        var term = termSet.Terms.GetByName(fieldValue);
                                        web.Context.Load(term, t => t.Id);
                                        web.Context.ExecuteQueryRetry();

                                        var termValue = new TaxonomyFieldValue();
                                        termValue.TermGuid = term.Id.ToString();
                                        txField.SetFieldValueByValue(listitem, termValue);
                                    }
                                    else
                                    {
                                        listitem[dataValue.Name.LocalName] = fieldValue;
                                    }
                                    break;
                                default:
                                    listitem[dataValue.Name.LocalName] = fieldValue;
                                    break;
                            }
                        }
                        listitem.Update();
                    }
                    web.Context.ExecuteQueryRetry(); // TODO: Run in batches?
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        /// <summary>
        /// Add indexed columns to a list.
        /// </summary>
        /// <param name="sourceWeb">Web where the list exists.</param>
        /// <param name="list">List to add indexed columns to.</param>
        /// <param name="indexedColumns">XML configuration for indexed columns to add.</param>
        /// <param name="tokenParser"></param>
        private static void AddIndexedColumnsToList(Web sourceWeb, List list, IEnumerable<XElement> indexedColumns, TokenParser tokenParser)
        {
            list.EnsureProperty(i => i.Fields);

            foreach (var indexedColumn in indexedColumns)
            {
                var name = indexedColumn.GetAttributeValue(tokenParser, "Name");

                var field = list.Fields.Where(i => i.InternalName.Equals(name, StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                if (field != null)
                {
                    field.Indexed = true;
                    field.Update();
                }
            }

            list.Update();
            sourceWeb.Context.ExecuteQuery();
        }

        /// <summary>
        /// Updates device channels in a web. This is done by modifying a hidden Device Channels list.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="listInstance">XML configuration for the device channels.</param>
        /// <param name="ns"></param>
        /// <param name="tokenParser"></param>
        public static void UpdateDeviceChannels(this ClientContext clientContext, XElement listInstance, XNamespace ns, TokenParser tokenParser)
        {
            var web = clientContext.Site.RootWeb;

            var list = web.GetListByTitle("Device Channels");
            if (list != null)
            {
                var deviceChannels = listInstance.GetDescendants(ns + "DeviceChannels", ns + "DeviceChannel").ToList();
                var finalChannels = new List<XElement>();
                
                foreach (var deviceChannel in deviceChannels)
                {
                    var query = new CamlQuery();
                    query.ViewXml = "<View><Query><Where><Eq><FieldRef Name='ChannelAlias'/><Value Type='Text'>" +
                        deviceChannel.GetAttributeValue(tokenParser, "ChannelAlias") +
                        "</Value></Eq></Where></Query></View>";

                    var items = list.GetItems(query);
                    clientContext.Load(items);
                    clientContext.ExecuteQueryRetry();

                    if (items.Any())
                    {
                        foreach (var item in items)
                        {
                            item.DeleteObject();
                        }

                        clientContext.ExecuteQueryRetry();
                    }
                }

                AddItemsToList(web, list, deviceChannels, tokenParser);
            }
        }

        /// <summary>
        /// Adds a file to a specific location on SharePoint.
        /// </summary>
        /// <param name="rootUrl">Unused.</param>
        /// <param name="web">Web where the file will go.</param>
        /// <param name="filePath">Path of the file in the solution hierarchy.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="serverPath">Where the file will go.</param>
        /// <param name="serverFolder">Parent folder of serverPath.</param>
        /// <returns></returns>
        private static File AddFile(string rootUrl, Web web, string filePath, string fileName, string serverPath, string serverFolder)
        {
            var fileUrl = string.Concat(serverPath, serverFolder, (string.IsNullOrEmpty(serverFolder) ? string.Empty : "/"), fileName);
            var folder = web.GetFolderByServerRelativeUrl(string.Concat(serverPath, serverFolder));

            FileCreationInformation spFile = new FileCreationInformation() {
                Content = System.IO.File.ReadAllBytes(filePath + fileName.Replace("/", "\\")),
                Url = fileUrl,
                Overwrite = true
            };
            var uploadFile = folder.Files.Add(spFile);
            web.Context.Load(uploadFile, f => f.CheckOutType, f => f.Level);
            web.Context.ExecuteQueryRetry();

            return uploadFile;
        }

        /// <summary>
        /// Makes sure a folder exists in SharePoint. The folder will be added if it does not exist.
        /// </summary>
        /// <param name="web">Web where the folder exists.</param>
        /// <param name="listUrl">Path of the item.</param>
        /// <param name="folderUrl">URL of the folder that should exist.</param>
        /// <param name="parentFolder">Parent of the folder.</param>
        /// <returns></returns>
        private static Folder EnsureFolder(this Web web, string listUrl, string folderUrl, Folder parentFolder)
        {
            Folder folder = null;
            var folderServerRelativeUrl = parentFolder == null ? listUrl.TrimEnd(Program._TrimChars) + "/" + folderUrl : parentFolder.ServerRelativeUrl.TrimEnd(Program._TrimChars) + "/" + folderUrl;

            if (string.IsNullOrEmpty(folderUrl)) {
                return null;
            }

            var lists = web.Lists;
            web.Context.Load(web);
            web.Context.Load(lists, l => l.Include(ll => ll.DefaultViewUrl));
            web.Context.ExecuteQueryRetry();

            ExceptionHandlingScope scope = new ExceptionHandlingScope(web.Context);
            using (scope.StartScope()) {
                using (scope.StartTry()) {
                    folder = web.GetFolderByServerRelativeUrl(folderServerRelativeUrl);
                    web.Context.Load(folder);
                }

                using (scope.StartCatch()) {
                    var list = lists.Where(l => l.DefaultViewUrl.IndexOf(listUrl, StringComparison.CurrentCultureIgnoreCase) >= 0).FirstOrDefault();

                    if (parentFolder == null) {
                        parentFolder = list.RootFolder;
                    }


                    folder = parentFolder.Folders.Add(folderUrl);
                    web.Context.Load(folder);
                }

                using (scope.StartFinally()) {
                    folder = web.GetFolderByServerRelativeUrl(folderServerRelativeUrl);
                    web.Context.Load(folder);
                }
            }

            web.Context.ExecuteQueryRetry();
            return folder;
        }

        /// <summary>
        /// Performs all the content approval steps for a file.
        /// </summary>
        /// <param name="uploadFile">The file to be approved.</param>
        private static void CheckInPublishAndApproveFile(File uploadFile)
        {
            if (uploadFile.CheckOutType != CheckOutType.None) {
                uploadFile.CheckIn("Updating branding", CheckinType.MajorCheckIn);
            }

            if (uploadFile.Level == FileLevel.Draft) {
                uploadFile.Publish("Updating branding");
            }

            uploadFile.Context.Load(uploadFile, f => f.ListItemAllFields);
            uploadFile.Context.ExecuteQueryRetry();

            if (uploadFile.ListItemAllFields["_ModerationStatus"].ToString() == "2") // SPModerationStatusType.Pending
            {
                uploadFile.Approve("Updating branding");
                uploadFile.Context.ExecuteQueryRetry();
            }
        }

        /// <summary>
        /// Checks out a file from SharePoint.
        /// </summary>
        /// <param name="web">Web where the file exists.</param>
        /// <param name="fileName">Name of the file.</param>
        /// <param name="filePath">Path where the file folder exists.</param>
        /// <param name="fileFolder">Folder where the file exists.</param>
        private static void CheckOutFile(this Web web, string fileName, string filePath, string fileFolder)
        {
            var fileUrl = string.Concat(filePath, fileFolder, (string.IsNullOrEmpty(fileFolder) ? string.Empty : "/"), fileName);
            var temp = web.GetFileByServerRelativeUrl(fileUrl);

            web.Context.Load(temp, f => f.Exists);
            web.Context.ExecuteQueryRetry();

            if (temp.Exists) {
                web.Context.Load(temp, f => f.CheckOutType);
                web.Context.ExecuteQueryRetry();

                if (temp.CheckOutType != CheckOutType.None) {
                    temp.UndoCheckOut();
                }

                temp.CheckOut();
                web.Context.ExecuteQueryRetry();
            }
        }

        /// <summary>
        /// EnsureFolder overload.
        /// </summary>
        /// <param name="web"></param>
        /// <param name="listUrl"></param>
        /// <param name="folderUrl"></param>
        /// <returns></returns>
        private static Folder EnsureFolder(this Web web, string listUrl, string folderUrl)
        {
            return EnsureFolder(web, listUrl, folderUrl, null);
        }

        /// <summary>
        /// Ensures each segment of a file path.
        /// </summary>
        /// <param name="web">Web where the file exists.</param>
        /// <param name="filePath">Path of the file folder.</param>
        /// <param name="fileFolder">File folder name.</param>
        /// <param name="fileName">File name.</param>
        private static void EnsureFolders(this Web web, string filePath, string fileFolder, string fileName)
        {
            var folder = EnsureFolder(web, filePath, fileFolder);
            //if the file name contains folders, ensure those folders exist as well
            IEnumerable<string> folderUrls = fileName.Split(new string[] { "/" }, StringSplitOptions.RemoveEmptyEntries);
            //remove the last entry, which is the file name
            folderUrls = folderUrls.Take(folderUrls.Count() - 1).ToArray();
            //if the length is greater than one, we have some folders to ensure
            var parent = folder;
            foreach (var folderUrl in folderUrls) {
                parent = EnsureFolder(web, filePath, folderUrl, parent);
            }
        }

        /// <summary>
        /// Uploads a page layout into the SharePoint master page gallery.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="name">Name of the layout.</param>
        /// <param name="folder">Folder to upload to.</param>
        /// <param name="title">Title to give the layout.</param>
        /// <param name="publishingAssociatedContentType">Content type associated with the file.</param>
        public static void UploadPageLayout(ClientContext clientContext, string name, string folder, string title, string publishingAssociatedContentType)
        {
            var web = clientContext.Web;
            var lists = web.Lists;
            var gallery = web.GetCatalog(116);
            clientContext.Load(lists, l => l.Include(ll => ll.DefaultViewUrl));
            clientContext.Load(gallery, g => g.RootFolder.ServerRelativeUrl);
            clientContext.ExecuteQueryRetry();

            Console.WriteLine("Uploading page layout {0} to {1}", name, clientContext.Web.ServerRelativeUrl);

            var masterPath = gallery.RootFolder.ServerRelativeUrl.TrimEnd(Program._TrimChars) + "/";

            EnsureFolder(web, masterPath, folder);
            CheckOutFile(web, name, masterPath, folder);

            var uploadFile = AddFile(web.Url, web, "Branding\\PageLayouts\\", name, masterPath, folder);

            SetPageLayoutMetadata(web, uploadFile, title, publishingAssociatedContentType);
            CheckInPublishAndApproveFile(uploadFile);
        }

        /// <summary>
        /// Update the ContentTypeId, Title, and PublishingAssociatedContentType properties for a file.
        /// </summary>
        /// <param name="web">Web were the file exists.</param>
        /// <param name="uploadFile">File that will be uploaded.</param>
        /// <param name="title">Title of the file.</param>
        /// <param name="publishingAssociatedContentType">Content type associated with the file.</param>
        private static void SetPageLayoutMetadata(Web web, File uploadFile, string title, string publishingAssociatedContentType)
        {
            var gallery = web.GetCatalog(116);
            web.Context.Load(gallery, g => g.ContentTypes);
            web.Context.ExecuteQueryRetry();

            var item = uploadFile.ListItemAllFields;
            web.Context.Load(item);

            // Get content type for ID to assign associated content type information
            ContentType associatedCt = web.GetContentTypeById(publishingAssociatedContentType);

            const string HTMLPAGE_LAYOUT_CONTENT_TYPE = "0x01010007FF3E057FA8AB4AA42FCB67B453FFC100E214EEE741181F4E9F7ACC43278EE8110003D357F861E29844953D5CAA1D4D8A3B001EC1BD45392B7A458874C52A24C9F70B";

            item["ContentTypeId"] = HTMLPAGE_LAYOUT_CONTENT_TYPE;
            item["Title"] = title;
            item["PublishingAssociatedContentType"] = string.Format(";#{0};#{1};#", associatedCt.Name, associatedCt.Id); ;

            item.Update();
            web.Context.ExecuteQueryRetry();
        }

        /// <summary>
        /// Takes a url and performs string manipulations so the OpenWeb command works correctly. 
        /// </summary>
        /// <param name="site"></param>
        /// <param name="url"></param>
        /// <returns></returns>
        public static Web OpenWebFromFullUrl(this Site site, string url)
        {
            url = url.Replace(site.Url, string.Empty);
            if (url.StartsWith("/"))
            {
                url = url.Substring(1);
            }

            return site.OpenWeb(url);
        }

        #endregion

        #region "deactivate branding functions"
        
        /// <summary>
        /// Remove a file from SharePoint.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="name"></param>
        /// <param name="folder"></param>
        /// <param name="path"></param>
        [Obsolete]
        public static void RemoveFile(this ClientContext clientContext, string name, string folder, string path)
        {
            try
            {
                var web = clientContext.Web;
                var filePath = web.ServerRelativeUrl.TrimEnd(Program._TrimChars) + "/" + path + "/";

                Console.WriteLine("Removing file {0} from {1}{2}", name, filePath, folder);

                DeleteFile(web, name, filePath, folder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Remove a folder from SharePoint.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="folder"></param>
        /// <param name="path"></param>
        [Obsolete]
        public static void RemoveFolder(this ClientContext clientContext, string folder, string path)
        {
            try
            {
                var web = clientContext.Web;
                var filePath = web.ServerRelativeUrl.TrimEnd(Program._TrimChars) + "/" + path + "/";
                var folderToDelete = web.GetFolderByServerRelativeUrl(string.Concat(filePath, folder));
                Console.WriteLine("Removing folder {0} from {1}", folder, path);
                folderToDelete.DeleteObject();
                clientContext.ExecuteQueryRetry();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }

        /// <summary>
        /// Remove a master page from SharePoint.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="name"></param>
        /// <param name="folder"></param>
        [Obsolete]
        public static void RemoveMasterPage(this ClientContext clientContext, string name, string folder)
        {
            try
            {
                var web = clientContext.Web;
                clientContext.Load(web, w => w.AllProperties);
                clientContext.ExecuteQueryRetry();

                Console.WriteLine("Deactivating and removing {0} from {1}", name, web.ServerRelativeUrl);

                //set master pages back to the defaults that were being used
                if (web.AllProperties.FieldValues.ContainsKey("OriginalMasterUrl"))
                {
                    web.MasterUrl = (string)web.AllProperties["OriginalMasterUrl"];
                }
                if (web.AllProperties.FieldValues.ContainsKey("CustomMasterUrl"))
                {
                    web.CustomMasterUrl = (string)web.AllProperties["CustomMasterUrl"];
                }
                web.Update();
                clientContext.ExecuteQueryRetry();

                //now that the master page is set back to its default, re-reference the web from context and delete the custom master pages
                web = clientContext.Web;
                var lists = web.Lists;
                var gallery = web.GetCatalog(116);
                clientContext.Load(lists, l => l.Include(ll => ll.DefaultViewUrl));
                clientContext.Load(gallery, g => g.RootFolder.ServerRelativeUrl);
                clientContext.ExecuteQueryRetry();
                var masterPath = gallery.RootFolder.ServerRelativeUrl.TrimEnd(new char[] { '/' }) + "/";
                DeleteFile(web, name, masterPath, folder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Remove a page layout from SharePoint.
        /// </summary>
        /// <param name="clientContext"></param>
        /// <param name="name"></param>
        /// <param name="folder"></param>
        [Obsolete]
        public static void RemovePageLayout(this ClientContext clientContext, string name, string folder)
        {
            try
            {
                var web = clientContext.Web;
                var lists = web.Lists;
                var gallery = web.GetCatalog(116);
                clientContext.Load(lists, l => l.Include(ll => ll.DefaultViewUrl));
                clientContext.Load(gallery, g => g.RootFolder.ServerRelativeUrl);
                clientContext.ExecuteQueryRetry();

                Console.WriteLine("Removing page layout {0} from {1}", name, clientContext.Web.ServerRelativeUrl);

                var masterPath = gallery.RootFolder.ServerRelativeUrl.TrimEnd(Program._TrimChars) + "/";

                DeleteFile(web, name, masterPath, folder);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        /// <summary>
        /// Deletes a file from SharePoint.
        /// </summary>
        /// <param name="web"></param>
        /// <param name="fileName"></param>
        /// <param name="serverPath"></param>
        /// <param name="serverFolder"></param>
        [Obsolete]
        private static void DeleteFile(this Web web, string fileName, string serverPath, string serverFolder)
        {
            try
            {
                var fileUrl = string.Concat(serverPath, serverFolder, (string.IsNullOrEmpty(serverFolder) ? string.Empty : "/"), fileName);
                var fileToDelete = web.GetFileByServerRelativeUrl(fileUrl);
                fileToDelete.DeleteObject();
                web.Context.ExecuteQueryRetry();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        #endregion
    }
}