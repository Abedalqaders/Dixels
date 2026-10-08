using System;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.AspNetCore.ExceptionHandling;
using Volo.Abp.DependencyInjection;
using Volo.Abp.ExceptionHandling;

namespace Dixels.ExceptionHandling;

/// <summary>
/// ABP answers every <see cref="BusinessException"/> with 403 Forbidden. For this API that is
/// wrong: "the room is closed on Sundays" or "that space type is still in use" is a rejected
/// request (400), not a permission problem — and clients, proxies and monitoring treat 403 as
/// "this user may not do this". So any Dixels-coded business error that would otherwise be a
/// 403 becomes a 400. Codes mapped explicitly in <c>AbpExceptionHttpStatusCodeOptions</c>
/// (the 409 conflicts, and the one deliberate 403) keep their mapping, and real
/// authorization failures stay 403.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IHttpExceptionStatusCodeFinder))]
public class DixelsHttpExceptionStatusCodeFinder : DefaultHttpExceptionStatusCodeFinder
{
    public const string DixelsErrorCodePrefix = "Dixels:";

    public DixelsHttpExceptionStatusCodeFinder(IOptions<AbpExceptionHttpStatusCodeOptions> options)
        : base(options)
    {
    }

    public override HttpStatusCode GetStatusCode(HttpContext httpContext, Exception exception)
    {
        var status = base.GetStatusCode(httpContext, exception);

        if (status == HttpStatusCode.Forbidden
            && exception is IBusinessException
            && exception is IHasErrorCode { Code: { } code }
            && code.StartsWith(DixelsErrorCodePrefix, StringComparison.Ordinal)
            && !Options.ErrorCodeToHttpStatusCodeMappings.ContainsKey(code))
        {
            return HttpStatusCode.BadRequest;
        }

        return status;
    }
}
