using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kafkaesque.Internals
{
    class DirSnap
    {
        readonly List<FileSnap> _files;

        public DirSnap(string directoryPath)
        {
            DirectoryPath = directoryPath ?? throw new ArgumentNullException(nameof(directoryPath));

            _files = Directory
                .GetFiles(directoryPath, "log-*.dat")
                .Select(FileSnap.Parse)
                .ToList();
        }

        public string DirectoryPath { get; }

        public bool IsEmpty => _files.Count == 0;

        public IOrderedEnumerable<FileSnap> GetFiles() => _files.OrderBy(f => f.FileNumber);

        public FileSnap FirstFile() => GetFiles().First();

        public FileSnap LastFile() => GetFiles().Last();

        public string GetFilePath(int fileNumber) => Path.Combine(DirectoryPath, $"log-{fileNumber}.dat");
    }
}