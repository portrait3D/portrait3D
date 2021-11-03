using System;

namespace Portrait3D
{
    using Microsoft.Kinect.Toolkit.Fusion;
    using System.IO;
    using System.Globalization;
    using Microsoft.Win32;

    class Exporter
    {
        /// <summary>
        /// Save mesh in binary .STL file
        /// </summary>
        /// <param name="mesh">Calculated mesh object</param>
        /// <param name="writer">Binary file writer</param>
        private static void SaveBinaryStlMesh(Mesh mesh, BinaryWriter writer)
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

            char[] header = new char[80];
            writer.Write(header);

            // Write number of triangles
            int triangles = vertices.Count / 3;
            writer.Write(triangles);

            // Sequentially write the normal, 3 vertices of the triangle and attribute, for each triangle
            for (int i = 0; i < triangles; i++)
            {
                // Write normal
                var normal = normals[i * 3];
                writer.Write(normal.X);
                writer.Write(-normal.Y);
                writer.Write(-normal.Z);

                // Write vertices
                for (int j = 0; j < 3; j++)
                {
                    var vertex = vertices[(i * 3) + j];
                    writer.Write(vertex.X);
                    writer.Write(-vertex.Y);
                    writer.Write(-vertex.Z);
                }

                ushort attribute = 0;
                writer.Write(attribute);
            }
        }

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

            // Write the header lines
            writer.WriteLine("#");
            writer.WriteLine("# OBJ file created by Microsoft Kinect Fusion");
            writer.WriteLine("#");

            // Sequentially write the 3 vertices of the triangle, for each triangle
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];

                string vertexString = "v " + vertex.X.ToString(CultureInfo.InvariantCulture) + " ";
                vertexString += (-vertex.Y).ToString(CultureInfo.InvariantCulture) + " " + (-vertex.Z).ToString(CultureInfo.InvariantCulture);

                writer.WriteLine(vertexString);
            }

            // Sequentially write the 3 normals of the triangle, for each triangle
            for (int i = 0; i < normals.Count; i++)
            {
                var normal = normals[i];

                string normalString = "vn " + normal.X.ToString(CultureInfo.InvariantCulture) + " ";

                if (flipAxes)
                {
                    normalString += (-normal.Y).ToString(CultureInfo.InvariantCulture) + " " + (-normal.Z).ToString(CultureInfo.InvariantCulture);
                }
                else
                {
                    normalString += normal.Y.ToString(CultureInfo.InvariantCulture) + " " + normal.Z.ToString(CultureInfo.InvariantCulture);
                }

                writer.WriteLine(normalString);
            }

            // Sequentially write the 3 vertex indices of the triangle face, for each triangle
            // Note this is typically 1-indexed in an OBJ file when using absolute referencing!
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
        /// Save mesh in ASCII .PLY file with per-vertex color
        /// </summary>
        /// <param name="mesh">Calculated mesh object</param>
        /// <param name="writer">The text writer</param>
        private static void SaveAsciiPlyMesh(Mesh mesh, TextWriter writer)
        {
            if (null == mesh || null == writer)
            {
                return;
            }

            var vertices = mesh.GetVertices();
            var indices = mesh.GetTriangleIndexes();

            // Check mesh arguments
            if (0 == vertices.Count || 0 != vertices.Count % 3 || vertices.Count != indices.Count)
            {
                throw new ArgumentException(Properties.Resources.InvalidMeshArgument);
            }

            int faces = indices.Count / 3;

            // Write the PLY header lines
            writer.WriteLine("ply");
            writer.WriteLine("format ascii 1.0");
            writer.WriteLine("comment file created by Microsoft Kinect Fusion");

            writer.WriteLine("element vertex " + vertices.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("property float x");
            writer.WriteLine("property float y");
            writer.WriteLine("property float z");

            writer.WriteLine("element face " + faces.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("property list uchar int vertex_index");
            writer.WriteLine("end_header");

            // Sequentially write the 3 vertices of the triangle, for each triangle
            for (int i = 0; i < vertices.Count; i++)
            {
                var vertex = vertices[i];

                string vertexString = vertex.X.ToString(CultureInfo.InvariantCulture) + " ";
                vertexString += (-vertex.Y).ToString(CultureInfo.InvariantCulture) + " " + (-vertex.Z).ToString(CultureInfo.InvariantCulture);

                writer.WriteLine(vertexString);
            }

            // Sequentially write the 3 vertex indices of the triangle face, for each triangle, 0-referenced in PLY files
            for (int i = 0; i < faces; i++)
            {
                string baseIndex0 = (i * 3).ToString(CultureInfo.InvariantCulture);
                string baseIndex1 = ((i * 3) + 1).ToString(CultureInfo.InvariantCulture);
                string baseIndex2 = ((i * 3) + 2).ToString(CultureInfo.InvariantCulture);

                string faceString = "3 " + baseIndex0 + " " + baseIndex1 + " " + baseIndex2;
                writer.WriteLine(faceString);
            }
        }


        /// <summary>
        /// Generates a save file dialog and saves based on the extension selected.
        /// </summary>
        /// <param name="mesh">Calculated mesh object</param>
        public static void ExportMeshToFile(Mesh mesh)
        {
            Stream stream;
            SaveFileDialog saveFileDialog = new SaveFileDialog();

            //Set our save dialog properties
            saveFileDialog.FileName = "Export.stl";
            saveFileDialog.Filter = "STL file (*.stl)|*.stl|OBJ file (*.obj)|*.obj|PLY file (*.ply)|*.ply|";
            saveFileDialog.DefaultExt = "stl";
            saveFileDialog.AddExtension = true;

            bool? cond = saveFileDialog.ShowDialog();
            if (cond.HasValue ? cond.Value : false)
            {
                if ((stream = saveFileDialog.OpenFile()) != null)
                {
                    //Fetch file extension
                    string extension = saveFileDialog.SafeFileName.Split('.')[1];

                    //Write per file extension
                    switch (extension)
                    {
                        case "stl":
                            SaveBinaryStlMesh(mesh, new BinaryWriter(stream));
                            break;

                        case "obj":
                            SaveAsciiObjMesh(mesh, new StreamWriter(stream));
                            break;

                        case "ply":
                            SaveAsciiPlyMesh(mesh, new StreamWriter(stream));
                            break;

                        default:
                            stream.Close();
                            throw new Exception("Extension not recognized");
                    }
                    stream.Close();
                }
            }
        }
    }
}
