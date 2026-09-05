using NetCodeGenerator.Serialization;
using System;
using System.IO;

namespace NetCodeGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length < 2)
            {
                Console.WriteLine("Usage: NetworkSerializationGenerator <inputFolder> <outputFolder>");
                return;
            }

            string inputFolder = args[0];
            string outputFolder = args[1];

            if (!Directory.Exists(inputFolder))
            {
                Console.WriteLine($"Input folder does not exist: {inputFolder}");
                return;
            }

            Directory.CreateDirectory(outputFolder);

            Console.WriteLine($"Scanning {inputFolder}...");
            var generator = new NetworkSerializationGenerator();
            generator.Generate(inputFolder, outputFolder);

            Console.WriteLine("Done.");
        }
    }
}