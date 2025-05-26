namespace StoreLake.Test
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    internal static class ResourceHelper
    {
        private const string path = "StoreLake.Test.";

        public static string GetSql(string resourcePath)
        {
            return GetResource(path + resourcePath + ".sql");
        }

        public static string GetResource(string resourcePath)
        {
            return LoadResourceText(typeof(ResourceHelper), resourcePath);
        }

        internal static string LoadResourceText(Type typeFromResourceAssembly, string resourceName)
        {
            using (TextReader textReader = LoadResourceString(typeFromResourceAssembly, resourceName))
            {
                return textReader.ReadToEnd();
            }
        }

        private static TextReader LoadResourceString(Type typeFromResourceAssembly, string resourceName)
        {
            Stream resourceStream = GetResourceStream(typeFromResourceAssembly, resourceName);
            TextReader textReader = new StreamReader(resourceStream);
            return textReader;
        }

        private static Stream GetResourceStream(Type typeResourceAssembly, string resourceName)
        {
            if (typeResourceAssembly == null)
                throw new ArgumentNullException(nameof(typeResourceAssembly));
            if (string.IsNullOrEmpty(resourceName))
                throw new ArgumentNullException(nameof(resourceName));

            Assembly resourceAssembly = typeResourceAssembly.Assembly;
            Stream resourceStream = resourceAssembly.GetManifestResourceStream(resourceName);
            if (resourceStream == null)
            {
                string[] names = resourceAssembly.GetManifestResourceNames()
                    .OrderBy(x => x).ToArray();

                if (names != null) { }

                throw new InvalidOperationException("Resource could not be found:[" + resourceName + "]."); // Available resources:" + string.Join(",", names));
            }
            else
                return resourceStream;
        }

        internal static IEnumerable<string> CollectResourceNamesByPrefix(Type typeResourceAssembly, string resourceNamePrefix)
        {
            if (typeResourceAssembly == null)
                throw new ArgumentNullException(nameof(typeResourceAssembly));

            Assembly resourceAssembly = typeResourceAssembly.Assembly;
            string[] names = resourceAssembly.GetManifestResourceNames()
                .OrderBy(x => x).ToArray();

            return names.Where(x => x.StartsWith(path + resourceNamePrefix)).ToArray();
        }
    }
}