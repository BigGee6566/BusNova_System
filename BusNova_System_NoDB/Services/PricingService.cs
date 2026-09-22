using System;
using System.Configuration;
using System.Globalization;

namespace BusNova_System.Services
{
    /// <summary>What a seat costs, and why.</summary>
    public class PriceQuote
    {
        public decimal BasePrice { get; set; }
        public decimal Price { get; set; }

        /// <summary>Positive when dearer than base, negative when cheaper.</summary>
        public decimal Adjustment { get { return Price - BasePrice; } }

        /// <summary>Each rule that moved the price, in the order applied. Shown to the passenger.</summary>
        public string[] Reasons { get; set; }

        public bool IsDiscounted { get { return Price < BasePrice; } }
        public bool IsSurcharged { get { return Price > BasePrice; } }
    }

    /// <summary>
    /// WP-11. Dynamic pricing.
    ///
    /// THE RULES ARE DOCUMENTED AND TESTABLE, WHICH IS THE POINT
    ///
    /// Dynamic pricing that nobody can explain is indistinguishable from
    /// overcharging, and it is the first thing a passenger complains
    /// about. So every rule here is stated, applied in a fixed order,
    /// bounded, and returned with the reason attached so the booking screen
    /// can show its working.
    ///
    /// In order:
    ///
    ///   1. Demand. Above 80% of seats sold, +15%. Below 30%, -10%.
    ///      A nearly-full bus is worth more; a nearly-empty one would
    ///      rather carry someone at a discount than run with the seat empty.
    ///
    ///   2. Lead time. Departing within 24 hours, +10%. More than 30 days
    ///      out, -10%. Late booking costs more because the alternative is
    ///      no seat at all; early booking is rewarded because it makes the
    ///      trip predictable.
    ///
    ///   3. Bounds. The final price is never below 70% or above 150% of
    ///      base, whatever the rules compute. A cap is what stops a bug in
    ///      one rule producing a fare nobody would pay, and a floor is what
    ///      stops the operator losing money on a quiet Tuesday.
    ///
    /// Every threshold and percentage is configurable. Pricing is a
    /// commercial decision, and a commercial decision that needs a rebuild
    /// is a commercial decision nobody makes.
    ///
    /// Rounding is to the nearest rand, away from zero, so a quoted fare is
    /// never a figure with cents a conductor cannot take in change.
    ///
    /// This class is pure: no database, no clock of its own. Everything it
    /// needs is an argument, which is what makes the rules testable.
    /// </summary>
    public static class PricingService
    {
        public static bool Enabled
        {
            get { return ReadBool("BusNova:DynamicPricing", false); }
        }

        public static decimal HighDemandThreshold { get { return ReadDecimal("BusNova:PricingHighDemandPercent", 80m); } }
        public static decimal LowDemandThreshold { get { return ReadDecimal("BusNova:PricingLowDemandPercent", 30m); } }
        public static decimal HighDemandUplift { get { return ReadDecimal("BusNova:PricingHighDemandUplift", 15m); } }
        public static decimal LowDemandDiscount { get { return ReadDecimal("BusNova:PricingLowDemandDiscount", 10m); } }

        public static int LastMinuteHours { get { return (int)ReadDecimal("BusNova:PricingLastMinuteHours", 24m); } }
        public static decimal LastMinuteUplift { get { return ReadDecimal("BusNova:PricingLastMinuteUplift", 10m); } }
        public static int EarlyBirdDays { get { return (int)ReadDecimal("BusNova:PricingEarlyBirdDays", 30m); } }
        public static decimal EarlyBirdDiscount { get { return ReadDecimal("BusNova:PricingEarlyBirdDiscount", 10m); } }

        public static decimal FloorPercent { get { return ReadDecimal("BusNova:PricingFloorPercent", 70m); } }
        public static decimal CeilingPercent { get { return ReadDecimal("BusNova:PricingCeilingPercent", 150m); } }

        /// <summary>
        /// Quotes a fare using the configured rules and the live clock.
        /// The overload below takes everything explicitly and is what the
        /// tests drive.
        /// </summary>
        public static PriceQuote Quote(decimal basePrice, int seatsSold, int capacity, DateTime departsAt)
        {
            return Quote(basePrice, seatsSold, capacity, departsAt, Helpers.AppClock.Now(), Enabled,
                new PricingRules
                {
                    HighDemandThreshold = HighDemandThreshold,
                    LowDemandThreshold = LowDemandThreshold,
                    HighDemandUplift = HighDemandUplift,
                    LowDemandDiscount = LowDemandDiscount,
                    LastMinuteHours = LastMinuteHours,
                    LastMinuteUplift = LastMinuteUplift,
                    EarlyBirdDays = EarlyBirdDays,
                    EarlyBirdDiscount = EarlyBirdDiscount,
                    FloorPercent = FloorPercent,
                    CeilingPercent = CeilingPercent
                });
        }

        /// <summary>The thresholds, so the tests can pin them instead of depending on Web.config.</summary>
        public class PricingRules
        {
            public decimal HighDemandThreshold = 80m;
            public decimal LowDemandThreshold = 30m;
            public decimal HighDemandUplift = 15m;
            public decimal LowDemandDiscount = 10m;
            public int LastMinuteHours = 24;
            public decimal LastMinuteUplift = 10m;
            public int EarlyBirdDays = 30;
            public decimal EarlyBirdDiscount = 10m;
            public decimal FloorPercent = 70m;
            public decimal CeilingPercent = 150m;
        }

        /// <summary>The whole policy, as a pure function.</summary>
        internal static PriceQuote Quote(decimal basePrice, int seatsSold, int capacity,
                                         DateTime departsAt, DateTime now, bool enabled, PricingRules rules)
        {
            if (basePrice < 0m) basePrice = 0m;

            var quote = new PriceQuote { BasePrice = basePrice, Price = basePrice };

            if (!enabled)
            {
                quote.Reasons = new[] { "Standard fare." };
                return quote;
            }

            var reasons = new System.Collections.Generic.List<string>();
            var multiplier = 1m;

            // ---- 1. demand ----
            // Capacity of zero means nobody recorded it. An unknown
            // occupancy is not a low occupancy, so it earns no discount
            // rather than the maximum one.
            if (capacity > 0)
            {
                var occupancy = (decimal)seatsSold * 100m / capacity;

                if (occupancy >= rules.HighDemandThreshold)
                {
                    multiplier += rules.HighDemandUplift / 100m;
                    reasons.Add("This trip is " + Math.Round(occupancy) + "% full, so the fare is " +
                                rules.HighDemandUplift.ToString("0.#") + "% higher.");
                }
                else if (occupancy <= rules.LowDemandThreshold)
                {
                    multiplier -= rules.LowDemandDiscount / 100m;
                    reasons.Add("This trip is quiet, so the fare is " +
                                rules.LowDemandDiscount.ToString("0.#") + "% lower.");
                }
            }

            // ---- 2. lead time ----
            var hoursAhead = (departsAt - now).TotalHours;

            if (hoursAhead <= rules.LastMinuteHours)
            {
                multiplier += rules.LastMinuteUplift / 100m;
                reasons.Add("Booking within " + rules.LastMinuteHours + " hours of departure adds " +
                            rules.LastMinuteUplift.ToString("0.#") + "%.");
            }
            else if (hoursAhead >= rules.EarlyBirdDays * 24)
            {
                multiplier -= rules.EarlyBirdDiscount / 100m;
                reasons.Add("Booking more than " + rules.EarlyBirdDays + " days ahead saves " +
                            rules.EarlyBirdDiscount.ToString("0.#") + "%.");
            }

            // ---- 3. bounds ----
            var floor = rules.FloorPercent / 100m;
            var ceiling = rules.CeilingPercent / 100m;

            if (multiplier < floor)
            {
                multiplier = floor;
                reasons.Add("The fare is held at the minimum of " + rules.FloorPercent.ToString("0.#") +
                            "% of the standard price.");
            }
            else if (multiplier > ceiling)
            {
                multiplier = ceiling;
                reasons.Add("The fare is capped at " + rules.CeilingPercent.ToString("0.#") +
                            "% of the standard price.");
            }

            // Whole rand. A conductor cannot make change for 7 cents.
            quote.Price = Math.Round(basePrice * multiplier, 0, MidpointRounding.AwayFromZero);

            if (reasons.Count == 0) reasons.Add("Standard fare.");
            quote.Reasons = reasons.ToArray();

            return quote;
        }

        /// <summary>
        /// Deliberately NOT NumberStyles.Number: that allows a thousands
        /// separator, and under InvariantCulture the comma is one, so
        /// "10,5" would parse as 105. Every percentage here would then be
        /// out by a factor of ten. Same defect as AUD-057.
        /// </summary>
        private static decimal ReadDecimal(string key, decimal fallback)
        {
            const NumberStyles Style = NumberStyles.AllowDecimalPoint
                                     | NumberStyles.AllowLeadingSign
                                     | NumberStyles.AllowLeadingWhite
                                     | NumberStyles.AllowTrailingWhite;

            decimal parsed;
            return decimal.TryParse(ConfigurationManager.AppSettings[key], Style,
                CultureInfo.InvariantCulture, out parsed) && parsed >= 0 ? parsed : fallback;
        }

        private static bool ReadBool(string key, bool fallback)
        {
            bool parsed;
            return bool.TryParse(ConfigurationManager.AppSettings[key], out parsed) ? parsed : fallback;
        }
    }
}
