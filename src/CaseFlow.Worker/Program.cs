using CaseFlow.Application;
using CaseFlow.Application.Abstractions;
using CaseFlow.Application.Cases;
using CaseFlow.Infrastructure;
using CaseFlow.Infrastructure.Scheduling;
using CaseFlow.Worker;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<WorkflowOptions>(builder.Configuration.GetSection(WorkflowOptions.SectionName));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Jobs run as the system rather than a request user.
builder.Services.AddSingleton<ICurrentUser, SystemCurrentUser>();

// The worker hosts both schedulers: Quartz for the cron maintenance jobs and
// the Hangfire server for the queued/delayed/recurring jobs (including the
// recurring outbox drain that replaced phase 4's poll loop).
builder.Services.AddCaseFlowHangfire();
builder.Services.AddCaseFlowHangfireServer();
builder.Services.AddCaseFlowQuartz(builder.Configuration);

var host = builder.Build();
host.Run();
