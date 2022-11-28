using Beis.Ebss.Document.Api.Filters;
using Beis.Ebss.Document.Api.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();


if (builder.Environment.IsDevelopment())
{
    builder.Services.AddCors(options =>
    {
        options.AddDefaultPolicy(
            policy => { policy.WithOrigins().AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod(); });
    });
}

builder.Services.AddSwaggerGen(c =>  
{  
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Document.Upload.Api", Version = "v1" });  
    c.OperationFilter<SwaggerFileOperationFilter>();  
}); 

builder.Services.AddApiVersioning(o => 
{
    o.DefaultApiVersion = new ApiVersion(1, 0);
    o.AssumeDefaultVersionWhenUnspecified = true; 
    o.ReportApiVersions = true;

    o.UseApiBehavior = false; // version everything by default
    
});

builder.Services.Configure<DocumentOptions>(builder.Configuration.GetSection(DocumentOptions.Section));
builder.Services.Configure<ClamAVOptions>(builder.Configuration.GetSection(ClamAVOptions.Section));

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "beis-ebss-document-service v1");
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();