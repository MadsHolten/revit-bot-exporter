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

            // SETTINGS
            Boolean opm = true;
            Boolean cdt = false;

            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            Transaction tx = new Transaction(doc);
            Dictionary<ElementId, string> ElementDict = new Dictionary<ElementId, string>();

            // Append URIs
            // Parameters.GenerateURIs(doc);


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

                String Namespace = $"{Host}/{ProNum}/";

                string NL = Environment.NewLine;
                string NLT = Environment.NewLine + "\t";

                // tString : Topology string
                // pString : Property string

                String tString =
                         "@prefix bot:\t<https://w3id.org/bot#> ." +
                    NL + "@prefix rdfs:\t<http://www.w3.org/2000/01/rdf-schema#> ." +
                    NL + "@prefix rvt:\t<https://example.org/rvt#> ." +
                    NL + $"@prefix inst:\t<{Namespace}> .";

                String pString = 
                         "@prefix props:\t<https://w3id.org/props#> ." +
                    NL + "@prefix rdfs:\t<http://www.w3.org/2000/01/rdf-schema#> ." +
                    NL + "@prefix xsd:\t<http://www.w3.org/2001/XMLSchema#> ." +
                    NL + "@prefix ex:\t\t<https://example.org/> ." +
                    NL + $"@prefix inst:\t<{Namespace}> .";

                if (cdt) pString += NL + "@prefix cdt:\t<http://w3id.org/lindt/custom_datatypes#> .";

                if (opm) pString += NL + "@prefix opm:\t<https://w3id.org/opm#> ." +
                        NL + "@prefix seas:\t<https://w3id.org/seas/> ." +
                        NL + "@prefix schema:\t<http://schema.org/> .";


                tString += NL + NL + "### ELEMENTS ###";
                pString += NL + NL + "### ELEMENTS ###";

                #region Walls

                List<Element> walls = new FilteredElementCollector(doc)
                    .OfClass(typeof(Wall)).WhereElementIsNotElementType().ToElements().ToList();

                tString += NL + NL + "# WALLS";
                pString += NL + NL + "# WALLS";

                foreach (Element e in walls)
                {

                    Wall wall = e as Wall;

                    string URI = Parameters.GenerateURIifNotExist(doc, e).Replace(Namespace, "inst:");

                    ElementDict.Add(e.Id, URI);

                    // Append classes to 
                    tString +=
                        NL + $"{URI}" +
                        NLT + "a bot:Element ;" +
                        NLT + $"rvt:guid \"{e.UniqueId}\" .";

                    string width = Math.Round(UnitUtils.ConvertFromInternalUnits(wall.Width, Autodesk.Revit.DB.DisplayUnitType.DUT_MILLIMETERS), 2).ToString().Replace(",", ".");
                    double curveLength = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                    string length = Math.Round(UnitUtils.ConvertFromInternalUnits(curveLength, Autodesk.Revit.DB.DisplayUnitType.DUT_MILLIMETERS), 2).ToString().Replace(",", ".");

                    if (cdt)
                    {
                        width = $"\"{width} mm\"^^cdt:length";
                        length = $"\"{length} mm\"^^cdt:length";
                    }
                    else
                    {
                        width = $"\"{width}\"^^xsd:decimal";
                        length = $"\"{length}\"^^xsd:decimal";
                    }

                    string name = $"\"{e.Name}\"";

                    if (opm)
                    {
                        pString += NL + Util.ToL3Prop(URI, "props:identityDataName", name, e.UniqueId);
                        pString += NL + Util.ToL3Prop(URI, "props:dimensionsWidth", width, e.UniqueId);
                        pString += NL + Util.ToL3Prop(URI, "props:dimensionsLength", length, e.UniqueId);
                    }
                    else
                    {
                        pString += NL + Util.ToL1Prop(URI, "props:identityDataName", name);
                        pString += NL + Util.ToL1Prop(URI, "props:dimensionsWidth", width);
                        pString += NL + Util.ToL1Prop(URI, "props:dimensionsLength", length);
                    }
                        
                }

                #endregion

                #region Windows and Doors

                
                IList<Element> WinDoor = new FilteredElementCollector(doc)
                    .WhereElementIsNotElementType().WherePasses(new LogicalOrFilter(new List<ElementFilter>
                        {                            
                            new ElementCategoryFilter(BuiltInCategory.OST_Windows),
                            new ElementCategoryFilter(BuiltInCategory.OST_Doors)

                         })).ToElements();

                tString += NL + NL + "# WINDOWS & DOORS";
                pString += NL + NL + "# WINDOWS & DOORS";

                foreach (Element e in WinDoor)
                {
                    string URI = Parameters.GenerateURIifNotExist(doc, e).Replace(Namespace, "inst:");
                    ElementDict.Add(e.Id, URI);

                    tString +=
                        NL + $"{URI}" +
                        NLT + "a bot:Element ;" +
                        NLT + "rvt:guid \"" + e.UniqueId + "\" .";

                    string name = $"\"{e.Name}\"";

                    if (opm)
                    {
                        pString += NL + Util.ToL3Prop(URI, "props:identityDataName", name, e.UniqueId);
                    }
                    else
                    {
                        pString += NL + Util.ToL1Prop(URI, "props:identityDataName", name);
                    }

                }

                #endregion

                #region Levels


                List<Level> levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level)).WhereElementIsNotElementType().ToElements().Cast<Level>()
                    .ToList();

                tString += NL + NL + "### STOREYS ###";

                foreach (Level e in levels)
                {
                    string URI = Parameters.GenerateURIifNotExist(doc, e).Replace(Namespace, "inst:");
                    ElementDict.Add(e.Id, URI);

                    tString +=
                        NL + $"<{URI}>" +
                        NLT + "a bot:Storey ;" +
                        NLT + "rvt:guid \"" + e.UniqueId + "\" .";

                    string name = $"\"{e.Name}\"";

                    if (opm)
                    {
                        pString += NL + Util.ToL3Prop(URI, "props:identityDataName", name, e.UniqueId);
                    }
                    else
                    {
                        pString += NL + Util.ToL1Prop(URI, "props:identityDataName", name);
                    }
                }

                #endregion

                #region Rooms/Spaces
                              
                List<Element> spaces = new FilteredElementCollector(doc)
                .OfClass(typeof(SpatialElement)).WhereElementIsNotElementType()
                  .Where(X => X.Category.Name == "Spaces" || X.Category.Name == "Rooms").ToList<Element>();

                tString += NL + NL + "### SPACES ###";
                pString += NL + NL + "### SPACES ###";

                foreach (Element e in spaces)
                {
                    string URI = Parameters.GenerateURIifNotExist(doc, e).Replace(Namespace, "inst:");
                    ElementDict.Add(e.Id, URI);

                    if (e.Category.Name == "Spaces")
                    {
                        Space space = e as Space;

                        tString +=
                            NL + $"{URI}" +
                            NLT + "a bot:Space ;" +
                            NLT + $"rvt:guid \"{space.UniqueId}\" .";

                        string area = Math.Round(UnitUtils.ConvertFromInternalUnits(space.Area, Autodesk.Revit.DB.DisplayUnitType.DUT_SQUARE_METERS), 2).ToString().Replace(",", ".");
                        string volume = Math.Round(UnitUtils.ConvertFromInternalUnits(space.Volume, Autodesk.Revit.DB.DisplayUnitType.DUT_CUBIC_METERS), 2).ToString().Replace(",", ".");

                        if (cdt)
                        {
                            area = $"\"{area} m2\"^^cdt:area";
                            volume = $"\"{volume} m3\"^^cdt:volume";
                        }
                        else
                        {
                            area = $"\"{area}\"^^xsd:decimal";
                            volume = $"\"{volume}\"^^xsd:decimal";
                        }

                        string name = $"\"{e.Name}\"";

                        if (space.LookupParameter("SpaceTypeURI") != null)
                        {
                            string typeURI = space.LookupParameter("SpaceTypeURI").AsString();
                            pString += NL + URI + NLT + $"ex:hasRequirementModel <{typeURI}> .";
                        }

                        if (opm)
                        {
                            pString += NL + Util.ToL3Prop(URI, "props:identityDataName", name, e.UniqueId);
                            pString += NL + Util.ToL3Prop(URI, "props:dimensionsArea", area, e.UniqueId);
                            pString += NL + Util.ToL3Prop(URI, "props:dimensionsVolume", volume, e.UniqueId);
                        }
                        else
                        {
                            pString += NL + Util.ToL1Prop(URI, "props:identityDataName", name);
                            pString += NL + Util.ToL1Prop(URI, "props:dimensionsArea", area);
                            pString += NL + Util.ToL1Prop(URI, "props:dimensionsVolume", volume);
                        }
                    }

                    if (e.Category.Name == "Rooms")
                    {
                        Room room = e as Room;

                        tString +=
                            NL + $"{URI}" +
                            NLT + "a bot:Space ;" +
                            NLT + $"rvt:guid \"{room.UniqueId}\" .";

                        string area = Math.Round(UnitUtils.ConvertFromInternalUnits(room.Area, Autodesk.Revit.DB.DisplayUnitType.DUT_SQUARE_METERS), 2).ToString().Replace(",", ".");
                        string volume = Math.Round(UnitUtils.ConvertFromInternalUnits(room.Volume, Autodesk.Revit.DB.DisplayUnitType.DUT_CUBIC_METERS), 2).ToString().Replace(",", ".");

                        if (cdt)
                        {
                            area = $"\"{area} m2\"^^cdt:area";
                            volume = $"\"{volume} m3\"^^cdt:volume";
                        }
                        else
                        {
                            area = $"\"{area}\"^^xsd:decimal";
                            volume = $"\"{volume}\"^^xsd:decimal";
                        }

                        string name = $"\"{room.Name}\"";
                        string number = $"\"{room.Number}\"";

                        if(room.LookupParameter("SpaceTypeURI") != null)
                        {
                            string typeURI = room.LookupParameter("SpaceTypeURI").AsString();
                            pString += NL + URI + NLT + $"ex:hasRequirementModel <{typeURI}> .";
                        }

                        if (opm)
                        {
                            pString += NL + Util.ToL3Prop(URI, "props:identityDataNumber", number, e.UniqueId);
                            pString += NL + Util.ToL3Prop(URI, "props:identityDataName", name, e.UniqueId);
                            pString += NL + Util.ToL3Prop(URI, "props:dimensionsArea", area, e.UniqueId);
                            pString += NL + Util.ToL3Prop(URI, "props:dimensionsVolume", volume, e.UniqueId);
                        }
                        else
                        {
                            pString += NL + Util.ToL1Prop(URI, "props:identityDataNumber", number);
                            pString += NL + Util.ToL1Prop(URI, "props:identityDataName", name);
                            pString += NL + Util.ToL1Prop(URI, "props:dimensionsArea", area);
                            pString += NL + Util.ToL1Prop(URI, "props:dimensionsVolume", volume);
                        }

                    }

                }

                #endregion

                #region Relationships

                tString += NL + NL + "### RELATIONSHIPS ###";

                tString += NL + NL + "# WINDOWS AND DOORS HOSTED IN A WALL";

                foreach (Element e in WinDoor)
                {
                    try
                    {
                        FamilyInstance FamIns = e as FamilyInstance;

                        tString +=
                                NL + $"{ElementDict[FamIns.Host.Id]} bot:hostsElement {ElementDict[e.Id]} .";
                    }
                    catch { }
                }

                tString += NL + NL + "# ROOMS AT EACH STOREY";

                foreach (Element e in spaces)
                {
                    
                    try
                    {                        
                        tString +=
                        NL + $"{ElementDict[e.LevelId]} bot:hasSpace {ElementDict[e.Id]} .";
                    }
                    catch { }
                }

                tString += NL + NL + "# ELEMENTS ADJACENT TO ROOMS";

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

                                    tString +=
                                        NL + $"{ElementDict[sp.Id]} bot:adjacentElement {ElementDict[id]} .";
                                }
                            }
                            catch
                            { }
                        }
                }

                #endregion

                using (StreamWriter writer =
                new StreamWriter(savefile.FileName))
                {
                    writer.Write(tString);
                }

                using (StreamWriter writer =
                new StreamWriter(savefile.FileName.Replace(".ttl", "_props.ttl")))
                {
                    writer.Write(pString);
                }

            }

            //TaskDialog.Show("Success", $"Successfully exported triples");

            return Result.Succeeded;

        }
    }
}
