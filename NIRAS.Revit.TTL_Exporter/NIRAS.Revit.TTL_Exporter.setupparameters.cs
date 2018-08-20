using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

using Autodesk;
using Autodesk.Revit;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB.Mechanical;
using Autodesk.Revit.DB.Electrical;
using Autodesk.Revit.DB.Plumbing;

namespace NIRAS.Revit.TTL_Exporter
{
    [Transaction(TransactionMode.Manual)]
    public class Addparameters : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Transaction tx = new Transaction(doc);



            Parameters.AddSharedParameters(doc);
            // Parameters.CopyParameters(doc);

            return Result.Succeeded;

        }
    }

    [Transaction(TransactionMode.Manual)]
    public class ClearURIs : IExternalCommand
    {
        public Result Execute(
          ExternalCommandData commandData,
          ref string message,
          ElementSet elements)
        {

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Transaction tx = new Transaction(doc);

            FilteredElementCollector el = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            int count = 0;

            tx.Start("Clear URIs");

            foreach (Element e in el)
            {
                if (e.LookupParameter("URI") != null && e.LookupParameter("URI").AsString() != "")
                {
                    count += 1;
                    e.LookupParameter("URI").Set("");
                }
            }

            tx.Commit();

            if(count > 0)
            {
                TaskDialog.Show("Success", $"Successfully cleared {count} URIs");
            }
            else
            {
                TaskDialog.Show("None", "No URIs to clear");
            }

            return Result.Succeeded;

        }
    }

    public class Parameters
    {
        public static void GenerateURIs(Document doc)
        {
            Transaction tx = new Transaction(doc);
            string error = null;

            String ProNum = (new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectInformation)
                   .FirstElement() as ProjectInfo).Number;

            String Host = (new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectInformation)
                   .FirstElement() as ProjectInfo).LookupParameter("Host").AsString();

            tx.Start("Generate URIs");

            FilteredElementCollector elements = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            int count = 0;

            foreach (Element e in elements)
            {
                // Generate URI if none is defined
                if (e.LookupParameter("URI") != null && e.LookupParameter("URI").AsString() == "")
                {
                    try
                    {

                        string s = Util.CreateURI(e, Host, ProNum);

                        e.LookupParameter("URI").Set(s);
                        count += 1;
                    }
                    catch (Exception err)
                    { error = err.ToString(); }
                }

            }

            if (error != null)
            {
                TaskDialog.Show("Error", error);
            }
            else if(count > 0)
            {
                TaskDialog.Show("Success", $"Generated {count} new URIs");
            }

            tx.Commit();

        }

        public static string GenerateURIifNotExist(Document doc, Element e)
        {
            string uri = null;

            String ProNum = (new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectInformation)
                   .FirstElement() as ProjectInfo).Number;

            String Host = (new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectInformation)
                   .FirstElement() as ProjectInfo).LookupParameter("Host").AsString();

            if (e.LookupParameter("URI") != null && String.IsNullOrEmpty(e.LookupParameter("URI").AsString()))
            {
                // Begin transaction
                Transaction tx = new Transaction(doc);
                tx.Start("Generate single URI");

                try
                {
                    uri = Util.CreateURI(e, Host, ProNum);

                    e.LookupParameter("URI").Set(uri);
                }
                catch
                { }

                tx.Commit();
            }
            else
            {
                uri = e.LookupParameter("URI").AsString();
            }

            return uri;

        }

            public static void AddSharedParameters(Document doc)
        {

            Transaction tx = new Transaction(doc);

            // ___ tilføjer Sharedparameter
            tx.Start("adding offset parameter");
            string orgSharedFilePath = doc.Application.SharedParametersFilename;
            doc.Application.SharedParametersFilename = @"C:\Temp\N_Shared_Parameters_GUID_TEST.txt";
            DefinitionFile DefFile = doc.Application.OpenSharedParameterFile();
            DefinitionGroup grp = DefFile.Groups.get_Item("URI");
            Definition Def1 = grp.Definitions.get_Item("URI");
            Definition Def2 = grp.Definitions.get_Item("Host");
            Definition Def3 = grp.Definitions.get_Item("SpaceTypeURI");

            // create a category set and insert all categories to it
            CategorySet allCategories = doc.Application.Create.NewCategorySet();

            foreach (Category category in doc.Settings.Categories)
            {
                if (category.AllowsBoundParameters)
                {
                    allCategories.Insert(category);
                }
            }

            // Add shared parameter Def1 to all categories
            Binding binding = doc.Application.Create.NewTypeBinding(allCategories);
            binding = doc.Application.Create.NewInstanceBinding(allCategories);

            BindingMap map = (new UIApplication(doc.Application)).ActiveUIDocument.Document.ParameterBindings;
            map.Insert(Def1, binding, BuiltInParameterGroup.PG_IDENTITY_DATA);

            // Get category "Project Information"
            CategorySet projInfoCategory = doc.Application.Create.NewCategorySet();
            Category projInfoCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_ProjectInformation);
            projInfoCategory.Insert(projInfoCat);

            // Add shared parameter Def2 to project information category
            Binding binding1 = doc.Application.Create.NewTypeBinding(projInfoCategory);
            binding1 = doc.Application.Create.NewInstanceBinding(projInfoCategory);

            map = (new UIApplication(doc.Application)).ActiveUIDocument.Document.ParameterBindings;
            map.Insert(Def2, binding1, BuiltInParameterGroup.PG_IDENTITY_DATA);

            // Create a category set with rooms and spaces
            CategorySet spacesCategory = doc.Application.Create.NewCategorySet();
            Category roomsCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Rooms);
            Category spacesCat = doc.Settings.Categories.get_Item(BuiltInCategory.OST_MEPSpaces);
            spacesCategory.Insert(roomsCat);
            spacesCategory.Insert(spacesCat);

            // Add shared parameter Def3 to space categories
            Binding binding2 = doc.Application.Create.NewTypeBinding(spacesCategory);
            binding2 = doc.Application.Create.NewInstanceBinding(spacesCategory);

            map = (new UIApplication(doc.Application)).ActiveUIDocument.Document.ParameterBindings;
            map.Insert(Def3, binding2, BuiltInParameterGroup.PG_IDENTITY_DATA);

            doc.Application.SharedParametersFilename = orgSharedFilePath;
            tx.Commit();



        }
    }
}