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



            Parameters.Add_sharedParameters(doc);
            // Parameters.CopyParameters(doc);



            return Result.Succeeded;

        }
    }

    public class Parameters
    {
        public static void CopyParameters(Document doc)
        {
            Transaction tx = new Transaction(doc);

            String ProNum = (new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectInformation)
                   .FirstElement() as ProjectInfo).Number;


            String Host = (new FilteredElementCollector(doc)
                   .OfCategory(BuiltInCategory.OST_ProjectInformation)
                   .FirstElement() as ProjectInfo).LookupParameter("Host").AsString();

            tx.Start("GUID parameter");

            FilteredElementCollector elements = new FilteredElementCollector(doc).WhereElementIsNotElementType();

            foreach (Element e in elements)
            {
                try
                {

                    string s = Host + "/" + ProNum + "/" + e.Category.Name + "/" + Guid.NewGuid().ToString();
                    s = s.Replace(" ", "_");

                    if (e.GroupId == null)
                        e.LookupParameter("URI").Set(s);
                }
                catch
                { }

            }

            tx.Commit();




        }

        public static void Add_sharedParameters(Document doc)
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