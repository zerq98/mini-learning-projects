var builder = WebApplication.CreateBuilder(args);
builder.Services.AddSignalR();
builder.Services.AddSingleton<JobStore>();
var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHub<JobHub>("/hubs/jobs");

// Plain HTTP endpoint - proves you can push to SignalR clients from anywhere (curl, controller, worker).
app.MapPost("/jobs", (string name, JobStore jobs) => jobs.Start(name));

app.Run();
