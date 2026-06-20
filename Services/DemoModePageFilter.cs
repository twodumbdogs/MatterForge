using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CMIForge.Services;

public class DemoModePageFilter(
    DemoModeService demoModeService,
    ContentModerationService contentModerationService) : IAsyncPageFilter
{
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context)
    {
        return Task.CompletedTask;
    }

    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        if (!demoModeService.IsEnabled ||
            !HttpMethods.IsPost(request.Method))
        {
            await next();
            return;
        }

        var handlerName = context.HandlerMethod?.MethodInfo.Name;
        if (demoModeService.ShouldBlockPost(request.Path, handlerName))
        {
            context.Result = new RedirectToPageResult("/System/DemoBlocked", new { reason = "action" });
            return;
        }

        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(context.HttpContext.RequestAborted);
            var moderation = contentModerationService.ValidateForm(form);
            if (!moderation.IsAllowed)
            {
                context.Result = new RedirectToPageResult("/System/DemoBlocked", new { reason = "content" });
                return;
            }
        }

        await next();
    }
}
