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
using System.Windows.Forms;
using Autodesk.Revit.DB.Architecture;

namespace NIRAS.Revit.TTL_Exporter
{
    [Transaction(TransactionMode.Manual)]
    public class Export_TTL_File_Main : IExternalCommand
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
            Dictionary<ElementId, string> ElementDict = new Dictionary<ElementId, string>();
            Parameters.Add_sharedParameters(doc);


            SaveFileDialog savefile = new SaveFileDialog();
            // set a default file name
            savefile.FileName = doc.Title + ".ttl";
            // set filters - this can be done in properties as well
            savefile.Filter = "Text files (*.ttl)|*.ttl|All files (*.*)|*.*";

            if (savefile.ShowDialog() == DialogResult.OK)
            {

                String ProNum = (new FilteredElementCollector(doc)
                       .OfCategory(BuiltInCategory.OST_ProjectInformation)
                       .FirstElement() as ProjectInfo).Number;


                String Host = (new FilteredElementCollector(doc)
                       .OfCategory(BuiltInCategory.OST_ProjectInformation)
                       .FirstElement() as ProjectInfo).LookupParameter("Host").AsString();

                string NL = Environment.NewLine;
                string NLT = Environment.NewLine + "\t";

                String Mstrign =
                           @" @prefix bot:      <https://w3id.org/bot#> ."
                    + NL + @" @prefix rdfs:     <http://www.w3.org/2000/01/rdf-schema#> ."
                    + NL + @" @prefix rvt:      <https://example.org/rvt#> ."
                    + NL + @" @prefix cdt:      <http://w3id.org/lindt/custom_datatypes#> ."
                    + NL + @" @prefix props:  	<https://w3id.org/props#> .";

                

                Mstrign += NL + NL + "### ELEMENTS ###";

                #region Walls

                List<Element> walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElements().ToList();

                Mstrign += NL + NL + "# WALLS";

                foreach (Element e in walls)
                {
                    
                    
                    string guid = Util.GetGuid(doc,e,Host,ProNum);
                    ElementDict.Add(e.Id, guid);


                    Mstrign +=
                        NL + "<" + guid + ">" +
                        NLT + "a bot:Element ;" +
                        NLT + "rdfs:label \"" + e.Name + "\" ;" +
                        NLT + "rvt:guid \"" + e.UniqueId + "\" ;" +
                        NLT + "props:dimensionsWidth\" " + (e as Wall).Width * 304.8  + " mm\"^^cdt:length ;" +
                        NLT + "props:dimensionsLength\" " + (e as Wall).get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsValueString() + " mm\"^^cdt:length .";

                }

                #endregion

                #region Windows and Doors

                
                IList<Element> WinDoor = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType().WherePasses(new LogicalOrFilter(new List<ElementFilter>
                        {                            
                            new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                            new ElementCategoryFilter(BuiltInCategory.OST_Doors)

                         })).ToElements();

                Mstrign += NL + NL + "# WINDOWS & DOORS";


                foreach (Element e in WinDoor)
                {
                    string guid = Util.GetGuid(doc, e, Host, ProNum);
                    ElementDict.Add(e.Id, guid);

                    Mstrign +=
                        NL + @"<" + guid + ">" +
                        NLT + "a bot:Element ;" +
                        NLT + "rdfs:label \"" + e.Name + "\" ;" +
                        NLT + "rvt:guid \"" + e.UniqueId + "\" .";

                }

                #endregion

                #region Levels


                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level)).WhereElementIsNotElementType().ToElements().Cast<Level>()
                    .ToList();

                Mstrign += NL + NL + "### STOREYS ###";

                foreach (Level e in levels)
                {
                    string guid = Util.GetGuid(doc, e, Host, ProNum);
                    ElementDict.Add(e.Id, guid);

                    Mstrign +=
                        NL + @"<" + guid + ">" +
                        NLT + "a bot:Storey ;" +
                        NLT + "rdfs:label \"" + e.Name + "\" ;" +
                        NLT + "rvt:guid \"" + e.UniqueId + "\" .";
                }

                #endregion

                #region Rooms/Spaces
                              
                List<Element> spaces = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement)).WhereElementIsNotElementType()
                  .Where(X => X.Category.Name == "Spaces" || X.Category.Name == "Rooms").ToList<Element>();

                Mstrign += NL + NL + "### SPACES ###";

                foreach (Element e in spaces)
                {
                    string guid = Util.GetGuid(doc, e, Host, ProNum);
                    ElementDict.Add(e.Id, guid);                    

                    if (e.Category.Name == "Spaces")
                    {
                        Space Spac = e as Space;

                        Mstrign +=
                            NL + @"<" + guid + ">" +
                            NLT + "a bot:Space ;" +
                            NLT + "rdfs:label \"" + e.Name + "\" ;" +
                            NLT + "rvt:guid \"" + e.UniqueId + "\" ;" +
                            NLT + "props:dimensionsArea\" " + Spac.Area * Math.Pow(304.8, 2) + " mm2\"^^cdt:ucum ;" +
                            NLT + "props:dimensionsVolume\" " + Spac.Volume * Math.Pow(304.8, 3) + " cm3\"^^cdt:volume .";
                    }

                    if (e.Category.Name == "Rooms")
                    {
                        Room room = e as Room;

                        Mstrign +=
                            NL + @"<" + guid + ">" +
                            NLT + "a bot:Space ;" +
                            NLT + "rdfs:label \"" + e.Name + "\" ;" +
                            NLT + "rvt:guid \"" + e.UniqueId + "\" ;" +
                            NLT + "props:dimensionsArea\" " + room.Area * Math.Pow(304.8, 2) + " mm2 \"^^cdt:area ;" +
                            NLT + "props:dimensionsVolume\" " + room.Volume * Math.Pow(304.8, 3) + " cm3\"^^cdt:volume .";
                    }

                }

                #endregion

                Mstrign += NL + NL + "### RELATIONSHIPS ###";

                Mstrign += NL + NL + "# WINDOWS AND DOORS HOSTED IN A WALL";

                foreach (Element e in WinDoor)
                {
                    try
                    {
                        FamilyInstance FamIns = e as FamilyInstance;

                        Mstrign +=
                                NL + "<" + ElementDict[FamIns.Host.Id] + "> bot:hostsElement <" + ElementDict[e.Id] + "> .";
                    }
                    catch { }
                }

                Mstrign += NL + NL + "# ROOMS AT EACH STOREY";

                foreach (Element e in spaces)
                {
                    
                    try
                    {                        
                        Mstrign +=
                        NL + "<" + ElementDict[e.LevelId] + "> bot:hasSpace <" + ElementDict[e.Id] + "> .";
                    }
                    catch { }
                }

                Mstrign += NL + NL + "# ELEMENTS ADJACENT TO ROOMS";

                foreach (SpatialElement sp in spaces)
                {
                    SpatialElementBoundaryOptions SpaEleBdOp = new SpatialElementBoundaryOptions();
                    IList<IList<BoundarySegment>> BdSegLoops = sp.GetBoundarySegments(SpaEleBdOp);

                    foreach (IList<BoundarySegment> BdSegLoop in BdSegLoops)
                        foreach (BoundarySegment BdSeg in BdSegLoop)
                        {
                            ElementId id = null;

                            try
                            {
                                id = BdSeg.ElementId;

                                if (doc.GetElement(id).Category.Name == "Walls")
                                {

                                    Mstrign +=
                                        NL + "<" + ElementDict[sp.Id] + "> bot:adjacentElement <" + ElementDict[id] + "> .";
                                }
                            }
                            catch
                            { }
                        }
                }





                using (StreamWriter writer =
                new StreamWriter(savefile.FileName))
                {
                    writer.Write(Mstrign);
                }
            }

            return Result.Succeeded;

        }
    }
}
