using FolderFolio.Configuration;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IValidateOptions<FolderFolioOptions>, FolderFolioOptionsValidator>();
builder.Services.AddOptions<FolderFolioOptions>()
    .Bind(builder.Configuration.GetSection(FolderFolioOptions.SectionName))
    .ValidateOnStart();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();

app.Run();

public partial class Program { }
