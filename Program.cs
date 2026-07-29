using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
var app = builder.Build();

var config = app.Configuration.GetSection("LineBot");
var accessToken = config["ChannelAccessToken"] ?? "";
var channelSecret = config["ChannelSecret"] ?? "";

var orConfig = app.Configuration.GetSection("OpenRouter");
var orApiKey = orConfig["ApiKey"] ?? "";
var orModel = orConfig["Model"] ?? "openai/gpt-3.5-turbo";

app.MapPost("/webhook", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();

    var signature = ctx.Request.Headers["X-Line-Signature"].ToString();
    if (!string.IsNullOrEmpty(channelSecret) && channelSecret != "YOUR_CHANNEL_SECRET")
    {
        var hash = HMACSHA256Hash(channelSecret, body);
        if (signature != hash)
        {
            Console.WriteLine("[WARN] Invalid signature");
            return Results.Unauthorized();
        }
    }

    Console.WriteLine($"[Webhook] {body}");

    var json = JObject.Parse(body);
    var events = json["events"] as JArray;
    if (events == null) return Results.Ok();

    foreach (var ev in events)
    {
        var type = ev["type"]?.ToString();
        if (type != "message") continue;

        var replyToken = ev["replyToken"]?.ToString();
        var message = ev["message"];
        if (replyToken == null || message == null) continue;

        var msgType = message["type"]?.ToString();
        var replyMessages = new List<object>();

        switch (msgType)
        {
            case "text":
                var inputText = message["text"]?.ToString() ?? "";
                var aiReply = await AskAI(orApiKey, orModel, inputText);
                replyMessages.Add(new { type = "text", text = aiReply });

                // แสดงรูปเมื่อ AI แนะนำสินค้า (ตอบมีคำว่า [แสดงรูป:xxx])
                // AI จะตัดสินใจเองว่าควรแสดงรูปหรือไม่
                if (aiReply.Contains("[แสดงรูป:ปลา]"))
                {
                    replyMessages.Add(new
                    {
                        type = "image",
                        originalContentUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/v1785158052/fish_lydt4m.jpg",
                        previewImageUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/c_scale,w_240/v1785158052/fish_lydt4m.jpg"
                    });
                }
                if (aiReply.Contains("[แสดงรูป:ผลไม้]"))
                {
                    replyMessages.Add(new
                    {
                        type = "image",
                        originalContentUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/v1785158884/fruits_dclzg8.jpg",
                        previewImageUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/c_scale,w_240/v1785158884/fruits_dclzg8.jpg"
                    });
                }
                if (aiReply.Contains("[แสดงรูป:รองเท้า]"))
                {
                    replyMessages.Add(new
                    {
                        type = "image",
                        originalContentUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/v1785157562/cld-sample-5.jpg",
                        previewImageUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/c_scale,w_240/v1785157562/cld-sample-5.jpg"
                    });
                }
                if (aiReply.Contains("[แสดงรูป:อาหาร]"))
                {
                    replyMessages.Add(new
                    {
                        type = "image",
                        originalContentUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/v1785157562/cld-sample-4.jpg",
                        previewImageUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/c_scale,w_240/v1785157562/cld-sample-4.jpg"
                    });
                }
                if (aiReply.Contains("[แสดงรูป:ดอกไม้]"))
                {
                    replyMessages.Add(new
                    {
                        type = "image",
                        originalContentUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/v1785157542/sample.jpg",
                        previewImageUrl = "https://res.cloudinary.com/cfj5lt9q/image/upload/c_scale,w_240/v1785157542/sample.jpg"
                    });
                }

                // ลบ tag ออกจากข้อความที่ส่งให้ลูกค้า
                replyMessages[0] = new { type = "text", text = aiReply
                    .Replace("[แสดงรูป:ปลา]", "").Replace("[แสดงรูป:ผลไม้]", "")
                    .Replace("[แสดงรูป:รองเท้า]", "").Replace("[แสดงรูป:อาหาร]", "")
                    .Replace("[แสดงรูป:ดอกไม้]", "").Trim() };
                break;
            case "sticker":
                replyMessages.Add(new
                {
                    type = "sticker",
                    packageId = message["packageId"]?.ToString(),
                    stickerId = message["stickerId"]?.ToString()
                });
                break;
            default:
                replyMessages.Add(new { type = "text", text = "ขอโทษครับ ตอนนี้รองรับแค่ข้อความและสติกเกอร์เท่านั้น" });
                break;
        }

        await ReplyAsync(accessToken, replyToken, replyMessages.Take(5).ToList());
    }

    return Results.Ok();
});

app.MapGet("/", () => "LINE Bot is running locally!");

app.Run();

static async Task<string> AskAI(string apiKey, string model, string userMessage)
{
    try
    {
        using var client = new HttpClient();
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        var payload = new
        {
            model = model,
            messages = new[]
            {
                new { role = "system", content = "คุณเป็นผู้ช่วยขายสินค้าออนไลน์ ร้านมีสินค้า 5 อย่าง: 1) ปลาตากแห้ง 2) ผลไม้อบแห้ง 3) รองเท้า 4) อาหาร 5) ดอกไม้\n\nกฎ:\n- ถ้าลูกค้าถามรายละเอียดสินค้า ให้ตอบรายละเอียดอย่างเดียว ไม่ต้องแสดงรูป\n- ถ้าลูกค้าแสดงความต้องการ/ความรู้สึก/อยากได้อะไร ให้แนะนำสินค้าที่เหมาะพร้อมแนบ tag แสดงรูป\n- ถ้าลูกค้าขอดูรูป/ขอดูสินค้า ให้แนบ tag แสดงรูป\n\nวิธีแนบรูป: ใส่ tag ท้ายข้อความ (เลือกได้หลายตัว)\n[แสดงรูป:ปลา] [แสดงรูป:ผลไม้] [แสดงรูป:รองเท้า] [แสดงรูป:อาหาร] [แสดงรูป:ดอกไม้]\n\nตอบสั้นกระชับเป็นภาษาไทย เป็นมิตร ช่วยปิดการขาย" },
                new { role = "user", content = userMessage }
            }
        };

        var jsonContent = new StringContent(
            JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

        var response = await client.PostAsync("https://openrouter.ai/api/v1/chat/completions", jsonContent);
        var result = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"[AI] Status={response.StatusCode}");

        var jsonResult = JObject.Parse(result);
        var reply = jsonResult["choices"]?[0]?["message"]?["content"]?.ToString();
        return reply ?? "ขอโทษครับ ไม่สามารถตอบได้ในตอนนี้";
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[AI Error] {ex.Message}");
        return "ขอโทษครับ เกิดข้อผิดพลาดในการเชื่อมต่อ AI";
    }
}

static async Task ReplyAsync(string token, string replyToken, List<object> messages)
{
    var payload = new { replyToken, messages };
    var json = JsonConvert.SerializeObject(payload);

    using var client = new HttpClient();
    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
    var content = new StringContent(json, Encoding.UTF8, "application/json");

    var response = await client.PostAsync("https://api.line.me/v2/bot/message/reply", content);
    var result = await response.Content.ReadAsStringAsync();
    Console.WriteLine($"[Reply] Status={response.StatusCode} Body={result}");
}

static string HMACSHA256Hash(string secret, string message)
{
    var keyBytes = Encoding.UTF8.GetBytes(secret);
    var msgBytes = Encoding.UTF8.GetBytes(message);
    using var hmac = new HMACSHA256(keyBytes);
    var hash = hmac.ComputeHash(msgBytes);
    return Convert.ToBase64String(hash);
}
