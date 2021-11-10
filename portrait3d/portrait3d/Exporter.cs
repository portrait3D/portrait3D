using System;

namespace Portrait3D
{
    using Microsoft.Kinect.Toolkit.Fusion;
    using System.IO;
    using System.Globalization;
    using System.Collections.ObjectModel;

    /// <summary>
    /// Class for mesh export to file
    /// </summary>
    class Exporter
    {
        // Name of the file containing the next name to use for the next export file
        private const string DirectoryPath = "..\\..\\..\\";
        private const string ExportNameFileName = "exportName.txt";

        /// <summary>
        /// Save mesh in ASCII Wavefront .OBJ file
        /// </summary>
        /// <param name="mesh">Calculated mesh object</param>
        /// <param name="writer">The text writer</param>
        private static void SaveAsciiObjMesh(Mesh mesh, TextWriter writer)
        {
            if (null == mesh || null == writer)
            {
                return;
            }

            var vertices = mesh.GetVertices();
            var normals = mesh.GetNormals();
            var indices = mesh.GetTriangleIndexes();

            // Check mesh arguments
            if (0 == vertices.Count || 0 != vertices.Count % 3 || vertices.Count != indices.Count)
            {
                throw new ArgumentException(Properties.Resources.InvalidMeshArgument);
            }

            var centers = GetMeshXZCenters(vertices);

            // Sequentially write the 3 vertices of the triangle, for each triangle
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];

                string vertexString = "v " + (vertex.X - centers.Item1).ToString(CultureInfo.InvariantCulture) + " " + (-vertex.Y).ToString(CultureInfo.InvariantCulture) + 
                    " " + (-vertex.Z + centers.Item2).ToString(CultureInfo.InvariantCulture);

                writer.WriteLine(vertexString);
            }

            // Sequentially write the 3 normals of the triangle, for each triangle
            for (int i = 0; i < normals.Count; i++)
            {
                var normal = normals[i];
                              
                writer.WriteLine("vn " + normal.X.ToString(CultureInfo.InvariantCulture) + " " + (-normal.Y).ToString(CultureInfo.InvariantCulture) + " " + (-normal.Z).ToString(CultureInfo.InvariantCulture));
            }

            // Sequentially write the 3 vertex indices of the triangle face, for each triangle, starts at 1
            for (int i = 0; i < vertices.Count / 3; i++)
            {
                string baseIndex0 = ((i * 3) + 1).ToString(CultureInfo.InvariantCulture);
                string baseIndex1 = ((i * 3) + 2).ToString(CultureInfo.InvariantCulture);
                string baseIndex2 = ((i * 3) + 3).ToString(CultureInfo.InvariantCulture);

                string faceString = "f " + baseIndex0 + "//" + baseIndex0 + " " + baseIndex1 + "//" + baseIndex1 + " " + baseIndex2 + "//" + baseIndex2;
                writer.WriteLine(faceString);
            }
        }

        /// <summary>
        /// Calculates the center point of the model as the middle between the farthest X coordinates and Z coordinates
        /// </summary>
        /// <param name="vertex">List of vertices of the mesh</param>
        /// <returns>A tuple with the first element being the middle point between the two farthest X coordinates 
        /// and the second being the middle point between the two farthest Z coordinates</returns>
        private static Tuple<float, float> GetMeshXZCenters(ReadOnlyCollection<Vector3> vertices)
        {
            float maxX = float.MinValue;
            float minX = float.MaxValue;
            float maxZ = float.MinValue;
            float minZ = float.MaxValue;

            foreach (Vector3 vertex in vertices)
            {
                maxX = Math.Max(maxX, vertex.X);
                minX = Math.Min(minX, vertex.X);
                maxZ = Math.Max(maxZ, vertex.Z);
                minZ = Math.Min(minZ, vertex.Z);
            }

            return new Tuple<float, float>((maxX - minX) / 2.0f, (maxZ - minZ) / 2.0f);
        }

        /// <summary>
        /// Generates a save file dialog and saves based on the extension selected.
        /// </summary>
        /// <param name="mesh">Calculated mesh object</param>
        public static void ExportMeshToFile(Mesh mesh)
        {
            string exportFileName = File.Exists(DirectoryPath + ExportNameFileName) ? File.ReadAllText(DirectoryPath + ExportNameFileName) : "0";
            exportFileName = (int.Parse(exportFileName) + 1).ToString();

            Stream stream = File.OpenWrite(DirectoryPath + exportFileName + ".obj");
            StreamWriter streamWriter = new StreamWriter(stream);

            SaveAsciiObjMesh(mesh, streamWriter);
            streamWriter.Close();

            File.WriteAllText(DirectoryPath + ExportNameFileName, exportFileName);
        }
    }
}
