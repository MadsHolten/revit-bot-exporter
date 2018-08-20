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
            // Escape illegal characters in guid
            guid = System.Uri.EscapeDataString(guid);

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


        public static string GetFacesAndEdges(Element e)
        {
            String xx = "";
            bool RetainCurvedSurfaceFacets = true;

            // Get element geometry
            Options opt = new Options();
            GeometryElement geomElem = e.get_Geometry(opt);

            int[] triangleIndices = new int[3];
            XYZ[] triangleCorners = new XYZ[3];
            List<string> faceVertices = new List<string>();
            List<string> faceNormals = new List<string>();
            List<string> faceElements = new List<string>();

            //// First we need to get transformation
            //LocationCurve lc = e.Location as LocationCurve;

            //// Get curve starting- and endpoint
            //XYZ startingPoint = lc.Curve.GetEndPoint(0);
            //XYZ endPoint = lc.Curve.GetEndPoint(1);

            foreach (GeometryObject geomObj in geomElem)
            {
                Solid geomSolid = geomObj as Solid;
                if (null != geomSolid)
                {

                    faceVertices.Clear();
                    faceNormals.Clear();
                    faceElements.Clear();

                    foreach (Face face in geomSolid.Faces)
                    {
                        // Triangulate face to get mesh
                        Mesh mesh = face.Triangulate();

                        int nTriangles = mesh.NumTriangles;

                        IList<XYZ> vertices = mesh.Vertices;

                        int nVertices = vertices.Count;

                        List<int> vertexCoordsMm = new List<int>(3 * nVertices);

                        // A vertex may be reused several times with 
                        // different normals for different faces, so 
                        // we cannot precalculate normals per vertex.
                        // List<double> normals = new List<double>( 3 * nVertices );

                        // Extract vertices
                        foreach (XYZ v in vertices)
                        {
                            vertexCoordsMm.Add(ConvertLengthToMM(v.X));
                            vertexCoordsMm.Add(ConvertLengthToMM(v.Y));
                            vertexCoordsMm.Add(ConvertLengthToMM(v.Z));
                        }

                        // Loop over triangles
                        for (int i = 0; i < nTriangles; ++i)
                        {
                            MeshTriangle triangle = mesh.get_Triangle(i);

                            for (int j = 0; j < 3; ++j)
                            {
                                int k = (int)triangle.get_Index(j);
                                triangleIndices[j] = k;
                                triangleCorners[j] = vertices[k];
                            }

                            // Calculate constant triangle facet normal.
                            XYZ v = triangleCorners[1]
                              - triangleCorners[0];
                            XYZ w = triangleCorners[2]
                              - triangleCorners[0];
                            XYZ triangleNormal = v
                              .CrossProduct(w)
                              .Normalize();

                            // List to store vertice indexes in the form: [v1//vn1 v2//vn2 v3//vn3]
                            List<string> vertIndexes = new List<string>();

                            for (int j = 0; j < 3; ++j)
                            {
                                int nFaceVertices = faceVertices.Count;

                                //if(nFaceVertices != faceNormals.Count)
                                //{
                                //    xx += "expected equal number of face vertex and normal coordinates\n";
                                //}

                                int i3 = triangleIndices[j] * 3;

                                // Rotate the X, Y and Z directions, 
                                // since the Z direction points upward
                                // in Revit as opposed to sideways or
                                // outwards or forwards in WebGL.

                                string vStr = $"v {vertexCoordsMm[i3]} {vertexCoordsMm[i3 + 1]} {vertexCoordsMm[i3 + 2]}";

                                // get vertice index
                                int vidx = faceVertices.IndexOf(vStr);

                                // add if not exist
                                if (vidx == -1)
                                {
                                    faceVertices.Add(vStr);
                                    vidx = faceVertices.Count-1;
                                }
                                    

                                string vnStr = "";
                                if (RetainCurvedSurfaceFacets)
                                {
                                    vnStr = $"vn {Math.Round(triangleNormal.X, 2)} {Math.Round(triangleNormal.Y, 2)} {Math.Round(triangleNormal.Z, 2)}";
                                }
                                else
                                {
                                    UV uv = face.Project(
                                      triangleCorners[j]).UVPoint;

                                    XYZ normal = face.ComputeNormal(uv);

                                    vnStr = $"vn {Math.Round(normal.X, 2)} {Math.Round(normal.Y, 2)} {Math.Round(normal.Z, 2)}";
                                }

                                // get face normal index
                                int vnidx = faceNormals.IndexOf(vnStr);

                                // add if not in list
                                if(vnidx == -1)
                                {
                                    faceNormals.Add(vnStr);
                                    vnidx = faceNormals.Count - 1;
                                }

                                // add indexes to list
                                vertIndexes.Add($"{vidx+1}//{vnidx+1}");

                            }

                            // Store face elements
                            string fStr = $"f {vertIndexes[0]} {vertIndexes[1]} {vertIndexes[2]}";
                            faceElements.Add(fStr);

                        }

                    }

                    // Write to string
                    xx += String.Join("\n", faceVertices) + "\n";
                    xx += String.Join("\n", faceNormals) + "\n";
                    xx += String.Join("\n", faceElements) + "\n";

                }
            }

           

            return xx;
        }

        public static int ConvertLengthToMM(Double len)
        {
            return Convert.ToInt32(Math.Round(UnitUtils.ConvertFromInternalUnits(len, Autodesk.Revit.DB.DisplayUnitType.DUT_MILLIMETERS)));
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