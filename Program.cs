
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TrixCompareDb.Data;
using Microsoft.AspNetCore.Diagnostics;
using System.Text.Json;
using System.Text;
using TrixCompareDb.Services;

var builder = WebApplication.CreateBuilder(args);


// DB
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("TrixCompareDbTest")));


// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddScoped<TableRepository>();
builder.Services.AddScoped<CompareTables>();
builder.Services.AddScoped<UpdateTableService>();


// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// CORS (per Vue)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        p => p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
});


var app = builder.Build();
app.UseCors("AllowAll");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // Return minimal JSON error in production instead of HTML error page
    app.UseExceptionHandler(exceptionApp =>
    {
        exceptionApp.Run(async context =>
        {
            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json; charset=utf-8";
            var exFeature = context.Features.Get<IExceptionHandlerFeature>();
            if (exFeature?.Error != null)
            {
                var err = new { error = "An unexpected error occurred.", detail = exFeature.Error.Message };
                var json = JsonSerializer.Serialize(err);
                await context.Response.WriteAsync(json, Encoding.UTF8);
            }
        });
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
