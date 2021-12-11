//
// Copyright (C) 2021 David Norris <danorris@gmail.com>. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Norris.EagleBOM
{
    public class StockFile
    {
        public static StockFile Read(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                return Read(stream);
        }

        public static StockFile Read(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                return Read(reader);
        }

        public static StockFile Read(TextReader reader)
        {
            var stockFile = new StockFile();

            while (true)
            {
                string line = reader.ReadLine();
                if (line == null) break;

                string[] tokens = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                if (tokens.Length < 7) continue;

                var part = new StockPart()
                {
                    VendorPartNumber    = tokens[0].Trim(),
                    Package             = tokens[3].Trim(),
                    Value               = tokens[4].Trim(),
                    Manufacturer        = tokens[5].Trim(),
                    Description         = tokens[6].Trim(),
                };

                string[] quantityTokens = tokens[1].Trim().Split('/');
                if (quantityTokens.Length >= 1) uint.TryParse(quantityTokens[0].Trim(), out part.QuantityAvailable);
                if (quantityTokens.Length >= 2) uint.TryParse(quantityTokens[1].Trim(), out part.QuantityInHouse);

                decimal.TryParse(tokens[2].Trim(), out part.UnitPrice);

                stockFile.Parts.Add(part);
            }

            return stockFile;
        }

        public ICollection<StockPart> Parts { get; } = new List<StockPart>();
    }
}
