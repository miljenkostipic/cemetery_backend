using Cemetery.Application.Abstractions;
using Cemetery.Application.Billing;
using Cemetery.Application.Exceptions;
using Cemetery.Domain.Billing;
using Cemetery.Domain.Identity;
using Cemetery.Domain.Layout;
using NSubstitute;
using CemeteryPlace = Cemetery.Domain.Layout.Cemetery;

namespace Cemetery.Application.Tests;

public sealed class BillingHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_clerk_adds_and_lists_a_fee()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var billing = Substitute.For<IBillingRepository>();
        var fee = FeeSchedule.Add(tenant, cemetery.Id, "annual", "Upkeep", "25.00", "EUR");
        billing.ListFeesAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { fee });
        billing.AddFeeAsync(Arg.Any<FeeSchedule>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var layouts = Layouts(cemetery);
        var unit = Unit();
        var added = new AddFeeScheduleHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, billing, unit);

        var created = await added.Handle(new AddFeeSchedule(cemetery.Id, "service", "  Wreath  ", "15.50", "CHF"), CancellationToken.None);
        var listed = await new ListFeeSchedulesHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), layouts, billing)
            .Handle(new ListFeeSchedules(cemetery.Id), CancellationToken.None);

        Assert.Equal("Wreath", created.Name);
        Assert.Equal("15.50", created.Amount);
        Assert.Equal("CHF", created.Currency);
        Assert.Equal("service", created.Kind);
        Assert.Equal("Upkeep", listed[0].Name);
        Assert.Equal("25.00", listed[0].Amount);
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_cemetery_has_no_fees()
    {
        var handler = new ListFeeSchedulesHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.CemeteryClerk),
            Substitute.For<ILayoutRepository>(),
            Substitute.For<IBillingRepository>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new ListFeeSchedules(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("cemetery.not_found", error.Code);
    }

    [Fact]
    public async Task A_field_worker_cannot_add_a_fee()
    {
        var handler = new AddFeeScheduleHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.FieldWorker),
            Substitute.For<ILayoutRepository>(),
            Substitute.For<IBillingRepository>(),
            Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<ForbiddenException>(() => handler.Handle(new AddFeeSchedule(Guid.CreateVersion7(), "annual", "Upkeep", "25.00", "EUR"), CancellationToken.None));

        Assert.Equal("register.forbidden", error.Code);
    }

    [Fact]
    public async Task A_clerk_removes_an_unused_fee()
    {
        var tenant = Guid.CreateVersion7();
        var fee = FeeSchedule.Add(tenant, Guid.CreateVersion7(), "annual", "Upkeep", "25.00", "EUR");
        var billing = Substitute.For<IBillingRepository>();
        billing.FindFeeAsync(fee.Id, Arg.Any<CancellationToken>()).Returns(fee);
        billing.FeeIsChargedAsync(fee.Id, Arg.Any<CancellationToken>()).Returns(false);
        var unit = Unit();
        var handler = new RemoveFeeScheduleHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), billing, unit);

        var removed = await handler.Handle(new RemoveFeeSchedule(fee.Id), CancellationToken.None);

        Assert.True(removed);
        billing.Received(1).RemoveFee(fee);
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_fee_used_by_a_charge_stays_on_the_schedule()
    {
        var tenant = Guid.CreateVersion7();
        var fee = FeeSchedule.Add(tenant, Guid.CreateVersion7(), "annual", "Upkeep", "25.00", "EUR");
        var billing = Substitute.For<IBillingRepository>();
        billing.FindFeeAsync(fee.Id, Arg.Any<CancellationToken>()).Returns(fee);
        billing.FeeIsChargedAsync(fee.Id, Arg.Any<CancellationToken>()).Returns(true);
        var unit = Substitute.For<IUnitOfWork>();
        var handler = new RemoveFeeScheduleHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), billing, unit);

        var error = await Assert.ThrowsAsync<ConflictException>(() => handler.Handle(new RemoveFeeSchedule(fee.Id), CancellationToken.None));

        Assert.Equal("fee.in_use", error.Code);
        billing.DidNotReceive().RemoveFee(Arg.Any<FeeSchedule>());
        await unit.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_fee_cannot_be_removed()
    {
        var billing = Substitute.For<IBillingRepository>();
        billing.FindFeeAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((FeeSchedule?)null);
        var handler = new RemoveFeeScheduleHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.CemeteryClerk), billing, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RemoveFeeSchedule(Guid.CreateVersion7()), CancellationToken.None));

        Assert.Equal("fee.not_found", error.Code);
    }

    [Fact]
    public async Task A_yearly_run_charges_open_sites_and_lists_them_as_arrears()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var fee = FeeSchedule.Add(tenant, cemetery.Id, "annual", "Upkeep", "25.00", "EUR");
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
        var billing = Substitute.For<IBillingRepository>();
        IReadOnlyList<Charge>? stored = null;
        billing.ListFeesAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { fee });
        billing.ChargedPairsAsync(cemetery.Id, 2026, Arg.Any<CancellationToken>()).Returns(new HashSet<ChargeKey>());
        billing.AddChargesAsync(Arg.Do<IReadOnlyList<Charge>>(rows => stored = rows), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var layouts = Layouts(cemetery);
        layouts.ListByCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { site });
        var raised = await new RunYearHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, billing, Unit())
            .Handle(new RunYear(cemetery.Id, 2026), CancellationToken.None);
        Assert.NotNull(stored);
        billing.ListChargesAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(stored);
        billing.ListArrearsAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var charges = await new ListChargesHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, billing)
            .Handle(new ListCharges(cemetery.Id), CancellationToken.None);
        var arrears = await new ListArrearsHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), layouts, billing)
            .Handle(new ListArrears(cemetery.Id), CancellationToken.None);

        Assert.Equal(1, raised.ChargeCount);
        Assert.Equal(2026, raised.Year);
        Assert.Equal("A-1", charges[0].GraveSiteCode);
        Assert.Equal("25.00", charges[0].Amount);
        Assert.Equal("open", charges[0].Status);
        Assert.Equal(new DateOnly(2026, 3, 31), charges[0].DueOn);
        Assert.Null(charges[0].LatestPaymentId);
        Assert.Equal(charges[0].Id, arrears[0].ChargeId);
        Assert.Equal("A-1", arrears[0].GraveSiteCode);
    }

    [Fact]
    public async Task A_yearly_run_refuses_a_year_outside_the_range()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var billing = Substitute.For<IBillingRepository>();
        var handler = new RunYearHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), Layouts(cemetery), billing, Unit());

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => handler.Handle(new RunYear(cemetery.Id, 1999), CancellationToken.None));

        Assert.Equal("charge.year_invalid", error.Code);
        await billing.DidNotReceive().AddChargesAsync(Arg.Any<IReadOnlyList<Charge>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_missing_cemetery_cannot_be_charged()
    {
        var handler = new RunYearHandler(
            User(),
            Tenant(Guid.CreateVersion7()),
            Membership(MembershipRole.OrganizationAdmin),
            Substitute.For<ILayoutRepository>(),
            Substitute.For<IBillingRepository>(),
            Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new RunYear(Guid.CreateVersion7(), 2026), CancellationToken.None));

        Assert.Equal("cemetery.not_found", error.Code);
    }

    [Fact]
    public async Task A_clerk_reminds_open_charges()
    {
        var tenant = Guid.CreateVersion7();
        var cemetery = CemeteryPlace.Open(tenant, "Pilot", true, Now);
        var site = GraveSite.Place(tenant, cemetery.Id, Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
        var charge = Charge.Open(tenant, site, FeeSchedule.Add(tenant, cemetery.Id, "annual", "Upkeep", "25.00", "EUR"), 2026);
        var billing = Substitute.For<IBillingRepository>();
        billing.ListOpenChargesAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(new[] { charge });
        var unit = Unit();
        var handler = new SendRemindersHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), Layouts(cemetery), billing, unit);

        var result = await handler.Handle(new SendReminders(cemetery.Id), CancellationToken.None);

        Assert.Equal(1, result.RemindedCount);
        Assert.Equal("reminded", charge.Status);
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clerk_records_a_bank_payment_and_voids_it()
    {
        var tenant = Guid.CreateVersion7();
        var site = GraveSite.Place(tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), null, "A-1", GraveSiteKind.SingleGrave, Box());
        var charge = Charge.Open(tenant, site, FeeSchedule.Add(tenant, site.CemeteryId, "annual", "Upkeep", "25.00", "EUR"), 2026);
        var billing = Substitute.For<IBillingRepository>();
        billing.FindChargeAsync(charge.Id, Arg.Any<CancellationToken>()).Returns(charge);
        Payment? stored = null;
        billing.When(repository => repository.AddPayment(Arg.Any<Payment>())).Do(call => stored = call.Arg<Payment>());
        var unit = Unit();
        var recorded = await new RecordManualPaymentHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), billing, unit)
            .Handle(new RecordManualPayment(charge.Id, "bank", "25.00", new DateOnly(2026, 10, 7), "HR12"), CancellationToken.None);
        Assert.NotNull(stored);
        billing.FindPaymentAsync(stored.Id, Arg.Any<CancellationToken>()).Returns(stored);

        var receipt = await new GetReceiptHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), billing)
            .Handle(new GetReceipt(stored.Id), CancellationToken.None);
        var reopened = await new VoidPaymentHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), billing, unit)
            .Handle(new VoidPayment(stored.Id, "Wrong year"), CancellationToken.None);

        Assert.Equal("succeeded", recorded.Status);
        Assert.StartsWith("R-", recorded.ReceiptNumber, StringComparison.Ordinal);
        Assert.Equal(recorded.ReceiptNumber, receipt.ReceiptNumber);
        Assert.Equal("open", reopened.Status);
        Assert.Null(reopened.LatestPaymentId);
        Assert.Equal("voided", stored.Status);
    }

    [Fact]
    public async Task A_missing_payment_cannot_be_voided()
    {
        var billing = Substitute.For<IBillingRepository>();
        billing.FindPaymentAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((Payment?)null);
        var handler = new VoidPaymentHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.CemeteryClerk), billing, Substitute.For<IUnitOfWork>());

        var error = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(new VoidPayment(Guid.CreateVersion7(), "Wrong year"), CancellationToken.None));

        Assert.Equal("payment.not_found", error.Code);
    }

    [Fact]
    public async Task A_clerk_sees_every_provider_and_saves_a_connection()
    {
        var tenant = Guid.CreateVersion7();
        var billing = Substitute.For<IBillingRepository>();
        var existing = MerchantAccount.Connect(tenant, "paypal", new MerchantCredentials("paypal-1", "secret-ok", false));
        billing.ListProvidersAsync(Arg.Any<CancellationToken>()).Returns(new[] { existing });
        billing.FindProviderAsync("monri", Arg.Any<CancellationToken>()).Returns((MerchantAccount?)null);
        billing.AddProviderAsync(Arg.Any<MerchantAccount>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var unit = Unit();
        var listed = await new ListPaymentProvidersHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), billing)
            .Handle(new ListPaymentProviders(), CancellationToken.None);
        var saved = await new SavePaymentProviderHandler(User(), Tenant(tenant), Membership(MembershipRole.OrganizationAdmin), billing, unit)
            .Handle(new SavePaymentProvider("monri", "merchant-9", "secret-ok", true), CancellationToken.None);

        Assert.Equal(3, listed.Count);
        Assert.False(listed.Single(row => row.Provider == "paypal").Enabled);
        Assert.True(listed.Single(row => row.Provider == "paypal").Connected);
        Assert.Equal("paypal-1", listed.Single(row => row.Provider == "paypal").MerchantId);
        Assert.False(listed.Single(row => row.Provider == "monri").Connected);
        Assert.Null(listed.Single(row => row.Provider == "payrexx").MerchantId);
        Assert.Equal("monri", saved.Provider);
        Assert.True(saved.Connected);
        Assert.True(saved.Enabled);
        Assert.Equal("merchant-9", saved.MerchantId);
        await billing.Received(1).AddProviderAsync(Arg.Is<MerchantAccount>(account => account.Secret == "secret-ok" && account.Provider == "monri"), Arg.Any<CancellationToken>());
        await unit.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Saving_again_replaces_the_connection()
    {
        var tenant = Guid.CreateVersion7();
        var billing = Substitute.For<IBillingRepository>();
        var existing = MerchantAccount.Connect(tenant, "monri", new MerchantCredentials("merchant-1", "secret-ok", true));
        billing.FindProviderAsync("monri", Arg.Any<CancellationToken>()).Returns(existing);
        var saved = await new SavePaymentProviderHandler(User(), Tenant(tenant), Membership(MembershipRole.CemeteryClerk), billing, Unit())
            .Handle(new SavePaymentProvider("monri", "merchant-2", "next-secret", false), CancellationToken.None);

        Assert.Equal("merchant-2", existing.MerchantId);
        Assert.Equal("next-secret", existing.Secret);
        Assert.False(saved.Enabled);
        Assert.True(saved.Connected);
        await billing.DidNotReceive().AddProviderAsync(Arg.Any<MerchantAccount>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_unknown_provider_is_not_stored()
    {
        var billing = Substitute.For<IBillingRepository>();
        billing.FindProviderAsync("stripe", Arg.Any<CancellationToken>()).Returns((MerchantAccount?)null);
        var unit = Substitute.For<IUnitOfWork>();
        var handler = new SavePaymentProviderHandler(User(), Tenant(Guid.CreateVersion7()), Membership(MembershipRole.CemeteryClerk), billing, unit);

        var error = await Assert.ThrowsAsync<DomainRuleException>(() => handler.Handle(new SavePaymentProvider("stripe", "merchant-1", "secret-ok", true), CancellationToken.None));

        Assert.Equal("payment.provider_invalid", error.Code);
        await unit.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validators_reject_the_same_codes_as_the_form()
    {
        var fee = new AddFeeScheduleValidator().Validate(new AddFeeSchedule(Guid.CreateVersion7(), "nope", "\n", "0.00", "USD"));
        var year = new RunYearValidator().Validate(new RunYear(Guid.CreateVersion7(), 1999));
        var provider = new SavePaymentProviderValidator().Validate(new SavePaymentProvider("stripe", "x", "short", true));
        var validFee = new AddFeeScheduleValidator().Validate(new AddFeeSchedule(Guid.CreateVersion7(), "annual", "Upkeep", "25.00", "EUR"));
        var validYear = new RunYearValidator().Validate(new RunYear(Guid.CreateVersion7(), 2026));
        var validProvider = new SavePaymentProviderValidator().Validate(new SavePaymentProvider("monri", "merchant-1", "secret-ok", false));
        var missingFee = new RemoveFeeScheduleValidator().Validate(new RemoveFeeSchedule(Guid.Empty));
        var presentFee = new RemoveFeeScheduleValidator().Validate(new RemoveFeeSchedule(Guid.CreateVersion7()));
        var shortReason = new VoidPaymentValidator().Validate(new VoidPayment(Guid.CreateVersion7(), "ab"));
        var voidable = new VoidPaymentValidator().Validate(new VoidPayment(Guid.CreateVersion7(), "Wrong year"));

        Assert.Contains(fee.Errors, error => error.ErrorCode == "fee.kind_invalid");
        Assert.Contains(fee.Errors, error => error.ErrorCode == "fee.name_invalid");
        Assert.Contains(fee.Errors, error => error.ErrorCode == "fee.amount_invalid");
        Assert.Contains(fee.Errors, error => error.ErrorCode == "fee.currency_invalid");
        Assert.Contains(year.Errors, error => error.ErrorCode == "charge.year_invalid");
        Assert.Contains(provider.Errors, error => error.ErrorCode == "payment.provider_invalid");
        Assert.Contains(provider.Errors, error => error.ErrorCode == "payment.merchant_invalid");
        Assert.Contains(provider.Errors, error => error.ErrorCode == "payment.secret_invalid");
        Assert.True(validFee.IsValid);
        Assert.True(validYear.IsValid);
        Assert.True(validProvider.IsValid);
        Assert.Contains(missingFee.Errors, error => error.ErrorCode == "fee.not_found");
        Assert.True(presentFee.IsValid);
        Assert.Contains(shortReason.Errors, error => error.ErrorCode == "payment.reason_invalid");
        Assert.True(voidable.IsValid);
    }

    private static ILayoutRepository Layouts(CemeteryPlace cemetery)
    {
        var layouts = Substitute.For<ILayoutRepository>();
        layouts.FindCemeteryAsync(cemetery.Id, Arg.Any<CancellationToken>()).Returns(cemetery);
        return layouts;
    }

    private static IUnitOfWork Unit()
    {
        var unit = Substitute.For<IUnitOfWork>();
        unit.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        return unit;
    }

    private static ICurrentUser User()
    {
        var current = Substitute.For<ICurrentUser>();
        current.UserId.Returns(Guid.CreateVersion7());
        return current;
    }

    private static ITenantContext Tenant(Guid organizationId)
    {
        var tenant = Substitute.For<ITenantContext>();
        tenant.OrganizationId.Returns(organizationId);
        return tenant;
    }

    private static IMembershipRepository Membership(MembershipRole role)
    {
        var memberships = Substitute.For<IMembershipRepository>();
        memberships.RoleInCurrentTenantAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(role);
        return memberships;
    }

    private static GeoPolygon Box() =>
        GeoPolygon.Create([
            new GeoPoint(16, 45),
            new GeoPoint(16.001, 45),
            new GeoPoint(16.001, 45.001),
            new GeoPoint(16, 45.001),
            new GeoPoint(16, 45),
        ]);
}
