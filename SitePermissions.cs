using Microsoft.SharePoint.Client;
using System;

namespace PRFT.SharePoint
{
    public class SitePermissions
    {

        static string ContentManagerName = "Content Manager";
        static string ContentContributorName = "Content Contributor";
        static string VisitorName = "Visitor";

        public static void CreatePermissions(ClientContext clientContext)
        {
            Console.WriteLine("Adding custom permissions . . . ");

            // add read permissions to a permission collection
            BasePermissions permissions = new BasePermissions();
            permissions.Set(PermissionKind.ViewPages);
            permissions.Set(PermissionKind.ViewListItems);

            RoleDefinitionCreationInformation roleDefinitionCreationInfo;
            Microsoft.SharePoint.Client.RoleDefinition roleDefinition;

            // create custom permission levels - adjust post-provision

            roleDefinitionCreationInfo = new RoleDefinitionCreationInformation();
            roleDefinitionCreationInfo.BasePermissions = permissions;
            roleDefinitionCreationInfo.Name = ContentContributorName;
            roleDefinitionCreationInfo.Description = ContentContributorName;
            roleDefinition = clientContext.Web.RoleDefinitions.Add(roleDefinitionCreationInfo);

            roleDefinitionCreationInfo = new RoleDefinitionCreationInformation();
            roleDefinitionCreationInfo.BasePermissions = permissions;
            roleDefinitionCreationInfo.Name = ContentManagerName;
            roleDefinitionCreationInfo.Description = ContentManagerName;
            roleDefinition = clientContext.Web.RoleDefinitions.Add(roleDefinitionCreationInfo);

            roleDefinitionCreationInfo = new RoleDefinitionCreationInformation();
            roleDefinitionCreationInfo.BasePermissions = permissions;
            roleDefinitionCreationInfo.Name = VisitorName;
            roleDefinitionCreationInfo.Description = VisitorName;
            roleDefinition = clientContext.Web.RoleDefinitions.Add(roleDefinitionCreationInfo);


            clientContext.ExecuteQuery();
        }

        public static void ApplyPermissions(ClientContext clientContext, Web web)
        {
            clientContext.Load(web, w => w.Url);
            clientContext.ExecuteQuery();

            string webVisitor = String.Format("{0}s", VisitorName);

            // check to see if group has already been created and assigned to this sub-site
            int groupId = -1;
            try
            {
                groupId = web.GetGroupID(webVisitor);
            }
            catch
            {
            }

            if (groupId != -1)
                return;


            Console.WriteLine("Applying custom permissions to {0}", web.Url);

            web.AddGroup(webVisitor, VisitorName, true);
            web.AddPermissionLevelToGroup(webVisitor, VisitorName);

            string webContributor = String.Format("{0} {1}s", web.Title, ContentContributorName);
            web.AddGroup(webContributor, ContentContributorName, true);
            web.AddPermissionLevelToGroup(webContributor, ContentContributorName);

            string webManager = String.Format("{0} {1}s", web.Title, ContentManagerName);
            web.AddGroup(webManager, ContentManagerName, true);
            web.AddPermissionLevelToGroup(webManager, ContentManagerName);


            web.Update();

            clientContext.ExecuteQuery();
        }
    }
}



