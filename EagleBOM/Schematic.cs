//
// Copyright (C) 2021 David Norris <danorris@gmail.com>. All rights reserved.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;

namespace Norris.EagleBOM
{
    public class Schematic
    {
        public static Schematic Read(string filename)
        {
            using (var stream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                return Read(stream);
        }

        public static Schematic Read(Stream stream)
        {
            using (var reader = new StreamReader(stream, Encoding.UTF8, true, 1024, true))
                return Read(reader);
        }

        public static Schematic Read(TextReader reader)
        {
            var schematic = new Schematic();

            var xml = new XmlDocument();
            xml.Load(reader);

			foreach (XmlElement deviceSet in xml.SelectNodes("//deviceset"))
			{
				string deviceSetName = deviceSet.GetAttribute("name");
				bool userValue = deviceSet.GetAttribute("uservalue") == "yes";

				foreach (XmlElement device in deviceSet.SelectNodes("devices/device"))
				{
					XmlNode defaultTechnology = device.SelectSingleNode("technologies/technology[@name='']");

					schematic.Devices.Add(new Device()
					{
                        Name                    = deviceSet.GetAttribute("name") + device.GetAttribute("name")?.Trim(),
						Manufacturer            = defaultTechnology?.SelectSingleNode("attribute[@name='MFG']/@value")?.Value?.Trim(),
						ManufacturerPartNumber  = defaultTechnology?.SelectSingleNode("attribute[@name='MFGPN']/@value")?.Value?.Trim(),
						VendorPartNumber        = defaultTechnology?.SelectSingleNode("attribute[@name='MOUSERPN']/@value")?.Value?.Trim(),
						HasUserValue            = deviceSet.GetAttribute("uservalue") == "yes",
						Package                 = device.GetAttribute("package")?.Trim(),
					});
				}
			}

			foreach (XmlElement part in xml.SelectNodes("//part"))
			{
				string deviceSetName = part.GetAttribute("deviceset")?.Trim() ?? "";

				if (ExemptComponents.Contains(deviceSetName))
					continue;

				string deviceName = deviceSetName + (part.GetAttribute("device")?.Trim() ?? "");
				Device device = schematic.Devices.FirstOrDefault(d => d.Name == deviceName);

				BomEntry entry;
				schematic.BOM.Add(entry = new BomEntry
				{
					Name = part.GetAttribute("name")?.Trim(),
					Value = part.HasAttribute("value") ? part.GetAttribute("value") : null,
					Device = device,
					Manufacturer = part.SelectSingleNode("attribute[@name='MFG']/@value")?.Value?.Trim() ?? device.Manufacturer,
					ManufacturerPartNumber = part.SelectSingleNode("attribute[@name='MFGPN']/@value")?.Value?.Trim() ?? device.ManufacturerPartNumber,
					VendorPartNumber = part.SelectSingleNode("attribute[@name='MOUSERPN']/@value")?.Value?.Trim() ?? device.VendorPartNumber,
				});

				if (entry.Manufacturer == null)
					Console.Error.WriteLine("WARNING: Part {0} ({1}) is missing MFG attribute", entry.Name, deviceName);
				if (entry.ManufacturerPartNumber == null)
					Console.Error.WriteLine("WARNING: Part {0} ({1}) is missing MFGPN attribute", entry.Name, deviceName);
				if (entry.VendorPartNumber == null)
					Console.Error.WriteLine("WARNING: Part {0} ({1}) is missing MOUSERPN attribute", entry.Name, deviceName);
				if (entry.Value != null && device != null && !device.HasUserValue)
					Console.Error.WriteLine("WARNING: Part {0} ({1}) has a value but device has no user-editable value", entry.Name, deviceName);
			}

			return schematic;
        }

        public ICollection<BomEntry> BOM { get; } = new List<BomEntry>();

        public ICollection<Device> Devices { get; } = new List<Device>();

        public ICollection<Package> Packages { get; } = new List<Package>();

		private static HashSet<string> ExemptComponents = new HashSet<string>
		{
			"GROUND/GND/EARTH",
			"+3.3V",
			"+3.3V_A",
			"+12V",
			"-12V",
			"+5V",
			"A4L-LOC"
		};
    }
}
