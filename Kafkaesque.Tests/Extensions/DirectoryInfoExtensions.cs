using System;
using System.IO;
using System.Linq;
using Tababular;

namespace Kafkaesque.Tests.Extensions
{
    static class DirectoryInfoExtensions
    {
        static readonly TableFormatter DirectoryFormatter = new TableFormatter(new Hints { CollapseVerticallyWhenSingleLine = true });

        public static void DumpDirectoryContentsToConsole(this DirectoryInfo directoryInfo)
        {
            var files = directoryInfo.GetFiles()
                .Select(file => new { FilePath = file, Length = file.Length, Size = file.Length.FormatAsHumanReadableSize() })
                .ToList();

            Console.WriteLine($@"Listing directory {directoryInfo.FullName}:

{DirectoryFormatter.FormatObjects(files)}

");

        }
    }
}