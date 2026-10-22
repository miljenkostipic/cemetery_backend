using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;

namespace Cemetery.Domain.Tests;

public sealed class BillingRulesTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid CemeteryId = Guid.CreateVersion7();

    [Theory]
    [InlineData("annual")]
    [InlineData("service")]
    public void Known_fee_kinds_are_accepted(string kind) => Assert.True(FeeKinds.IsKnown(kind));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Annual")]
    public void Unknown_fee_kinds_are_refused(string? kind) => Assert.False(FeeKinds.IsKnown(kind));

    [Theory]
    [InlineData("EUR")]
    [InlineData("CHF")]
    [InlineData("BAM")]
    public void Known_currencies_are_accepted(string currency) => Assert.True(FeeCurrencies.IsKnown(currency));

    [Theory]
    [InlineData(null)]
    [InlineData("eur")]
    [InlineData("USD")]
    public void Unknown_currencies_are_refused(string? currency) => Assert.False(FeeCurrencies.IsKnown(currency));

    [Theory]
    [InlineData("a")]
    [InlineData("  Annual fee  ")]
    public void A_fee_name_is_trimmed(string raw)
    {
        var name = raw.Trim();

        Assert.Equal(name, FeeName.Require(raw));
        Assert.True(FeeName.Accepts(raw));
    }

    [Fact]
    public void An_eighty_character_fee_name_is_kept()
    {
        var name = new string('a', FeeName.MaxLength);

        Assert.Equal(name, FeeName.Require(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\nAnnual")]
    [InlineData("Annual\u007f")]
    public void A_blank_or_control_fee_name_is_refused(string? raw)
    {
        var error = Assert.Throws<DomainRuleException>(() => FeeName.Require(raw));

        Assert.Equal(FeeName.Invalid, error.Code);
        Assert.False(FeeName.Accepts(raw));
    }

    [Fact]
    public void A_long_fee_name_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() => FeeName.Require(new string('a', FeeName.MaxLength + 1)));

        Assert.Equal(FeeName.Invalid, error.Code);
    }

    [Theory]
    [InlineData("0.01", "0.01")]
    [InlineData("25.50", "25.50")]
    [InlineData("999999.99", "999999.99")]
    public void A_positive_fee_amount_is_kept(string raw, string text)
    {
        Assert.Equal(text, FeeAmount.Text(FeeAmount.Require(raw)));
        Assert.True(FeeAmount.Accepts(raw));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("25")]
    [InlineData("0.00")]
    [InlineData("01.50")]
    [InlineData("1000000.00")]
    public void A_fee_amount_must_be_a_positive_two_decimal_value(string? raw)
    {
        var error = Assert.Throws<DomainRuleException>(() => FeeAmount.Require(raw));

        Assert.Equal(FeeAmount.Invalid, error.Code);
        Assert.False(FeeAmount.Accepts(raw));
    }

    [Theory]
    [InlineData(2000)]
    [InlineData(2100)]
    public void A_billing_year_inside_the_range_is_kept(int year)
    {
        Assert.Equal(year, BillingYear.Require(year));
        Assert.True(BillingYear.Accepts(year));
        Assert.Equal(new DateOnly(year, BillingYear.DueMonth, BillingYear.DueDay), BillingYear.DueOn(year));
    }

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    public void A_billing_year_outside_the_range_is_refused(int year)
    {
        var error = Assert.Throws<DomainRuleException>(() => BillingYear.Require(year));

        Assert.Equal(BillingYear.Invalid, error.Code);
        Assert.False(BillingYear.Accepts(year));
    }

    [Fact]
    public void A_fee_is_stored_for_its_cemetery()
    {
        var fee = FeeSchedule.Add(Tenant, CemeteryId, " annual ".Trim(), "  Upkeep  ", "25.00", "EUR");

        Assert.NotEqual(Guid.Empty, fee.Id);
        Assert.Equal(Tenant, fee.TenantId);
        Assert.Equal(CemeteryId, fee.CemeteryId);
        Assert.Equal(FeeKinds.Annual, fee.Kind);
        Assert.Equal("Upkeep", fee.Name);
        Assert.Equal(25.00m, fee.Amount);
        Assert.Equal(FeeCurrencies.Euro, fee.Currency);
    }

    [Fact]
    public void A_fee_needs_a_tenant_and_a_cemetery()
    {
        var missingTenant = Assert.Throws<DomainRuleException>(() => FeeSchedule.Add(Guid.Empty, CemeteryId, "annual", "Upkeep", "25.00", "EUR"));
        var missingCemetery = Assert.Throws<DomainRuleException>(() => FeeSchedule.Add(Tenant, Guid.Empty, "annual", "Upkeep", "25.00", "EUR"));

        Assert.Equal("tenant.required", missingTenant.Code);
        Assert.Equal("cemetery.not_found", missingCemetery.Code);
    }

    [Theory]
    [InlineData("nope", "Upkeep", "25.00", "EUR", "fee.kind_invalid")]
    [InlineData("annual", "Upkeep", "25.00", "USD", "fee.currency_invalid")]
    [InlineData("annual", " ", "25.00", "EUR", "fee.name_invalid")]
    [InlineData("annual", "Upkeep", "0.00", "EUR", "fee.amount_invalid")]
    public void An_invalid_fee_is_refused(string kind, string name, string amount, string currency, string code)
    {
        var error = Assert.Throws<DomainRuleException>(() => FeeSchedule.Add(Tenant, CemeteryId, kind, name, amount, currency));

        Assert.Equal(code, error.Code);
    }

    [Theory]
    [InlineData("open", true)]
    [InlineData("reminded", true)]
    [InlineData("paid", false)]
    public void Arrears_are_open_or_reminded_charges(string status, bool arrears) =>
        Assert.Equal(arrears, ChargeStatuses.IsArrears(status));

    [Fact]
    public void A_yearly_run_charges_each_open_site_once_for_each_annual_fee()
    {
        var annual = Fee("annual", "Upkeep");
        var service = Fee("service", "Wreath");
        var elsewhere = FeeSchedule.Add(Tenant, Guid.CreateVersion7(), "annual", "Other", "10.00", "EUR");
        var open = Site(CemeteryId, "A-1");
        var duplicate = Site(CemeteryId, "A-2");
        var closed = Site(CemeteryId, "A-3");
        var other = Site(elsewhere.CemeteryId, "B-1");
        closed.Close();
        var already = new HashSet<ChargeKey> { new(duplicate.Id, annual.Id) };

        var created = YearlyBilling.Raise(Tenant, CemeteryId, 2026, [annual, service, elsewhere], [open, duplicate, closed, other], already);

        var charge = Assert.Single(created);
        Assert.Equal(open.Id, charge.GraveSiteId);
        Assert.Equal(annual.Id, charge.FeeScheduleId);
        Assert.Equal("A-1", charge.GraveSiteCode);
        Assert.Equal("", charge.HolderName);
        Assert.Equal(FeeKinds.Annual, charge.Kind);
        Assert.Equal("Upkeep", charge.Description);
        Assert.Equal(25.00m, charge.Amount);
        Assert.Equal(FeeCurrencies.Euro, charge.Currency);
        Assert.Equal(ChargeStatuses.Open, charge.Status);
        Assert.Equal(new DateOnly(2026, BillingYear.DueMonth, BillingYear.DueDay), charge.DueOn);
        Assert.Equal(2026, charge.Year);
        Assert.Null(charge.LatestPaymentId);
    }

    [Fact]
    public void A_year_with_no_sites_raises_nothing()
    {
        var created = YearlyBilling.Raise(Tenant, CemeteryId, 2026, [Fee("annual", "Upkeep")], [], new HashSet<ChargeKey>());

        Assert.Empty(created);
    }

    [Fact]
    public void A_year_with_no_annual_fees_raises_nothing()
    {
        var created = YearlyBilling.Raise(Tenant, CemeteryId, 2026, [], [Site(CemeteryId, "A-1")], new HashSet<ChargeKey>());

        Assert.Empty(created);
    }

    [Fact]
    public void A_charge_stays_inside_its_cemetery()
    {
        var fee = Fee("annual", "Upkeep");
        var site = Site(CemeteryId, "A-1");
        var foreignFee = FeeSchedule.Add(Guid.CreateVersion7(), CemeteryId, "annual", "Other", "10.00", "EUR");
        var foreignSite = GraveSite.Place(Guid.CreateVersion7(), CemeteryId, Guid.CreateVersion7(), null, "A-2", GraveSiteKind.SingleGrave, Box());
        var otherCemetery = FeeSchedule.Add(Tenant, Guid.CreateVersion7(), "annual", "Other", "10.00", "CHF");

        var missingTenant = Assert.Throws<DomainRuleException>(() => Charge.Open(Guid.Empty, site, fee, 2026));
        var foreignSiteError = Assert.Throws<DomainRuleException>(() => Charge.Open(Tenant, foreignSite, fee, 2026));
        var foreignFeeError = Assert.Throws<DomainRuleException>(() => Charge.Open(Tenant, site, foreignFee, 2026));
        var otherCemeteryError = Assert.Throws<DomainRuleException>(() => Charge.Open(Tenant, site, otherCemetery, 2026));
        var yearError = Assert.Throws<DomainRuleException>(() => Charge.Open(Tenant, site, fee, 1999));

        Assert.Equal("tenant.required", missingTenant.Code);
        Assert.Equal("tenant.required", foreignSiteError.Code);
        Assert.Equal("tenant.required", foreignFeeError.Code);
        Assert.Equal("cemetery.not_found", otherCemeteryError.Code);
        Assert.Equal(BillingYear.Invalid, yearError.Code);
    }

    [Theory]
    [InlineData("monri")]
    [InlineData("paypal")]
    [InlineData("payrexx")]
    public void Known_providers_can_be_connected(string provider)
    {
        Assert.True(BillingProviders.IsKnown(provider));
        Assert.Contains(provider, BillingProviders.All);

        var account = MerchantAccount.Connect(Tenant, provider, new MerchantCredentials("merchant-1", "secret-ok", true));

        Assert.Equal(provider, account.Provider);
        Assert.Equal("merchant-1", account.MerchantId);
        Assert.Equal("secret-ok", account.Secret);
        Assert.True(account.Enabled);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("stripe")]
    [InlineData("Monri")]
    public void An_unknown_provider_is_refused(string? provider)
    {
        Assert.False(BillingProviders.IsKnown(provider));
        var error = Assert.Throws<DomainRuleException>(() => BillingProviders.Require(provider));

        Assert.Equal("payment.provider_invalid", error.Code);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("merchant.id")]
    public void A_merchant_id_is_kept(string merchantId)
    {
        Assert.Equal(merchantId, MerchantCode.Require(merchantId));
        Assert.True(MerchantCode.Accepts(merchantId));
    }

    [Fact]
    public void A_sixty_four_character_merchant_id_is_kept()
    {
        var merchantId = new string('a', MerchantCode.MaxLength);

        Assert.Equal(merchantId, MerchantCode.Require(merchantId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("a")]
    [InlineData("has space")]
    public void A_merchant_id_must_be_a_short_token(string? merchantId)
    {
        var error = Assert.Throws<DomainRuleException>(() => MerchantCode.Require(merchantId));

        Assert.Equal(MerchantCode.Invalid, error.Code);
        Assert.False(MerchantCode.Accepts(merchantId));
    }

    [Fact]
    public void A_long_merchant_id_is_refused()
    {
        var error = Assert.Throws<DomainRuleException>(() => MerchantCode.Require(new string('a', MerchantCode.MaxLength + 1)));

        Assert.Equal(MerchantCode.Invalid, error.Code);
    }

    [Fact]
    public void A_provider_secret_must_be_a_single_line()
    {
        Assert.Equal(new string('a', ProviderSecret.MinLength), ProviderSecret.Require(new string('a', ProviderSecret.MinLength)));
        Assert.Equal(new string('a', ProviderSecret.MaxLength), ProviderSecret.Require(new string('a', ProviderSecret.MaxLength)));
        Assert.True(ProviderSecret.Accepts("secret-ok"));
        Assert.False(ProviderSecret.Accepts(null));
        Assert.False(ProviderSecret.Accepts("short"));
        Assert.False(ProviderSecret.Accepts(new string('a', ProviderSecret.MaxLength + 1)));
        Assert.False(ProviderSecret.Accepts(" secret-ok"));
        Assert.False(ProviderSecret.Accepts("secret-ok "));
        Assert.False(ProviderSecret.Accepts("secret\nok"));
        Assert.False(ProviderSecret.Accepts("secret\rok"));
    }

    [Fact]
    public void Replacing_a_connection_keeps_the_provider_and_drops_the_old_secret()
    {
        var account = MerchantAccount.Connect(Tenant, "monri", new MerchantCredentials("merchant-1", "secret-ok", true));

        account.Replace(new MerchantCredentials("merchant-2", "next-secret", false));

        Assert.Equal("monri", account.Provider);
        Assert.Equal("merchant-2", account.MerchantId);
        Assert.Equal("next-secret", account.Secret);
        Assert.False(account.Enabled);
    }

    [Fact]
    public void A_connection_needs_a_tenant_and_a_known_provider()
    {
        var credentials = new MerchantCredentials("merchant-1", "secret-ok", true);
        var missingTenant = Assert.Throws<DomainRuleException>(() => MerchantAccount.Connect(Guid.Empty, "monri", credentials));
        var unknown = Assert.Throws<DomainRuleException>(() => MerchantAccount.Connect(Tenant, "stripe", credentials));

        Assert.Equal("tenant.required", missingTenant.Code);
        Assert.Equal("payment.provider_invalid", unknown.Code);
    }

    [Fact]
    public void An_open_charge_can_be_reminded_once()
    {
        var charge = Charge.Open(Tenant, Site(CemeteryId, "A-1"), Fee("annual", "Upkeep"), 2026);

        Assert.True(charge.Remind());
        Assert.Equal(ChargeStatuses.Reminded, charge.Status);
        Assert.False(charge.Remind());
        Assert.Equal(1, ReminderBilling.Mark([charge, Charge.Open(Tenant, Site(CemeteryId, "A-2"), Fee("annual", "Upkeep"), 2026)]));
    }

    [Fact]
    public void A_cash_payment_can_be_voided_and_the_charge_opens_again()
    {
        var charge = Charge.Open(Tenant, Site(CemeteryId, "A-1"), Fee("annual", "Upkeep"), 2026);
        var payment = Payment.Record(charge, "cash", "25.00", new DateOnly(2026, 10, 7), " HR12 ");

        Assert.Equal(ChargeStatuses.Paid, charge.Status);
        Assert.Equal(payment.Id, charge.LatestPaymentId);
        Assert.Equal("succeeded", payment.Status);
        Assert.Equal("HR12", payment.Reference);
        Assert.StartsWith("R-", payment.ReceiptNumber, StringComparison.Ordinal);

        payment.Void("  Wrong year ");
        charge.Reopen();

        Assert.Equal("voided", payment.Status);
        Assert.Equal("Wrong year", payment.VoidReason);
        Assert.Equal(ChargeStatuses.Open, charge.Status);
        Assert.Null(charge.LatestPaymentId);
    }

    [Fact]
    public void A_payment_refuses_a_bad_amount_method_date_or_second_void()
    {
        var charge = Charge.Open(Tenant, Site(CemeteryId, "A-1"), Fee("annual", "Upkeep"), 2026);
        var amount = Assert.Throws<DomainRuleException>(() => Payment.Record(charge, "cash", "10.00", new DateOnly(2026, 10, 7), null));
        var method = Assert.Throws<DomainRuleException>(() => Payment.Record(charge, "monri", "25.00", new DateOnly(2026, 10, 7), null));
        var date = Assert.Throws<DomainRuleException>(() => Payment.Record(charge, "bank", "25.00", new DateOnly(1999, 1, 1), null));
        var reference = Assert.Throws<DomainRuleException>(() => Payment.Record(charge, "bank", "25.00", new DateOnly(2026, 10, 7), "=cmd"));
        var payment = Payment.Record(charge, "bank", "25.00", new DateOnly(2026, 10, 7), null);
        payment.Void("Wrong desk");
        var again = Assert.Throws<DomainRuleException>(() => payment.Void("Wrong desk"));
        var paid = Assert.Throws<DomainRuleException>(() => Payment.Record(charge, "cash", "25.00", new DateOnly(2026, 10, 7), null));

        Assert.Equal("payment.amount_invalid", amount.Code);
        Assert.Equal("payment.method_invalid", method.Code);
        Assert.Equal(PaymentDate.Invalid, date.Code);
        Assert.Equal(PaymentReference.Invalid, reference.Code);
        Assert.Equal("payment.not_voidable", again.Code);
        Assert.Equal("charge.already_paid", paid.Code);
        Assert.False(BillingReason.Accepts("ab"));
        Assert.True(PaymentReference.Accepts(null));
    }

    private static FeeSchedule Fee(string kind, string name) =>
        FeeSchedule.Add(Tenant, CemeteryId, kind, name, "25.00", "EUR");

    private static GraveSite Site(Guid cemeteryId, string code) =>
        GraveSite.Place(Tenant, cemeteryId, Guid.CreateVersion7(), null, code, GraveSiteKind.SingleGrave, Box());

    private static GeoPolygon Box() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}
