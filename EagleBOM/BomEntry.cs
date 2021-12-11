//
// Copyright (C) 2021 David Norris <danorris@gmail.com>. All rights reserved.
//

namespace Norris.EagleBOM
{
    public class BomEntry
    {
        public string Name;
        public string Value;
        public string Manufacturer;
        public string ManufacturerPartNumber;
        public string VendorPartNumber;
        public bool HasUserValue;
        public Device Device;
    }
}
