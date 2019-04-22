using System;
using System.IO;

namespace Kafkaesque.Internals
{
    class FileSnap
    {
        public string FilePath { get; }
        public int FileNumber { get; }

        public FileSnap(string filePath, int fileNumber)
        {
            FilePath = filePath;
            FileNumber = fileNumber;
        }

        public static FileSnap Create(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath) ?? "";
            var parts = fileName.Split('-');

            if (parts.Length != 2)
            {
                throw new FormatException($"The file path '{filePath}' could not be interpreted as a proper log file, because it doesn't consist of two parts separated by '-'");
            }

            var fileNumberString = parts[1];
            
            if (!int.TryParse(fileNumberString, out var fileNumber))
            {
                throw new FormatException($"The file path '{filePath}' could not be interpreted as a proper log file, because the string '{fileNumberString}'");
            }

            return new FileSnap(filePath, fileNumber);
        }
    }
}