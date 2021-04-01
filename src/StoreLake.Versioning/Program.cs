using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace StoreLake.Versioning
{
    class Program
    {
        static int Main(string[] args)
        {
            try
            {
                Test01(ExtractFileName(args));
                return 0;
            }
            catch (Exception ex)
            {
                DumpException(ex);
                return -1;
            }
        }

        private static string ExtractFileName(string[] args)
        {
            // /f:common.props
            if (args.Length != 1 && args.Length != 2)
            {
                Console.WriteLine("USE this.exe /root=<root directory> /f:common.props");
            }
            string root = null;
            for (int ix = 0; ix < args.Length; ix++)
            {
                //Console.WriteLine(ix + " > " + args[ix]);
                var kv = args[ix].Split('=');
                if (kv.Length != 2)
                {
                    throw new InvalidOperationException("Invalid key=value argument specified:" + args[ix]);
                }

                Console.WriteLine(ix + " > " + kv[0] + "=" + kv[1]);
                if (kv[0] == "/root")
                {
                    root = kv[1];
                }
                else
                {
                    throw new InvalidOperationException("Unknown key=value argument specified:" + args[ix]);
                }
            }
            return root;
        }

        private static void DumpException(Exception ex)
        {
            Console.WriteLine(ex);
            if (ex.InnerException != null)
            {
                DumpException(ex.InnerException);
            }
            ReflectionTypeLoadException rtlex = ex as ReflectionTypeLoadException;
            if (rtlex != null)
            {
                Console.WriteLine("LoaderExceptions:" + rtlex.LoaderExceptions.Length);
                foreach (var err in rtlex.LoaderExceptions)
                {
                    DumpException(err);
                }
            }
        }

        private static void Test01(string root)
        {
            DirectoryInfo di = new DirectoryInfo(root);

            if (!di.Exists)
                throw new InvalidOperationException("Directory does not exists:" + di.FullName);
            Console.WriteLine(di.FullName);
            int updates_count = 0;
            foreach (var fi in di.GetFiles("common.props", SearchOption.AllDirectories))
            {
                Console.WriteLine(fi.FullName);
                //XDocument xDoc = XDocument.Parse(File.ReadAllText(fi.FullName));
                XDocument xDoc = XDocument.Load(fi.FullName);
                XElement xProject = xDoc.Root;
                bool fileChanged = false;
                foreach (var xPropertyGroup in xProject.Elements().Where(e => e.Name.LocalName == "PropertyGroup"))
                {
                    foreach (var xPackageVersion in xPropertyGroup.Elements().Where(e => e.Name.LocalName == "PackageVersion"))
                    {
                        //Console.WriteLine(xPackageVersion.Value);
                        Version vv = new Version(xPackageVersion.Value);
                        //Console.WriteLine(vv);
                        if (vv.Revision >= 0)
                            vv = new Version(vv.Major, vv.Minor, vv.Build, vv.Revision + 1);
                        else if (vv.Build >= 0)
                            vv = new Version(vv.Major, vv.Minor, vv.Build + 1);
                        else if (vv.Build >= 0)
                            vv = new Version(vv.Major, vv.Minor + 1);
                        else
                        {
                            vv = new Version(vv.Major + 1, 0);
                        }
                        xPackageVersion.Value = vv.ToString();
                        //Console.WriteLine(xPackageVersion.Value);
                        updates_count++;
                        fileChanged = true;
                    }
                }
                if (fileChanged)
                {
                    xDoc.Save(fi.FullName);
                }
            }
            if (updates_count == 0)
            {
                throw new NotSupportedException("common.props file with PackageVersion could not be found.");
            }
        }
    }
}
