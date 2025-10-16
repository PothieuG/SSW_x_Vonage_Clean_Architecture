using SSW_x_Vonage_Clean_Architecture.Application;
using SSW_x_Vonage_Clean_Architecture.Infrastructure;
using SSW_x_Vonage_Clean_Architecture.WebApi;
using SSW_x_Vonage_Clean_Architecture.WebApi.Endpoints;
using SSW_x_Vonage_Clean_Architecture.WebApi.Extensions;
using SSW_x_Vonage_Clean_Architecture.WebApi.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddCustomProblemDetails();

builder.Services.AddWebApi(builder.Configuration);
builder.Services.AddApplication();
builder.AddInfrastructure();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.MapOpenApi();
app.MapCustomScalarApiReference();
app.UseHealthChecks();
app.UseHttpsRedirection();
app.UseStaticFiles();

app.MapHeroEndpoints();
app.MapTeamEndpoints();
app.MapCallEndpoints();
app.UseEventualConsistencyMiddleware();

app.MapDefaultEndpoints();
app.UseExceptionHandler();

app.Run();