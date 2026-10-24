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

        public Variant(string name, string powertrain, string output, string zeroToSixty, int defaultWheel,
            PaintOption[] paints, UpholsteryOption[] upholstery, string equipment)
        {
            Name = name;
            Powertrain = powertrain;
            Output = output;
            ZeroToSixty = zeroToSixty;
            DefaultWheel = defaultWheel;
            Paints = paints;
            Upholstery = upholstery;
            Equipment = ParseEquipment(equipment);
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
    public static readonly Variant[] Variants =
    {
        new("AMG GLE 63 S 4MATIC+", "Handcrafted AMG 4.0L V8 biturbo, hybrid assist", "603 hp", "3.7 s", 0,
            Paints(AlpineGrey, MoonlightWhite, MysticBlue, IrelandGreen, SiliconGrey, CoteDAzur),
            new[]
            {
                Seat(Nappa, "Black"), Seat(Nappa, "Macchiato Beige"), Seat(Nappa, "Red Pepper"), Seat(Nappa, "White"),
                Seat(Nappa, "Yacht Blue", true), Seat(Nappa, "Truffle Brown", true), Seat(Nappa, "Carmine Red", true),
            },
            "S-SSSSSSSOSSSOOO-"),
        new("AMG GLE 53 4MATIC+", "AMG 3.0L inline-6 turbo, hybrid assist", "428 hp", "4.9 s", 1,
            Paints(AlpineGrey, MoonlightWhite, MysticBlue, IrelandGreen, SiliconGrey, CoteDAzur),
            new[]
            {
                Seat(Leather, "Black"), Seat(Leather, "Macchiato Beige"), Seat(Leather, "Red Pepper"), Seat(Leather, "White"),
                Seat(Nappa, "Yacht Blue", true), Seat(Nappa, "Carmine Red", true),
            },
            "S-SSSSSSSOOSSOOO-"),
        new("GLE 580 4MATIC", "4.0L V8 biturbo, mild hybrid", "510 hp", "4.3 s", 1,
            Paints(AlpineGrey, MoonlightWhite, MysticBlue, IrelandGreen),
            new[]
            {
                Seat(Leather, "Black"), Seat(Leather, "Macchiato Beige"), Seat(Leather, "Bahia Brown"),
                Seat(Leather, "Catalana Beige"), Seat(Nappa, "Truffle Brown", true),
            },
            "S-SSSSSSSOSSSOOOO"),
        new("GLE 450e 4MATIC", "2.0L inline-4 turbo plug-in hybrid", "381 hp", "5.8 s", 2,
            Paints(AlpineGrey),
            new[]
            {
                Seat(MbTex, "Black"), Seat(MbTex, "Macchiato Beige"), Seat(MbTex, "Bahia Brown"),
                Seat(Nappa, "Black"), Seat(Nappa, "Macchiato Beige"),
            },
            "SSOOOSSS-OOOOOOO-"),
        new("GLE 450 4MATIC", "3.0L inline-6 turbo, mild hybrid", "375 hp", "5.3 s", 1,
            Paints(AlpineGrey, MysticBlue, IrelandGreen),
            new[]
            {
                Seat(MbTex, "Black"), Seat(MbTex, "Macchiato Beige"), Seat(MbTex, "Bahia Brown"),
                Seat(Nappa, "Black"), Seat(Nappa, "Macchiato Beige"),
            },
            "S-SSOSSS-OOOSOOOO"),
        new("GLE 350 4MATIC", "2.0L inline-4 turbo, mild hybrid", "255 hp", "7.0 s", 2,
            Paints(AlpineGrey), MbTexSeats,
            "S-O--SSS--OOOOOO-"),
        new("GLE 350", "2.0L inline-4 turbo, mild hybrid (RWD)", "255 hp", "6.9 s", 2,
            Paints(AlpineGrey), MbTexSeats,
            "--O--SSS--OOOOOO-"),
    };

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
