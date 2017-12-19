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
    class Util
    {

        public static string GetGuid(Document doc, Element e, string Host,string ProNum)
        {
            Transaction tx = new Transaction(doc);


            string guid = Host + "/" + ProNum + "/" + e.Category.Name + "/" + e.UniqueId;
            guid = guid.Replace(" ", "_");
            

            if (e.LookupParameter("URI").AsString() == "" && e.GroupId == null)
            {
                tx.Start("Add URL");

                e.LookupParameter("URI").Set(guid);
                tx.Commit();
            }

            



            return guid;

        }



    }
}
