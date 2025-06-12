using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Mvc;
using Newtonsoft.Json;
using TokenTest.Models;
using System.Linq;

public class TokenController : Controller
{
    public ActionResult Index()
    {
        ViewBag.TokenUrl = TempData["tokenUrl"];
        ViewBag.ClientId = TempData["clientId"];
        ViewBag.ClientSecret = TempData["clientSecret"];
        ViewBag.Username = TempData["username"];
        ViewBag.Scope = TempData["scope"];
        ViewBag.CustomExpiresTime = TempData["customExpiresTime"];

        TempData.Keep();
        return View();
    }

    [HttpPost]
    public async Task<ActionResult> GetToken(string tokenUrl, string clientId, string clientSecret,
        string username, string password, string scope, string customExpiresTime)
    {
        var values = new Dictionary<string, string>
        {
            { "grant_type", "password" },
            { "client_id", clientId },
            { "client_secret", clientSecret },
            { "username", username },
            { "password", password },
            { "scope", scope },
            { "custom_expires_time", customExpiresTime }
        };

        TokenResponse token = null;
        string responseString = "";
        System.Net.ServicePointManager.ServerCertificateValidationCallback =
    delegate { return true; };
        using (var client = new HttpClient())
        {
            var content = new FormUrlEncodedContent(values);
            var response = await client.PostAsync(tokenUrl, content);
            responseString = await response.Content.ReadAsStringAsync();

            try
            {
                token = JsonConvert.DeserializeObject<TokenResponse>(responseString);
            }
            catch (Exception ex)
            {
                ViewBag.Token = new TokenResponse
                {
                    Error = "Invalid response",
                    ErrorDescription = ex.Message
                };
                ViewBag.RawJson = responseString;
                return View("Index");
            }
        }

        ViewBag.Token = token;
        ViewBag.RawJson = responseString;

        if (token != null && !string.IsNullOrEmpty(token.AccessToken))
        {
            using (var db = new TokenDbContext())
            {
                var existingToken = db.Tokens.FirstOrDefault(t =>
                    t.ClientId == clientId &&
                    t.ClientSecret == clientSecret &&
                    t.Username == username);

                if (existingToken != null)
                {
                    existingToken.Scope = scope;
                    existingToken.AccessToken = token.AccessToken;
                    existingToken.RefreshToken = token.RefreshToken;
                    existingToken.TokenType = token.TokenType;
                    existingToken.ExpiresIn = token.ExpiresIn;
                    existingToken.CreatedAt = DateTime.Now;
                }
                else
                {
                    var dbToken = new Token
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        Username = username,
                        Scope = scope,
                        AccessToken = token.AccessToken,
                        RefreshToken = token.RefreshToken,
                        TokenType = token.TokenType,
                        ExpiresIn = token.ExpiresIn,
                        CreatedAt = DateTime.Now,
                    };
                    db.Tokens.Add(dbToken);
                }

                db.SaveChanges();
            }
        }

        ViewBag.IsSubmitted = true;

        ViewBag.TokenUrl = tokenUrl;
        ViewBag.ClientId = clientId;
        ViewBag.ClientSecret = clientSecret;
        ViewBag.Username = username;
        ViewBag.Scope = scope;
        ViewBag.CustomExpiresTime = customExpiresTime;

        return View("Index");
    }


    [HttpPost]
    public ActionResult SaveTokenContext(string clientId, string clientSecret, string username, string scope, string customExpiresTime)
    {
        TempData["clientId"] = clientId;
        TempData["clientSecret"] = clientSecret;
        TempData["username"] = username;
        TempData["scope"] = scope;
        TempData["customExpiresTime"] = customExpiresTime;

        return RedirectToAction("Index");
    }


    public ActionResult List(string searchText, DateTime? expiresFrom, DateTime? expiresTo, string sortBy = "CreatedAt", int page = 1)
    {
        int pageSize = 12;
        using (var db = new TokenDbContext())
        {
            var query = db.Tokens.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchText))
            {
                query = query.Where(t => t.Username.Contains(searchText));
            }

            if (expiresFrom.HasValue)
            {
                query = query.Where(t => t.ExpiresAt >= expiresFrom.Value);
            }

            if (expiresTo.HasValue)
            {
                query = query.Where(t => t.ExpiresAt <= expiresTo.Value);
            }

            // Sort
            switch (sortBy)
            {
                case "CreatedAt":
                    query = query.OrderBy(t => t.CreatedAt);
                    break;
                case "CreatedAt_desc":
                    query = query.OrderByDescending(t => t.CreatedAt);
                    break;
                case "ExpiresAt":
                    query = query.OrderBy(t => t.ExpiresAt);
                    break;
                case "ExpiresAt_desc":
                    query = query.OrderByDescending(t => t.ExpiresAt);
                    break;
                default:
                    query = query.OrderBy(t => t.CreatedAt);
                    break;
            }

            int totalItems = query.Count();

            var tokens = query.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Page = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;
            ViewBag.SearchText = searchText;
            ViewBag.ExpiresFrom = expiresFrom?.ToString("yyyy-MM-dd");
            ViewBag.ExpiresTo = expiresTo?.ToString("yyyy-MM-dd");
            ViewBag.SortBy = sortBy;

            ViewBag.ClientId = TempData["clientId"];
            ViewBag.ClientSecret = TempData["clientSecret"];
            ViewBag.Username = TempData["username"];
            ViewBag.Scope = TempData["scope"];
            ViewBag.CustomExpiresTime = TempData["customExpiresTime"];

            TempData.Keep();

            return View(tokens);
        }
    }



    // GET: Token/Edit/5
    public ActionResult Edit(int id)
    {
        using (var db = new TokenDbContext())
        {
            var token = db.Tokens.Find(id);
            if (token == null)
                return HttpNotFound();
            return View(token);
        }
    }



    // POST: Token/Edit/5
    [HttpPost]
    public ActionResult Edit(Token updatedToken)
    {
        using (var db = new TokenDbContext())
        {
            var existingToken = db.Tokens.Find(updatedToken.Id);
            if (existingToken == null)
                return HttpNotFound();

            existingToken.Scope = updatedToken.Scope;
            existingToken.TokenType = updatedToken.TokenType;
            existingToken.ExpiresIn = updatedToken.ExpiresIn;
            existingToken.AccessToken = updatedToken.AccessToken;
            existingToken.RefreshToken = updatedToken.RefreshToken;

            db.SaveChanges();
            return RedirectToAction("List");
        }
    }

    [HttpPost]
    public JsonResult UpdateTokenField(int id, string field, string value)
    {
        using (var db = new TokenDbContext())
        {
            var token = db.Tokens.Find(id);
            if (token == null)
                return Json(new { success = false, message = "Token does not exist" });

            switch (field)
            {
                // "AccessToken":
                    //token.AccessToken = value;
                    //break;
                //case "ClientId":
                    //token.ClientId = value;
                    //break;
                //case "ClientSecret":
                    //token.ClientSecret = value;
                    //break;
               // case "Username":
                    //token.Username = value;
                    //break;
                case "Scope":
                    token.Scope = value;
                    break;
                case "TokenType":
                    token.TokenType = value;
                    break;
               // case "ExpiresAt":
                    //DateTime dt;
                    //if (DateTime.TryParse(value, out dt))
                        //token.ExpiresAt = dt;
                    //else
                        //return Json(new { success = false, message = "Invalid date" });
                    //break;

                default:
                    return Json(new { success = false, message = "Invalid field" });
            }

            db.SaveChanges();
            return Json(new { success = true });
        }
    }


}
