//
// Copyright (C) 2021 David Norris <danorris@gmail.com>. All rights reserved.
//

namespace Norris.EagleBOM
{
    public class StockPart
    {
        public string VendorPartNumber;
        public uint QuantityAvailable = 0;
        public uint QuantityInHouse = 0;
        public decimal UnitPrice = 0;
        public string Package;
        public string Value;
        public string Manufacturer;
        public string Description;
    }
}
