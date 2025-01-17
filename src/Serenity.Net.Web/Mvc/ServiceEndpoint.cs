﻿using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Serenity.Services;

/// <summary>
/// Subclass of controller for service endpoints
/// </summary>
[HandleServiceException]
public abstract class ServiceEndpoint : Controller
{
    private IDbConnection connection;
    private UnitOfWork unitOfWork;
    private IRequestContext context;

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (unitOfWork != null)
        {
            unitOfWork.Dispose();
            unitOfWork = null;
        }

        if (connection != null)
        {
            connection.Dispose();
            connection = null;
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var uowParam = context.ActionDescriptor.Parameters.FirstOrDefault(x => x.ParameterType == typeof(IUnitOfWork));
        if (uowParam != null)
        {
            var connectionKey = GetType().GetCustomAttribute<ConnectionKeyAttribute>();
            if (connectionKey == null)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(connectionKey));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

            connection = HttpContext.RequestServices.GetRequiredService<ISqlConnections>().NewByKey(connectionKey.Value);

            var attribute = (context.ActionDescriptor as ControllerActionDescriptor)?
                .MethodInfo.GetCustomAttribute<TransactionSettingsAttribute>() ??
                GetType().GetCustomAttribute<TransactionSettingsAttribute>();

            var settings = context.HttpContext?.RequestServices?
                .GetService<IOptions<TransactionSettings>>()?.Value;

            var isolationLevel = attribute?.IsolationLevel ??
                settings?.IsolationLevel ?? IsolationLevel.Unspecified;

            var deferStart = attribute?.HasDeferStart == true ?
                attribute.DeferStart : (settings?.DeferStart ?? false);

            unitOfWork = new UnitOfWork(connection, isolationLevel, deferStart);

            context.ActionArguments[uowParam.Name] = unitOfWork;
            base.OnActionExecuting(context);
            return;
        }

        var cnnParam = context.ActionDescriptor.Parameters.FirstOrDefault(x => x.ParameterType == typeof(IDbConnection));
        if (cnnParam != null)
        {
            var connectionKey = GetType().GetCustomAttribute<ConnectionKeyAttribute>();
            if (connectionKey == null)
#pragma warning disable CA2208 // Instantiate argument exceptions correctly
                throw new ArgumentNullException(nameof(connectionKey));
#pragma warning restore CA2208 // Instantiate argument exceptions correctly

            connection = HttpContext.RequestServices.GetRequiredService<ISqlConnections>().NewByKey(connectionKey.Value);
            context.ActionArguments[cnnParam.Name] = connection;
            base.OnActionExecuting(context);
            return;
        }

        base.OnActionExecuting(context);
    }

    /// <inheritdoc/>
    public override void OnActionExecuted(ActionExecutedContext context)
    {
        if (unitOfWork != null)
        {
            try
            {
                if (context != null && context.Exception != null)
                    unitOfWork.Dispose();
                else
                    unitOfWork.Commit();
            }
            catch (InvalidOperationException)
            {
                // if a DDL error occurs transaction might turn into a zombie
                // and we'll get an error here
            }

            unitOfWork = null;
        }

        if (connection != null)
        {
            connection.Dispose();
            connection = null;
        }

        context.Result = (context.Result as ActionResult) ?? new Result<object>(context.Result);

        base.OnActionExecuted(context);
    }

    /// <summary>
    /// Gets the request context
    /// </summary>
    protected IRequestContext Context
    {
        get
        {
            if (context == null)
                context = HttpContext?.RequestServices?.GetRequiredService<IRequestContext>();

            return context;
        }
        set
        {
            context = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    /// <summary>
    /// Gets the cache from the request context
    /// </summary>
    protected ITwoLevelCache Cache => Context?.Cache;

    /// <summary>
    /// Gets the localizer from the request context
    /// </summary>
    protected ITextLocalizer Localizer => Context?.Localizer;

    /// <summary>
    /// Gets the permission service from the request context
    /// </summary>
    protected IPermissionService Permissions => Context?.Permissions;
}