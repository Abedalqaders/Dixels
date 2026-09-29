using System;
using System.Net;
using Dixels.ExceptionHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Shouldly;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Dixels;

/// <summary>
/// The HTTP status a client sees for each kind of failure. No host needed: the finder is a
/// pure function of the exception and the configured code mappings.
/// </summary>
public class HttpExceptionStatusCodeTests
{
    private static readonly DixelsHttpExceptionStatusCodeFinder Finder = new(Options.Create(new AbpExceptionHttpStatusCodeOptions
    {
        // Mirrors DixelsHttpApiModule.ConfigureHttpStatusCodes.
        ErrorCodeToHttpStatusCodeMappings =
        {
            [DixelsDomainErrorCodes.BookingOverlap] = HttpStatusCode.Conflict,
            [DixelsDomainErrorCodes.BookingIdempotencyKeyReused] = HttpStatusCode.Conflict,
        },
    }));

    private static HttpStatusCode StatusOf(Exception exception) => Finder.GetStatusCode(new DefaultHttpContext(), exception);

    [Fact]
    public void A_broken_booking_rule_is_a_bad_request_not_forbidden()
    {
        StatusOf(new BusinessException(DixelsDomainErrorCodes.BookingOutsideHours)).ShouldBe(HttpStatusCode.BadRequest);
        StatusOf(new BusinessException(DixelsDomainErrorCodes.SpaceTypeInUse)).ShouldBe(HttpStatusCode.BadRequest);
        StatusOf(new BusinessException(DixelsDomainErrorCodes.ParentIsDeleted)).ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public void Explicit_conflict_mappings_win()
    {
        StatusOf(new BusinessException(DixelsDomainErrorCodes.BookingOverlap)).ShouldBe(HttpStatusCode.Conflict);
        StatusOf(new BusinessException(DixelsDomainErrorCodes.BookingIdempotencyKeyReused)).ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public void Other_failures_keep_their_usual_status()
    {
        // An anonymous context: ABP answers 401 here, 403 once the user is authenticated.
        StatusOf(new AbpAuthorizationException()).ShouldBe(HttpStatusCode.Unauthorized);
        StatusOf(new EntityNotFoundException()).ShouldBe(HttpStatusCode.NotFound);
        // A business error from an ABP module (no Dixels: prefix) is left as ABP defines it.
        StatusOf(new BusinessException("Volo.Abp.Identity:010001")).ShouldBe(HttpStatusCode.Forbidden);
    }
}
