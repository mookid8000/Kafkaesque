﻿using System;
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

            _files = GetFiles(directoryPath);
        }

        public string DirectoryPath { get; }

        public bool IsEmpty => _files.Count == 0;

        public IOrderedEnumerable<FileSnap> GetFiles() => _files.OrderBy(f => f.FileNumber);

        public FileSnap FirstFile() => GetFiles().First();

        public FileSnap LastFile() => GetFiles().Last();

        public string GetFilePath(int fileNumber) => Path.Combine(DirectoryPath, $"log-{fileNumber:000000000}.dat");

        public void RegisterFile(string filePath)
        {
            _files.Add(FileSnap.Create(filePath));
        }

        public void RemoveFile(FileSnap file)
        {
            _files.Remove(file);
        }

        static List<FileSnap> GetFiles(string directoryPath)
        {
            try
            {
                return Directory
                    .GetFiles(directoryPath, "log-*.dat")
                    .Select(FileSnap.Create)
                    .ToList();
            }
            catch (DirectoryNotFoundException)
            {
                return new List<FileSnap>();
            }
        }
    }
}