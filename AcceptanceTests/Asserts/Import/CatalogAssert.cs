using System.Collections.Generic;
using System.Xml;
using AgGateway.ADAPT.ApplicationDataModel.ADM;
using AgGateway.ADAPT.ApplicationDataModel.Common;

namespace AcceptanceTests.Asserts.Import
{
    public class CatalogAssert
    {
        public static void AreEqual(XmlNode taskData, Catalog catalog, Dictionary<string, List<UniqueId>> linkList)
        {
            CropAssert.AreEqual(taskData.SelectNodes("CTP"), catalog.Crops, catalog, linkList);
            FarmAssert.AreEqual(taskData.SelectNodes("FRM"), catalog.Farms, catalog, linkList);
            FieldAssert.AreEqual(taskData.SelectNodes("PFD"), catalog.Fields, catalog, linkList);
            GrowerAssert.AreEqual(taskData.SelectNodes("CTR"), catalog.Growers, linkList);
            ProductAssert.AreEqual(taskData.SelectNodes("PDT"), catalog, linkList);
        }
    }
}