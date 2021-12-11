//
// Copyright (C) 2021 David Norris <danorris@gmail.com>. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;

namespace Norris.EagleBOM
{
    public class CommandLine
    {
        public static void PrintUsage(TextWriter writer)
        {
            writer.WriteLine("Usage: EagleBOM [options] schematic");
            writer.WriteLine();
            writer.WriteLine("Options:");
            writer.WriteLine();
            writer.WriteLine("    /sheets <sheets>");
            writer.WriteLine("        Only consider parts defined on the specified sheets. <sheets> can be a");
            writer.WriteLine("        number or a comma-separated list of numbers or hyphenated ranges.");
            writer.WriteLine();
            writer.WriteLine("    /stock <stockfile>");
            writer.WriteLine("        Use the specified stock file. If not given, the program searches for");
            writer.WriteLine("        'Mouser Stock.txt' in a predefined list of directories.");
            writer.WriteLine();
            writer.WriteLine("    /order <orderfile>");
            writer.WriteLine("        Check the BOM against the specified order file. If not given, no order");
            writer.WriteLine("        verification is done.");
            writer.WriteLine();
            writer.WriteLine("    /copies <n>");
            writer.WriteLine("        Assume N copies of each component. The default (of course) is 1.");
            writer.WriteLine();
            writer.WriteLine("    /individual");
            writer.WriteLine("        List each component separately in the BOM (don't group by type).");
            writer.WriteLine();
            writer.WriteLine("The BOM table is printed to standard output, while errors and warnings are");
            writer.WriteLine("printed to standard error.");
            writer.WriteLine();
        }

        public static CommandLine Parse(string[] args)
        {
            var commandLine = new CommandLine();
            int i = 0;

            string getNextArgument()
            {
                if (++i >= args.Length)
                    throw new ArgumentException(args[i - 1] + " requires an argument");
                return args[i];
            }

            for (; i < args.Length; ++i)
            {
                if (args[i] == "/h" || args[i] == "/help" || args[i] == "/?")
                    commandLine.Help = true;

                else if (args[i] == "/sheets")
                    commandLine.ParseSheets(getNextArgument());

                else if (args[i] == "/stock")
                    commandLine.StockFile = getNextArgument();

                else if (args[i] == "/order")
                    commandLine.OrderFile = getNextArgument();

                else if (args[i] == "/copies")
                {
                    if (!uint.TryParse(getNextArgument(), out uint copies))
                        throw new ArgumentException("Invalid copy count");
                    commandLine.Copies = copies;
                }

                else if (args[i] == "/individual")
                    commandLine.IndividualParts = true;

                else if (args[i].StartsWith("/"))
                    throw new ArgumentException("Unknown option {0}", args[i]);

                else
                {
                    if (commandLine.SchematicFile != null)
                        throw new ArgumentException("Extra arguments given");
                    commandLine.SchematicFile = args[i];
                }
            }

            if (commandLine.SchematicFile == null)
                throw new ArgumentException("No schematic filename given");

            return commandLine;
        }

        public bool Help { get; private set; }

        public HashSet<uint> Sheets { get; } = new HashSet<uint>();

        public string StockFile { get; private set; }

        public string OrderFile { get; private set; }

        public uint Copies { get; private set; } = 1;

        public string SchematicFile { get; private set; }

        public bool IndividualParts { get; private set; } = false;

        private void ParseSheets(string sheets)
        {
            uint a, b;

            foreach (string part in sheets.Split(','))
            {
                string[] ab = part.Split('-');

                if (ab.Length != 1 && ab.Length != 2)
                    throw new ArgumentException("Invalid sheet range");

                if (!uint.TryParse(ab[0], out a))
                    throw new ArgumentException("Invalid sheet range");

                if (ab.Length == 2)
                {
                    if (!uint.TryParse(ab[1], out b))
                        throw new ArgumentException("Invalid sheet range");
                }
                else b = a;

                for (uint i = a; i <= b; ++i)
                    Sheets.Add(i);
            }
        }
    }
}
