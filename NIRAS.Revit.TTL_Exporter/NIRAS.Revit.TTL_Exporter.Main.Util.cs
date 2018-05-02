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

namespace NIRAS.Revit.TTL_Exporter
{
    class Util
    {

        public static string GetGuid(Document doc, Element e, string Host,string ProNum)
        {
            Transaction tx = new Transaction(doc);

            string cat = e.Category.Name;
            string elType = cat.ToLower();   // Make lower case
            elType = elType.Remove(cat.Length - 1); // Singularize

            //string guid = Host + "/" + ProNum + "/" + e.Category.Name + "/" + e.UniqueId;
            string guid = $"{Host}/{ProNum}/{elType}_{ e.UniqueId }";
            guid = guid.Replace(" ", "_");
            
            tx.Start("Add URL");

            e.LookupParameter("URI").Set(guid);
            tx.Commit();

            return guid;

        }

        public static string ToL1Prop(string property, string value)
        {
            return $"{property} {value}";
        }

        public static string ToL3Prop(string property, string value, string guid)
        {
            // Get property without prefix
            string prop = Regex.Matches(property, @"([^:]+)$")[0].Value;

            string propURI = $"inst:{prop}_{guid}";
            string stateURI = $"inst:state_{prop}_{ Guid.NewGuid().ToString() }";

            return $"{property} {propURI} .\n" +
                $"{propURI}\n" +
                $"\tseas:evaluation {stateURI} .\n" +
                $"{stateURI}\n" +
                $"\ta opm:CurrentState ;\n" +
                $"\tschema:value {value}";
        }



    }
}
