using DocumentStorageApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using NkodSk.Abstractions;
using NkodSk.RdfFileStorage;
using NkodSk.RdfFulltextIndex;
using System.Security.Cryptography;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAuthentication(o =>
{
    o.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    o.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
}
).AddJwtBearer(o =>
{
    RSA rsa = RSA.Create();
    string? publicKeyString = builder.Configuration["Jwt:Key"];
    if (string.IsNullOrEmpty(publicKeyString))
    {
        throw new Exception("Unable to get Jwt:Key");
    }

    try
    {
        rsa.ImportFromPem(publicKeyString);
    }
    catch (Exception e)
    {
        throw new Exception("Unable to decode Jwt:Key", e);
    }

    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new RsaSecurityKey(rsa.ExportParameters(false)),
        ValidateIssuerSigningKey = true
    };
});
builder.Services.AddAuthorization();

builder.Services.AddSingleton<ILanguagesSource, DefaultLanguagesSource>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<IHttpContextValueAccessor, HttpContextValueAccessor>();
builder.Services.AddSingleton<IFileStorage>(_ =>
{
    string? fileStroragePath = builder.Configuration["StoragePath"];
    if (!Directory.Exists(fileStroragePath))
    {
        throw new Exception($"Directory from configutation StoragePath does not exist ({fileStroragePath})");
    }
    return new Storage(fileStroragePath);
});
builder.Services.AddScoped<IFileStorageAccessPolicy, DefaultFileAccessPolicy>();
builder.Services.AddSingleton<FulltextStorageMap>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DocumentStorage API",
        Version = "v1",
    });
    options.AddSecurityDefinition("bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer",
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
});

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "v1");
        options.RoutePrefix = string.Empty;
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/files/{id}", [AllowAnonymous] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, Guid id) =>
{
    FileState? fileState = storage.GetFileState(id, accessPolicy);
    return fileState is not null ? Results.Ok(fileState) : Results.NotFound();
});

app.MapGet("/files/{id}/content", [AllowAnonymous] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, Guid id) =>
{
    Stream? stream = storage.OpenReadStream(id, accessPolicy);
    return stream is not null ? Results.Stream(stream) : Results.NotFound();
});

app.MapGet("/files/{id}/metadata", [AllowAnonymous] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, Guid id) =>
{
    FileMetadata? metadata = storage.GetFileMetadata(id, accessPolicy);
    return metadata is not null ? Results.Ok(metadata) : Results.NotFound();
});

app.MapPost("/files/query", [AllowAnonymous] ([FromServices] IFileStorage storage, [FromServices] IFileStorageAccessPolicy accessPolicy, [FromServices] FulltextStorageMap fulltextStorage, [FromBody] FileStorageQuery? query) =>
{
    query ??= new FileStorageQuery();
    query.QueryText = query.QueryText?.Trim();

    FileStorageResponse response;
    if (!string.IsNullOrEmpty(query.QueryText))
    {
        FulltextResponse fulltextResponse = fulltextStorage.Search(query);
        if (fulltextResponse.Documents.Count > 0)
        {
            FileStorageQuery internalQuery = new FileStorageQuery
            {
                OnlyIds = fulltextResponse.Documents.Select(d => d.Id).ToList(),
                QueryText = null,
                OnlyPublishers = query.OnlyPublishers,
                OnlyPublished = query.OnlyPublished,
                RequiredFacets = query.RequiredFacets,
                OrderDefinitions = query.OrderDefinitions,
            };
            response = storage.GetFileStates(internalQuery, accessPolicy);
        }
        else
        {
            response = new FileStorageResponse(new List<FileState>(), 0, new List<Facet>());
        }
    }
    else
    {
        response = storage.GetFileStates(query, accessPolicy);
    }

    return Results.Ok(response);
});

app.MapPost("/files/by-publisher", [AllowAnonymous] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, [FromServices] FulltextStorageMap fulltextStorage, [FromBody] FileStorageQuery? query) =>
{
    query ??= new FileStorageQuery();

    FileStorageGroupResponse response = storage.GetFileStatesByPublisher(query, accessPolicy);

    if (!string.IsNullOrEmpty(query.QueryText))
    {
        FulltextResponse fulltextResponse = fulltextStorage.Search(new FileStorageQuery
        {
            QueryText = query.QueryText,
            OnlyTypes = new List<FileType> { FileType.PublisherRegistration },
        });
        if (fulltextResponse.Documents.Count > 0)
        {
            HashSet<Guid> keys = new HashSet<Guid>(fulltextResponse.Documents.Count);
            foreach (FulltextResponseDocument document in fulltextResponse.Documents)
            {
                keys.Add(document.Id);
            }
            List<FileStorageGroup> groups = new List<FileStorageGroup>(response.Groups.Count);
            foreach (FileStorageGroup group in response.Groups)
            {
                if (group.PublisherFileState?.Metadata is not null && keys.Contains(group.PublisherFileState.Metadata.Id))
                {
                    groups.Add(group);
                }
            }
            response = new FileStorageGroupResponse(groups, groups.Count);
        }
        else
        {
            response = new FileStorageGroupResponse(new List<FileStorageGroup>(), 0);
        }
    }

    return Results.Ok(response);
});

app.MapPost("/files", [Authorize] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, [FromServices] FulltextStorageMap fulltext,[FromBody] InsertModel insertData) =>
{
    if (insertData.Metadata is null)
    {
        return Results.BadRequest("Metadata is required");
    }

    if (insertData.Content is not null)
    {
        try
        {
            storage.InsertFile(insertData.Content, insertData.Metadata, insertData.EnableOverwrite, accessPolicy);
            FileState? state = storage.GetFileState(insertData.Metadata.Id, accessPolicy);
            if (state is not null)
            {
                fulltext.Index(new[] { state });
            }
        } 
        catch (NkodSk.RdfFileStorage.UnauthorizedAccessException)
        {
            return Results.Forbid();
        }
    }
    else
    {
        return Results.BadRequest("Content is required");
    }

    return Results.Ok();
});

app.MapPost("/files/stream", [Authorize] async (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, HttpRequest request, IFormFile file) =>
{
    IFormCollection form = await request.ReadFormAsync();

    string? metadataEncoded = form.ContainsKey("metadata") ? form["metadata"].FirstOrDefault() : null;
    if (metadataEncoded == null)
    {
        return Results.BadRequest();
    }

    bool enableOverwrite;
    if (!form.TryGetValue("enableOverwrite", out StringValues strings) || strings.Count == 0 || !bool.TryParse(strings[0], out enableOverwrite))
    {
        enableOverwrite = false;
    }

    if (file is not null)
    {
        FileMetadata? metadata;

        try
        {
            metadata = JsonConvert.DeserializeObject<FileMetadata>(metadataEncoded);

            if (metadata == null)
            {
                return Results.BadRequest();
            }
        }
        catch
        {
            return Results.BadRequest();
        }

        try
        {
            using Stream sourceStream = file.OpenReadStream();
            using Stream writeStream = storage.OpenWriteStream(metadata, enableOverwrite, accessPolicy);
            await sourceStream.CopyToAsync(writeStream).ConfigureAwait(false);
        }
        catch (NkodSk.RdfFileStorage.UnauthorizedAccessException)
        {
            return Results.Forbid();
        }                
    }
    else
    {
        return Results.BadRequest("Content is required");
    }

    return Results.Ok();
});

app.MapPost("/files/metadata", [Authorize] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, [FromServices] FulltextStorageMap fulltext, [FromBody] FileMetadata metadata) =>
{
    if (metadata is null)
    {
        return Results.BadRequest("Metadata is required");
    }

    try
    {
        storage.UpdateMetadata(metadata, accessPolicy);
        FileState? state = storage.GetFileState(metadata.Id, accessPolicy);
        if (state is not null)
        {
            fulltext.Index(new[] { state });
        }
    }
    catch (NkodSk.RdfFileStorage.UnauthorizedAccessException)
    {
        return Results.Forbid();
    }

    return Results.Ok();
});

app.MapDelete("/files/{id}", [Authorize] (IFileStorage storage, IFileStorageAccessPolicy accessPolicy, [FromServices] FulltextStorageMap fulltext, Guid id) =>
{
    try
    {
        storage.DeleteFile(id, accessPolicy);
        fulltext.RemoveFromIndex(id);
    }
    catch (NkodSk.RdfFileStorage.UnauthorizedAccessException)
    {
        return Results.Forbid();
    }

    return Results.Ok();
});

app.Run();

public partial class Program { }