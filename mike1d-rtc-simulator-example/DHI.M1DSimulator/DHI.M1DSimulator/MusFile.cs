using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DHI.M1DSimulator
{
    /// <summary>
    /// Class for reading MIKE URBAN selection (mus) files.
    /// </summary>
    public class MusFile
    {
        public const string CommentSeparator = "#";

        /// <summary>
        /// Reads all .mus-files in directory into a dictionary.
        /// </summary>
        /// <param name="musDirectory"></param>
        /// <returns>Dictionary[musFileName][category]</returns>
        public static Dictionary<string, Dictionary<string, HashSet<string>>> ReadAllInDirectory(string musDirectory)
        {
            var musFilePaths = Directory.GetFiles(musDirectory, "*.mus");

            var output = new Dictionary<string, Dictionary<string, HashSet<string>>>();
            foreach (var musFilePath in musFilePaths)
            {
                var musFileContent = ReadMusFile(musFilePath);
                var musFileName = Path.GetFileNameWithoutExtension(musFilePath);
                output.Add(musFileName, musFileContent);
            }

            return output;
        }

        /// <summary>
        /// Reads mus-file [UTF8 encoding] and categorizes text lines depending on headline category.
        /// </summary>
        /// <param name="musFilePath">Filepath to .mus-file.</param>
        /// <param name="typePrefix">Prefix in all .mus-file headlines separating categories, e.g. msm_links.</param>
        /// <returns>Dictionary with structured .mus-file content.</returns>
        public static Dictionary<string, HashSet<string>> ReadMusFile(string musFilePath, string typePrefix = "ms")
        {
            var musContent = new Dictionary<string, HashSet<string>>();
            var musFile = File.ReadAllLines(musFilePath, Encoding.UTF8); // ToDo: Handle ANSI encoding with æøå

            var type = "";
            foreach (var line in musFile)
            {
                if (!string.IsNullOrEmpty(line))
                {
                    if (line.StartsWith(typePrefix))
                    {
                        type = line.Substring(line.LastIndexOf("_") + 1).Trim();
                        // Only add new type when not already in list
                        if (!string.IsNullOrEmpty(type) & !musContent.ContainsKey(type))
                            musContent.Add(type, new HashSet<string>());
                    }
                    else // Add MU id to list depending on the id type
                    {
                        if (!string.IsNullOrEmpty(type) & !line.Trim().StartsWith(CommentSeparator))
                            musContent[type].Add(RemoveComment(line, CommentSeparator));
                    }
                }
            }

            return musContent;
        }

        /// <summary>
        /// Combine lists from different categories.
        /// </summary>
        /// <param name="musContent"></param>
        /// <param name="categories"></param>
        /// <returns></returns>
        public static string[] GetCategories(Dictionary<string, HashSet<string>> musContent, params string[] categories)
        {
            var output = new List<string>();
            foreach (var category in categories)
                if (musContent.ContainsKey(category))
                    output.AddRange(musContent[category]);

            return output.ToArray();
        }

        /// <summary>
        /// Get list related to a specific category and add a prefix to all entries.
        /// </summary>
        /// <param name="musContent"></param>
        /// <param name="category"></param>
        /// <param name="prefix"></param>
        /// <returns></returns>
        public static string[] GetCategory(Dictionary<string, HashSet<string>> musContent, string category, string prefix = "")
        {
            if (musContent.ContainsKey(category))
                return musContent[category].Select(m => prefix + m).ToArray();

            return new string[0]; //throw new Exception("Could not find category: " + category);
        }

        private static string RemoveComment(string stringWithComment, string commentSeparator = "#")
        {
            // Removes characters from string after the first separator char (default: '#')
            string commentFreeString;
            var index = stringWithComment.IndexOf(commentSeparator);
            if (index > 0)
                commentFreeString = stringWithComment.Substring(0, index);
            else
                commentFreeString = stringWithComment;

            return commentFreeString.Trim(' ');
        }

        /// <summary>
        /// Reads all .mus-files in directory into a dictionary of selections.
        /// </summary>
        /// <param name="musDirectory"></param>
        /// <returns></returns>
        public static Dictionary<string, Selection> ReadAllInDirectoryToSelections(string musDirectory)
        {
            var selections = new Dictionary<string, Selection>();
            var musFiles = ReadAllInDirectory(musDirectory);
            foreach (var musFile in musFiles)
                selections.Add(musFile.Key, new Selection(musFile.Value));
            return selections;
        }
    }
}