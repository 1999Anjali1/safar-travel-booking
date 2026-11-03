using Microsoft.EntityFrameworkCore;
using Safar.Application.Common;
using Safar.Domain.Entities;
using Safar.Domain.Enums;

namespace Safar.Infrastructure.Persistence;

/// <summary>
/// Fills an empty database with realistic demo data: staff + customer accounts, tour packages,
/// past and upcoming departures, and six months of bookings so the dashboard has something to show.
/// All demo accounts use the password: Password@123
/// </summary>
public static class DbSeeder
{
    public const string DemoPassword = "Password@123";

    public static async Task SeedAsync(AppDbContext db, IPasswordService passwords, TimeProvider clock, CancellationToken ct = default)
    {
        if (await db.Users.AnyAsync(ct)) return;

        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var rng = new Random(42); // deterministic demo data

        // ---------- users ----------
        User NewUser(string name, string email, UserRole role, string? phone = null)
        {
            var u = new User { FullName = name, Email = email, Role = role, Phone = phone, CreatedAt = now.AddDays(-200) };
            u.PasswordHash = passwords.Hash(u, DemoPassword);
            return u;
        }

        var admin = NewUser("Fatima Al-Sulaiti", "admin@safar.qa", UserRole.Admin, "+974 5500 1001");
        var agent = NewUser("Ravi Menon", "agent@safar.qa", UserRole.Agent, "+974 5500 1002");
        var demoCustomer = NewUser("Anjali Nair", "customer@safar.qa", UserRole.Customer, "+974 5512 3456");

        var customerNames = new[]
        {
            ("Mohammed Al-Kuwari", "Qatari"), ("Sara Thomas", "Indian"), ("Ahmed Hassan", "Egyptian"),
            ("Maria Santos", "Filipino"), ("Omar Khalid", "Jordanian"), ("Priya Sharma", "Indian"),
            ("James Wilson", "British"), ("Aisha Rahman", "Bangladeshi"), ("Yusuf Demir", "Turkish"),
            ("Leila Haddad", "Lebanese"), ("Arun Kumar", "Indian"), ("Noor Al-Thani", "Qatari")
        };
        var customers = customerNames.Select((c, i) =>
            NewUser(c.Item1, $"{c.Item1.Split(' ')[0].ToLower()}{i}@example.com", UserRole.Customer, $"+974 55{rng.Next(100000, 999999)}"))
            .ToList();
        customers.Insert(0, demoCustomer);

        db.Users.AddRange(admin, agent);
        db.Users.AddRange(customers);

        // ---------- promo codes ----------
        var welcome = new PromoCode
        {
            Code = "WELCOME10", Description = "10% off your first trip (max QAR 250)", DiscountPercent = 10,
            MaxDiscount = 250, ValidUntil = now.AddYears(1)
        };
        var nationalDay = new PromoCode
        {
            Code = "NATIONALDAY", Description = "Qatar National Day - 15% off", DiscountPercent = 15,
            ValidUntil = new DateTime(today.Year, 12, 31, 23, 59, 59, DateTimeKind.Utc), MaxUses = 100
        };
        var summer = new PromoCode
        {
            Code = "SUMMER20", Description = "Summer sale (ended)", DiscountPercent = 20,
            ValidUntil = now.AddMonths(-2), IsActive = false
        };
        db.PromoCodes.AddRange(welcome, nationalDay, summer);

        // ---------- packages ----------
        var packages = SamplePackages(now);
        db.TourPackages.AddRange(packages);
        await db.SaveChangesAsync(ct);

        // ---------- departures: every ~2 weeks, 5 months back to 4 months ahead ----------
        foreach (var p in packages)
        {
            var gap = p.DurationDays <= 2 ? 7 : 14;
            var capacity = p.DurationDays <= 2 ? 30 : 16;
            for (var date = today.AddDays(-150 + rng.Next(0, gap)); date <= today.AddDays(120); date = date.AddDays(gap))
            {
                p.Departures.Add(new Departure
                {
                    StartDate = date,
                    Capacity = capacity,
                    // a little seasonal pricing on some departures
                    PriceOverride = rng.NextDouble() < 0.2 ? Math.Round(p.BasePrice * 1.15m, 0) : null
                });
            }
        }
        await db.SaveChangesAsync(ct);

        // ---------- bookings ----------
        string[] requests = ["Vegetarian meals please", "Celebrating an anniversary", "Need a child seat", "Ground-floor room if possible"];
        foreach (var p in packages.Where(p => p.IsPublished))
        {
            foreach (var d in p.Departures)
            {
                var daysAway = d.StartDate.DayNumber - today.DayNumber;
                // past trips are fuller than far-future ones
                var targetFill = daysAway < 0 ? rng.Next(45, 95) : daysAway < 30 ? rng.Next(30, 80) : rng.Next(0, 45);
                var targetSeats = d.Capacity * targetFill / 100;

                while (d.SeatsBooked < targetSeats)
                {
                    var customer = customers[rng.Next(customers.Count)];
                    var seats = Math.Min(rng.Next(1, 5), d.Capacity - d.SeatsBooked);
                    var createdAt = d.StartDate.ToDateTime(new TimeOnly(rng.Next(7, 22), rng.Next(0, 60)), DateTimeKind.Utc)
                        .AddDays(-rng.Next(5, 45));
                    if (createdAt > now) createdAt = now.AddDays(-rng.Next(1, 25)).AddHours(-rng.Next(0, 24));

                    var travellers = Enumerable.Range(0, seats).Select(i => new Traveller
                    {
                        FullName = i == 0 ? customer.FullName : $"{FirstNames[rng.Next(FirstNames.Length)]} {customer.FullName.Split(' ').Last()}",
                        Nationality = customerNames.FirstOrDefault(c => c.Item1 == customer.FullName).Item2 ?? "Indian",
                        PassportNumber = $"{(char)('A' + rng.Next(26))}{rng.Next(1000000, 9999999)}"
                    }).ToList();

                    var promo = rng.NextDouble() < 0.05 && createdAt < welcome.ValidUntil ? welcome : null;
                    var booking = Booking.Create(customer.Id, d, travellers, promo, customer.Email, customer.Phone ?? "+974 5500 0000",
                        rng.NextDouble() < 0.2 ? requests[rng.Next(requests.Length)] : null, createdAt);
                    booking.User = customer;

                    var paidAt = createdAt.AddMinutes(rng.Next(1, 10));
                    booking.Confirm(new Payment
                    {
                        Amount = booking.Total, Status = PaymentStatus.Succeeded, Method = rng.NextDouble() < 0.7 ? "Card" : "ApplePay",
                        TransactionId = $"TXN-{paidAt:yyyyMMdd}-{rng.Next(10000000, 99999999)}", CreatedAt = paidAt
                    }, paidAt);

                    // ~8% of customers cancel a few days later
                    if (rng.NextDouble() < 0.08)
                    {
                        var cancelAt = paidAt.AddDays(rng.Next(1, 5));
                        if (cancelAt < now && DateOnly.FromDateTime(cancelAt) < d.StartDate)
                            booking.Cancel("Change of plans", cancelAt);
                    }

                    db.Bookings.Add(booking);
                }
            }
        }

        await db.SaveChangesAsync(ct);
    }

    private static readonly string[] FirstNames =
        ["Adam", "Zara", "Ali", "Meera", "Hana", "Rahul", "Lina", "Sami", "Diya", "Karim", "Emma", "Ivan"];

    private static List<TourPackage> SamplePackages(DateTime now) =>
    [
        new()
        {
            Slug = "doha-city-highlights", Title = "Doha City Highlights", Destination = "Doha", Country = "Qatar",
            Category = PackageCategory.City, DurationDays = 1, BasePrice = 180, IsPublished = true, CreatedAt = now.AddDays(-180),
            Summary = "A half-day guided tour of Doha's icons - from the Museum of Islamic Art to Souq Waqif and the Pearl.",
            Description = "Start at the Corniche with views of the West Bay skyline, then visit the Museum of Islamic Art and MIA Park. " +
                          "Walk through the lanes of Souq Waqif and the Falcon Souq, stop at Katara Cultural Village, " +
                          "and finish with coffee at the Pearl-Qatar marina. Hotel pick-up and drop-off included.",
            Highlights = ["Museum of Islamic Art", "Souq Waqif & Falcon Souq", "Katara Cultural Village", "The Pearl-Qatar"],
            Inclusions = ["Hotel pick-up and drop-off", "English / Arabic speaking guide", "Bottled water", "Arabic coffee & dates"]
        },
        new()
        {
            Slug = "desert-safari-inland-sea", Title = "Desert Safari & Inland Sea", Destination = "Khor Al Adaid", Country = "Qatar",
            Category = PackageCategory.Desert, DurationDays = 1, BasePrice = 350, IsPublished = true, CreatedAt = now.AddDays(-175),
            Summary = "Dune bashing in a 4x4, sandboarding and a swim where the desert meets the sea - a UNESCO-recognised reserve.",
            Description = "Our licensed drivers take you across the Sealine dunes to Khor Al Adaid, the Inland Sea on the Saudi border. " +
                          "Try sandboarding, ride a camel, and relax at a Bedouin-style camp with refreshments before the sunset drive back.",
            Highlights = ["Dune bashing in a Land Cruiser", "Khor Al Adaid Inland Sea", "Camel ride", "Sandboarding"],
            Inclusions = ["4x4 with professional driver", "Camel ride", "Sandboards", "Soft drinks & snacks", "Hotel transfers"]
        },
        new()
        {
            Slug = "overnight-desert-camp", Title = "Overnight Bedouin Desert Camp", Destination = "Sealine", Country = "Qatar",
            Category = PackageCategory.Desert, DurationDays = 2, BasePrice = 690, IsPublished = true, CreatedAt = now.AddDays(-160),
            Summary = "Sleep under the stars in a traditional majlis tent with a BBQ dinner, live oud music and a sunrise dune walk.",
            Description = "An afternoon safari followed by a night at a private beachfront desert camp. Enjoy a BBQ buffet, " +
                          "henna painting, stargazing and a campfire. Wake up for sunrise over the dunes and breakfast before the return.",
            Highlights = ["Beachfront desert camp", "BBQ dinner & live oud", "Stargazing", "Sunrise dune walk"],
            Inclusions = ["Tent accommodation", "Dinner & breakfast", "Desert safari", "Kayaks & sandboards", "Transfers"]
        },
        new()
        {
            Slug = "maldives-island-escape", Title = "Maldives Island Escape", Destination = "Malé Atolls", Country = "Maldives",
            Category = PackageCategory.Beach, DurationDays = 5, BasePrice = 4950, IsPublished = true, CreatedAt = now.AddDays(-150),
            Summary = "Four nights in a beach villa with speedboat transfers, a snorkelling safari and a sandbank picnic.",
            Description = "Fly direct from Doha to Malé and transfer by speedboat to a 4-star island resort. Days are yours to snorkel " +
                          "the house reef, join a dolphin cruise, or relax at the spa. Includes a private sandbank picnic on day 3.",
            Highlights = ["Beach villa", "Snorkelling safari", "Sunset dolphin cruise", "Private sandbank picnic"],
            Inclusions = ["Return flights from Doha", "4 nights half board", "Speedboat transfers", "Snorkel gear", "Travel insurance"]
        },
        new()
        {
            Slug = "georgia-mountains-tbilisi", Title = "Georgia: Tbilisi & the Caucasus", Destination = "Tbilisi", Country = "Georgia",
            Category = PackageCategory.Adventure, DurationDays = 6, BasePrice = 3450, IsPublished = true, CreatedAt = now.AddDays(-140),
            Summary = "Old Tbilisi, the Kazbegi mountains and Kakheti wine country - one of the most popular trips from Qatar.",
            Description = "Explore Tbilisi's Old Town and sulphur baths, drive the Georgian Military Highway to Gudauri and Kazbegi, " +
                          "and hike to Gergeti Trinity Church. Finish in Sighnaghi, the 'city of love', overlooking the Alazani valley.",
            Highlights = ["Old Tbilisi & cable car", "Kazbegi & Gergeti Trinity Church", "Gudauri viewpoint", "Sighnaghi"],
            Inclusions = ["Return flights", "5 nights 4-star hotels with breakfast", "Private driver-guide", "Entrance fees"]
        },
        new()
        {
            Slug = "istanbul-heritage-week", Title = "Istanbul Heritage Week", Destination = "Istanbul", Country = "Türkiye",
            Category = PackageCategory.Cultural, DurationDays = 5, BasePrice = 3200, IsPublished = true, CreatedAt = now.AddDays(-130),
            Summary = "Hagia Sophia, the Blue Mosque, a Bosphorus cruise and the Grand Bazaar with an expert historian guide.",
            Description = "Stay in Sultanahmet within walking distance of the Old City's greatest monuments. Guided visits to Hagia Sophia, " +
                          "Topkapi Palace and the Basilica Cistern, an evening Bosphorus dinner cruise, and a free day for shopping.",
            Highlights = ["Hagia Sophia & Blue Mosque", "Topkapi Palace", "Bosphorus dinner cruise", "Grand Bazaar"],
            Inclusions = ["Return flights", "4 nights boutique hotel", "Daily breakfast", "Guided tours", "Airport transfers"]
        },
        new()
        {
            Slug = "baku-long-weekend", Title = "Baku Long Weekend", Destination = "Baku", Country = "Azerbaijan",
            Category = PackageCategory.City, DurationDays = 4, BasePrice = 2150, IsPublished = true, CreatedAt = now.AddDays(-120),
            Summary = "Flame Towers, the walled Old City and the mud volcanoes of Gobustan - a short flight from Doha.",
            Description = "Discover Icherisheher (the UNESCO-listed Old City), the Heydar Aliyev Center and the Baku Boulevard. " +
                          "A day trip takes you to Gobustan's rock art and mud volcanoes, plus the eternal flame at Yanar Dag.",
            Highlights = ["Old City & Maiden Tower", "Heydar Aliyev Center", "Gobustan mud volcanoes", "Yanar Dag"],
            Inclusions = ["Return flights", "3 nights 4-star hotel", "Breakfast", "City tour & Gobustan trip", "Visa assistance"]
        },
        new()
        {
            Slug = "bali-honeymoon", Title = "Bali Honeymoon Retreat", Destination = "Ubud & Seminyak", Country = "Indonesia",
            Category = PackageCategory.Honeymoon, DurationDays = 7, BasePrice = 7800, IsPublished = true, CreatedAt = now.AddDays(-110),
            Summary = "Private pool villas in Ubud and Seminyak, a couples' spa ritual and a candlelit beach dinner.",
            Description = "Three nights in a jungle pool villa in Ubud with rice-terrace and waterfall tours, then three nights by the beach " +
                          "in Seminyak. Includes a Balinese couples' massage, a floating breakfast and a private beach dinner.",
            Highlights = ["Private pool villas", "Tegallalang rice terraces", "Couples' spa ritual", "Candlelit beach dinner"],
            Inclusions = ["Return flights", "6 nights villas with breakfast", "Private transfers", "Spa & dinner experiences"]
        },
        new()
        {
            Slug = "salalah-khareef", Title = "Salalah Khareef Season", Destination = "Salalah", Country = "Oman",
            Category = PackageCategory.Adventure, DurationDays = 4, BasePrice = 2600, IsPublished = false, CreatedAt = now.AddDays(-10),
            Summary = "Green mountains, waterfalls and misty beaches during Dhofar's monsoon season. (Draft - not yet published)",
            Description = "Visit Wadi Darbat, Ain Khor waterfall and Mughsail beach blowholes during the Khareef. Draft itinerary.",
            Highlights = ["Wadi Darbat", "Mughsail blowholes", "Frankincense Land Museum"],
            Inclusions = ["Return flights", "3 nights hotel", "Daily tours"]
        }
    ];
}
