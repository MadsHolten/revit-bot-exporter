using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;

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
using Autodesk.Revit.DB.IFC;

namespace NIRAS.Revit.TTL_Exporter
{
    class Util
    {

        public static string TypeNameToId(string Name)
        {
            string id = Name.Replace("(", "").Replace(")", "").Replace(" ", "_");

            id = System.Uri.EscapeDataString(id);

            return id;
        }

        public static string CreateURI(Element e, string Host,string ProNum)
        {

            string cat = e.Category.Name;
            string elType = cat.ToLower();   // Make lower case
            elType = elType.Remove(cat.Length - 1); // Singularize

            string guid = System.Uri.EscapeDataString(e.GetIFCGUID());
            string uri = $"{Host}/{ProNum}/{elType}_{ guid }";
            //string uri = $"{Host}/{ProNum}/{elType}_{ e.UniqueId }";

            uri = uri.Replace(" ", "_");

            return uri;

        }

        public static string ToL1Prop(string foi, string property, string value)
        {
            return $"{foi}\n" + 
                $"{property} {value} .";
        }

        public static string ToL3Prop(string foi, string property, string value, string guid)
        {
            // Get property without prefix
            string prop = Regex.Matches(property, @"([^:]+)$")[0].Value;

            string propURI = $"inst:{prop}_{guid}";
            string stateURI = $"inst:state_{prop}_{ Guid.NewGuid().ToString() }";

            return $"{foi}\n" +
                $"\t{property} {propURI} .\n" +
                $"{propURI}\n" +
                $"\topm:hasPropertyState {stateURI} .\n" +
                $"{stateURI}\n" +
                $"\ta opm:CurrentPropertyState ;\n" +
                $"\tschema:value {value} .\n";
        }



    }

    static class Extensions
    {
        /// <summary>
        /// Get IFC GUID for element
        /// </summary>
        /// <param name="e">Revit Element</param>
        /// <returns>String IFCGUID</returns>
        public static string GetIFCGUID(this Element e)
        {
            // generate IFC GUID using IFC API
            string ifcid = ExporterIFCUtils.CreateAlternateGUID(e);

            // fallback to uniqueId in case of error
            if (ifcid == null || ifcid == string.Empty)
            {
                return e.UniqueId;
            }

            return ifcid;
        }
    }
}
