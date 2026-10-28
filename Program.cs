// DEPS
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

// CODE

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet ("/api/steam", async ([FromQuery] string input, [FromServices] IHttpClientFactory clientFactory) =>
{
    var client = clientFactory.CreateClient();
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

    string cleanInput = input.Trim().TrimEnd('/');
    string steamId = "";

    if (cleanInput.Contains("/profiles/"))
    {
        steamId = cleanInput.Split("/profiles/").Last();
    }
    else if (cleanInput.Contains("/id/"))
    {
        string customId = cleanInput.Split("/id/").Last();

        string resolveUrl = $"https://api.steampowered.com/ISteamUser/ResolveVanityURL/v1/?key=4E7B42A45C96B74A82F18A3D801DF745&vanityurl={customId}";
        var resolveResponse = await client.GetFromJsonAsync<JsonElement>(resolveUrl);

        if (resolveResponse.GetProperty("response").GetProperty("succes").GetInt32() == 1)
        {
            steamId = resolveResponse.GetProperty("response").GetProperty("steamId").GetString()!;
        }
        else
        {
            return Results.Problem("Не удалось найти профиль. Проверьте ссылку.");
        }
    }
    else
    {
        steamId = cleanInput;
    }

    string inventoryUrl = $"https://steamcommunity.com/inventory/{steamId}/730/2?l=english";
   
   try
    {
        var response = await client.GetAsync(inventoryUrl);

        if (!response.IsSuccessStatusCode)
        {
            return Results.Problem($"Ошибка. Проверьте приватность инвентаря Steam. Код ошибки: {response.StatusCode}");
        }

        var jsonText = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(jsonText);

        var root = doc.RootElement;

        var assets = root.GetProperty("assets").EnumerateArray();

        var descriptions = root.GetProperty("descriptions").EnumerateArray()
            .ToDictionary(d => d.GetProperty("classid").GetString()!);

        //-----------------------------------------------------------------------
        var myItems = assets.Select(asset =>
        {
           string classId = asset.GetProperty("classid").GetString()!;
           var desc = descriptions[classId];

           string? color = desc.TryGetProperty("name_color", out var colorElement) ? colorElement.GetString() : null;

           string rarity = "Другое";
           if (desc.TryGetProperty("tags", out var tagsElement) && tagsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsElement.EnumerateArray())
                {
                    if (tag.TryGetProperty("category", out var category) && category.GetString() == "Rarity")
                    {
                        if(tag.TryGetProperty("localized_tag_name", out var tagName))
                        {
                            rarity = tagName.GetString() ?? "Другое";
                        }

                        if (string.IsNullOrEmpty(color) && tag.TryGetProperty("color", out var tagColor))
                        {
                            color = tagColor.GetString();
                        }
                        break;
                    }
                }
            }

           return new
           {
               AssetId = asset.GetProperty("assetid").GetString(),
               Name = desc.GetProperty("market_name").GetString(),
               IconUrl = $"https://community.cloudflare.steamstatic.com/economy/image/{desc.GetProperty("icon_url").GetString()}",
               Tradable = desc.GetProperty("tradable").GetInt32() == 1,
               Color = color,
               Rarity = rarity
           };
        }).ToList();
        return Results.Json(myItems);
    }
    catch (Exception ex)
    {
        return Results.Problem($"Ошибка сервера: {ex.Message}");
    }
});

app.Run();