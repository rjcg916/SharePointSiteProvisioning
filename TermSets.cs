using Microsoft.SharePoint.Client;
using System;
using System.Xml.Linq;
using Microsoft.SharePoint.Client.Taxonomy;
using OfficeDevPnP.Core.Framework.Provisioning.ObjectHandlers;
using PRFT.SharePoint.PnP.Framework.Provisioning.ObjectHandlers;

namespace PRFT.SharePoint
{
    class TermSets
    {

        public static void CreateTermSets(TokenParser _TokenParser, XElement _Branding, ClientContext clientContext, string termGroupName)
        {

            Console.WriteLine("Processing Term Sets . . . ");

            // This code assumes: 
            // managed metadata service is running on the farm
            // default termstore exists for site collection 
            // permission to managed metadata service have been granted


            // start a taxonomy session and connect to the Term Store
            TaxonomySession taxonomySession = TaxonomyExtensions.GetTaxonomySession(clientContext.Site);
            TermStore termStore = taxonomySession.GetDefaultSiteCollectionTermStore();


            // connect to Site Collection Terms (aka Term Group with Site Collection Name)
            Microsoft.SharePoint.Client.Taxonomy.TermGroup termGroup = termStore.GetTermGroupByName(termGroupName);

            //process each termset 
            foreach (var termset in _Branding.GetDescendants("termsets", "termset"))
            {

                // fetch file path 
                string termSetFilePath = termset.GetAttributeValue(_TokenParser, "termSetFilePath");

                Console.WriteLine("Creating Term Set from contents of: {0}", termSetFilePath);

                // Create TermSet via File Import
                Microsoft.SharePoint.Client.Taxonomy.TermSet termSet = TaxonomyExtensions.ImportTermSet(termGroup, termSetFilePath);

            }

        }

    }
}
