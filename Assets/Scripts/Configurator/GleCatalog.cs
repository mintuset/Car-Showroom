using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Static product data for the Mercedes-Benz GLE SUV (V167, 2026 model year, US market).
///
/// Sources (October 2026): MBUSA dealer trim guides (Mercedes-Benz of San Antonio,
/// Mercedes-Benz of Coral Gables), J.D. Power 2026 GLE preview, Edmunds, and dealer
/// colour guides (Vin Devers Autohaus, CarsDirect, Mercedes-Benz of Scottsdale).
/// Paint and upholstery RGB values are visual approximations of the factory colours.
/// Per-trim colour / upholstery availability follows the dealer guides, which only say
/// MANUFAKTUR finishes are "mainly higher trims and AMG"; confirm against the official
/// configurator before quoting it to a customer.
/// </summary>
public static class GleCatalog
{
    public const string ModelName = "Mercedes-Benz GLE SUV";
    public const string ModelYear = "2026";
    public const string SourceNote =
        "2026 US-market equipment from MBUSA dealer trim guides. Packages and availability vary; confirm with the official configurator.";

    public enum Availability { NotAvailable, Optional, Standard }
    public enum PaintFinish { Solid, Metallic }

    public sealed class PaintOption
    {
        public readonly string Name;
        public readonly Color Color;
        public readonly PaintFinish Finish;
        public readonly bool Manufaktur;

        public PaintOption(string name, string hex, PaintFinish finish, bool manufaktur = false)
        {
            Name = name;
            ColorUtility.TryParseHtmlString(hex, out Color);
            Finish = finish;
            Manufaktur = manufaktur;
        }
    }

    public sealed class UpholsteryOption
    {
        public readonly string Material;
        public readonly string Name;
        public readonly Color Color;
        public readonly bool Manufaktur;

        public UpholsteryOption(string material, string name, string hex, bool manufaktur = false)
        {
            Material = material;
            Name = name;
            ColorUtility.TryParseHtmlString(hex, out Color);
            Manufaktur = manufaktur;
        }

        public string DisplayName => Manufaktur ? $"MANUFAKTUR {Material} {Name}" : $"{Material} {Name}";
    }

    public sealed class WheelOption
    {
        public readonly string Name;
        public readonly string Description;

        public WheelOption(string name, string description)
        {
            Name = name;
            Description = description;
        }
    }

    public sealed class Variant
    {
        public readonly string Name;
        public readonly string Powertrain;
        public readonly string Output;
        public readonly string ZeroToSixty;
        public readonly int DefaultWheel;
        public readonly PaintOption[] Paints;
        public readonly UpholsteryOption[] Upholstery;
        /// <summary>One entry per <see cref="Features"/> row.</summary>
        public readonly Availability[] Equipment;
        /// <summary>US base MSRP, excluding destination.</summary>
        public readonly int BasePriceUsd;
        /// <summary>One entry per <see cref="Wheels"/> option; the default wheel is always 0.</summary>
        public readonly int[] WheelPricesUsd;

        public Variant(string name, string powertrain, string output, string zeroToSixty, int defaultWheel,
            PaintOption[] paints, UpholsteryOption[] upholstery, string equipment, int basePriceUsd, int[] wheelPricesUsd)
        {
            Name = name;
            Powertrain = powertrain;
            Output = output;
            ZeroToSixty = zeroToSixty;
            DefaultWheel = defaultWheel;
            Paints = paints;
            Upholstery = upholstery;
            Equipment = ParseEquipment(equipment);
            BasePriceUsd = basePriceUsd;
            WheelPricesUsd = wheelPricesUsd;
        }
    }

    // ---------------------------------------------------------------------
    // Paint
    // ---------------------------------------------------------------------
    static readonly PaintOption PolarWhite = new("Polar White", "#ECECE8", PaintFinish.Solid);
    static readonly PaintOption Black = new("Black", "#0B0B0C", PaintFinish.Solid);
    static readonly PaintOption ObsidianBlack = new("Obsidian Black Metallic", "#121318", PaintFinish.Metallic);
    static readonly PaintOption SeleniteGrey = new("Selenite Grey Metallic", "#4D5256", PaintFinish.Metallic);
    static readonly PaintOption CirrusSilver = new("Cirrus Silver Metallic", "#A3A7AB", PaintFinish.Metallic);
    static readonly PaintOption TwilightBlue = new("Twilight Blue Metallic", "#1B2639", PaintFinish.Metallic);

    static readonly PaintOption AlpineGrey = new("MANUFAKTUR Alpine Grey", "#9B9D99", PaintFinish.Solid, true);
    static readonly PaintOption MoonlightWhite = new("MANUFAKTUR Moonlight White Metallic", "#DCDAD3", PaintFinish.Metallic, true);
    static readonly PaintOption MysticBlue = new("MANUFAKTUR Mystic Blue Metallic", "#22304E", PaintFinish.Metallic, true);
    static readonly PaintOption IrelandGreen = new("MANUFAKTUR Ireland Mid Green Metallic", "#3E5143", PaintFinish.Metallic, true);
    static readonly PaintOption SiliconGrey = new("MANUFAKTUR Silicon Grey", "#6D7174", PaintFinish.Solid, true);
    static readonly PaintOption CoteDAzur = new("MANUFAKTUR Cote d'Azur Light Blue Metallic", "#6F93B4", PaintFinish.Metallic, true);

    static readonly PaintOption[] StandardPaints = { PolarWhite, Black, ObsidianBlack, SeleniteGrey, CirrusSilver, TwilightBlue };

    static PaintOption[] Paints(params PaintOption[] manufaktur)
    {
        var all = new PaintOption[StandardPaints.Length + manufaktur.Length];
        StandardPaints.CopyTo(all, 0);
        manufaktur.CopyTo(all, StandardPaints.Length);
        return all;
    }

    // ---------------------------------------------------------------------
    // Upholstery
    // ---------------------------------------------------------------------
    const string MbTex = "MB-Tex";
    const string Leather = "Leather";
    const string Nappa = "Nappa Leather";

    static UpholsteryOption Seat(string material, string name, bool manufaktur = false) =>
        new(material, name, UpholsteryHex(name), manufaktur);

    static string UpholsteryHex(string name) => name switch
    {
        "Black" => "#1B1B1C",
        "Macchiato Beige" => "#CDBB9F",
        "Bahia Brown" => "#5A3A28",
        "Catalana Beige" => "#B38D63",
        "White" => "#E4E1DA",
        "Red Pepper" => "#7A1D1B",
        "Yacht Blue" => "#1F2C45",
        "Truffle Brown" => "#4A3227",
        "Carmine Red" => "#8D1A20",
        _ => "#808080",
    };

    static readonly UpholsteryOption[] MbTexSeats =
    {
        Seat(MbTex, "Black"), Seat(MbTex, "Macchiato Beige"), Seat(MbTex, "Bahia Brown"),
    };

    // ---------------------------------------------------------------------
    // Wheels (one per tyre set child under the car, in order)
    // ---------------------------------------------------------------------
    public static readonly WheelOption[] Wheels =
    {
        new("21-in AMG Multi-Spoke", "Black with high-sheen finish"),
        new("20-in AMG Twin 5-Spoke", "Black with high-sheen finish, red brake calipers"),
        new("21-in AMG 5-Spoke", "Black with high-sheen finish, red brake calipers"),
        new("22-in AMG Cross-Spoke Forged", "Matte black (Night Package style)"),
    };

    // ---------------------------------------------------------------------
    // Equipment
    // ---------------------------------------------------------------------
    public static readonly string[] Features =
    {
        "4MATIC all-wheel drive",
        "Plug-in hybrid (up to 49 mi EV range)",
        "AMG body styling",
        "20-in or larger AMG wheels",
        "AIRMATIC air suspension",
        "Panorama sunroof",
        "64-colour LED ambient lighting",
        "Heated front seats",
        "Leather upholstery",
        "Nappa leather (AMG Line Interior)",
        "Ventilated front seats",
        "Burmester surround sound",
        "Surround View camera",
        "Head-up display",
        "Multicontour massage seats",
        "DRIVER ASSISTANCE package",
        "Third-row seating",
    };

    // Equipment strings: one char per Features row. S = standard, O = optional, - = not available.
    // Wheel prices follow the Wheels order; a wheel smaller than the trim's standard set is priced at 0 (no credit).
    public static readonly Variant[] Variants =
    {
        new("AMG GLE 63 S 4MATIC+", "Handcrafted AMG 4.0L V8 biturbo, hybrid assist", "603 hp", "3.7 s", 0,
            Paints(AlpineGrey, MoonlightWhite, MysticBlue, IrelandGreen, SiliconGrey, CoteDAzur),
            new[]
            {
                Seat(Nappa, "Black"), Seat(Nappa, "Macchiato Beige"), Seat(Nappa, "Red Pepper"), Seat(Nappa, "White"),
                Seat(Nappa, "Yacht Blue", true), Seat(Nappa, "Truffle Brown", true), Seat(Nappa, "Carmine Red", true),
            },
            "S-SSSSSSSOSSSOOO-",
            131_800, new[] { 0, 0, 200, 3_350 }),
        new("AMG GLE 53 4MATIC+", "AMG 3.0L inline-6 turbo, hybrid assist", "428 hp", "4.9 s", 1,
            Paints(AlpineGrey, MoonlightWhite, MysticBlue, IrelandGreen, SiliconGrey, CoteDAzur),
            new[]
            {
                Seat(Leather, "Black"), Seat(Leather, "Macchiato Beige"), Seat(Leather, "Red Pepper"), Seat(Leather, "White"),
                Seat(Nappa, "Yacht Blue", true), Seat(Nappa, "Carmine Red", true),
            },
            "S-SSSSSSSOOSSOOO-",
            90_000, new[] { 1_100, 0, 1_300, 4_450 }),
        new("GLE 580 4MATIC", "4.0L V8 biturbo, mild hybrid", "510 hp", "4.3 s", 1,
            Paints(AlpineGrey, MoonlightWhite, MysticBlue, IrelandGreen),
            new[]
            {
                Seat(Leather, "Black"), Seat(Leather, "Macchiato Beige"), Seat(Leather, "Bahia Brown"),
                Seat(Leather, "Catalana Beige"), Seat(Nappa, "Truffle Brown", true),
            },
            "S-SSSSSSSOSSSOOOO",
            90_000, new[] { 1_100, 0, 1_100, 2_750 }),
        new("GLE 450e 4MATIC", "2.0L inline-4 turbo plug-in hybrid", "381 hp", "5.8 s", 2,
            Paints(AlpineGrey),
            new[]
            {
                Seat(MbTex, "Black"), Seat(MbTex, "Macchiato Beige"), Seat(MbTex, "Bahia Brown"),
                Seat(Nappa, "Black"), Seat(Nappa, "Macchiato Beige"),
            },
            "SSOOOSSS-OOOOOOO-",
            72_250, new[] { 1_100, 1_050, 0, 2_750 }),
        new("GLE 450 4MATIC", "3.0L inline-6 turbo, mild hybrid", "375 hp", "5.3 s", 1,
            Paints(AlpineGrey, MysticBlue, IrelandGreen),
            new[]
            {
                Seat(MbTex, "Black"), Seat(MbTex, "Macchiato Beige"), Seat(MbTex, "Bahia Brown"),
                Seat(Nappa, "Black"), Seat(Nappa, "Macchiato Beige"),
            },
            "S-SSOSSS-OOOSOOOO",
            72_250, new[] { 1_100, 0, 1_100, 2_750 }),
        new("GLE 350 4MATIC", "2.0L inline-4 turbo, mild hybrid", "255 hp", "7.0 s", 2,
            Paints(AlpineGrey), MbTexSeats,
            "S-O--SSS--OOOOOO-",
            64_750, new[] { 1_100, 1_050, 0, 2_750 }),
        new("GLE 350", "2.0L inline-4 turbo, mild hybrid (RWD)", "255 hp", "6.9 s", 2,
            Paints(AlpineGrey), MbTexSeats,
            "--O--SSS--OOOOOO-",
            62_250, new[] { 1_100, 1_050, 0, 2_750 }),
    };

    // ---------------------------------------------------------------------
    // Pricing
    // ---------------------------------------------------------------------
    // Base MSRPs: Edmunds / U.S. News 2026 GLE trim pricing. Destination: U.S. News.
    // Options: dealer window stickers (metallic $750, MANUFAKTUR paint $1,750, Exclusive Nappa $1,560)
    // and Edmunds per-trim option lists (wheels, MANUFAKTUR interior $9,000).
    // Converted at the Oct 2026 rate; excludes Indian import duty, GST, registration and insurance.
    public const int DestinationUsd = 1_350;
    public const float UsdToInr = 95.00f;

    const int MetallicPaintUsd = 750;
    const int ManufakturPaintUsd = 1_750;
    const int NappaUpgradeUsd = 1_560;
    const int ManufakturInteriorUsd = 9_000;

    public static int PaintPriceUsd(PaintOption paint) =>
        paint.Manufaktur ? ManufakturPaintUsd
        : paint.Finish == PaintFinish.Metallic ? MetallicPaintUsd
        : 0;

    /// <summary>The first upholstery entry of each trim is its standard material.</summary>
    public static int UpholsteryPriceUsd(Variant variant, UpholsteryOption seat) =>
        seat.Manufaktur ? ManufakturInteriorUsd
        : seat.Material == variant.Upholstery[0].Material ? 0
        : NappaUpgradeUsd;

    public static long ToInr(int usd) => (long)Mathf.Round(usd * UsdToInr);

    /// <summary>Indian digit grouping: ₹1,25,21,000.</summary>
    public static string FormatInr(long rupees)
    {
        string digits = Math.Abs(rupees).ToString();
        var sb = new StringBuilder();
        int firstGroup = digits.Length > 3 ? digits.Length - 3 : digits.Length;
        // Everything before the last three digits is grouped in pairs.
        int lead = firstGroup % 2 == 0 ? 2 : 1;
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && i < firstGroup && (i - lead) % 2 == 0) sb.Append(',');
            if (i == firstGroup && firstGroup != digits.Length) sb.Append(',');
            sb.Append(digits[i]);
        }
        return (rupees < 0 ? "-₹" : "₹") + sb;
    }

    /// <summary>Lakh / crore shorthand, as Indian price lists quote it.</summary>
    public static string FormatInrShort(long rupees) =>
        rupees >= 1_00_00_000 ? $"₹{rupees / 1e7:0.00} Cr" : $"₹{rupees / 1e5:0.00} Lakh";

    static Availability[] ParseEquipment(string equipment)
    {
        if (equipment.Length != Features.Length)
            Debug.LogError($"GleCatalog: equipment string '{equipment}' has {equipment.Length} entries, expected {Features.Length}.");

        var result = new Availability[Features.Length];
        for (int i = 0; i < result.Length && i < equipment.Length; i++)
        {
            result[i] = equipment[i] switch
            {
                'S' => Availability.Standard,
                'O' => Availability.Optional,
                _ => Availability.NotAvailable,
            };
        }
        return result;
    }
}
