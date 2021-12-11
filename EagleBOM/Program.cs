//
// Copyright (C) 2021 David Norris <danorris@gmail.com>. All rights reserved.
//

using NaturalSort.Extension;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;

namespace Norris.EagleBOM
{
    public class Program
	{
		private static string GetDefaultStockFilename()
		{
			string fn = Path.Combine(
				Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
				"Mouser Stock.txt");

			if (!File.Exists(fn))
				fn = Path.Combine(Environment.CurrentDirectory, "Mouser Stock.txt");

			if (!File.Exists(fn))
            {
				string here = Environment.CurrentDirectory;

				do
				{
					fn = Path.Combine(here, "BOM", "Mouser Stock.txt");
					string there = Path.GetDirectoryName(here);
					if (there == here || there == null) break;
					here = there;
				} while (!File.Exists(fn));
            }

			return fn;
		}

		public static int Main(string[] args)
        {
			try
			{
				CommandLine commandLine;

				try
				{
					commandLine = CommandLine.Parse(args);
				}

				catch (ArgumentException ex)
				{
					CommandLine.PrintUsage(Console.Error);
					Console.Error.WriteLine("Error: {0}", ex.Message);
					return -1;
				}

				if (commandLine.Help)
				{
					CommandLine.PrintUsage(Console.Out);
					return 0;
				}

				StockFile stockFile = StockFile.Read(commandLine.StockFile ?? GetDefaultStockFilename());
				Schematic schematic = Schematic.Read(commandLine.SchematicFile);

				decimal totalPrice = 0m;

				Console.WriteLine("All parts in design:");
				Console.WriteLine();

				IOrderedEnumerable<IGrouping<string, BomEntry>> query;
				if (commandLine.IndividualParts)
					query = schematic.BOM
						.GroupBy(part => string.Join("|", part.Name, part.Device?.Name ?? "", part.Value, part.Manufacturer, part.ManufacturerPartNumber, part.VendorPartNumber))
						.OrderBy(group => group.First().Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
				else
					query = schematic.BOM
						.GroupBy(part => string.Join("|", part.Device?.Name ?? "", part.Value, part.Manufacturer, part.ManufacturerPartNumber, part.VendorPartNumber))
						.OrderBy(group => group.First().Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort());

				foreach (var group in query)
				{
					BomEntry part = group.First();

					StockPart stockEntry = null;
					if (part.VendorPartNumber != null && null != (stockEntry = stockFile.Parts.FirstOrDefault(p => p.VendorPartNumber == part.VendorPartNumber)))
					{
						foreach (var individual in group)
						{
							if (stockEntry.Package != individual.Device?.Package)
								Console.Error.WriteLine("WARNING: Part {0} package {1} doesn't match Mouser stock spec {2} (MOUSERPN {3})",
									individual.Name, individual.Device?.Package, stockEntry.Package, individual.VendorPartNumber);

							if (stockEntry.Value != individual.Value)
								Console.Error.WriteLine("WARNING: Part {0} value {1} doesn't match Mouser stock spec {2} (MOUSERPN {3})",
									individual.Name, individual.Value ?? "-", stockEntry.Value ?? "-", individual.VendorPartNumber);
						}

						if (stockEntry.Manufacturer != part.Manufacturer)
							Console.Error.WriteLine("WARNING: Part {0} manufacturer {1} doesn't match Mouser stock spec {2} (MOUSERPN {3})",
								part.Name, part.Manufacturer, stockEntry.Manufacturer, part.VendorPartNumber);
					}

					else
					{
						Console.Error.WriteLine("WARNING: Part {0} ({1}) not found in Mouser stock", part.VendorPartNumber, part.Device?.Name);
					}

					decimal thisPrice = (stockEntry?.UnitPrice ?? 0m) * group.Count();
					totalPrice += thisPrice;

					bool first = true;
					string nameList = "";
					foreach (var el in group.OrderBy(x => x.Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
					{
						if (nameList.Length + 1 + el.Name.Length > 28)
						{
							if (first)
								Console.WriteLine("{0,-30}{1,-13}{2,-30}{3,-30}{4,-25}{5,10}{6,10}{7,8}{8,5}{9,8}",
									nameList + ',',
									string.IsNullOrEmpty(part.Value) ? "-" : part.Value,
									part.Manufacturer,
									part.ManufacturerPartNumber,
									part.VendorPartNumber,
									stockEntry?.QuantityAvailable,
									(stockEntry == null || stockEntry.QuantityInHouse == 0) ? "" : stockEntry.QuantityInHouse.ToString(),
									stockEntry?.UnitPrice.ToString("0.00"),
									group.Count(),
									thisPrice.ToString("0.00"));
							else
								Console.WriteLine("{0},", nameList);

							nameList = el.Name;
							first = false;
						}

						else
						{
							if (nameList.Length == 0) nameList = el.Name;
							else nameList += "," + el.Name;
						}
					}

					if (first)
						Console.WriteLine("{0,-30}{1,-13}{2,-30}{3,-30}{4,-25}{5,10}{6,10}{7,8}{8,5}{9,8}",
							nameList,
							string.IsNullOrEmpty(part.Value) ? "-" : part.Value,
							part.Manufacturer,
							part.ManufacturerPartNumber,
							part.VendorPartNumber,
							stockEntry?.QuantityAvailable,
							(stockEntry == null || stockEntry.QuantityInHouse == 0) ? "" : stockEntry.QuantityInHouse.ToString(),
							stockEntry?.UnitPrice.ToString("0.00"),
							group.Count(),
							thisPrice.ToString("0.00"));
					else
						Console.WriteLine("{0}", nameList);
				}

				Console.WriteLine();
				Console.WriteLine("Most expensive parts:");
				Console.WriteLine();

				foreach (var group in schematic.BOM
					.GroupBy(part => string.Join("|", part.Device?.Name, part.Value, part.Manufacturer, part.ManufacturerPartNumber, part.VendorPartNumber))
					.OrderByDescending(group => (GetPrice(stockFile, group.First()) ?? 0m) * group.Count())
					.Take(10))
				{
					string names = string.Join(",", group.Select(g => g.Name));
					BomEntry part = group.First();
					StockPart stockEntry = null;
					if (part.VendorPartNumber != null)
						stockEntry = stockFile.Parts.FirstOrDefault(x => x.VendorPartNumber == part.VendorPartNumber);
					decimal thisPrice = (stockEntry?.UnitPrice ?? 0m) * group.Count();
					Console.WriteLine("{0,-30}{1,-13}{2,-30}{3,-30}{4,-25}{5,10}{6,10}{7,8}{8,5}{9,8}",
						names,
						string.IsNullOrEmpty(part.Value) ? "-" : part.Value,
						part.Manufacturer,
						part.ManufacturerPartNumber,
						part.VendorPartNumber,
						stockEntry?.QuantityAvailable,
						(stockEntry == null || stockEntry.QuantityInHouse == 0) ? "" : stockEntry.QuantityInHouse.ToString(),
						stockEntry?.UnitPrice.ToString("0.00"),
						group.Count(),
						thisPrice.ToString("0.00"));
				}

				Console.WriteLine();
				Console.WriteLine("Total price: {0:0.00}", totalPrice);

				/*
				Console.WriteLine();
				Console.WriteLine("Packages in use:");
				Console.WriteLine();

				foreach (var package in schematic.Packages.OrderBy(entry => entry.Name, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
				{
					var list = package.Value;
					list.Sort(StringComparison.OrdinalIgnoreCase.WithNaturalSort());
					string usedParts = list[0];
					int k = 1;
					bool first = true;

					while (k < list.Count)
					{
						if (usedParts.Length + 1 + list[k].Length > 100)
						{
							Console.WriteLine("    {0,-60}{1}",
								first ? package.Key : "",
								usedParts);
							first = false;
							usedParts = list[k];
						}

						else
						{
							usedParts += ',' + list[k];
						}

						++k;
					}

					Console.WriteLine("    {0,-60}{1}",
						first ? package.Key : "",
						usedParts);
				}
				*/

				return 0;
			}

			catch (AggregateException ex)
            {
				foreach (var iex in ex.InnerExceptions)
					Console.Error.WriteLine("Error: {0}", iex.Message);
				return -1;
            }

			catch (Exception ex)
            {
				Console.Error.WriteLine("Error: {0}", ex.Message);
				return -1;
            }
        }

		/*
				public static int Mainx(string[] args)
				{
					bool showIndividualParts = false;
					string schFilename = null;
					string orderCsvFilename = null;
					int? orderMultiplier = null;

					for (int i = 0; i < args.Length; ++i)
					{
						if (args[i] == "/individual")
							showIndividualParts = true;
						else if (args[i].StartsWith("/"))
						{
							Console.Error.WriteLine("Unknown option {0}", args[i]);
							return -1;
						}
						else if (schFilename == null)
							schFilename = args[i];
						else if (orderCsvFilename == null)
							orderCsvFilename = args[i];
						else if (!orderMultiplier.HasValue)
						{
							if (!int.TryParse(args[i], out int value))
							{
								Console.Error.WriteLine("Invalid order multiplier");
								return -1;
							}
						}
						else
						{
							Console.Error.WriteLine("Unknown extra option {0}", args[i]);
							return -1;
						}
					}

					if (schFilename == null)
					{
						Console.Error.WriteLine("Usage: EagleBOM <sch> [order_csv [mult]]");
						return -1;
					}

					try
					{
						var xml = new XmlDocument();
						xml.Load(schFilename);

						var library = new Dictionary<string, Info>();

						foreach (XmlElement deviceSet in xml.SelectNodes("//deviceset"))
						{
							string deviceSetName = deviceSet.GetAttribute("name");
							bool userValue = deviceSet.GetAttribute("uservalue") == "yes";

							foreach (XmlElement device in deviceSet.SelectNodes("devices/device"))
							{
								string deviceName = device.GetAttribute("name");
								string package = device.GetAttribute("package");
								string deviceKey = deviceSetName + deviceName;

								var defaultTechnology = device.SelectSingleNode("technologies/technology[@name='']");

								string mfg = defaultTechnology.SelectSingleNode("attribute[@name='MFG']/@value")?.Value;
								string mfgpn = defaultTechnology.SelectSingleNode("attribute[@name='MFGPN']/@value")?.Value;
								string mouserpn = defaultTechnology.SelectSingleNode("attribute[@name='MOUSERPN']/@value")?.Value;

								library.Add(deviceKey, new Info()
								{
									mfg = mfg,
									mfgpn = mfgpn,
									mouserpn = mouserpn,
									userValue = userValue,
									package = package,
								});
							}
						}

						var exceptions = new HashSet<string>
						{
							"GROUND/GND/EARTH",
							"+3.3V",
							"+3.3V_A",
							"+12V",
							"-12V",
							"+5V",
							"A4L-LOC"
						};

						var stock = new Dictionary<string, StockEntry>();
						var packages = new Dictionary<string, List<string>>();

						using (var file = new FileStream(GetMouserStockFilename(), FileMode.Open, FileAccess.Read))
						using (var reader = new StreamReader(file))
						{
							while (true)
							{
								string line = reader.ReadLine();
								if (line == null) break;

								string[] tokens = line.Split(new[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
								if (tokens.Length < 7) continue;

								int qty = 0;
								int mystock = 0;
								int slash = tokens[1].IndexOf('/');

								if (slash >= 0)
								{
									qty = int.Parse(tokens[1].Substring(0, slash));
									mystock = int.Parse(tokens[1].Substring(slash + 1));
								}

								else
								{
									qty = int.Parse(tokens[1]);
								}

								stock.Add(tokens[0], new StockEntry()
								{
									qty = qty,
									mystock = mystock,
									price = decimal.Parse(tokens[2]),
									package = tokens[3],
									value = tokens[4] == "-" ? null : tokens[4],
									mfg = tokens[5],
									description = tokens[6],
								});
							}
						}

						var parts = new List<Part>();

						foreach (XmlElement part in xml.SelectNodes("//part"))
						{
							string deviceSetName = part.GetAttribute("deviceset");

							if (exceptions.Contains(deviceSetName))
								continue;

							string name = part.GetAttribute("name");
							string value = part.HasAttribute("value") ? part.GetAttribute("value") : null;
							string deviceName = part.GetAttribute("device");
							string deviceKey = deviceSetName + deviceName;

							if (string.IsNullOrEmpty(value))
								value = null;

							library.TryGetValue(deviceKey, out Info info);

							string mfg = part.SelectSingleNode("attribute[@name='MFG']/@value")?.Value;
							string mfgpn = part.SelectSingleNode("attribute[@name='MFGPN']/@value")?.Value;
							string mouserpn = part.SelectSingleNode("attribute[@name='MOUSERPN']/@value")?.Value;

							mfg = mfg ?? info?.mfg;
							mfgpn = mfgpn ?? info?.mfgpn;
							mouserpn = mouserpn ?? info?.mouserpn;

							if (mfg == null)
								Console.Error.WriteLine("WARNING: Part {0} ({1}{2}) is missing MFG attribute", name, deviceSetName, deviceName);
							if (mfgpn == null)
								Console.Error.WriteLine("WARNING: Part {0} ({1}{2}) is missing MFGPN attribute", name, deviceSetName, deviceName);
							if (mouserpn == null)
								Console.Error.WriteLine("WARNING: Part {0} ({1}{2}) is missing MOUSERPN attribute", name, deviceSetName, deviceName);
							if (value != null && info != null && !info.userValue)
								Console.Error.WriteLine("WARNING: Part {0} ({1}{2}) has a value but device has no user-editable value", name, deviceSetName, deviceName);

							parts.Add(new Part()
							{
								name = name,
								type = deviceKey,
								value = value,
								package = info?.package,
								mfg = mfg,
								mfgpn = mfgpn,
								mouserpn = mouserpn,
							});

							if (info?.package != null)
							{
								if (!packages.TryGetValue(info?.package, out List<string> list))
								{
									list = new List<string>();
									packages.Add(info?.package, list);
								}

								list.Add(name);
							}
						}

						decimal totalPrice = 0m;

						Console.WriteLine("All parts in design:");
						Console.WriteLine();

						IOrderedEnumerable<IGrouping<string, Part>> query;
						if (showIndividualParts)
							query = parts
								.GroupBy(part => string.Join("|", part.name, part.type, part.value, part.mfg, part.mfgpn, part.mouserpn))
								.OrderBy(group => group.First().name, StringComparison.OrdinalIgnoreCase.WithNaturalSort());
						else
							query = parts
								.GroupBy(part => string.Join("|", part.type, part.value, part.mfg, part.mfgpn, part.mouserpn))
								.OrderBy(group => group.First().name, StringComparison.OrdinalIgnoreCase.WithNaturalSort());

						foreach (var group in query)
						{
							Part part = group.First();

							StockEntry stockEntry = null;
							if (part.mouserpn != null && stock.TryGetValue(part.mouserpn, out stockEntry))
							{
								foreach (var individual in group)
								{
									if (stockEntry.package != individual.package)
										Console.Error.WriteLine("WARNING: Part {0} package {1} doesn't match Mouser stock spec {2} (MOUSERPN {3})",
											individual.name, individual.package, stockEntry.package, individual.mouserpn);

									if (stockEntry.value != individual.value)
										Console.Error.WriteLine("WARNING: Part {0} value {1} doesn't match Mouser stock spec {2} (MOUSERPN {3})",
											individual.name, individual.value ?? "-", stockEntry.value ?? "-", individual.mouserpn);
								}

								if (stockEntry.mfg != part.mfg)
									Console.Error.WriteLine("WARNING: Part {0} manufacturer {1} doesn't match Mouser stock spec {2} (MOUSERPN {3})",
										part.name, part.mfg, stockEntry.mfg, part.mouserpn);
							}

							else
							{
								Console.Error.WriteLine("WARNING: Part {0} ({1}) not found in Mouser stock", part.mouserpn, part.type);
							}

							decimal thisPrice = (stockEntry?.price ?? 0m) * group.Count();
							totalPrice += thisPrice;

							bool first = true;
							string nameList = "";
							foreach (var el in group.OrderBy(x => x.name, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
							{
								if (nameList.Length + 1 + el.name.Length > 28)
								{
									if (first)
										Console.WriteLine("{0,-30}{1,-13}{2,-30}{3,-30}{4,-25}{5,10}{6,10}{7,8}{8,5}{9,8}",
											nameList + ',',
											string.IsNullOrEmpty(part.value) ? "-" : part.value,
											part.mfg,
											part.mfgpn,
											part.mouserpn,
											stockEntry?.qty,
											(stockEntry == null || stockEntry.mystock == 0) ? "" : stockEntry.mystock.ToString(),
											stockEntry?.price.ToString("0.00"),
											group.Count(),
											thisPrice.ToString("0.00"));
									else
										Console.WriteLine("{0},", nameList);

									nameList = el.name;
									first = false;
								}

								else
								{
									if (nameList.Length == 0) nameList = el.name;
									else nameList += "," + el.name;
								}
							}

							if (first)
								Console.WriteLine("{0,-30}{1,-13}{2,-30}{3,-30}{4,-25}{5,10}{6,10}{7,8}{8,5}{9,8}",
									nameList,
									string.IsNullOrEmpty(part.value) ? "-" : part.value,
									part.mfg,
									part.mfgpn,
									part.mouserpn,
									stockEntry?.qty,
									(stockEntry == null || stockEntry.mystock == 0) ? "" : stockEntry.mystock.ToString(),
									stockEntry?.price.ToString("0.00"),
									group.Count(),
									thisPrice.ToString("0.00"));
							else
								Console.WriteLine("{0}", nameList);
						}

						Console.WriteLine();
						Console.WriteLine("Most expensive parts:");
						Console.WriteLine();

						foreach (var group in parts
							.GroupBy(part => string.Join("|", part.type, part.value, part.mfg, part.mfgpn, part.mouserpn))
							.OrderByDescending(group => (GetPrice(stock, group.First()) ?? 0m) * group.Count())
							.Take(10))
						{
							string names = string.Join(",", group.Select(g => g.name));
							Part part = group.First();
							StockEntry stockEntry = null;
							if (part.mouserpn != null)
								stock.TryGetValue(part.mouserpn, out stockEntry);
							decimal thisPrice = (stockEntry?.price ?? 0m) * group.Count();
								Console.WriteLine("{0,-30}{1,-13}{2,-30}{3,-30}{4,-25}{5,10}{6,10}{7,8}{8,5}{9,8}",
									names,
									string.IsNullOrEmpty(part.value) ? "-" : part.value,
									part.mfg,
									part.mfgpn,
									part.mouserpn,
									stockEntry?.qty,
									(stockEntry == null || stockEntry.mystock == 0) ? "" : stockEntry.mystock.ToString(),
									stockEntry?.price.ToString("0.00"),
									group.Count(),
									thisPrice.ToString("0.00"));
						}

						Console.WriteLine();
						Console.WriteLine("Total price: {0:0.00}", totalPrice);

						Console.WriteLine();
						Console.WriteLine("Packages in use:");
						Console.WriteLine();

						foreach (var package in packages.OrderBy(entry => entry.Key, StringComparison.OrdinalIgnoreCase.WithNaturalSort()))
						{
							var list = package.Value;
							list.Sort(StringComparison.OrdinalIgnoreCase.WithNaturalSort());
							string usedParts = list[0];
							int k = 1;
							bool first = true;

							while (k < list.Count)
							{
								if (usedParts.Length + 1 + list[k].Length > 100)
								{
									Console.WriteLine("    {0,-60}{1}",
										first ? package.Key : "",
										usedParts);
									first = false;
									usedParts = list[k];
								}

								else
								{
									usedParts += ',' + list[k];
								}

								++k;
							}

							Console.WriteLine("    {0,-60}{1}",
								first ? package.Key : "",
								usedParts);
						}

						if (orderCsvFilename != null)
						{
							int mult = orderMultiplier ?? 1;

							Console.Error.WriteLine();
							Console.Error.WriteLine("Verifying order...");

							var cols = new Dictionary<string, int>();
							var order = new Dictionary<string, int>();
							int mouserNoCol = -1;
							int qtyCol = -1;

							Regex csvRegex = new Regex(",(?=(?:[^\"]*\"[^\"]*\")*(?![^\"]*\"))");

							using (var csv = new FileStream(orderCsvFilename, FileMode.Open, FileAccess.Read))
							using (var reader = new StreamReader(csv))
							{
								while (true)
								{
									string line = reader.ReadLine();
									if (line == null) break;

									string[] tokens = csvRegex.Split(line);

									if (cols.Count == 0)
									{
										for (int i = 0; i < tokens.Length; ++i)
											if (!string.IsNullOrEmpty(tokens[i]))
												cols.Add(tokens[i].ToLowerInvariant(), i);

										if (!cols.TryGetValue("mouser no", out mouserNoCol))
											throw new Exception("CSV order file missing 'Mouser No' column");
										if (!cols.TryGetValue("order qty.", out qtyCol))
											throw new Exception("CSV order file missing 'Order Qty.' column");
									}

									else
									{
										string mouserNo = tokens[mouserNoCol];
										int qty = int.Parse(tokens[qtyCol]);

										if (order.ContainsKey(mouserNo))
										{
											Console.Error.WriteLine("WARNING: Order has duplicate entry for PN {0}", mouserNo);
											order[mouserNo] += qty;
										}

										else
										{
											order.Add(mouserNo, qty);
										}
									}
								}
							}

							foreach (var item in parts
								.GroupBy(part => part.mouserpn))
							{
								string wantPn = item.Key;
								int wantQty = item.Count() * mult;

								if (!order.TryGetValue(wantPn, out int haveQty))
									Console.Error.WriteLine("WARNING; Order is missing PN {0} qty {1}", wantPn, wantQty);
								else if (haveQty != wantQty)
									Console.Error.WriteLine("WARNING; Order PN {0} has qty {1} but want {2}", wantPn, haveQty, wantQty);
								order.Remove(wantPn);
							}

							foreach (var entry in order)
							{
								Console.Error.WriteLine("WARNING: Order has extra PN {0} qty {1}", entry.Key, entry.Value);
							}

							Console.Error.WriteLine("Order verification complete.");
						}

						return 0;
					}

					catch (Exception ex)
					{
						Console.Error.WriteLine(ex);
						return -1;
					}
				}
		*/

		private static decimal? GetPrice(StockFile stockFile, BomEntry part)
		{
			if (part.VendorPartNumber == null)
				return null;

			StockPart stockPart = stockFile.Parts.FirstOrDefault(p => p.VendorPartNumber == part.VendorPartNumber);
			return stockPart?.UnitPrice;
		}
	}
}